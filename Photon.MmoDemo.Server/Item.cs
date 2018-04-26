// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Item.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Represents an entity in a world. 
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Photon.MmoDemo.Server
{
    using System;
    using System.Collections;

    using ExitGames.Concurrency.Fibers;

    using Photon.SocketServer.Concurrency;
    using Photon.MmoDemo.Common;
    using Photon.SocketServer;
    using Photon.MmoDemo.Server.Events;

    using ExitGames.Logging;
    using ExitGames.Logging.Log4Net;


    using log4net.Config;
    /// <summary>
    /// Copy of current Item's state
    /// </summary>
    public class ItemSnapshot
    {
        public ItemSnapshot(Item source, Vector position, Vector rotation, Region worldRegion, int propertiesRevision)
        {
            this.Source = source;
            this.Position = position;
            this.Rotation = rotation;
            this.PropertiesRevision = propertiesRevision;
        }

        public Item Source { get; private set; }
        public Vector Position { get; private set; }
        public Vector Rotation { get; private set; }
        public int PropertiesRevision { get; private set; }
    }


        public struct NetworkState
        {
            public Vector pos;
            public Vector? rot;
            public double totalMs; // total ms since simulation start
        };
    /// <summary>
    /// Represents an entity in a world. 
    /// </summary>
    /// <remarks>
    /// Items are event publisher and the counterpart to InterestAreas.
    /// </remarks>
    public class Item : IDisposable
    {
        private readonly string id;
       
        // Current region subscribes on item's events
        private readonly MessageChannel<ItemEventMessage> eventChannel;

        // Object (radar or attached) watches item position
        private readonly MessageChannel<ItemPositionMessage> positionUpdateChannel;

        // Object (radar or attached) watches item dispose
        private readonly MessageChannel<ItemDisposedMessage> disposeChannel;

        private readonly Hashtable properties;

        private readonly byte type;

        private readonly World world;

        private bool disposed;

        private IDisposable regionSubscription;



        protected NetworkState[] posBuffer = new NetworkState[20];  // previous positions used for rewind
        protected int currBufferIndex = 0;  // index of latest network state
        protected int posSetCount = 0;  // count for times position has been set
                                        // some assumptions about the buffer: SetPos() is called once per network update cycle and
                                        // network update cycles are roughly fixed size to be optimized, but it will still work if not fixed size

            // we're keeping a seperate buffer for rotations for now, this will need to be united with the rest soon
        protected Vector[] rotBuffer = new Vector[20];
        protected int currRotBufferIndex = 0;

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();


        public Item(Vector position, Vector rotation, Hashtable properties, MmoActorOperationHandler owner, string id, byte type, World world)
        {
            this.SetPos( position);
            this.Rotation = rotation;
            this.Owner = owner;
            this.eventChannel = new MessageChannel<ItemEventMessage>(ItemEventMessage.CounterEventSend);
            this.disposeChannel = new MessageChannel<ItemDisposedMessage>(MessageCounters.CounterSend);
            this.positionUpdateChannel = new MessageChannel<ItemPositionMessage>(MessageCounters.CounterSend);
            this.properties = properties ?? new Hashtable();
            if (properties != null)
            {
                this.PropertiesRevision++;
            }

            this.id = id;
            this.world = world;
            this.type = type;
            this.Velocity = new Vector(0, 0);
        }

        //public Item(Vector position, Vector rotation, Hashtable properties, MmoActorOperationHandler owner,
        //    string id, byte type, World world, double time)
        //{
        //    this.SetPos(position, time);
        //    this.Rotation = rotation;
        //    this.Owner = owner;
        //    this.eventChannel = new MessageChannel<ItemEventMessage>(ItemEventMessage.CounterEventSend);
        //    this.disposeChannel = new MessageChannel<ItemDisposedMessage>(MessageCounters.CounterSend);
        //    this.positionUpdateChannel = new MessageChannel<ItemPositionMessage>(MessageCounters.CounterSend);
        //    this.properties = properties ?? new Hashtable();
        //    if (properties != null)
        //    {
        //        this.PropertiesRevision++;
        //    }

        //    this.id = id;
        //    this.world = world;
        //    this.type = type;
        //}

        ~Item()
        {
            this.Dispose(false);
        }

        public IFiber Fiber { get { return this.Owner.Peer.RequestFiber; } }

        public Region CurrentWorldRegion { get; private set; }

        public MessageChannel<ItemDisposedMessage> DisposeChannel { get { return this.disposeChannel; } }

        public bool Disposed { get { return this.disposed; } }

        public MessageChannel<ItemEventMessage> EventChannel { get { return this.eventChannel; } }

        public string Id { get { return this.id; } }

        public MmoActorOperationHandler Owner { get; private set; }

        public Vector Rotation {
            get
            {
                return rotBuffer[currRotBufferIndex];
            }
            set
            {
                currRotBufferIndex = (currRotBufferIndex+1)%20;
                rotBuffer[currRotBufferIndex] = value;
            }
        }

        public Vector Velocity { get; set; }

        public Vector Position { get; private set; }

        public Vector GetLastRot()
        {
            if (posSetCount > 1)
            {
                int lastRotIndex = (currBufferIndex - 1) % 20;
                if (posBuffer[lastRotIndex].rot != null)
                    return (Vector)posBuffer[lastRotIndex].rot;
            }
            return Rotation;
        }

        public float GetCumulativeDotProducts(int howManyFrames)
        {
            bool printDebug = false;
            if ((new Random()).Next(10) < 2)
                printDebug = true;
            float ans = 0;
            for (int i = 0; i < howManyFrames; i++)
            {
                Vector curr = rotBuffer[(currRotBufferIndex - i)%20];
                Vector last = rotBuffer[(currRotBufferIndex - i - 1) % 20];
                if (printDebug)
                {
                    GlobalVars.log.InfoFormat("cuR " + curr.ToString());
                    GlobalVars.log.InfoFormat("las " + last.ToString());
                }
                ans += Vector.Dot(curr, last);
            }

            if (printDebug)
                return ans;
            return ans;
        }

        public void SetPos(Vector val, double totalTimeInMS)
        {
            NetworkState nextState = new NetworkState();
            nextState.pos = val;
            nextState.totalMs = totalTimeInMS;
            nextState.rot = Rotation;

            Position = val;
            posBuffer[currBufferIndex] = nextState;
            currBufferIndex++;
            currBufferIndex = currBufferIndex % 20;
            if (posSetCount < 20)
                posSetCount++;
        }

        public void SetPosRot(Vector pos, Vector rot, double totalTimeInMS)
        {
            NetworkState nextState = new NetworkState();
            nextState.pos = pos;
            nextState.rot = rot;
            nextState.totalMs = totalTimeInMS;

            Position = pos;
            posBuffer[currBufferIndex] = nextState;
            currBufferIndex++;
            currBufferIndex = currBufferIndex % 20;
            if (posSetCount < 20)
                posSetCount++;
        }

        public void SetPos(Vector val)
        {
            Position = val;
        }

        // get position, from buffer, that goes back to the timestamp totalms
        public Vector GetRewindedPos(double totalms)
        {
            if (posSetCount < 1)
            {
                log.InfoFormat("warning: rewind requested when buffer is empty");
                return Position;
            }
            else if (posSetCount == 1)
            {
                log.InfoFormat("warning: rewind requested when buffer has 1 element");
                return posBuffer[currBufferIndex].pos;
            }
            else
            {
                // get the interval of the time between two buffer recordings to use as an estimate of all intervals
                double bufferIntervalEstimate = posBuffer[currBufferIndex].totalMs - posBuffer[(currBufferIndex - 1) % 20].totalMs;
                int stepsBack = (int)Math.Floor(totalms / bufferIntervalEstimate); //estimated steps back in buffer
                if (stepsBack > 20)
                {
                    log.InfoFormat("error: rewind requested more than 20 steps back, some one is lagged out?");
                    return posBuffer[(currBufferIndex - posSetCount) % 20].pos; // return oldest pos
                }
                else if (stepsBack > posSetCount) //stepping back more than we have buffer recordings
                {
                    log.InfoFormat("warning: potentially rewinding back further than we have info for");
                    // start at the oldest position and if we need to move to newer positions until we have a match, do so
                    int ansBufferIndex = RewindHelperGetRightIndex(totalms, stepsBack);
                    return posBuffer[ansBufferIndex].pos;
                }
                else 
                {
                    
                    if (totalms > posBuffer[currBufferIndex].totalMs)
                        return posBuffer[currBufferIndex].pos;

                    int ansIndexRight = RewindHelperGetRightIndex(totalms, stepsBack+1);
                    int ansIndexLeft = (ansIndexRight-1)% 20;
                    if (totalms < posBuffer[ansIndexRight].totalMs && totalms > posBuffer[ansIndexLeft].totalMs)
                    {
                        float tlerp = (float)((totalms - posBuffer[ansIndexLeft].totalMs) /
                            (posBuffer[ansIndexRight].totalMs - posBuffer[ansIndexLeft].totalMs));
                        return posBuffer[ansIndexRight].pos * tlerp + posBuffer[ansIndexLeft].pos * (1 - tlerp);
                    }
                    else
                    {
                        log.InfoFormat("ERROR rewind something went wrong");
                        return posBuffer[ansIndexRight].pos;
                    }

                }
            }
        }

        // to be optimized this should only be called if totalMS is smaller thant the largest timestamp in buffer
        private int RewindHelperGetRightIndex(double totalMS, int stepsBack)
        {
            int ansBufferIndex = (currBufferIndex - Math.Min(posSetCount, stepsBack)) % 20 - 1;
            double oldestTime;
            do
            {
                ansBufferIndex++;
                oldestTime = posBuffer[ansBufferIndex%20].totalMs;
                if (ansBufferIndex > 40)    //todo: remove if it doesnt happen
                {
                    log.InfoFormat("error: rewind infinite loop situation");
                    return 1;
                }
            } while (oldestTime < totalMS);
            return ansBufferIndex;
        }

        public MessageChannel<ItemPositionMessage> PositionUpdateChannel { get { return this.positionUpdateChannel; } }

        public Hashtable Properties { get { return this.properties; } }

        public int PropertiesRevision { get; set; }

        public byte Type { get { return this.type; } }

        public World World { get { return this.world; } }

        public void Destroy()
        {
            this.OnDestroy();
        }

        /// <summary>
        /// Publishes a ItemPositionMessage in the PositionUpdateChannel
        /// Subscribes and unsubscribes regions if changed. 
        /// </summary>
        public void UpdateInterestManagement()
        {
            // inform attached interst area and radar
            ItemPositionMessage message = this.GetPositionUpdateMessage(this.Position);
            this.positionUpdateChannel.Publish(message);

            // update subscriptions if region changed
            Region prevRegion = this.CurrentWorldRegion;
            Region newRegion = this.World.GetRegion(this.Position);

            if (newRegion != this.CurrentWorldRegion)
            {
                this.CurrentWorldRegion = newRegion;

                if (this.regionSubscription != null)
                {
                    this.regionSubscription.Dispose();
                }

                var snapshot = this.GetItemSnapshot();
                var regMessage = new ItemRegionChangedMessage(prevRegion, newRegion, snapshot);

                if (prevRegion != null)
                {
                    prevRegion.DelistItem(this);    // remove from regions item list
                    prevRegion.ItemRegionChangedChannel.Publish(regMessage);
                }
                if (newRegion != null)
                {
                    newRegion.EnlistItem(this);     // add to regions item list
                    newRegion.ItemRegionChangedChannel.Publish(regMessage);

                    this.regionSubscription = new UnsubscriberCollection(
                        this.EventChannel.Subscribe(this.Fiber, (m) => newRegion.ItemEventChannel.Publish(m)), // route events through region to interest area
                        newRegion.RequestItemEnterChannel.Subscribe(this.Fiber, (m) => { m.InterestArea.OnItemEnter(this.GetItemSnapshot()); }), // region entered interest area fires message to let item notify interest area about enter
                        newRegion.RequestItemExitChannel.Subscribe(this.Fiber, (m) => { m.InterestArea.OnItemExit(this); }) // region exited interest area fires message to let item notify interest area about exit
                    );
                }

            }
        }

        // use this region update for when a new item is added and it has no previous region
        public void UpdateRegionSimple()
        {
            Region newRegion = this.World.GetRegion(this.Position);
            this.CurrentWorldRegion = newRegion;
            newRegion.EnlistItem(this);     // add to regions item list
            this.regionSubscription = new UnsubscriberCollection(
                this.EventChannel.Subscribe(this.Fiber, (m) => newRegion.ItemEventChannel.Publish(m)), // route events through region to interest area
                newRegion.RequestItemEnterChannel.Subscribe(this.Fiber, (m) => { m.InterestArea.OnItemEnter(this.GetItemSnapshot()); }), // region entered interest area fires message to let item notify interest area about enter
                newRegion.RequestItemExitChannel.Subscribe(this.Fiber, (m) => { m.InterestArea.OnItemExit(this); }) // region exited interest area fires message to let item notify interest area about exit
            );
        }


        ///// <summary>
        ///// Publishes a ItemPositionRotationMessage in the PositionUpdateChannel
        ///// Subscribes and unsubscribes regions if changed. 
        ///// </summary>
        //public void UpdateInterestManagementWithRot()
        //{
        //    // inform attached interst area and radar
        //    ItemPositionRotationMessage message = this.GetPosRotUpdateMessage(this.Position, this.Rotation);
        //    this.positionUpdateChannel.Publish(message);

        //    // update subscriptions if region changed
        //    Region prevRegion = this.CurrentWorldRegion;
        //    Region newRegion = this.World.GetRegion(this.Position);

        //    if (newRegion != this.CurrentWorldRegion)
        //    {
        //        this.CurrentWorldRegion = newRegion;

        //        if (this.regionSubscription != null)
        //        {
        //            this.regionSubscription.Dispose();
        //        }

        //        var snapshot = this.GetItemSnapshot();
        //        var regMessage = new ItemRegionChangedMessage(prevRegion, newRegion, snapshot);

        //        if (prevRegion != null)
        //        {
        //            prevRegion.DelistItem(this);    // remove from regions item list
        //            prevRegion.ItemRegionChangedChannel.Publish(regMessage);
        //        }
        //        if (newRegion != null)
        //        {
        //            newRegion.EnlistItem(this);     // add to regions item list
        //            newRegion.ItemRegionChangedChannel.Publish(regMessage);

        //            this.regionSubscription = new UnsubscriberCollection(
        //                this.EventChannel.Subscribe(this.Fiber, (m) => newRegion.ItemEventChannel.Publish(m)), // route events through region to interest area
        //                newRegion.RequestItemEnterChannel.Subscribe(this.Fiber, (m) => { m.InterestArea.OnItemEnter(this.GetItemSnapshot()); }), // region entered interest area fires message to let item notify interest area about enter
        //                newRegion.RequestItemExitChannel.Subscribe(this.Fiber, (m) => { m.InterestArea.OnItemExit(this); }) // region exited interest area fires message to let item notify interest area about exit
        //            );
        //        }

        //    }
        //}


        /// <summary>
        /// Updates the Properties and increments the PropertiesRevision.
        /// </summary>
        public void SetProperties(Hashtable propertiesSet, ArrayList propertiesUnset)
        {
            if (propertiesSet != null)
            {
                foreach (DictionaryEntry entry in propertiesSet)
                {
                    this.properties[entry.Key] = entry.Value;
                }
            }

            if (propertiesUnset != null)
            {
                foreach (object key in propertiesUnset)
                {
                    this.properties.Remove(key);
                }
            }

            this.PropertiesRevision++;
        }

        /// <summary>
        /// Creates an ItemSnapshot with a snapshot of the current attributes.
        /// </summary>
        protected internal ItemSnapshot GetItemSnapshot()
        {
            return new ItemSnapshot(this, this.Position, this.Rotation, this.CurrentWorldRegion, this.PropertiesRevision);
        }

        /// <summary>
        /// Creates an ItemPositionMessage with the current position.
        /// </summary>
        protected ItemPositionMessage GetPositionUpdateMessage(Vector position)
        {
            return new ItemPositionMessage(this, position);
        }

        protected ItemPositionRotationMessage GetPosRotUpdateMessage(Vector position, Vector rotation)
        {
            return new ItemPositionRotationMessage(this, position, rotation);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Publishes a ItemDisposedMessage through the DisposeChannel and disposes all channels.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.regionSubscription != null)
                {
                    this.regionSubscription.Dispose();
                }
                this.CurrentWorldRegion = null;
                this.disposeChannel.Publish(new ItemDisposedMessage(this));
                this.eventChannel.Dispose();
                this.disposeChannel.Dispose();
                this.positionUpdateChannel.Dispose();

                this.disposed = true;
            }
        }

        /// <summary>
        /// Publishes event ItemDestroyed in the Item.EventChannel.
        /// </summary>
        protected void OnDestroy()
        {
            var eventInstance = new ItemDestroyed { ItemId = this.Id };
            var eventData = new EventData((byte)EventCode.ItemDestroyed, eventInstance);
            var message = new ItemEventMessage(this, eventData, new SendParameters { ChannelId = Settings.ItemEventChannel });
            this.EventChannel.Publish(message);
        }

        /// <summary>
        /// Moves the item.
        /// </summary>
        public void Move(Vector position)
        {
            this.SetPos(position);
            this.UpdateInterestManagement();
        }

        /// <summary>
        /// Spawns the item.
        /// </summary>
        public void Spawn(Vector position)
        {
            this.SetPos(position);
            this.UpdateInterestManagement();
        }

        //public void Spawn(Vector position, Vector rotation)
        //{
        //    this.SetPos(position);
        //    this.Rotation = rotation;
        //    this.UpdateInterestManagement();
        //}

        // naive implementation
        //public Vector GetRewindedPos(float secondsBack)
        //{
        //    float framesBack = secondsBack / GlobalVars.secToUpdate;
        //    float framesBackFloor = (float)Math.Floor(framesBack);
        //    int startIndex = (int)framesBackFloor;
        //    float tLerp = ((framesBack * GlobalVars.secToUpdate) - (framesBackFloor * GlobalVars.secToUpdate)) / GlobalVars.secToUpdate;
        //    // Lerp(posBuffer[(currBufferIndex - startIndex) % 20], posBuffer[(currBufferIndex - startIndex+1) % 20], tLerp)
        //    return posBuffer[(currBufferIndex - startIndex) % 20].pos;
        //    // todo lerp
        //}

        /// <summary>
        /// Checks wheter the actor is allowed to change the item.
        /// </summary>
        public bool GrantWriteAccess(MmoActorOperationHandler actor)
        {
            return this.Owner == actor;
        }
    }
}
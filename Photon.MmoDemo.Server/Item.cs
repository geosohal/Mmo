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



        protected NStateBuffer nbuffer;

            // we're keeping a seperate buffer for rotations for now, this will need to be united with the rest soon
        protected Vector[] rotBuffer = new Vector[20];
        protected int currRotBufferIndex = 0;

        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        public float secSinceBursted;
        public bool wasBursted;
       // public bool IsThrusting;


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
         //   IsThrusting = false;
            wasBursted = false;
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
            Fiber.Dispose();
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
            AddNetworkState(val, (float)totalTimeInMS);
            Position = val;
        }


        private void AddNetworkState(Vector pos, float totalMs)
        {
            nbuffer.AddNetworkState(pos, totalMs);
        }

        public void SetPos(Vector val)
        {
            Position = val;
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
        public void UpdateInterestManagement(bool sendMessage)
        {
            if (sendMessage)
            {
                // inform attached interst area and radar
                ItemPositionMessage message = this.GetPositionUpdateMessage(this.Position);
                this.positionUpdateChannel.Publish(message);
            }
         //   Object obj = new Object();
         //   lock (obj)
            {
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

                        // i think for non avatar items like bullet - since they are not tied to ClientInterestArea
                        // their OnItemEnter and OnItemExit are empty or something idk. this was an attempt to
                        // manually unsub the fiber so that the on item enter/exit events don't keep getting called
                        // a good test would be to make the bullet last only .1sec so that it stays in only 1 region
                        // then see if the current setup removes the fiber subscription
                        //if (this.type == (byte)ItemType.Bullet)
                        //{
                        //    Fiber.DeregisterSubscription(prevRegion.RequestItemEnterChannel);
                        //    Fiber.DeregisterSubscription(prevRegion.RequestItemExitChannel);
                        //    Fiber.DeregisterSubscription(this.eventChannel);
                        //}

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
        }

        // use this region update for when a new item has no interest area and doesnt need to update position
        // (position update for it is done elsewhere) this is useful for projectiles and perhaps bots
        public void UpdateRegionSimple()
        {
            Region newRegion = this.World.GetRegion(this.Position);
            Region prevRegion = this.CurrentWorldRegion;

            if (newRegion != this.CurrentWorldRegion)
            {
                if (this.regionSubscription != null)
                {
                    this.regionSubscription.Dispose();
                }
                if (prevRegion != null)
                {
                    prevRegion.DelistItem(this);    // remove from regions item list
                }

                this.CurrentWorldRegion = newRegion;
                if (newRegion == null)
                {
                    log.InfoFormat("new region was NULL");
                    return;
                }
                newRegion.EnlistItem(this);     // add to regions item list
                this.regionSubscription = new UnsubscriberCollection(
                    this.EventChannel.Subscribe(this.Fiber, (m) => newRegion.ItemEventChannel.Publish(m)), // route events through region to interest area
                    newRegion.RequestItemEnterChannel.Subscribe(this.Fiber, (m) => { m.InterestArea.OnItemEnter(this.GetItemSnapshot()); }), // region entered interest area fires message to let item notify interest area about enter
                    newRegion.RequestItemExitChannel.Subscribe(this.Fiber, (m) => { m.InterestArea.OnItemExit(this); }) // region exited interest area fires message to let item notify interest area about exit
                );
            }
        }

        

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
                    GlobalVars.log.InfoFormat("region sub disposed for: " + this.id);
                }
                else
                    GlobalVars.log.InfoFormat("region sub was null for: " + this.id);

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
            this.UpdateInterestManagement(true);
        }

        /// <summary>
        /// Spawns the item.
        /// </summary>
        public void Spawn(Vector position)
        {
            this.SetPos(position);
            this.UpdateInterestManagement(true);
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
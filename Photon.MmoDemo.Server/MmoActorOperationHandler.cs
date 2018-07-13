// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MmoActorOperationHandler.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Сlient's Peer.CurrentOperationHandler after entering a world.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Photon.MmoDemo.Server
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using Photon.MmoDemo.Common;
    using Photon.MmoDemo.Server.Events;
    using Photon.MmoDemo.Server.Operations;
    using Photon.SocketServer;
    using Photon.MmoDemo.Server;
    using Photon.SocketServer.Rpc;
    using ExitGames.Logging;
    using ExitGames.Logging.Log4Net;
    using Photon.MmoDemo.Server.GameSpecific;
    using UnityEngine;
    using log4net.Config;
    using System.Diagnostics;

    /// <summary>
    /// Сlient's Peer.CurrentOperationHandler after entering a world.
    /// </summary>
    public sealed class MmoActorOperationHandler : MmoActor, IOperationHandler
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        Stopwatch watch; // measure time since last update event more accurately than the timer
        System.Timers.Timer updateTimer;    // timer used to regularly trigger update operation

        bool isMegaThrusting;
        bool isSuperFast;
        bool isBursting;
        public bool isSaberOut;
        bool isLaserOn;
        bool isBombOut;
        float saberTimeLeft;

        float laserTimeLeft;
        float currMaxVel;
        float bombTimeLeft;

        // bot manager handles all bot position changes
        // this eventually needs to be moved to World or maybe Region, if they don't change regions
        // because mobs/bots are not not inherently related to the player. they relate to the world by
        // bein inside it.
        // put it here because Item needs a Peer to initialize an item
        //BotManager botman;

        Random random;
        bool firstUpdate;
        bool wasdMode;

        public bool isBotMan;

        Vector mouseFwd;
        

        public MmoActorOperationHandler(PeerBase peer, World world, InterestArea interestArea, bool isBotMan)
            : base(peer, world)
        {
            random = new Random();

            this.isBotMan = isBotMan;

            this.AddInterestArea(interestArea);
            updateTimer = new System.Timers.Timer();
            updateTimer.Elapsed += new System.Timers.ElapsedEventHandler(OnTimersUpdateEvent);
            updateTimer.Interval = GlobalVars.msToUpdate; // 100ms
            updateTimer.Enabled = true;
            watch = new Stopwatch();
            watch.Start();
            isMegaThrusting = false;
            currMaxVel = GlobalVars.maxShipVel;

            isBursting = false;
            isSaberOut = false;
            isBombOut = false;
            
            firstUpdate = true;
            isSuperFast = false;
            mouseFwd = new Vector(1, 0);
            wasdMode = true;
            log.InfoFormat("initializing actor op handler ********************");
            // if this is the first peer connecting then bot manager isnt initialized
            if (BotManager.IsInitialized == false)
                BotManager.InitializeManager(world, GetDefaultSP());
        }

        private Vector GetRandomPosition()
        {
            var d = this.World.Area.Max - this.World.Area.Min;
            return this.World.Area.Min + new Vector { X = d.X * (float)this.random.NextDouble(), Y = d.Y * (float)this.random.NextDouble() };
        }

        // Specify what you want to happen when the Elapsed event is raised.
        private void OnTimersUpdateEvent(object source, System.Timers.ElapsedEventArgs e)
        {
            if (firstUpdate && !isBotMan)
            {
                // spawn random hp boxes
                for (int i =0; i < 15; i++)
                    AddHPBox(GetRandomPosition());
                log.InfoFormat("added hp boxes");
                firstUpdate = false;
            }
            watch.Stop();   // sets elapsed time to time since watch.Start() was called
            TimeSpan ts = watch.Elapsed;
            float msElapse = (float)ts.Milliseconds;

            UpdateMyProjectiles(msElapse);

            if (!isBotMan)
            {
                CheckForCollisions(msElapse);
                UpdateAvatarPos(msElapse);
            }

            watch = Stopwatch.StartNew();
        }

        private void AddHPBox(Vector pos)
        {
            string itemId = "hp" + GlobalVars.HPboxCount++;
            log.InfoFormat("adding " + itemId.ToString());
            Item hpbox = new Item(pos, Vector.Zero, new Hashtable(), this, itemId, (byte)ItemType.Resource, this.World);
            hpbox.World.ItemCache.AddItem(hpbox);
            AddItem(hpbox);
            hpbox.UpdateRegionSimple();
        }

        public void UpdateAvatarPos(float msElapsed)
        {
            float elapsedSeconds = msElapsed / 1000f;

            if (Avatar.wasBursted)
            {
                Avatar.secSinceBursted -= elapsedSeconds;
                if (Avatar.secSinceBursted <= 0)
                    Avatar.wasBursted = false;
            }
            // if avatar is over the usualy max speed and isn't megatthrustic then it just came out
            // of a mega thrust and we need to ease it's spee bakc to normal
            if (Avatar.Velocity.Len2 > GlobalVars.maxShipVelSq+10 && !isMegaThrusting && !isSuperFast)
            {
                if (Avatar.wasBursted)
                {
                    //currMaxVel -= GlobalVars.burstFadePerSec * elapsedSeconds;
                    Avatar.Velocity = Avatar.Velocity * GlobalVars.burstDampening;
                    //log.InfoFormat("burst damp");
                }
                else
                {
                    //log.InfoFormat("reducing booster");
                    currMaxVel -= GlobalVars.megaFadePerSec * elapsedSeconds;
                    Avatar.Velocity = Vector.Normalized(Avatar.Velocity) * currMaxVel;
                }
            }

                Vector oldPosition = Avatar.Position;
            //Vector oldRotation = Avatar.GetLastRot();

            Avatar.Move(Avatar.Position + Avatar.Velocity * elapsedSeconds);

   //         if ((oldPosition - Avatar.Position).Len2 < 0.1f)
   //             return;
           //   log.InfoFormat(" avpos changed to: {0}", Avatar.Position);
            // send event
            var eventInstance = new ItemMoved
            {
                ItemId = Avatar.Id,
                OldPosition = oldPosition,
                Position = Avatar.Position,
                Rotation = Avatar.Rotation,
                OldRotation = Avatar.Rotation
            };

            SendParameters sp = GetDefaultSP();

            var eventData = new EventData((byte)EventCode.ItemMoved, eventInstance);
            var message = new ItemEventMessage(Avatar, eventData, sp);
            Avatar.EventChannel.Publish(message);
        }

        public static SendParameters GetDefaultSP()
        {
            SendParameters sp = new SendParameters();
            sp.ChannelId = Settings.ItemEventChannel;
            sp.Encrypted = false;
            sp.Unreliable = true;
            sp.Flush = false;
            return sp;
        }

        /// <summary>
        /// Handles operations CreateWorld and EnterWorld.
        /// </summary>
        public static OperationResponse InvalidOperation(OperationRequest request)
        {
            string debugMessage = "InvalidOperation: " + (OperationCode)request.OperationCode;
            return new OperationResponse(request.OperationCode) { ReturnCode = (int)ReturnCode.InvalidOperation, DebugMessage = debugMessage };
        }

        /// <summary>
        /// Handles operation AddInterestArea: Creates a new InterestArea and optionally attaches it to an existing Item.
        /// </summary>
        public OperationResponse OperationAddInterestArea(PeerBase peer, OperationRequest request, SendParameters sendParameters)
        {
            var operation = new AddInterestArea(peer.Protocol, request);
            if (!operation.IsValid)
            {
                return new OperationResponse(request.OperationCode) { ReturnCode = (int)ReturnCode.InvalidOperationParameter, DebugMessage = operation.GetErrorMessage() };
            }

            operation.OnStart();
            InterestArea interestArea;
            if (this.TryGetInterestArea(operation.InterestAreaId, out interestArea))
            {
                return operation.GetOperationResponse((int)ReturnCode.InterestAreaAlreadyExists, "InterestAreaAlreadyExists");
            }

            interestArea = new ClientInterestArea(this.Peer, operation.InterestAreaId, this.World);
            this.AddInterestArea(interestArea);

            // attach interestArea to item            
            if (string.IsNullOrEmpty(operation.ItemId) == false)
            {
                Item item;

                bool actorItem = this.TryGetItem(operation.ItemId, out item);
                if (actorItem)
                {
                    // we are already in the item thread, invoke directly
                    return ItemOperationAddInterestArea(item, operation, interestArea);
                }
                else
                {
                    if (this.World.ItemCache.TryGetItem(operation.ItemId, out item) == false)
                    {
                        return operation.GetOperationResponse((int)ReturnCode.ItemNotFound, "ItemNotFound1");
                    }
                    else
                    {
                        log.InfoFormat("operationAddIA couldnt find item");
                        // second parameter (peer) allows us to send an error event to the client (in case of an error)
                        item.Fiber.Enqueue(() => this.ExecItemOperation(() => ItemOperationAddInterestArea(item, operation, interestArea), sendParameters));
                        // send response later
                        return null;
                    }
                }
            }
            else
            {
                // free floating interestArea
                lock (interestArea.SyncRoot)
                {
                    interestArea.Position = operation.Position;
                    interestArea.ViewDistanceEnter = operation.ViewDistanceEnter;
                    interestArea.ViewDistanceExit = operation.ViewDistanceExit;
                    interestArea.UpdateInterestManagement();
                }

                return operation.GetOperationResponse(MethodReturnValue.Ok);
            }
        }

        /// <summary>
        /// Handles operation AttachInterestArea: Attaches an existing InterestArea to an existing Item.
        /// </summary>
        public OperationResponse OperationAttachInterestArea(PeerBase peer, OperationRequest request, SendParameters sendParameters)
        {
            var operation = new AttachInterestArea(peer.Protocol, request);
            if (!operation.IsValid)
            {
                return new OperationResponse(request.OperationCode) { ReturnCode = (int)ReturnCode.InvalidOperationParameter, DebugMessage = operation.GetErrorMessage() };
            }

            operation.OnStart();
            InterestArea interestArea;
            if (this.TryGetInterestArea(operation.InterestAreaId, out interestArea) == false)
            {
                return operation.GetOperationResponse((int)ReturnCode.InterestAreaNotFound, "InterestAreaNotFound");
            }

            Item item;
            bool actorItem;
            if (string.IsNullOrEmpty(operation.ItemId))
            {
                item = this.Avatar;
                actorItem = true;

                // set return vaues
                operation.ItemId = item.Id;
            }
            else
            {
                actorItem = this.TryGetItem(operation.ItemId, out item);
                
            }

            if (actorItem)
            {
                // we are already in the item thread, invoke directly
                return this.ItemOperationAttachInterestArea(item, operation, interestArea, sendParameters);
            }
            else
            {
                // search world cache just to see if item exists at all
                if (this.World.ItemCache.TryGetItem(operation.ItemId, out item) == false)
                {
                    return operation.GetOperationResponse((int)ReturnCode.ItemNotFound, "ItemNotFound2");
                }
                else
                {
                    // second parameter (peer) allows us to send an error event to the client (in case of an error)
                    item.Fiber.Enqueue(() => this.ExecItemOperation(() => this.ItemOperationAttachInterestArea(item, operation, interestArea, sendParameters), sendParameters));

                    // response is sent later
                    return null;
                }
            }
        }

        /// <summary>
        /// Handles operation DestroyItem: Destroys an existing Item. 
        /// </summary>
        public OperationResponse OperationDestroyItem(PeerBase peer, OperationRequest request, SendParameters sendParameters)
        {
            var operation = new DestroyItem(peer.Protocol, request);
            if (!operation.IsValid)
            {
                return new OperationResponse(request.OperationCode) { ReturnCode = (int)ReturnCode.InvalidOperationParameter, DebugMessage = operation.GetErrorMessage() };
            }

            operation.OnStart();
            Item item;
            bool actorItem = this.TryGetItem(operation.ItemId, out item);
            
            if (actorItem)
            {
                // we are already in the item thread, invoke directly
                return this.ItemOperationDestroy(item, operation);
            }
            else 
            {
                // search world cache just to see if item exists at all
                if (this.World.ItemCache.TryGetItem(operation.ItemId, out item) == false)
                {
                    return operation.GetOperationResponse((int)ReturnCode.ItemNotFound, "ItemNotFound3");
                }
                else
                {
                    // second parameter (peer) allows us to send an error event to the client (in case of an error)
                    // error ItemAccessDenied or ItemNotFound will be returned
                    item.Fiber.Enqueue(() => this.ExecItemOperation(() => this.ItemOperationDestroy(item, operation), sendParameters));

                    // operation is continued later
                    return null;
                }
            }
        }

        /// <summary>
        /// Handles operation DetachInterestArea: Detaches an existing InterestArea from an Item.
        /// </summary>
        public OperationResponse OperationDetachInterestArea(PeerBase peer, OperationRequest request)
        {
            var operation = new DetachInterestArea(peer.Protocol, request);
            if (!operation.IsValid)
            {
                return new OperationResponse(request.OperationCode) { ReturnCode = (int)ReturnCode.InvalidOperationParameter, DebugMessage = operation.GetErrorMessage() };
            }

            operation.OnStart();
            InterestArea interestArea;
            if (this.TryGetInterestArea(operation.InterestAreaId, out interestArea) == false)
            {
                return operation.GetOperationResponse((int)ReturnCode.InterestAreaNotFound, "InterestAreaNotFound");
            }

            lock (interestArea.SyncRoot)
            {
                interestArea.Detach();
            }

            return operation.GetOperationResponse(MethodReturnValue.Ok);
        }

        /// <summary>
        /// Handles operation ExitWorld: Sends event WorldExited to the client, disposes the actor and replaces the peer's Peer.CurrentOperationHandler with the MmoPeer itself.
        /// </summary>
        public OperationResponse OperationExitWorld(PeerBase peer, OperationRequest request)
        {
            var operation = new Operation();
            operation.OnStart();

            this.ExitWorld();

            // don't send response
            operation.OnComplete();
            return null;
        }

        /// <summary>
        /// Handles operation GetProperties: Sends event ItemProperties to the client.
        /// </summary>
        public OperationResponse OperationGetProperties(PeerBase peer, OperationRequest request, SendParameters sendParameters)
        {
            var operation = new GetProperties(peer.Protocol, request);
            if (!operation.IsValid)
            {
                return new OperationResponse(request.OperationCode) { ReturnCode = (int)ReturnCode.InvalidOperationParameter, DebugMessage = operation.GetErrorMessage() };
            }

            operation.OnStart();
            Item item;
            bool actorItem = this.TryGetItem(operation.ItemId, out item);
            if (actorItem == false)
            {
                if (this.World.ItemCache.TryGetItem(operation.ItemId, out item) == false)
                {
                    return operation.GetOperationResponse((int)ReturnCode.ItemNotFound, "ItemNotFound4");
                }
            }

            if (actorItem)
            {
                // we are already in the item thread, invoke directly
                return this.ItemOperationGetProperties(item, operation);
            }
            else
            {
                // second parameter (peer) allows us to send an error event to the client (in case of an error)
                item.Fiber.Enqueue(() => this.ExecItemOperation(() => this.ItemOperationGetProperties(item, operation), sendParameters));

                // operation is continued later
                return null;
            }
        }

        /// <summary>
        /// Handles operation Move: Move the items and ultimately sends event ItemMoved to other clients.
        /// </summary>
        public OperationResponse OperationMove(PeerBase peer, OperationRequest request, SendParameters sendParameters)
        {
            var operation = new Move(peer.Protocol, request);
            if (!operation.IsValid)
            {
                return new OperationResponse(request.OperationCode) { ReturnCode = (int)ReturnCode.InvalidOperationParameter, DebugMessage = operation.GetErrorMessage() };
            }

            operation.OnStart();
            Item item;
            if (string.IsNullOrEmpty(operation.ItemId))
            {
                item = this.Avatar;

                // set return values
                operation.ItemId = item.Id;
            }
            else if (this.TryGetItem(operation.ItemId, out item) == false)
            {
                return operation.GetOperationResponse((int)ReturnCode.ItemNotFound, "ItemNotFound5");
            }

            return this.ItemOperationMove((Item)item, operation, sendParameters);
        }


        /// <summary>

        // through this function each actor/player checks for collisions with all bullets in regions 
        // that are in his interestareas. if a bullet if found to collide with his ship he sends out 
        // a damage event to all clients so that they can update the hp for the ship with his itemid
        // other collisions such as laser, saber, bot, and resource are done here as well
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        public void CheckForCollisions(float msElapsed)
        {
            float elapsedSeconds = msElapsed / 1000f;
            List<Tuple<Item, Region>> removeList = null; // list of bullets to be removed
            Vector saberstartPt = new Vector();
            Vector saberendPt = new Vector();
            if (isSaberOut)
            {
                if (!wasdMode)
                {
                    saberstartPt = Avatar.Position + Avatar.Rotation * GlobalVars.saberStart;
                    saberendPt = Avatar.Position + Avatar.Rotation * GlobalVars.saberLength;
                }
                else
                {
                    saberstartPt = Avatar.Position + mouseFwd * GlobalVars.saberStart;
                    saberendPt = Avatar.Position + mouseFwd * GlobalVars.saberLength;
                }

                //Random r = new Random();
                //if (r.Next(50) < 5)
                //{
                //    log.InfoFormat("av pos " + Avatar.Position.ToString());
                //    log.InfoFormat("av rot " + Avatar.Rotation.ToString());
                //    log.InfoFormat("sab st " + saberstartPt.ToString());
                //    log.InfoFormat("av rot " + saberendPt.ToString());
                //}

                saberTimeLeft -= elapsedSeconds;
                if (saberTimeLeft < 0)
                    isSaberOut = false;
            }
            Vector laserstartPt = new Vector();
            Vector laserendPt = new Vector();
            if (isLaserOn)
            {
                if (!wasdMode)
                {
                    laserstartPt = Avatar.Position + Avatar.Rotation * GlobalVars.laserStart;
                    laserendPt = laserstartPt + Avatar.Rotation * GlobalVars.laserLength;
                }
                else
                {
                    laserstartPt = Avatar.Position + mouseFwd* GlobalVars.laserStart;
                    laserendPt = laserstartPt + mouseFwd * GlobalVars.laserLength;
                }
                laserTimeLeft -= elapsedSeconds;
                if (laserTimeLeft < 0)
                    isLaserOn = false;
            }

            SendParameters sp = GetDefaultSP();
            foreach (KeyValuePair<byte, InterestArea> iaEntry in interestAreas)
            {
                foreach (Region reg in iaEntry.Value.regions)
                {
                    foreach (Item regionItem in reg.myitems)
                    {
 

                        Vector offset = new Vector();
                        // if its a bullet and it's not my own bullet
                        if (CollisionHelper.CheckItemCollisionAgainstProjectile(Avatar, regionItem, 
                            ref this.hitpoints, sp, this, GlobalVars.playerShipRadius2, GlobalVars.playerShipRadius))
                        {

                                //if (removeList == null)
                                //    removeList = new List<Tuple<Item, Region>>();
                                //removeList.Add(new Tuple<Item, Region>(regionItem, reg));   // set projectile to be removed
                        }
                        // if my light saber is out check for collision with other player ships
                        else if (((byte)regionItem.Type == (byte)ItemType.Avatar || (byte)regionItem.Type == (byte)ItemType.Bot) && isSaberOut && regionItem != Avatar)
                        {

                            if (MathHelper.Intersects(saberstartPt, saberendPt, regionItem.Position, GlobalVars.playerShipRadius2,
                                 ref offset, GlobalVars.playerShipRadius, null, true))
                            {
                                log.InfoFormat("saber collide with " + regionItem.Id);
   

                                var eventInstance = new HpEvent
                                {
                                    ItemId = regionItem.Id,
                                    HpChange = GlobalVars.saberDmgPerFrame,
                                };
                                var eventData = new EventData((byte)EventCode.HpEvent, eventInstance);
                                var message = new ItemEventMessage(regionItem, eventData, sp);
                                regionItem.EventChannel.Publish(message);

                               // regionItem.SetPos(regionItem.Position + offset * .9f); // move over instantly to uncollided position
                               // regionItem.Velocity = regionItem.Velocity + (offset / offset.Len) * Avatar.GetCumulativeDotProducts(8) * 20f;

                                if ((byte)regionItem.Type == (byte)ItemType.Bot)
                                {
                                    BotManager.mobTable[regionItem.Id].TakeDamage(GlobalVars.saberDmgPerFrame);
                                }
                            }
                        }
                       
                        // if my laser is on check for collision with other player ships
                        else if (((byte)regionItem.Type == (byte)ItemType.Avatar || (byte)regionItem.Type == (byte)ItemType.Bot) 
                            && isLaserOn && regionItem != Avatar)
                        {
                            if (MathHelper.Intersects(laserstartPt, laserendPt, regionItem.Position, GlobalVars.playerShipRadius2,
                                ref offset, GlobalVars.playerShipRadius, null, false))
                            {
                                log.InfoFormat("laser collide with " + regionItem.Id);
                                var eventInstance = new HpEvent
                                {
                                    ItemId = regionItem.Id,
                                    HpChange = GlobalVars.laserDmgPerFrame,
                                };
                                var eventData = new EventData((byte)EventCode.HpEvent, eventInstance);
                                var message = new ItemEventMessage(regionItem, eventData, sp);
                                regionItem.EventChannel.Publish(message);
                                if ((byte)regionItem.Type == (byte)ItemType.Bot)
                                {
                                    BotManager.mobTable[regionItem.Id].TakeDamage(GlobalVars.laserDmgPerFrame);
                                }
                            }
                        }
                        // if it's a bot and laser or saber isn't on, then pass a reference of your own item to the bot
                        // so he can change to chase state and follow you
                        else if ((byte)regionItem.Type == (byte)ItemType.Bot)
                        {
                            float distanceToBot2 = (regionItem.Position - Avatar.Position).Len2;
                            if (distanceToBot2 < GlobalVars.BotSight2)
                            {
                                BotManager.ChangeMobToChaseState(regionItem.Id, (MmoActor)this);
                            }
                        }
                        else if ((byte)regionItem.Type == (byte)ItemType.Resource)
                        {
                            float distanceToRes2 = (regionItem.Position - Avatar.Position).Len2;
                            if (distanceToRes2 < GlobalVars.playerShipRadius2 + GlobalVars.HPradius2)
                            {
                                log.InfoFormat("hp collide");

                                if (removeList == null)
                                    removeList = new List<Tuple<Item, Region>>();
                                removeList.Add(new Tuple<Item, Region>(regionItem, reg));   // set bullet to be removed

                                var eventInstance = new HpEvent
                                {
                                    ItemId = Avatar.Id,
                                    HpChange = -GlobalVars.HPboxvalue,
                                };
                                var eventData = new EventData((byte)EventCode.HpEvent, eventInstance);
                                var message = new ItemEventMessage(regionItem, eventData, sp);
                                regionItem.EventChannel.Publish(message);

                            }
                        }
                    } //foreach item in region
                } //foreach region
            }
            if (removeList == null)
                return;
            foreach (Tuple<Item,Region> itemToRemove in removeList)
            {
                log.InfoFormat("removin: " + itemToRemove.Item1.Id);
                if ((byte)itemToRemove.Item1.Type == (byte)ItemType.Bullet)
                {
                    // set projectile to dead and the owning player will remove it himself
                    ((Bullet)itemToRemove.Item1).SetDead();
                    continue;
                }
                itemToRemove.Item1.Destroy();
                itemToRemove.Item1.Dispose();
                this.RemoveItem(itemToRemove.Item1);
                itemToRemove.Item2.myitems.Remove(itemToRemove.Item1);
                itemToRemove.Item1.World.ItemCache.RemoveItem(itemToRemove.Item1.Id);

                var eventInstance = new ItemDestroyed { ItemId = itemToRemove.Item1.Id };
                var eventData = new EventData((byte)EventCode.ItemDestroyed, eventInstance);
                this.Peer.SendEvent(eventData, new SendParameters { ChannelId = Settings.ItemEventChannel });
            }
        }

        public void ApplyBurst()
        {
            foreach (KeyValuePair<byte, InterestArea> iaEntry in interestAreas)
            {
                foreach (Region reg in iaEntry.Value.regions)
                {
                    foreach (Item item in reg.myitems)
                    {
                        if ( (((byte)item.Type == (byte)ItemType.Avatar && item != this.Avatar) || (byte)item.Type == (byte)ItemType.Bot ||
                            (byte)item.Type == (byte)ItemType.Bomb))
                        {
                            log.InfoFormat("bursting: " + item.Id);
                            Vector difference = item.Position - this.Avatar.Position;
                            float len2 = difference.Len2;
                            if (len2 < GlobalVars.burstRadiusSq)
                            {
                                log.InfoFormat("BBrsting: " + item.Id);
                                // lerp force based on distance
                                float t = (GlobalVars.burstRadiusSq - len2) / GlobalVars.burstRadiusSq;
                                float force = GlobalVars.burstForce * t;
                                difference = difference / (float)Math.Sqrt(len2);
                                

                                if ( (byte)item.Type == (byte)ItemType.Avatar || (byte)item.Type == (byte)ItemType.Bot)
                                {
                                    item.wasBursted = true;
                                    item.secSinceBursted = GlobalVars.timeForBurstToLast;
                                    force = force * 2f; // double force to account for dampening effect of ship
                                }

                                item.Velocity = item.Velocity + difference * force ;
                            }
                        }
                    }
                }
            }

            SendParameters sp = GetDefaultSP();
            var eventInstance = new BurstEvent
            {
                Position = Avatar.Position,
            };
            var eventData = new EventData((byte)EventCode.Burst, eventInstance);
            var message = new ItemEventMessage(Avatar, eventData, sp);
            Avatar.EventChannel.Publish(message);
            

        }


        // update bullet and bomb positions
        public void UpdateMyProjectiles(float timeElapsed)
        {
            float elapsedSeconds = timeElapsed / 1000f;
            List<string> removeList =  null;

            bool test = false;
            foreach (KeyValuePair<string, Item> item in ownedItems)
            {
                if (item.Value.Type == (byte)ItemType.Bullet)
                {
                    if (!((Bullet)item.Value).IsAlive())// bullet epxired
                    {
                    //    log.InfoFormat("bllet died " + item.Value.Id);

                        if (removeList == null)
                            removeList = new List<string>();
                        removeList.Add(item.Value.Id);   // set bullet to be removed
                        continue;   // skip bullet since its expired

                    }
                    // reduce bullet life timer
                    ((Bullet)item.Value).DecreaseLife(elapsedSeconds);
                    Vector newPos;

                    newPos = item.Value.Position + item.Value.Rotation * elapsedSeconds;
                    // log.InfoFormat("bullet elapsed time {0}", elapsedSeconds.ToString());

                    // publish move event for bullet ItemMoved
                    {
                        SendParameters sp = GetDefaultSP();

                        // save previous for event
                        Vector oldPosition = item.Value.Position;
                        // move
                        //item.Value.Move(newPos);
                        item.Value.SetPos(newPos);
                        item.Value.UpdateRegionSimple();

                        // send event
                        var eventInstance = new ItemMoved
                        {
                            ItemId = item.Value.Id,
                            OldPosition = oldPosition,
                            Position = newPos,
                            Rotation = item.Value.Rotation, // not changing rotation
                            OldRotation = item.Value.Rotation
                        };
                        var eventData = new EventData((byte)EventCode.ItemMoved, eventInstance);
                        var message = new ItemEventMessage(item.Value, eventData, sp);
                        item.Value.EventChannel.Publish(message);
                    }
                }   // end of if bullet
                if (item.Value.Type == (byte)ItemType.Bomb)
                {
                    // bomb just exploded so send damage events to nearby people, send bomb event
                    // so clients fire particles, alter nearby people's velocities
                    if (!((Bomb)item.Value).IsAlive())
                    {
                        test = true;
                        log.InfoFormat("bomb expldoed");
                        // set bomb to be removed
                        if (removeList == null)
                            removeList = new List<string>();
                        removeList.Add(item.Key);

                        // inflict damage and force on nearby players
                        foreach (KeyValuePair<byte, InterestArea> iaEntry in interestAreas)
                        {
                            foreach (Region reg in iaEntry.Value.regions)
                            {
                                foreach (Item regionItem in reg.myitems)
                                {
                                    if ((byte)regionItem.Type == (byte)ItemType.Avatar ||
                                        (byte)regionItem.Type == (byte)ItemType.Bot)
                                    {
                                        // distance from bomb to player
                                        Vector difference = regionItem.Position - item.Value.Position;
                                        float distanceToBomb2 = difference.Len2;
                                        if (distanceToBomb2 < GlobalVars.bombRadius2)
                                        {
                                            log.InfoFormat("bomb explosion collision with " + regionItem.Id.ToString());
                                            SendParameters sp = GetDefaultSP();
                                          //  sp.Unreliable = false;

                                            float t = (GlobalVars.bombRadius2 - distanceToBomb2) / GlobalVars.bombRadius2;
                                            int damageToShip = (int)Math.Floor(t * GlobalVars.bombDmg);
                                            var eventInstance = new HpEvent
                                            {
                                                ItemId = regionItem.Id,
                                                HpChange = damageToShip
                                            };
                                            var eventData = new EventData((byte)EventCode.HpEvent, eventInstance);
                                            var message = new ItemEventMessage(regionItem, eventData, sp);
                                            regionItem.EventChannel.Publish(message);
                                            float force = GlobalVars.bombForce * t;
                                            difference = difference / (float)Math.Sqrt(distanceToBomb2);
                                            regionItem.Velocity = regionItem.Velocity + difference * force;

                                            
                                            if ((byte)regionItem.Type == (byte)ItemType.Bot)
                                            {
                                                bool haskey = BotManager.mobTable.ContainsKey(regionItem.Id);
                                                BotManager.mobTable[regionItem.Id].TakeDamage(damageToShip);
                                            }
                                        }
                                    }
                                }
                            }

                        }
                    } //end of if bomb exploded
                    else
                    {
                        ((Bomb)item.Value).DecreaseLife(elapsedSeconds);

                        if (item.Value.Velocity.Len2 > GlobalVars.maxBombVel2)
                            item.Value.Velocity = item.Value.Velocity * GlobalVars.bombDamp;
                        Vector newPos = item.Value.Position + item.Value.Velocity * elapsedSeconds;

                        SendParameters sp = GetDefaultSP();

                        // save previous for event
                        Vector oldPosition = item.Value.Position;
                        // move
                        item.Value.SetPos(newPos);

                        // send event
                        var eventInstance = new ItemMoved
                        {
                            ItemId = item.Value.Id,
                            OldPosition = oldPosition,
                            Position = newPos,
                            Rotation = item.Value.Rotation, // not changing rotation
                            OldRotation = item.Value.Rotation
                        };
                        var eventData = new EventData((byte)EventCode.ItemMoved, eventInstance);
                        var message = new ItemEventMessage(item.Value, eventData, sp);
                        item.Value.EventChannel.Publish(message);
                    }
                } // if bomb
            }   // foreach item

            if (removeList == null)
            {
                return;
            }
            foreach (string str in removeList)
            {
                
                Item itemToRemove = ownedItems[str];
                
                bool removed = this.RemoveItem(itemToRemove);
                
                itemToRemove.CurrentWorldRegion.myitems.Remove(itemToRemove);
                itemToRemove.Destroy();
                itemToRemove.Dispose();
                itemToRemove.World.ItemCache.RemoveItem(itemToRemove.Id);
                bool removed2 = ownedItems.Remove(str);
                log.InfoFormat("removing projectil " + str + removed.ToString() + "disposed: " + itemToRemove.Disposed.ToString());

                // debug code
                //foreach (KeyValuePair<byte, InterestArea> iaEntry in interestAreas)
                //{
                //    foreach (Region reg in iaEntry.Value.regions)
                //    {

                //        log.InfoFormat("items in region[" + reg.ToString() + "]" + reg.myitems.Count);
                //    }
                //}
            }
        }

        public OperationResponse OperationFireBullet(PeerBase peer, OperationRequest request, SendParameters sendParameters)
        {
            var operation = new FireBullet(peer.Protocol, request);
           // operation.Position = Avatar.Position;
           // operation.Rotation = Avatar.Rotation;
            if (!operation.IsValid)
            {
                log.InfoFormat("invalid fire bullet op");
                return new OperationResponse(request.OperationCode) { ReturnCode = (int)ReturnCode.InvalidOperationParameter, DebugMessage = operation.GetErrorMessage() };
            }
            operation.OnStart();

            Item item;
            if (string.IsNullOrEmpty(operation.ItemId))
            {
                item = this.Avatar;

                // set return values
                operation.ItemId = item.Id;
            }
            else if (this.TryGetItem(operation.ItemId, out item) == false)
            {
                log.InfoFormat("cant find item with id: " + operation.ItemId);
                return operation.GetOperationResponse((int)ReturnCode.ItemNotFound, "playerWithIdNotFound1");
            }

            return this.OperationFireBulletHelper((Item)item, operation, sendParameters, peer.RoundTripTime);
        }

        public OperationResponse OperationFireSaber(PeerBase peer, OperationRequest request, SendParameters sendParameters)
        {
            var operation = new FireSaber(peer.Protocol, request);
            // operation.Position = Avatar.Position;
            // operation.Rotation = Avatar.Rotation;
            if (!operation.IsValid)
            {
                return new OperationResponse(request.OperationCode) { ReturnCode = (int)ReturnCode.InvalidOperationParameter, DebugMessage = operation.GetErrorMessage() };
            }
            operation.OnStart();

            Item item;
            if (string.IsNullOrEmpty(operation.ItemId))
            {
                item = this.Avatar;

                // set return values
                operation.ItemId = item.Id;
            }
            else if (this.TryGetItem(operation.ItemId, out item) == false)
            {
                return operation.GetOperationResponse((int)ReturnCode.ItemNotFound, "playerWithIdNotFound1");
            }

            if (!operation.Rotation.IsZero)
            {
                mouseFwd = operation.Rotation;
            }
            return this.OperationFireSaberHelper((Item)item, operation, sendParameters, peer.RoundTripTime);
        }




        private OperationResponse OperationFireSaberHelper(Item item, FireSaber operation, SendParameters sendParameters, int roundTripTime)
        {
            isSaberOut = true;
            saberTimeLeft = GlobalVars.saberOnTime;

            // should always be OK
            MethodReturnValue result = this.CheckAccess(item);
            if (result.IsOk)
            {
                // send event
                var eventInstance = new SaberFired
                {
                    ItemId = item.Id,
                };

                var eventData = new EventData((byte)EventCode.SaberSpawn, eventInstance);
                sendParameters.ChannelId = Settings.ItemEventChannel;
                var message = new ItemEventMessage(item, eventData, sendParameters);
                item.EventChannel.Publish(message); // anyone in the players event channel will need to spawn the bullet
                                                    // no response sent
                operation.OnComplete();
                return null;
            }

            return operation.GetOperationResponse(result);
        }
        public OperationResponse OperationEndSuperFast(PeerBase peer, OperationRequest request, SendParameters sendParameters)
        {
            var operation = new EndSuperFast(peer.Protocol, request);
            if (!operation.IsValid)
                return new OperationResponse(request.OperationCode) { ReturnCode = (int)ReturnCode.InvalidOperationParameter, DebugMessage = operation.GetErrorMessage() };

            operation.OnStart();

            Item item;
            if (string.IsNullOrEmpty(operation.ItemId))
            {
                item = this.Avatar;
                // set return values
                operation.ItemId = item.Id;
            }
            else if (this.TryGetItem(operation.ItemId, out item) == false)
                return operation.GetOperationResponse((int)ReturnCode.ItemNotFound, "playerWithIdNotFound1");


            return this.OperationEndSuperFastHelper((Item)item, operation, sendParameters);
        }

        private OperationResponse OperationEndSuperFastHelper(Item item, EndSuperFast operation, SendParameters sendParameters)
        {
            log.InfoFormat("end superfast");
            isSuperFast = false;
            

            // should always be OK
            MethodReturnValue result = this.CheckAccess(item);
            if (result.IsOk)
            {
                // send event
                var eventInstance = new StartSuperFastEvent
                {
                    ItemId = item.Id,
                };

                var eventData = new EventData((byte)EventCode.EndSuperFast, eventInstance);
                sendParameters.ChannelId = Settings.ItemEventChannel;
                var message = new ItemEventMessage(item, eventData, sendParameters);
                item.EventChannel.Publish(message); // anyone in the players event channel will need to spawn the bullet
                                                    // no response sent
                operation.OnComplete();
                return null;
            }
            return operation.GetOperationResponse(result);
        }

        public OperationResponse OperationStartSuperFast(PeerBase peer, OperationRequest request, SendParameters sendParameters)
        {
            var operation = new StartSuperFast(peer.Protocol, request);
            if (!operation.IsValid)
                return new OperationResponse(request.OperationCode) { ReturnCode = (int)ReturnCode.InvalidOperationParameter, DebugMessage = operation.GetErrorMessage() };

            operation.OnStart();

            Item item;
            if (string.IsNullOrEmpty(operation.ItemId))
            {
                item = this.Avatar;
                // set return values
                operation.ItemId = item.Id;
            }
            else if (this.TryGetItem(operation.ItemId, out item) == false)
                return operation.GetOperationResponse((int)ReturnCode.ItemNotFound, "playerWithIdNotFound1");


            return this.OperationStSuperFastHelper((Item)item, operation, sendParameters);
        }

        private OperationResponse OperationStSuperFastHelper(Item item, StartSuperFast operation, SendParameters sendParameters)
        {
            log.InfoFormat("st superfast");
            isSuperFast = true;
            Avatar.Rotation = operation.Rotation;
            Avatar.Velocity = Avatar.Rotation * GlobalVars.SuperFastVel;

            // should always be OK
            MethodReturnValue result = this.CheckAccess(item);
            if (result.IsOk)
            {
                // send event
                var eventInstance = new StartSuperFastEvent
                {
                    ItemId = item.Id,
                };

                var eventData = new EventData((byte)EventCode.StartSuperFast, eventInstance);
                sendParameters.ChannelId = Settings.ItemEventChannel;
                var message = new ItemEventMessage(item, eventData, sendParameters);
                item.EventChannel.Publish(message); // anyone in the players event channel will need to spawn the bullet
                                                    // no response sent
                operation.OnComplete();
                return null;
            }
            return operation.GetOperationResponse(result);
        }


        public OperationResponse OperationShootBomb(PeerBase peer, OperationRequest request, SendParameters sendParameters)
        {
            var operation = new ShootBomb(peer.Protocol, request);
            // operation.Position = Avatar.Position;
            // operation.Rotation = Avatar.Rotation;
            if (!operation.IsValid)
            {
                return new OperationResponse(request.OperationCode) { ReturnCode = (int)ReturnCode.InvalidOperationParameter, DebugMessage = operation.GetErrorMessage() };
            }
            operation.OnStart();

            Item item;
            if (string.IsNullOrEmpty(operation.ItemId))
            {
                item = this.Avatar;

                // set return values
                operation.ItemId = item.Id;
            }
            else if (this.TryGetItem(operation.ItemId, out item) == false)
            {
                return operation.GetOperationResponse((int)ReturnCode.ItemNotFound, "playerWithIdNotFound1");
            }

            return this.OperationShootBombHelper((Item)item, operation, sendParameters, peer.RoundTripTime);
        }

        private OperationResponse OperationShootBombHelper(Item item, ShootBomb operation, SendParameters sendParameters, int roundTripTime)
        {
            log.InfoFormat("shooting bomb w rot:" + operation.Rotation);
            isBombOut = true;
            bombTimeLeft = GlobalVars.bombTime;

            // should always be OK
            MethodReturnValue result = this.CheckAccess(item);
            if (result.IsOk)
            {
                Object obj = new Object();
                Bomb newBomb;
                lock (obj)
                {
                    if (GlobalVars.bulletCount > 9000) GlobalVars.bulletCount = 0;

                    float oneWayTripTime = ((float)roundTripTime / 1000f) / 2;
                    float watchElapsed = (float)watch.Elapsed.Milliseconds;
                  //  log.InfoFormat("oneway tr time: {0} watch: {1}", oneWayTripTime.ToString(), watchElapsed.ToString());
                    // update bullet's position using it's velocity and the time elapsed (time for packet to get to server)
                    Vector newPos = Avatar.Position + Avatar.Velocity * watchElapsed / 1000f;
                 //   log.InfoFormat("avpos:" + Avatar.Position.ToString());
                //    log.InfoFormat("newpos for bult:" + newPos.ToString());
                    // + operation.Rotation * oneWayTripTime * GlobalVars.bulletSpeed;

                    Vector bombVelocity = Avatar.Velocity + operation.Rotation * GlobalVars.bombSpeed;
                //    log.InfoFormat("bvel " + bombVelocity.ToString());
                    newBomb = new Bomb(newPos, new Vector(0,0), null, this,
                        "zz" + (GlobalVars.bulletCount++ % 99).ToString(), (byte)ItemType.Bomb, world);
                    newBomb.secsFromLastUpdateDone = watchElapsed;
                    newBomb.Velocity = bombVelocity;
                    newBomb.forward = operation.Rotation;
                }
                newBomb.World.ItemCache.AddItem(newBomb);
                AddItem(newBomb);

                // may need to manually update reigions here

                newBomb.UpdateRegionSimple();

                // send event
                var eventInstance = new BombFired
                {
                    ItemId = newBomb.Id,
                    Position = Avatar.Position,
                };

                var eventData = new EventData((byte)EventCode.BombSpawn, eventInstance);
                sendParameters.ChannelId = Settings.ItemEventChannel;
                var message = new ItemEventMessage(item, eventData, sendParameters);
                item.EventChannel.Publish(message); // anyone in the players event channel will need to spawn the bullet
                                                    // no response sent
                operation.OnComplete();
                return null;
            }

            return operation.GetOperationResponse(result);
        }

        // untested custom operation for when the player fires a laser weapon
        public OperationResponse OperationFireLaser(PeerBase peer, OperationRequest request, SendParameters sendParameters)
        {
            var operation = new FireLaser(peer.Protocol, request);
            if (!operation.IsValid)
            {
                return new OperationResponse(request.OperationCode) { ReturnCode = (int)ReturnCode.InvalidOperationParameter, DebugMessage = operation.GetErrorMessage() };
            }
            operation.OnStart();

            Item item;
            if (string.IsNullOrEmpty(operation.ItemId))
            {
                item = this.Avatar;

                // set return values
                operation.ItemId = item.Id;
            }
            else if (this.TryGetItem(operation.ItemId, out item) == false)
            {
                return operation.GetOperationResponse((int)ReturnCode.ItemNotFound, "playerWithIdNotFound");
            }

            if (!operation.Rotation.IsZero)
            {
                mouseFwd = operation.Rotation;
            }

            return this.OperationFireLaserHelper((Item)item, operation, sendParameters);
        }



        /// <summary>
        /// Handles operation MoveInterestArea: Moves one of the actor's InterestArea.
        /// </summary>
        public OperationResponse OperationMoveInterestArea(PeerBase peer, OperationRequest request)
        {
            var operation = new MoveInterestArea(peer.Protocol, request);
            if (!operation.IsValid)
            {
                return new OperationResponse(request.OperationCode) { ReturnCode = (int)ReturnCode.InvalidOperationParameter, DebugMessage = operation.GetErrorMessage() };
            }

            operation.OnStart();
            InterestArea interestArea;
            if (this.TryGetInterestArea(operation.InterestAreaId, out interestArea))
            {
                lock (interestArea.SyncRoot)
                {
                    interestArea.Position = operation.Position;
                    interestArea.UpdateInterestManagement();
                }

                // don't send response
                return null;
            }

            return operation.GetOperationResponse((int)ReturnCode.InterestAreaNotFound, "InterestAreaNotFound");
        }

        /// <summary>
        /// Handles operation RemoveInterestArea: Removes one of the actor's InterestAreas.
        /// </summary>
        public OperationResponse OperationRemoveInterestArea(PeerBase peer, OperationRequest request)
        {
            var operation = new RemoveInterestArea(peer.Protocol, request);
            if (!operation.IsValid)
            {
                return new OperationResponse(request.OperationCode) { ReturnCode = (int)ReturnCode.InvalidOperationParameter, DebugMessage = operation.GetErrorMessage() };
            }

            operation.OnStart();
            InterestArea interestArea;
            if (this.TryGetInterestArea(operation.InterestAreaId, out interestArea))
            {
                lock (interestArea.SyncRoot)
                {
                    interestArea.Detach();
                    interestArea.Dispose();
                }

                this.RemoveInterestArea(operation.InterestAreaId);
                return operation.GetOperationResponse(MethodReturnValue.Ok);
            }

            return operation.GetOperationResponse((int)ReturnCode.InterestAreaNotFound, "InterestAreaNotFound");
        }

        /// <summary>
        /// Handles operation SetProperties: Sets the Item.Properties of an Item and ultimately sends event ItemPropertiesSet to other clients.
        /// </summary>
        public OperationResponse OperationSetProperties(PeerBase peer, OperationRequest request, SendParameters sendParameters)
        {
            var operation = new SetProperties(peer.Protocol, request);
            if (!operation.IsValid)
            {
                return new OperationResponse(request.OperationCode) { ReturnCode = (int)ReturnCode.InvalidOperationParameter, DebugMessage = operation.GetErrorMessage() };
            }

            operation.OnStart();
            Item item;
            if (string.IsNullOrEmpty(operation.ItemId))
            {
                item = this.Avatar;

                // set return values
                operation.ItemId = item.Id;
            }
            else if (this.TryGetItem(operation.ItemId, out item) == false)
            {
                return operation.GetOperationResponse((int)ReturnCode.ItemNotFound, "ItemNotFound7");
            }

            return this.ItemOperationSetProperties(item, operation, sendParameters);
        }

        /// <summary>
        /// Handles operation SetViewDistance: Changes the subscribe and unsubscribe radius for an InterestArea.
        /// </summary>
        public OperationResponse OperationSetViewDistance(PeerBase peer, OperationRequest request)
        {
            var operation = new SetViewDistance(peer.Protocol, request);
            if (!operation.IsValid)
            {
                return new OperationResponse(request.OperationCode) { ReturnCode = (int)ReturnCode.InvalidOperationParameter, DebugMessage = operation.GetErrorMessage() };
            }

            operation.OnStart();
            InterestArea interestArea;
            if (this.TryGetInterestArea(operation.InterestAreaId, out interestArea) == false)
            {
                return operation.GetOperationResponse((int)ReturnCode.InterestAreaNotFound, "InterestAreaNotFound");
            }

            lock (interestArea.SyncRoot)
            {
                interestArea.ViewDistanceEnter = operation.ViewDistanceEnter;
                interestArea.ViewDistanceExit = operation.ViewDistanceExit;
                interestArea.UpdateInterestManagement();
            }

            // bot test code
          //  Random r = new Random();
          //  if (r.Next(100) < 3)
          //      AddBotToWorld();


            // don't send response
            return null;
        }

        /// <summary>
        /// Handles operation SpawnItem: Creates a new Item and optionally subscribes an InterestArea to it.
        /// </summary>
        public OperationResponse OperationSpawnItem(PeerBase peer, OperationRequest request)
        {
            log.InfoFormat("Spawn item operation started for peer: {0}", this.Peer.ToString());
            var operation = new SpawnItem(peer.Protocol, request);
            if (!operation.IsValid)
            {
                log.InfoFormat("Spawn item operation invalid");
                return new OperationResponse(request.OperationCode) { ReturnCode = (int)ReturnCode.InvalidOperationParameter, DebugMessage = operation.GetErrorMessage() };
            }

            operation.OnStart();
            var item = new Item(operation.Position, operation.Rotation, operation.Properties, this, operation.ItemId, operation.ItemType, this.World);
            if (this.World.ItemCache.AddItem(item))
            {
                log.InfoFormat("adding item");
                this.AddItem(item);
                return this.ItemOperationSpawn(item, operation);
            }
            log.InfoFormat("Spawn item operation for peer: {0}", this.Peer.ToString());
            item.Dispose();
            return operation.GetOperationResponse((int)ReturnCode.ItemAlreadyExists, "ItemAlreadyExists");
        }

        /// <summary>
        /// Handles operation SubscribeItem: Manually subscribes item (does not affect interest area updates).
        /// The client receives event ItemSubscribed on success.
        /// </summary>
        /// <remarks>        
        /// If the submitted SubscribeItem.PropertiesRevision is null or smaller than the item Item.PropertiesRevision event ItemProperties is sent to the client.        
        /// </remarks>
        public OperationResponse OperationSubscribeItem(PeerBase peer, OperationRequest request, SendParameters sendParameters)
        {
            log.InfoFormat("sub item operation");
            var operation = new SubscribeItem(peer.Protocol, request);
            if (!operation.IsValid)
            {
                return new OperationResponse(request.OperationCode) { ReturnCode = (int)ReturnCode.InvalidOperationParameter, DebugMessage = operation.GetErrorMessage() };
            }

            operation.OnStart();

            Item item;
            bool actorItem = this.TryGetItem(operation.ItemId, out item);
            if (actorItem == false)
            {
                if (this.World.ItemCache.TryGetItem(operation.ItemId, out item) == false)
                {
                    return operation.GetOperationResponse((int)ReturnCode.ItemNotFound, "ItemNotFound8");
                }
            }

            if (actorItem)
            {
                // we are already in the item thread, invoke directly
                return this.ItemOperationSubscribeItem(item, operation);
            }
            else
            {
                // second parameter (peer) allows us to send an error event to the client (in case of an error)
                item.Fiber.Enqueue(() => this.ExecItemOperation(() => this.ItemOperationSubscribeItem(item, operation), sendParameters));

                // operation continues later
                return null;
            }
        }

        /// <summary>
        /// Handles operation UnsubscribeItem: manually unsubscribes an existing InterestArea from an existing Item.
        /// The client receives event ItemUnsubscribed on success.
        /// </summary>
        public OperationResponse OperationUnsubscribeItem(PeerBase peer, OperationRequest request, SendParameters sendParameters)
        {
            log.DebugFormat("op unsub item");
            var operation = new UnsubscribeItem(peer.Protocol, request);
            if (!operation.IsValid)
            {
                return new OperationResponse(request.OperationCode) { ReturnCode = (int)ReturnCode.InvalidOperationParameter, DebugMessage = operation.GetErrorMessage() };
            }

            operation.OnStart();

            Item item;
            bool actorItem = this.TryGetItem(operation.ItemId, out item);
            if (actorItem == false)
            {
                if (this.World.ItemCache.TryGetItem(operation.ItemId, out item) == false)
                {
                    return operation.GetOperationResponse((int)ReturnCode.ItemNotFound, "ItemNotFound9");
                }
            }

            this.interestItems.UnsubscribeItem(item);
            
            if (actorItem)
            {
                // we are already in the item thread, invoke directly
                return this.ItemOperationUnsubscribeItem(item, operation);
            }
            else
            {
                // second parameter (peer) allows us to send an error event to the client (in case of an error)
                item.Fiber.Enqueue(() => this.ExecItemOperation(() => this.ItemOperationUnsubscribeItem(item, operation), sendParameters));

                // operation continues later
                return null;
            }
        }

        /// <summary>
        ///   Handles operation RaiseGenericEvent. Sends event ItemGeneric to an Item owner or the subscribers of an Item />.
        /// </summary>
        public OperationResponse OperationRaiseGenericEvent(PeerBase peer, OperationRequest request, SendParameters sendParameters)
        {
            var operation = new RaiseGenericEvent(peer.Protocol, request);
            if (!operation.IsValid)
            {
                return new OperationResponse(request.OperationCode) { ReturnCode = (int)ReturnCode.InvalidOperationParameter, DebugMessage = operation.GetErrorMessage() };
            }

            operation.OnStart();
            Item item;
            bool actorItem = true;
            if (this.TryGetItem(operation.ItemId, out item) == false)
            {
                if (this.World.ItemCache.TryGetItem(operation.ItemId, out item) == false)
                {
                    return operation.GetOperationResponse((int)ReturnCode.ItemNotFound, "ItemNotFound10");
                }

                actorItem = false;
            }

            if (actorItem)
            {
                // we are already in the item thread, invoke directly
                return ItemOperationRaiseGenericEvent(item, operation, sendParameters);
            }

            // second parameter (peer) allows us to send an error event to the client (in case of an error)
            item.Fiber.Enqueue(() => this.ExecItemOperation(() => ItemOperationRaiseGenericEvent(item, operation, sendParameters), sendParameters));

            // operation continued later
            return null;
        }


        /// <summary>
        /// Disposes the actor, stops any further operation handling and disposes the Peer.
        /// </summary>
        public void OnDisconnect(PeerBase peer)
        {
            this.Dispose();
            ((Peer)peer).SetCurrentOperationHandler(null);
            peer.Dispose();
        }

        /// <summary>
        /// Kicks the actor from the world (event WorldExited is sent to the client) and then disconnects the client.
        /// </summary>
        /// <remarks>
        /// Called by DisconnectByOtherPeer after being enqueued to the PeerBase.RequestFiber.
        /// It kicks the actor from the world (event WorldExited) and then continues the original request by calling the original peer's OnOperationRequest method.        
        /// </remarks>
        public void OnDisconnectByOtherPeer(PeerBase otherPeer, OperationRequest otherRequest, SendParameters sendParameters)
        {
            this.ExitWorld();

            // disconnect peer after the exit world event is sent
            this.Peer.RequestFiber.Enqueue(() => this.Peer.RequestFiber.Enqueue(this.Peer.Disconnect));

            // continue execution of other request
            PeerHelper.InvokeOnOperationRequest(otherPeer, otherRequest, sendParameters);
        }

        /// <summary>
        /// Enqueues OnDisconnectByOtherPeer to the PeerBase.RequestFiber.
        /// </summary>
        /// <remarks>
        /// This method is intended to be used to disconnect a user's peer if he connects with multiple clients while the application logic wants to allow just one.
        /// </remarks>
        public void DisconnectByOtherPeer(PeerBase otherPeer, OperationRequest otherRequest, SendParameters sendParameters)
        {
            this.Peer.RequestFiber.Enqueue(() => this.OnDisconnectByOtherPeer(otherPeer, otherRequest, sendParameters));
        }

        public OperationResponse OnOperationRequest(PeerBase peer, OperationRequest operationRequest, SendParameters sendParameters)
        {
            if ((OperationCode)operationRequest.OperationCode != OperationCode.Move
                && (OperationCode)operationRequest.OperationCode != OperationCode.VelocityRotation
                && (OperationCode)operationRequest.OperationCode != OperationCode.Breaks)
            {
                log.InfoFormat(Avatar.Id.Substring(0,3) + " op req code: {0}, rel: {1}" , 
                    ((OperationCode)operationRequest.OperationCode).ToString(), 
                    sendParameters.Unreliable.ToString());
            }
            switch ((OperationCode)operationRequest.OperationCode)
            {
                case OperationCode.AddInterestArea:
                    return this.OperationAddInterestArea(peer, operationRequest, sendParameters);

                case OperationCode.AttachInterestArea:
                    return this.OperationAttachInterestArea(peer, operationRequest, sendParameters);

                case OperationCode.DestroyItem:
                    return this.OperationDestroyItem(peer, operationRequest, sendParameters);

                case OperationCode.DetachInterestArea:
                    return this.OperationDetachInterestArea(peer, operationRequest);

                case OperationCode.ExitWorld:
                    return this.OperationExitWorld(peer, operationRequest);

                case OperationCode.GetProperties:
                    return this.OperationGetProperties(peer, operationRequest, sendParameters);

                case OperationCode.Move:
                    return this.OperationMove(peer, operationRequest, sendParameters);

                case OperationCode.MoveInterestArea:
                    return this.OperationMoveInterestArea(peer, operationRequest);

                case OperationCode.RemoveInterestArea:
                    return this.OperationRemoveInterestArea(peer, operationRequest);

                case OperationCode.SetProperties:
                    return this.OperationSetProperties(peer, operationRequest, sendParameters);

                case OperationCode.SetViewDistance:
                    return this.OperationSetViewDistance(peer, operationRequest);

                case OperationCode.SpawnItem:
                    return this.OperationSpawnItem(peer, operationRequest);

                case OperationCode.SubscribeItem:
                    return this.OperationSubscribeItem(peer, operationRequest, sendParameters);

                case OperationCode.UnsubscribeItem:
                    return this.OperationUnsubscribeItem(peer, operationRequest, sendParameters);

                case OperationCode.RadarSubscribe:
                    return MmoInitialOperationHandler.OperationRadarSubscribe(peer, operationRequest, sendParameters);

                case OperationCode.SubscribeCounter:
                    return CounterOperations.SubscribeCounter(peer, operationRequest);

                case OperationCode.UnsubscribeCounter:
                    return CounterOperations.SubscribeCounter(peer, operationRequest);

                case OperationCode.RaiseGenericEvent:
                    return this.OperationRaiseGenericEvent(peer, operationRequest, sendParameters);

                case OperationCode.FireLaser:
                    return this.OperationFireLaser(peer, operationRequest, sendParameters);
                case OperationCode.FireBullet:
                    return this.OperationFireBullet(peer, operationRequest, sendParameters);

                case OperationCode.CreateWorld:
                case OperationCode.EnterWorld:
                    return InvalidOperation(operationRequest);
                case OperationCode.VelocityRotation:
                    return OperationVelocityRot(peer, operationRequest, sendParameters);
                case OperationCode.Breaks:
                 //   log.InfoFormat("applying breaks");
                    Avatar.Velocity = Avatar.Velocity * GlobalVars.breaksDampFactor;
                    return new OperationResponse(operationRequest.OperationCode);
                case OperationCode.Burst:
                    log.InfoFormat("bursting");
                    ApplyBurst();
                    return new OperationResponse(operationRequest.OperationCode);
                case OperationCode.FireSaber:

                    return this.OperationFireSaber(peer, operationRequest, sendParameters);

                case OperationCode.ShootBomb:

                    return this.OperationShootBomb(peer, operationRequest, sendParameters);
                case OperationCode.StartSuperFast:
                    return this.OperationStartSuperFast(peer, operationRequest, sendParameters);
                case OperationCode.EndSuperFast:
                    return this.OperationEndSuperFast(peer, operationRequest, sendParameters);



            }

            return new OperationResponse(operationRequest.OperationCode)
                {
                    ReturnCode = (int)ReturnCode.OperationNotSupported,
                    DebugMessage = "OperationNotSupported: " + operationRequest.OperationCode
                };
        }

        public OperationResponse OperationVelocityRot(PeerBase peer, OperationRequest request, SendParameters sendParameters)
        {
            var operation = new VelocityRotation(peer.Protocol, request);
            if (!operation.IsValid)
            {
                return new OperationResponse(request.OperationCode) { ReturnCode = (int)ReturnCode.InvalidOperationParameter, DebugMessage = operation.GetErrorMessage() };
            }
           
            operation.OnStart();
            Item item;
            if (string.IsNullOrEmpty(operation.ItemId))
            {
                item = this.Avatar;

                // set return values
                operation.ItemId = item.Id;
            }
            else if (this.TryGetItem(operation.ItemId, out item) == false)
            {
                return operation.GetOperationResponse((int)ReturnCode.ItemNotFound, "ItemNotFound5");
            }


            return this.ItemOperationVelRot((Item)item, operation, sendParameters);
        }
    

        private static OperationResponse ItemOperationAddInterestArea(Item item, AddInterestArea operation, InterestArea interestArea)
        {
            log.DebugFormat("io add interest area");
            if (item.Disposed)
            {
                return operation.GetOperationResponse((int)ReturnCode.ItemNotFound, "ItemNotFound11");
            }

            lock (interestArea.SyncRoot)
            {
                interestArea.AttachToItem(item);
                interestArea.ViewDistanceEnter = operation.ViewDistanceEnter;
                interestArea.ViewDistanceExit = operation.ViewDistanceExit;
                interestArea.UpdateInterestManagement();
            }

            operation.OnComplete();
            return operation.GetOperationResponse(MethodReturnValue.Ok);
        }

        private MethodReturnValue CheckAccess(Item item)
        {
            if (item.Disposed)
            {
                return MethodReturnValue.New((int)ReturnCode.ItemNotFound, "ItemNotFound12");
            }

            if (((Item)item).GrantWriteAccess(this))
            {
                return MethodReturnValue.Ok;
            }

            return MethodReturnValue.New((int)ReturnCode.ItemAccessDenied, "ItemAccessDenied");
        }

        /// <summary>
        /// Executes an item operation and returns an error response in case an exception occurs.
        /// </summary>
        private void ExecItemOperation(Func<OperationResponse> operation, SendParameters sendParameters)
        {
            OperationResponse result = operation();
            if (result != null)
            {
                this.Peer.SendOperationResponse(result, sendParameters);
            }
        }

        private void ExitWorld()
        {
            log.InfoFormat("exiting " + this.Avatar.Id);
            var worldExited = new WorldExited { WorldName = ((World)this.World).Name };
            this.Dispose();

            // set initial handler
            ((MmoPeer)this.Peer).SetInitialOperationhandler();            

            var eventData = new EventData((byte)EventCode.WorldExited, worldExited);

            // use item channel to ensure that this event arrives in correct order with move/subscribe events
            this.Peer.SendEvent(eventData, new SendParameters { ChannelId = Settings.ItemEventChannel });
        }

        private OperationResponse ItemOperationAttachInterestArea(
            Item item, AttachInterestArea operation, InterestArea interestArea, SendParameters sendParameters)
        {
            log.InfoFormat("io attach interest area");
            if (item.Disposed)
            {
                return operation.GetOperationResponse((int)ReturnCode.ItemNotFound, "ItemNotFound13");
            }

            lock (interestArea.SyncRoot)
            {
                interestArea.Detach();
                interestArea.AttachToItem(item);
                interestArea.UpdateInterestManagement();
            }

            // use item channel to ensure that this event arrives before any move or subscribe events
            OperationResponse response = operation.GetOperationResponse(MethodReturnValue.Ok);
            sendParameters.ChannelId = Settings.ItemEventChannel;
            this.Peer.SendOperationResponse(response, sendParameters);

            operation.OnComplete();
            return null;
        }

        private OperationResponse ItemOperationDestroy(Item item, DestroyItem operation)
        {
            MethodReturnValue result = this.CheckAccess(item);
            if (result.IsOk)
            {
                item.Destroy();
                item.Dispose();
                this.RemoveItem(item);

                item.World.ItemCache.RemoveItem(item.Id);
                var eventInstance = new ItemDestroyed { ItemId = item.Id };
                var eventData = new EventData((byte)EventCode.ItemDestroyed, eventInstance);
                this.Peer.SendEvent(eventData, new SendParameters { ChannelId = Settings.ItemEventChannel });

                // no response, event is sufficient
                operation.OnComplete();
                return null;
            }

            return operation.GetOperationResponse(result);
        }

        private OperationResponse ItemOperationGetProperties(Item item, GetProperties operation)
        {
            if (item.Disposed)
            {
                return operation.GetOperationResponse((int)ReturnCode.ItemNotFound, "ItemNotFound14");
            }

            if (item.Properties != null)
            {
                if (operation.PropertiesRevision.HasValue == false || operation.PropertiesRevision.Value != item.PropertiesRevision)
                {
                    var properties = new ItemProperties
                        {
                            ItemId = item.Id,
                            PropertiesRevision = item.PropertiesRevision,
                            PropertiesSet = new Hashtable(item.Properties)
                        };

                    var eventData = new EventData((byte)EventCode.ItemProperties, properties);
                    this.Peer.SendEvent(eventData, new SendParameters { ChannelId = Settings.ItemEventChannel });
                }
            }

            // no response sent
            operation.OnComplete();
            return null;
        }
        private OperationResponse OperationFireBulletHelper(Item item, FireBullet operation, SendParameters sendParameters, int roundTripTime)
        {
          //  log.InfoFormat("player rot: {0}", this.Avatar.Rotation.ToString());

            // should always be OK
            MethodReturnValue result = this.CheckAccess(item);
            if (result.IsOk)
            {
                Object obj = new Object();
                Bullet newBullet;
                lock (obj)
                {
                    if (GlobalVars.bulletCount > 9000) GlobalVars.bulletCount = 0;

                    float oneWayTripTime = ((float)roundTripTime / 1000f) / 2;
                    float watchElapsed = (float)watch.Elapsed.Milliseconds;
                    //     log.InfoFormat("blt trip time: {0} watch: {1}", oneWayTripTime.ToString(), watchElapsed.ToString());
                    // update bullet's position using it's velocity and the time elapsed (time for packet to get to server)
                    Vector newPos = item.Position + item.Velocity * oneWayTripTime;//watchElapsed / 1000f;
              //      log.InfoFormat("avpos:" + Avatar.Position.ToString());
              //      log.InfoFormat("newpos for bult:" + newPos.ToString());
              // + operation.Rotation * oneWayTripTime * GlobalVars.bulletSpeed;

                    Vector bulletVelocity = item.Velocity + operation.Rotation * GlobalVars.bulletSpeed;
               //     log.InfoFormat("bvel " + bulletVelocity.ToString());
                    newBullet = new Bullet(newPos, bulletVelocity, null, this, 
                        "bt" + (GlobalVars.bulletCount++%99).ToString(), (byte)ItemType.Bullet, world);
                    newBullet.secsFromLastUpdateDone = watchElapsed;
                    newBullet.forward = operation.Rotation;
                }
                newBullet.World.ItemCache.AddItem(newBullet);
                AddItem(newBullet);

                // may need to manually update reigions here

                newBullet.UpdateRegionSimple();
                
              //  a different way to spawn item by specifics, (found out it had problems moving the item though)
                 log.InfoFormat("adding bullet {0}, pos: {1}, ROT: {2}", newBullet.Id, operation.Position.ToString(), operation.Rotation.ToString());
                // send event
                var eventInstance = new BulletFired
                {
                    ItemId = newBullet.Id,
                    Position = operation.Position,
                    Rotation = operation.Rotation,
                };

                var eventData = new EventData((byte)EventCode.BulletSpawn, eventInstance);
                sendParameters.ChannelId = Settings.ItemEventChannel;
                var message = new ItemEventMessage(item, eventData, sendParameters);
                item.EventChannel.Publish(message); // anyone in the players event channel will need to spawn the bullet
                // no response sent
                
                operation.OnComplete();
                return null;
            }

            return operation.GetOperationResponse(result);
        }

        private void AddBotToWorld()
        {
            //Item newbot = botman.AddBot(Avatar.Position, Avatar.Position, 20f, this, world);
            //newbot.World.ItemCache.AddItem(newbot);
            //AddItem(newbot);
            //newbot.UpdateRegionSimple();

            //log.InfoFormat("adding bot to player");
            //// send event
            //var eventInstance = new BotSpawn
            //{
            //    ItemId = newbot.Id,
            //    Position = newbot.Position,
            //    Rotation = newbot.Rotation,
            //};

            //SendParameters sp = GetDefaultSP();
            //sp.Unreliable = false;
            //var eventData = new EventData((byte)EventCode.BotSpawn, eventInstance);
            //var message = new ItemEventMessage(Avatar, eventData, sp);
            //Avatar.EventChannel.Publish(message); // anyone in the players event channel will need to spawn the bot
        }

        private OperationResponse OperationFireLaserHelper(Item item, FireLaser operation, SendParameters sendParameters)
        {
            // should always be OK
            MethodReturnValue result = this.CheckAccess(item);
            if (result.IsOk)
            {
                // send event
                var eventInstance = new LaserFired
                {
                    ItemId = item.Id,
                    Position = item.Position,
                    Rotation = item.Rotation,
                };


                var eventData = new EventData((byte)EventCode.FireLaser, eventInstance);
                sendParameters.ChannelId = Settings.ItemEventChannel;
                var message = new ItemEventMessage(item, eventData, sendParameters);
                item.EventChannel.Publish(message);

                isLaserOn = true;
                laserTimeLeft = GlobalVars.laserOnTime;

                // no response sent
                operation.OnComplete();
                return null;
            }
            return operation.GetOperationResponse(result);
        }

        private OperationResponse ItemOperationMove(Item item, Move operation, SendParameters sendParameters)
        {
            // should always be OK
            MethodReturnValue result = this.CheckAccess(item);
            if (result.IsOk)
            {
                // save previous for event
                Vector oldPosition = item.Position;
                Vector oldRotation = item.Rotation;

                // move
                item.Rotation = operation.Rotation;
                item.Move(operation.Position);
              //  log.InfoFormat("operation move test pos: {0}", operation.Position.ToString());
                // send event
                var eventInstance = new ItemMoved
                    {
                        ItemId = item.Id,
                        OldPosition = oldPosition,
                        Position = operation.Position,
                        Rotation = operation.Rotation,
                        OldRotation = oldRotation
                    };

                var eventData = new EventData((byte)EventCode.ItemMoved, eventInstance);
                sendParameters.ChannelId = Settings.ItemEventChannel;
                var message = new ItemEventMessage(item, eventData, sendParameters);
                item.EventChannel.Publish(message);

                // no response sent
                operation.OnComplete();
                return null;
            }

            return operation.GetOperationResponse(result);
        }

        private OperationResponse ItemOperationVelRot(Item item, VelocityRotation operation, SendParameters sendParameters)
        {
            // should always be OK
            MethodReturnValue result = this.CheckAccess(item);
            if (result.IsOk)
            {
                // move
                if (operation.Rotation.IsZero == false)
                {
                  //  log.InfoFormat("applying rot" + operation.Rotation.ToString());
                    item.Rotation = operation.Rotation;
                }
                if (!operation.Velocity.IsZero)
                {
                    item.Velocity += operation.Velocity;
                  //  item.IsThrusting = true;
                }
                if (operation.MouseFwd.IsZero == false)
                {
                    mouseFwd = operation.MouseFwd;
                }
              //  else
                 //   item.IsThrusting = false;

                if (operation.IsMegaThrust != null && operation.IsMegaThrust)
                {
                  //  log.InfoFormat("mEGA thursting");
                    isMegaThrusting = true;
                    isSuperFast = false;
                }
                else
                    isMegaThrusting = false;

                if (!isMegaThrusting && !isSuperFast)
                {
                    if (item.Velocity.Len2 > GlobalVars.maxShipVelSq)
                        item.Velocity = Vector.Normalized(item.Velocity) * GlobalVars.maxShipVel;
                }
                else if (isSuperFast)
                {
                    currMaxVel = GlobalVars.SuperFastVel * 3;
                    if (!wasdMode)
                        item.Velocity = item.Rotation * GlobalVars.megaMaxVel;
                    else
                        item.Velocity = mouseFwd * GlobalVars.megaMaxVel;
                }
                else
                {
                    currMaxVel = GlobalVars.megaMaxVel;
                    if (item.Velocity.Len2 > GlobalVars.megaMaxVelSq)
                        item.Velocity = Vector.Normalized(item.Velocity) * GlobalVars.megaMaxVel;
                }

               // log.InfoFormat("vel: " + item.Velocity.ToString() + " rot:" + item.Rotation.ToString());
              
                // no response sent
                operation.OnComplete();
                return null;
            }

            return operation.GetOperationResponse(result);
        }

        private OperationResponse ItemOperationSetProperties(Item item, SetProperties operation, SendParameters sendParameters)
        {
            MethodReturnValue result = this.CheckAccess(item);
            if (result.IsOk)
            {
                item.SetProperties(operation.PropertiesSet, operation.PropertiesUnset);
                var eventInstance = new ItemPropertiesSet
                    {
                        ItemId = item.Id,
                        PropertiesRevision = item.PropertiesRevision,
                        PropertiesSet = operation.PropertiesSet,
                        PropertiesUnset = operation.PropertiesUnset
                    };

                var eventData = new EventData((byte)EventCode.ItemPropertiesSet, eventInstance);
                sendParameters.ChannelId = Settings.ItemEventChannel;
                var message = new ItemEventMessage(item, eventData, sendParameters);
                item.EventChannel.Publish(message);

                // no response sent
                operation.OnComplete();
                return null;
            }

            return operation.GetOperationResponse(result);
        }

        private OperationResponse ItemOperationSpawn(Item item, SpawnItem operation)
        {
            // this should always return Ok
            MethodReturnValue result = this.CheckAccess(item);

            if (result.IsOk)
            {
                item.Rotation = operation.Rotation;
                item.Spawn(operation.Position);
                ((World)this.World).Radar.AddItem(item, operation.Position);
            }

            operation.OnComplete();
            return operation.GetOperationResponse(result);
        }

        private OperationResponse ItemOperationSubscribeItem(Item item, SubscribeItem operation)
        {
            log.InfoFormat("io sub");
            if (item.Disposed)
            {
                return operation.GetOperationResponse((int)ReturnCode.ItemNotFound, "ItemNotFound");
            }

            this.interestItems.SubscribeItem(item);

            var subscribeEvent = new ItemSubscribed
            {
                ItemId = item.Id,
                ItemType = item.Type,
                Position = item.Position,
                PropertiesRevision = item.PropertiesRevision,
                Rotation = item.Rotation
            };

            var eventData = new EventData((byte)EventCode.ItemSubscribed, subscribeEvent);
            this.Peer.SendEvent(eventData, new SendParameters { ChannelId = Settings.ItemEventChannel });

            if (operation.PropertiesRevision.HasValue == false || operation.PropertiesRevision.Value != item.PropertiesRevision)
            {
                var properties = new ItemPropertiesSet
                    {
                        ItemId = item.Id,
                        PropertiesRevision = item.PropertiesRevision,
                        PropertiesSet = new Hashtable(item.Properties)
                    };
                var propEventData = new EventData((byte)EventCode.ItemPropertiesSet, properties);
                this.Peer.SendEvent(propEventData, new SendParameters { ChannelId = Settings.ItemEventChannel });
            }

            // don't send response
            operation.OnComplete();
            return null;
        }

        private OperationResponse ItemOperationUnsubscribeItem(Item item, UnsubscribeItem operation)
        {
            log.DebugFormat("iounsub");
            if (item.Disposed)
            {
                return operation.GetOperationResponse((int)ReturnCode.ItemNotFound, "ItemNotFound15");
            }

            this.interestItems.UnsubscribeItem(item);

            var unsubscribeEvent = new ItemUnsubscribed { ItemId = item.Id };

            var eventData = new EventData((byte)EventCode.ItemUnsubscribed, unsubscribeEvent);
            this.Peer.SendEvent(eventData, new SendParameters { ChannelId = Settings.ItemEventChannel });

            // don't send response
            operation.OnComplete();
            return null;
        }

        private static OperationResponse ItemOperationRaiseGenericEvent(Item item, RaiseGenericEvent operation, SendParameters sendParameters)
        {
            if (item.Disposed)
            {
                return operation.GetOperationResponse((int)ReturnCode.ItemNotFound, "ItemNotFound16");
            }

            var eventInstance = new ItemGeneric
            {
                ItemId = item.Id,
                CustomEventCode = operation.CustomEventCode,
                EventData = operation.EventData
            };

            var eventData = new EventData((byte)EventCode.ItemGeneric, eventInstance);
            sendParameters.Unreliable = (Reliability)operation.EventReliability == Reliability.Unreliable;
            sendParameters.ChannelId = Settings.ItemEventChannel;
            switch (operation.EventReceiver)
            {
                case (byte)EventReceiver.ItemOwner:
                    {
                        item.Owner.Peer.SendEvent(eventData, sendParameters);
                        break;
                    }

                case (byte)EventReceiver.ItemSubscriber:
                    {
                        var message = new ItemEventMessage(item, eventData, sendParameters);
                        item.EventChannel.Publish(message);
                        break;
                    }

                default:
                    {
                        return operation.GetOperationResponse((int)ReturnCode.ParameterOutOfRange, "Invalid EventReceiver " + operation.EventReceiver);
                    }
            }

            // no response
            operation.OnComplete();
            return null;
        }


    }
}
using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using Photon.MmoDemo.Common;
using Photon.SocketServer;
using Photon.MmoDemo.Server.Events;
using System.Diagnostics;

namespace Photon.MmoDemo.Server.GameSpecific
{
    public enum BotState
    {
        Patroling,
        Chasing,
        Attacking
    };

    public enum BotType
    {
        Swarm,  // majority type, slow but shoot from distance
        Mother, // mother ship
        FastSwarm,  // majority type, fast but low health and melee only
        Strong  // strong and higher damage but much fewer than swarm
    }


    public class Mob
    {
        public Item mobItem;
        public MmoActor target;
        public Vector currTargetPos;
        public BotState state;
        public BotType type;
        private int hp;
        public bool isDead;
        private float timeTillReload;   // sec until mob can shoot another bullet
        private float timeSinceVelUpdate;   // sec since velocity was updated

        private MobMother mother;

        public Mob(Item item, MobMother mother, BotType type)
        {
            this.mobItem = item;
            this.mother = mother;
            this.type = type;
            this.state = BotState.Patroling;
            if (type == BotType.Swarm)
                hp = GlobalVars.SwarmMobHP;
            else if (type == BotType.Strong)
                hp = GlobalVars.StrongMobHP;
            else if (type == BotType.Mother)
                hp = GlobalVars.MotherHP;
            else if (type == BotType.FastSwarm)
                hp = GlobalVars.FastSwarmMobHP;
            isDead = false;
            timeTillReload = 0;
            timeSinceVelUpdate = 0;
        }

        // patroling state function for setting random target near the mother mob
        public void ChooseNewTargetPosAndSetVel()
        {
            if (mobItem.Disposed)
                return;

            if (type == BotType.Swarm)
                currTargetPos = mother.mobItem.Position + MathHelper.GetRandomVector(GlobalVars.MaxDistFromMother*.9f);
            else if (type == BotType.FastSwarm)
                currTargetPos = mother.mobItem.Position + MathHelper.GetRandomVector(GlobalVars.MaxDistFromMother);
            SetVelToTarget();
        }

        private void SetVelToTarget()
        {
            if (timeSinceVelUpdate > 0)
                return;
            else
                timeSinceVelUpdate = GlobalVars.SecTillVelocityUpdate;
            Vector moveDir = Vector.Normalized(currTargetPos - mobItem.Position);
            if (type == BotType.Swarm)
                mobItem.Velocity = moveDir * GlobalVars.SwarmSpeed;
            else if (type == BotType.Strong)
                mobItem.Velocity = moveDir * GlobalVars.StrongMobSpeed;
            else if (type == BotType.Mother)
                mobItem.Velocity = moveDir * GlobalVars.MotherSpeed;
            else if (type == BotType.FastSwarm)
                mobItem.Velocity = moveDir * GlobalVars.FastSwarmSpeed;

            mobItem.Rotation = moveDir;
        }

        public void TakeDamage(int amount)
        {
            hp -= amount;
            //if (hp <= 0)
             //  DestroyMob();
        }

        public void UpdateState(float elapsedSeconds)
        {
            if (hp <= 0 && isDead == false)
              DestroyMob();
        }

        public void UpdatePosition(float elapsedSeconds, SendParameters sp)
        {
            if (mobItem.Disposed)
                return;
            Vector oldPosition = mobItem.Position;
            this.timeSinceVelUpdate -= elapsedSeconds;
            if (mobItem.wasBursted)
            {
                mobItem.secSinceBursted -= elapsedSeconds;
                if (mobItem.secSinceBursted <= 0)
                    mobItem.wasBursted = false;
            }

            if (state == BotState.Patroling)
            {
                float distToTargetSq = (currTargetPos - oldPosition).Len2;
                if (distToTargetSq < 800 || distToTargetSq > GlobalVars.MaxDistFromMotherSq)
                {
                    ChooseNewTargetPosAndSetVel();
                    //  GlobalVars.log.InfoFormat(mobItem.Id + "chose new velo");
                }
                if (mobItem.Velocity.Len2 > GlobalVars.maxShipVelSq + 1)
                {
                    mobItem.Velocity = mobItem.Velocity * GlobalVars.burstDampening;
                    SetVelToTarget();
                }
            }
            if (state == BotState.Chasing)
            {
                Vector diff = target.Avatar.Position - mobItem.Position;
                float distToTarg2 = diff.Len2;
                if (type == BotType.FastSwarm)
                {
                    currTargetPos = target.Avatar.Position;
                    SetVelToTarget();
                    // if bot is colliding with it's targetted player do damage
                    if (distToTarg2 < GlobalVars.swarmShipRadius2)
                    {
                        target.Hitpoints = target.Hitpoints - 1;

                        var eventInstance1 = new HpEvent
                        {
                            ItemId = target.Avatar.Id,
                            HpChange = 1,
                        };
                        var eventData1 = new EventData((byte)EventCode.HpEvent, eventInstance1);
                        var message1 = new ItemEventMessage(target.Avatar, eventData1, sp);
                        target.Avatar.EventChannel.Publish(message1);
                    }
                }

                if (type == BotType.Swarm)
                {
                    if (distToTarg2 > GlobalVars.BotChasingStopDist2)
                    {
                        currTargetPos = target.Avatar.Position;
                        SetVelToTarget();
                    }
                    else
                    {
                        mobItem.Velocity = Vector.Zero;

                    }
                    if (timeTillReload > 0)
                        timeTillReload -= elapsedSeconds;

                    if (timeTillReload <= 0)    // bullet is ready
                    {
                        // check if target is within shoot range
                        if (distToTarg2 < GlobalVars.BotShotSight2)
                        {
                            timeTillReload = GlobalVars.BotReloadTime;
                            mobItem.Rotation = diff / (float)Math.Sqrt(distToTarg2);
                            BotManager.MobFireBullet(mobItem);

                        }
                    }
                }
            }





            //mobItem.Move(oldPosition + mobItem.Velocity * elapsedSeconds);
            mobItem.SetPos(oldPosition + mobItem.Velocity * elapsedSeconds);
            mobItem.UpdateRegionSimple();


            if (GlobalVars.TrueTenPercent)
            {
            //    GlobalVars.log.InfoFormat("mob Pos " + oldPosition.ToString());
            }

            // send event
            var eventInstance = new ItemMoved
            {
                ItemId = mobItem.Id,
                OldPosition = oldPosition,
                Position = mobItem.Position,
                Rotation = mobItem.Rotation, // not changing rotation
                OldRotation = mobItem.Rotation
            };
            var eventData = new EventData((byte)EventCode.ItemMoved, eventInstance);
            var message = new ItemEventMessage(mobItem, eventData, sp);
            mobItem.EventChannel.Publish(message);
        }

        public void CheckCollisions(float elapsedSeconds, SendParameters sp, MmoPeer serverPeer)
        {
            if (mobItem.Disposed)
                return;

            float radius = GlobalVars.swarmShipRadius;
            if ((type == BotType.Swarm) || type == BotType.FastSwarm)
                radius = GlobalVars.swarmShipRadius;
            else if (type == BotType.Mother)
                radius = GlobalVars.motherShipRadius;
            float radiusSq = radius * radius;
            foreach (Item regionItem in mobItem.CurrentWorldRegion.myitems)
            {
                //  if (regionItem.Type == (byte)ItemType.Bullet)
                //       GlobalVars.log.InfoFormat("mob col check w " + regionItem.Id.ToString());
                if (CollisionHelper.CheckItemCollisionAgainstProjectile(mobItem, regionItem,
                    ref this.hp, sp, (MmoActorOperationHandler)BotManager.serverPeer.CurrentOperationHandler, radiusSq, radius))
                {
                    // check if mob is dead
                    if (this.hp <= 0)
                    {
                        GlobalVars.log.InfoFormat("mob killed " + mobItem.Id);
                        DestroyMob();
                    }
                }
            }
        }

        // destroys mob, note it doesnt remove itself from the mother that must be done later
        public void DestroyMob()
        {
            isDead = true;
            mobItem.Destroy();
            mobItem.Dispose();
            mobItem.CurrentWorldRegion.myitems.Remove(mobItem);
            mobItem.World.ItemCache.RemoveItem(mobItem.Id);
            BotManager.mobTable.Remove(mobItem.Id);
        }

        public void ChangeToChaseState(MmoActor target)
        {
            this.target = target;
            state = BotState.Chasing;
        }
    }


    public class MobMother : Mob
    {
        public List<Mob> childMobs;
        public float timeTillNextSpawn;

        public MobMother(Item item, BotType type)
            :base(item, null, type)
        {
            childMobs = new List<Mob>();
            this.timeTillNextSpawn = 0;
        }
    }


    /// <summary>
    /// bot manager is a global static class that adds mother ship mobs which then spawn
    /// smaller mobs. mobs are put into regions and update is called to update mob positions
    /// bot updates may be made at different times from other updates in actoroperationhandler
    /// </summary>

    public static class BotManager
    {
        private static List<MobMother> motherMobs;
        private static Random r;   // todo deterministic rand
        public static bool IsInitialized;
        public static MmoPeer serverPeer;
        private static System.Timers.Timer timer;
        private static Stopwatch watch;
        private static World world;
        private static float timeTillMotherSpawn;
        private static SendParameters Sp;
        public static Dictionary<string, Mob> mobTable;
        

        static BotManager()
        {
            motherMobs = new List<MobMother>();
            mobTable = new Dictionary<string, Mob>();
            r = new Random(52);
            IsInitialized = false;
        }

        public static void InitializeManager(World world, SendParameters sp)
        {
            Sp = sp;
            IsInitialized = true;
            BotManager.world = world;
            DummyPeer dp = new DummyPeer(SocketServer.Protocol.GpBinaryV162, "botmn");
            var initRequest = new InitRequest(SocketServer.Protocol.GpBinaryV162, dp);
            serverPeer = new MmoPeer(initRequest);
            serverPeer.Initialize(initRequest);
            GlobalVars.log.InfoFormat("initializing bot man");
            timeTillMotherSpawn = 0;
            // enter world with delay, just seems safer
            timer = new System.Timers.Timer();
            timer.Elapsed += new System.Timers.ElapsedEventHandler(EnterWorld);
            timer.Interval = 500; // 500ms
            timer.Enabled = true;
        }

        private static void EnterWorld(object source, System.Timers.ElapsedEventArgs e)
        {
            timer.Enabled = false;
            GlobalVars.log.InfoFormat("enterworld function in bot man");
            var request = new OperationRequest { OperationCode = (byte)OperationCode.EnterWorld, Parameters = new Dictionary<byte, object>() };
            request.Parameters.Add((byte)ParameterCode.WorldName, world.Name);
            request.Parameters.Add((byte)ParameterCode.Username, "botmn");
            request.Parameters.Add((byte)ParameterCode.Position, new Vector(500,500));
            request.Parameters.Add((byte)ParameterCode.ViewDistanceEnter, new Vector(400f,400f));
            request.Parameters.Add((byte)ParameterCode.ViewDistanceExit, new Vector(800f, 800f));

            SendParameters sp = MmoActorOperationHandler.GetDefaultSP();
       //     sp.Unreliable = false;
            ((MmoInitialOperationHandler)serverPeer.CurrentOperationHandler).OperationEnterWorld(
                serverPeer, request, sp, true);

            timer = new System.Timers.Timer();
            timer.Elapsed += new System.Timers.ElapsedEventHandler(Update);
            timer.Interval = 50; // 50ms
            timer.Enabled = true;
            watch = new Stopwatch();
            watch.Start();
        }

        public static void MobFireBullet(Item mob)
        {
            GlobalVars.log.InfoFormat("mob" + mob.Id + "firing");
            var request = new OperationRequest {  OperationCode = (byte)OperationCode.FireBullet, Parameters = new Dictionary<byte, object>() };
            request.Parameters.Add((byte)ParameterCode.ItemId, mob.Id);
            request.Parameters.Add((byte)ParameterCode.Rotation, mob.Rotation);
            request.Parameters.Add((byte)ParameterCode.Position, mob.Position);
            SendParameters sp = MmoActorOperationHandler.GetDefaultSP();
            ((MmoActorOperationHandler)serverPeer.CurrentOperationHandler).OperationFireBullet(serverPeer, request, sp);


        }

        private static void Update(object source, System.Timers.ElapsedEventArgs e)
        {
            watch.Stop();   // sets elapsed time to time since watch.Start() was called
            TimeSpan ts = watch.Elapsed;
            float msElapsed = (float)ts.Milliseconds;
            float elapsedSeconds = msElapsed / 1000f;

            if (timeTillMotherSpawn > 0)
                timeTillMotherSpawn -= elapsedSeconds;

            if (motherMobs.Count < GlobalVars.MaxMotherBots && timeTillMotherSpawn <= 0)
            {
                Vector pos = GetRandomCentralPosition();
                AddMotherBotHelper(pos, world);
                timeTillMotherSpawn = GlobalVars.SecForMotherToRespawn;
            }


            foreach (MobMother mm in motherMobs)
            {
                if (mm.isDead && mm.childMobs.Count == 0)
                {
                    motherMobs.Remove(mm);
                    break;
                }
                mm.timeTillNextSpawn -= elapsedSeconds;
                // if its time to spawn next mob
                if (!mm.isDead && mm.timeTillNextSpawn <= 0 && mm.childMobs.Count < GlobalVars.MaxSwarmPerMother)
                {
                    // spawn next mob
                    Vector displacement = MathHelper.GetRandomVector(GlobalVars.SpawnDistFromMother);
                    Vector mobPos = mm.mobItem.Position + displacement;
                    AddMobToMotherrHelper(mobPos, mm, world);

                    float t = (float)mm.childMobs.Count / (float)GlobalVars.MaxSwarmPerMother;
                    mm.timeTillNextSpawn = MathHelper.Lerp(GlobalVars.SecMinForSwarmSpawn,
                        GlobalVars.SecMaxForSwarmSpawn, t);
                }

                mm.CheckCollisions(elapsedSeconds, Sp, serverPeer);

                foreach (Mob mob in mm.childMobs)
                {
                    // update positions of all children (also sends itemmoved event)
                    mob.UpdatePosition(elapsedSeconds, MmoActorOperationHandler.GetDefaultSP());
                    mob.CheckCollisions(elapsedSeconds, Sp, serverPeer);
                    mob.UpdateState(elapsedSeconds);
                }

                RemoveDeadMobsFromMother(mm);
            }
            watch = Stopwatch.StartNew();
        }

        // remove recently deceased mobs from list
        private static void RemoveDeadMobsFromMother(MobMother mm)
        {
            mm.childMobs.RemoveAll(item => item.isDead == true);
            
        }

        private static void AddMotherBotHelper(Vector pos, World world)
        {
            string botId = "mb" + GlobalVars.motherBotCount.ToString();
            GlobalVars.log.InfoFormat("adding bot " + botId);
            GlobalVars.motherBotCount = (GlobalVars.motherBotCount + 1) % 99;
            Item mobitem = new Item(pos, Vector.Zero, new Hashtable(),
                (MmoActorOperationHandler)serverPeer.CurrentOperationHandler, botId, (byte)ItemType.Bot, world);
            MobMother newMother = new MobMother(mobitem, BotType.Mother);
            motherMobs.Add(newMother);
            SpawnMobHelper(newMother);


        }
        /*
         *             Item botitem;
            string botId;
            Mob newMob;
            if (r.Next(10) < 3)
            {
                botId = "bo" + GlobalVars.runningBotCount.ToString();
                botitem =  new Item(pos, Vector.Zero, new Hashtable(),
            (MmoActorOperationHandler)serverPeer.CurrentOperationHandler, botId, (byte)ItemType.Bot, world);
                newMob = new Mob(botitem, mother, BotType.Swarm);
            }
            else
            {
                botId = "bf" + GlobalVars.runningBotCount.ToString();
                botitem = new Item(pos, Vector.Zero, new Hashtable(),
        (MmoActorOperationHandler)serverPeer.CurrentOperationHandler, botId, (byte)ItemType.Bot, world);
                newMob = new Mob(botitem, mother, BotType.FastSwarm);
            }
            GlobalVars.log.InfoFormat("adding bot " + botId);
            GlobalVars.runningBotCount = (GlobalVars.runningBotCount + 1)%9999;
  */
        public static void AddMobToMotherrHelper(Vector pos, MobMother mother, World world)
        {
            string botId = "bo" + GlobalVars.runningBotCount.ToString();
            GlobalVars.log.InfoFormat("adding bot " + botId);
            GlobalVars.runningBotCount = (GlobalVars.runningBotCount + 1)%9999;
            Item botitem = new Item(pos, Vector.Zero, new Hashtable(), 
                (MmoActorOperationHandler)serverPeer.CurrentOperationHandler, botId, (byte)ItemType.Bot, world);
            Mob newMob;
            if (r.Next(10) < 3)
            {
                newMob = new Mob(botitem, mother, BotType.Swarm);
            }
            else
            {
                newMob = new Mob(botitem, mother, BotType.FastSwarm);
            }
            newMob.ChooseNewTargetPosAndSetVel();
            mother.childMobs.Add(newMob);
            SpawnMobHelper(newMob);
        }

        public static void ChangeMobToChaseState(string mobId, MmoActor targetPlayerItem)
        {
            bool hasKey = mobTable.ContainsKey(mobId);
            if (hasKey)
            {
                mobTable[mobId].ChangeToChaseState(targetPlayerItem);
            }
        }

        private static void SpawnMobHelper(Mob newMob)
        {
            mobTable.Add(newMob.mobItem.Id,newMob);
            newMob.mobItem.World.ItemCache.AddItem(newMob.mobItem);
            newMob.mobItem.UpdateRegionSimple();

            GlobalVars.log.InfoFormat("adding bot to botman");
            // send event
            var eventInstance = new BotSpawn
            {
                ItemId = newMob.mobItem.Id,
                Position = newMob.mobItem.Position,
                Rotation = newMob.mobItem.Rotation,
            };

            SendParameters sp = MmoActorOperationHandler.GetDefaultSP();
          //  sp.Unreliable = false;
            var eventData = new EventData((byte)EventCode.BotSpawn, eventInstance);
            var message = new ItemEventMessage(newMob.mobItem, eventData, sp);
            newMob.mobItem.EventChannel.Publish(message);
            ((MmoActorOperationHandler)serverPeer.CurrentOperationHandler).AddItem(newMob.mobItem);
            //todo add bot to radar, just check that the world is the right one
            // ((World)this.World).Radar.AddItem(item, operation.Position);
        }

        private static Vector GetRandomPosition()
        {
            var d = world.Area.Max - world.Area.Min;
            return world.Area.Min + new Vector { X = d.X * (float)r.NextDouble(), Y = d.Y * (float)r.NextDouble() };
        }

        private static Vector GetRandomCentralPosition()
        {
            var d = (world.Area.Max - world.Area.Min)/2;
            return (world.Area.Min+d/2) + new Vector { X = d.X * (float)r.NextDouble(), Y = d.Y * (float)r.NextDouble() };
        }
    }
}

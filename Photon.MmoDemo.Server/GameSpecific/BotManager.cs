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
        Attacking
    };

    public enum BotType
    {
        Swarm,  // majority type, fast but weak
        Mother, // mother ship
        Strong  // strong and higher damage but much fewer than swarm
    }


    public class Mob
    {
        public Item mobItem;
        public Item target;
        public Vector currTargetPos;
        public BotState state;
        public BotType type;
        private int hp;
        public bool isDead;

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
            isDead = false;
        }

        public void ChooseNewTargetPosAndSetVel()
        {
            if (mobItem.Disposed)
                return;
            Vector randNearbyPos;
            do
            {
               // GlobalVars.log.InfoFormat("trying pos");
                float randLength = (float)(new Random()).NextDouble() * GlobalVars.patrolRoamDistance;
                randNearbyPos = mobItem.Position + MathHelper.GetRandomVector(randLength);
            } while ((randNearbyPos - mother.mobItem.Position).Len2 < GlobalVars.MaxDistFromMotherSq);
            
            currTargetPos = randNearbyPos;
            //  GlobalVars.log.InfoFormat("set new target pos" + currTargetPos.ToString());
            SetVelToTarget();
        }

        private void SetVelToTarget()
        {
            Vector moveDir = Vector.Normalized(currTargetPos - mobItem.Position);
            if (type == BotType.Swarm)
                mobItem.Velocity = moveDir * GlobalVars.SwarmSpeed;
            else if (type == BotType.Strong)
                mobItem.Velocity = moveDir * GlobalVars.StrongMobSpeed;
            else if (type == BotType.Mother)
                mobItem.Velocity = moveDir * GlobalVars.MotherSpeed;
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

            float distToTargetSq = (currTargetPos - oldPosition).Len2;
            if (distToTargetSq < 1000 || distToTargetSq > GlobalVars.MaxDistFromMotherSq)
            {
                ChooseNewTargetPosAndSetVel();
              //  GlobalVars.log.InfoFormat(mobItem.Id + "chose new velo");
            }

            if (mobItem.wasBursted)
            {
                mobItem.secSinceBursted -= elapsedSeconds;
                if (mobItem.secSinceBursted <= 0)
                    mobItem.wasBursted = false;
            }

            if (mobItem.Velocity.Len2 > GlobalVars.maxShipVelSq+1)
            {
               // if (mobItem.wasBursted)
                {
                    mobItem.Velocity = mobItem.Velocity * GlobalVars.burstDampening;
                    SetVelToTarget();
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
            if ((type == BotType.Swarm))
                radius = GlobalVars.swarmShipRadius;
            else if (type == BotType.Mother)
                radius = GlobalVars.motherShipRadius;
            float radiusSq = radius * radius;
            foreach (Item regionItem in mobItem.CurrentWorldRegion.myitems)
            {
              //  if (regionItem.Type == (byte)ItemType.Bullet)
             //       GlobalVars.log.InfoFormat("mob col check w " + regionItem.Id.ToString());
                if (CollisionHelper.CheckItemCollisionAgainstProjectile(mobItem, regionItem,
                    ref this.hp, sp, null, radiusSq, radius))
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
        private static MmoPeer serverPeer;
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
            timer.Interval = 50; // 500ms
            timer.Enabled = true;
            watch = new Stopwatch();
            watch.Start();
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

        public static void AddMobToMotherrHelper(Vector pos, MobMother mother, World world)
        {
            string botId = "bo" + GlobalVars.runningBotCount.ToString();
            GlobalVars.log.InfoFormat("adding bot " + botId);
            GlobalVars.runningBotCount = (GlobalVars.runningBotCount + 1)%9999;
            Item botitem = new Item(pos, Vector.Zero, new Hashtable(), 
                (MmoActorOperationHandler)serverPeer.CurrentOperationHandler, botId, (byte)ItemType.Bot, world);
            Mob newMob = new Mob(botitem, mother, BotType.Swarm);
            newMob.ChooseNewTargetPosAndSetVel();
            mother.childMobs.Add(newMob);
            SpawnMobHelper(newMob);
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

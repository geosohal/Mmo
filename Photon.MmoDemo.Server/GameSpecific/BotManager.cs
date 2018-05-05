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
        public int hp;

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

          //  ChooseNewTargetPosAndSetVel();
        }

        public void ChooseNewTargetPosAndSetVel()
        {
            Vector randNearbyPos;
            do
            {
                GlobalVars.log.InfoFormat("trying pos");
                float randLength = (float)(new Random()).Next() * GlobalVars.patrolRoamDistance;
                randNearbyPos = mobItem.Position + MathHelper.GetRandomVector(randLength);
            } while ((randNearbyPos - mother.mobItem.Position).Len2 < GlobalVars.MaxDistFromMotherSq);
            
            currTargetPos = randNearbyPos;
            GlobalVars.log.InfoFormat("set new target pos" + currTargetPos.ToString());
            Vector moveDir = Vector.Normalized(currTargetPos - mobItem.Position);
            if (type == BotType.Swarm)
                mobItem.Velocity = moveDir * GlobalVars.SwarmSpeed;
            else if (type == BotType.Strong)
                mobItem.Velocity = moveDir * GlobalVars.StrongMobSpeed;
            else if (type == BotType.Mother)
                mobItem.Velocity = moveDir * GlobalVars.MotherSpeed;
            mobItem.Rotation = moveDir;
        }

        public void UpdatePosition(float elapsedSeconds, SendParameters sp)
        {
            Vector oldPosition = mobItem.Position;

            float distToTargetSq = (currTargetPos - oldPosition).Len2;
            if (distToTargetSq < 1000)
            {
                ChooseNewTargetPosAndSetVel();
                GlobalVars.log.InfoFormat(mobItem.Id + "chose new velo");
            }

            if ((new Random()).Next(16) < 2)
            {
                GlobalVars.log.InfoFormat("curr.Targ " + currTargetPos.ToString());
                GlobalVars.log.InfoFormat("curr Pos " + oldPosition.ToString());
                GlobalVars.log.InfoFormat("dist to target sq " + distToTargetSq.ToString());
            }

            mobItem.SetPos(oldPosition + mobItem.Velocity * elapsedSeconds);

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
        

        static BotManager()
        {
            motherMobs = new List<MobMother>();
            r = new Random(52);
            IsInitialized = false;
        }

        public static void InitializeManager(World world)
        {
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
            sp.Unreliable = false;
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
                Vector pos = GetRandomPosition();
                AddMotherBotHelper(pos, world);
                timeTillMotherSpawn = GlobalVars.SecForMotherToRespawn;
            }

            foreach (MobMother mm in motherMobs)
            {
                mm.timeTillNextSpawn -= elapsedSeconds;
                // if its time to spawn next mob
                if (mm.timeTillNextSpawn <= 0 && mm.childMobs.Count < GlobalVars.MaxSwarmPerMother)
                {
                    // spawn next mob
                    Vector displacement = MathHelper.GetRandomVector(GlobalVars.SpawnDistFromMother);
                    Vector mobPos = mm.mobItem.Position + displacement;
                    AddMobToMotherrHelper(mobPos, mm, world);

                    float t = mm.childMobs.Count / GlobalVars.MaxSwarmPerMother;
                    mm.timeTillNextSpawn = MathHelper.Lerp(GlobalVars.SecMinForSwarmSpawn,
                        GlobalVars.SecMaxForSwarmSpawn, t);
                }

                // update positions of all children (also sends itemmoved event)
                foreach (Mob mob in mm.childMobs)
                {
                    mob.UpdatePosition(elapsedSeconds, MmoActorOperationHandler.GetDefaultSP());
                }
            }




            watch = Stopwatch.StartNew();
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
            SpawnMobHelper(mobitem);


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
            SpawnMobHelper(botitem);
        }

        private static void SpawnMobHelper(Item mobitem)
        {
            mobitem.World.ItemCache.AddItem(mobitem);
            mobitem.UpdateRegionSimple();

            GlobalVars.log.InfoFormat("adding bot to botman");
            // send event
            var eventInstance = new BotSpawn
            {
                ItemId = mobitem.Id,
                Position = mobitem.Position,
                Rotation = mobitem.Rotation,
            };

            SendParameters sp = MmoActorOperationHandler.GetDefaultSP();
            sp.Unreliable = false;
            var eventData = new EventData((byte)EventCode.BotSpawn, eventInstance);
            var message = new ItemEventMessage(mobitem, eventData, sp);
            mobitem.EventChannel.Publish(message);
        }

        private static Vector GetRandomPosition()
        {
            var d = world.Area.Max - world.Area.Min;
            return world.Area.Min + new Vector { X = d.X * (float)r.NextDouble(), Y = d.Y * (float)r.NextDouble() };
        }

    }
}

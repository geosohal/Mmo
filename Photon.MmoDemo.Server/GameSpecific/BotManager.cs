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
   

    /// <summary>
    /// bot manager is a global static class that adds mother ship mobs which then spawn
    /// smaller mobs. mobs are put into regions and update is called to update mob positions
    /// bot updates may be made at different times from other updates in actoroperationhandler
    /// 
    /// update: this also handles spawning and storing asteroids
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


        // asteroid make members:
        public static float xradius = 10f; // new asteroids get a random position xradius from the x position of the maker
        public static float speedX = 0f;
        public static float speedVarianceX = .1f;
        public static float speedY = 1f;
        public static float speedVarianceY = .3f;
        public static float timeToMake = .6f;
        private static float timeSinceLastMake = float.MaxValue;


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
                mm.Regenerate(elapsedSeconds);
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

            // Asteroid spawner update:
            //if (timeSinceLastMake > timeToMake)
            //{
            //    int numAsteroids = asteroids.Length;
            //    int randIndex = Random.Range(0, numAsteroids - 1);

            //    Transform asteroidTransform = this.transform;
            //    Vector3 astPos =
            //        new Vector3(transform.position.x + Random.RandomRange(-xradius, xradius), transform.position.y, transform.position.z);
            //    GameObject newAsteroid = Instantiate(asteroids[randIndex], astPos, Quaternion.identity);
            //    float xVel = speedX + Random.RandomRange(-speedVarianceX, speedVarianceX);
            //    float yVel = speedY + Random.RandomRange(-speedVarianceY, speedVarianceY);
            //    Vector3 asteroidVel = new Vector3(xVel, 0, yVel);
            //    newAsteroid.GetComponent<Rigidbody>().velocity = asteroidVel;
            //    newAsteroid.GetComponent<Rigidbody>().AddTorque(
            //        Random.RandomRange(-torqueRange, torqueRange), Random.RandomRange(-torqueRange, torqueRange), Random.RandomRange(-torqueRange, torqueRange));
            //    timeSinceLastMake = 0;
            //    rb.asteroids.Add(newAsteroid.GetComponent<Asteroid>());
            //}

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
                GlobalVars.runningBotCount = (GlobalVars.runningBotCount + 1)%9999;
                botitem =  new Item(pos, Vector.Zero, new Hashtable(),
            (MmoActorOperationHandler)serverPeer.CurrentOperationHandler, botId, (byte)ItemType.Bot, world);
                newMob = new Mob(botitem, mother, BotType.Swarm);
            }
            else
            {
                botId = "bf" + GlobalVars.runningBotCount.ToString();
                GlobalVars.runningBotCount = (GlobalVars.runningBotCount + 1)%9999;
                botitem = new Item(pos, Vector.Zero, new Hashtable(),
        (MmoActorOperationHandler)serverPeer.CurrentOperationHandler, botId, (byte)ItemType.Bot, world);
                newMob = new Mob(botitem, mother, BotType.FastSwarm);
            }
            GlobalVars.log.InfoFormat("adding bot " + botId);
            
  */
        public static void AddMobToMotherrHelper(Vector pos, MobMother mother, World world)
        {
            bool isFastSwarm = r.Next(10) > 3 ? true : false;
            string botId;
            if (!isFastSwarm)
                botId = "bo" + GlobalVars.runningBotCount.ToString();
            else
                botId = "bf" + GlobalVars.runningBotCount.ToString();
            GlobalVars.log.InfoFormat("adding bot " + botId);
            GlobalVars.runningBotCount = (GlobalVars.runningBotCount + 1)%9999;
            Item botitem = new Item(pos, Vector.Zero, new Hashtable(), 
                (MmoActorOperationHandler)serverPeer.CurrentOperationHandler, botId, (byte)ItemType.Bot, world);
            Mob newMob;
            if (!isFastSwarm)
                newMob = new Mob(botitem, mother, BotType.Swarm);
            else
                newMob = new Mob(botitem, mother, BotType.FastSwarm);
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

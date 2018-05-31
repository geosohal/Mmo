using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExitGames.Logging;
using ExitGames.Logging.Log4Net;

using log4net.Config;

namespace Photon.MmoDemo.Server
{
    static class GlobalVars
    {
        public static bool IsDebugMode = false;
        public static readonly ILogger log = LogManager.GetCurrentClassLogger();

        // global counter for number of bullets used for naming bullet's item id
        // remember to use locking when naming
        public static int bulletCount = 0;
        public static int bombCount = 0;



        public static float bulletSpeed = 600f; // value moved to clients DemoSettings
        public static float bulletLiveTime = 2f;

        public static int msToUpdate = 50; // ms to wait to call update on server side
                                            // for collisions and velocity/position updates
        public static float secToUpdate = .05f;  // same as above but in seconds

        public static float playerShipRadius2 = 660f;    // radius squared
        public static float playerShipRadius = 25.6904652f;
        public static int bulletDamage = 8;
        public static int playerHitPoints = 500;    // also in clientside ItemBehavior

        public static int maxShipVel = 300;
        public static int maxShipVelSq = 90000;

        public static int megaMaxVel = 600;
        public static int megaMaxVelSq = 360000;
        public static int megaFadePerSec = 55;
        public static float burstDampening = .998f;
        public static float timeForBurstToLast = 2f;

        public static float breaksDampFactor = .85f;

        public static int SuperFastVel = 3600;

        public static float burstRadius = 900f;
        public static float burstRadiusSq = 810000f;
        public static float burstForce = 4000f;

        public static int saberDmgPerFrame = 9;
        public static float saberOnTime = 11f;   // in client: saberTimeLeft
        public static float saberLength = 200f;
        public static float saberStart = 50f;
        public static float laserStart = 100f;
        public static float laserLength = 1300f;


        public static int laserDmgPerFrame = 3;
        public static float laserOnTime = 3.5f;  // also in client for visuals: mlaserTimeLeft

        public static float bombTime = 4.5f;  // time to explodey
        public static float bombDmg = 125f;
        public static float bombSpeed = 130f;
        public static float bombRadius = 600f;
        public static float bombRadius2 = 360000f;
        public static float bombForce = 4000f;
        public static float bombBallRadius = 27;
        public static float bombBallRadius2 = 729;
        public static float maxBombVel = 600;
        public static float maxBombVel2 = 360000;
        public static float bombDamp = .92f;

        public static int HPboxvalue = 20;
        public static int HPboxCount = 0;   // counter that we increment to make hp item ids
        public static int HPradius = 30;
        public static int HPradius2 = 900;
        public static int RandHPsPerActor = 10; // spawn this many random hp boxes for each player in game

        /// <summary>
        /// constants and variables related to bots
        /// </summary>
        /// 
        public static int runningBotCount = 0;
        public static int motherBotCount;

        public static int MaxSwarmPerMother = 30;
        public static int MaxMotherBots = 1;
        public static int OddsStrongBot = 10;   // chance out of 100 that swarm bot is a strong one
        public static int SecForMotherToRespawn = 22;

        public static int SecMaxForSwarmSpawn = 8; // when swarm is almost full it takes this long for respawn
        public static int SecMinForSwarmSpawn = 2;  // when swarm is nera empty it takes this long
        public static float MaxDistFromMother = 800;
        public static float MaxDistFromMotherSq = 640000;
        public static float SpawnDistFromMother = 250f;

        public static int FastSwarmMobHP = 11;
        public static int SwarmMobHP = 30;  // also in BotBehaviour.cs
        public static int MotherHP = 600;
        public static int StrongMobHP = 12;

        public static int FastSwarmSpeed = 160;
        public static int SwarmSpeed = 85;
        public static int MotherSpeed = 20;
        public static int StrongMobSpeed = 40;

        public static float patrolRoamDistance = 400f;

        public static float swarmShipRadius = 30f;
        public static float swarmShipRadius2 = 900f;
        public static float motherShipRadius = 400f;
        public static float motherShipRadius2 = 160000f;

        public static float BotSight = 300f;
        public static float BotSight2 = 90000f;

        // distance from player that mob will keep shooting bullets
        public static float BotShotSight = 600f;
        public static float BotShotSight2 = 360000f;

        // chasing mob stops moving closer to you when he gets this close
        public static float BotChasingStopDist2 = 10000;

        public static float BotReloadTime = 5f;
        public static float fastSwarmDmgPerSec = 10f;

        // seconds until bot will update their velocity
        public static float SecTillVelocityUpdate = .5f;

        ////////////////////////////////////////


        public static bool TrueTenPercent = false;

    }
}

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

        public static int botCount = 0;

        public static float bulletSpeed = 600f; // value moved to clients DemoSettings
        public static float bulletLiveTime = 2f;
        public static int msToUpdate = 12; // ms to wait to call update on server side
                                            // for collisions and velocity/position updates
        public static float secToUpdate = .012f;  // same as above but in seconds
        public static float playerShipRadius2 = 660f;    // radius squared
        public static float playerShipRadius = 25.6904652f;
        public static int bulletDamage = 8;
        public static int playerHitPoints = 500;    // also in clientside ItemBehavior

        public static int maxShipVel = 180;
        public static int maxShipVelSq = 32400;

        public static int megaMaxVel = 600;
        public static int megaMaxVelSq = 360000;
        public static int megaFadePerSec = 55;
        public static float burstDampening = .998f;
        public static float timeForBurstToLast = 2f;

        public static float breaksDampFactor = .85f;

        public static float burstRadius = 900f;
        public static float burstRadiusSq = 810000f;
        public static float burstForce = 4000f;

        public static int saberDmgPerFrame = 7;
        public static float saberOnTime = 9f;   // in client: saberTimeLeft
        public static float saberLength = 200f;
        public static float saberStart = 50f;
        public static float laserStart = 100f;
        public static float laserLength = 1300f;


        public static int laserDmgPerFrame = 1;
        public static float laserOnTime = 1f;  // also in client for visuals: laserTimeLeft

        public static float bombTime = 4.5f;  // time to explodey
        public static float bombDmg = 25f;
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

    }
}

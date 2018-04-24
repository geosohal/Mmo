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

        public static int botCount = 0;

        public static float bulletSpeed = 600f; // value moved to clients DemoSettings
        public static float bulletLiveTime = 2f;
        public static int msToUpdate = 50; // ms to wait to call update on server side
                                            // for collisions and velocity/position updates
        public static float secToUpdate = .05f;  // same as above but in seconds
        public static float playerShipRadius2 = 660f;    // radius squared
        public static int bulletDamage = 8;
        public static int playerHitPoints = 100;    // also in clientside ItemBehavior

        public static int maxShipVel = 180;
        public static int maxShipVelSq = 32400;

        public static int megaMaxVel = 400;
        public static int megaMaxVelSq = 160000;
        public static int megaFadePerSec = 65;

        public static float breaksDampFactor = .6f;

        public static float burstRadius = 900f;
        public static float burstRadiusSq = 810000f;
        public static float burstForce = 4000f;

        public static int saberDmgPerFrame = 5;
        public static float saberOnTime = 4f;   // in client: saberTimeLeft

        public static int laserDmgPerFrame = 1;
        public static float laserOnTime = 1f;  // also in client for visuals: laserTimeLeft
    }
}

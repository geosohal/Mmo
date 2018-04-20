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
        public static float bulletSpeed = 300f; // value moved to clients DemoSettings
        public static float bulletLiveTime = 5f;
        public static int msToUpdate = 50; // ms to wait to call update on server side
                                            // for collisions and velocity/position updates
        public static float secToUpdate = .05f;  // same as above but in seconds
        public static float playerShipRadius2 = 460f;    // radius squared
        public static int bulletDamage = 8;
        public static int playerHitPoints = 100;    // also in clientside ItemBehavior
    }
}

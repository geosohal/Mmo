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
    public static class AsteroidMaker
    {
        public static float xradius = 10f; // new asteroids get a random position xradius from the x position of the maker
        public static float speedX = 0f;
        public static float speedVarianceX = .1f;
        public static float speedY = 1f;
        public static float speedVarianceY = .3f;
        public static float timeToMake = .6f;

        public static bool IsInitialized;
        private static Stopwatch watch;
        private static System.Timers.Timer timer;

        private static float timeSinceLastMake = float.MaxValue;

        static AsteroidMaker()
        {
            IsInitialized = false;
        }

        public static void Initialize(World world, SendParameters sp)
        {

            IsInitialized = true;
        }

    }
}
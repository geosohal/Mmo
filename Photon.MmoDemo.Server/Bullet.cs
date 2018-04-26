
namespace Photon.MmoDemo.Server
{
    using System;
    using System.Collections;

    using ExitGames.Concurrency.Fibers;

    using Photon.SocketServer.Concurrency;
    using Photon.MmoDemo.Common;
    using Photon.SocketServer;
    using Photon.MmoDemo.Server.Events;


    public class Bullet : Item
    {
        private float timeToLive;
        public float? secsFromLastUpdateDone; // if this is bullets first update this member stores
                                              // how much of the first update is already done due to being applied at spawn
        public Vector forward;

        public Bullet(Vector position, Vector velocity, Hashtable properties, MmoActorOperationHandler owner, string id, byte type, World world)
            : base(position, velocity, properties, owner, id, type, world)
        {
            timeToLive = GlobalVars.bulletLiveTime;
            secsFromLastUpdateDone = null;
        }

        public void DecreaseLife(float timeElapsed)
        {
            timeToLive -= timeElapsed;
        }

        public bool IsAlive()
        {
            return (timeToLive > 0);
        }
    }

    public class Bomb : Item
    {
        private float timeToLive;
        public float? secsFromLastUpdateDone; // if this is bullets first update this member stores
                                              // how much of the first update is already done due to being applied at spawn
        public Vector forward;

        // remember rotation is velocity lol
        public Bomb(Vector position, Vector velocity, Hashtable properties, MmoActorOperationHandler owner, string id, byte type, World world)
            : base(position, velocity, properties, owner, id, type, world)
        {
            timeToLive = GlobalVars.bombTime;
            secsFromLastUpdateDone = null;
        }

        public void DecreaseLife(float timeElapsed)
        {
            timeToLive -= timeElapsed;
        }

        public bool IsAlive()
        {
            return (timeToLive > 0);
        }
    }
}

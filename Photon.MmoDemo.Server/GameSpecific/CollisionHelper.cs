using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Photon.MmoDemo.Common;
using Photon.MmoDemo.Server.Events;
using Photon.SocketServer;

namespace Photon.MmoDemo.Server.GameSpecific
{
    public class CollisionHelper
    {

        //; checks the ship item for collision with a potential projectile item, actor can be passed as
        // null if this ship is an npc. returns whether or not to delete the projectile
        public static bool CheckItemCollisionAgainstProjectile(Item ship, Item projectile, ref int hitPoints,
            SendParameters sp, MmoActorOperationHandler actor, float shipRadiusSq, float shipRadius)
        {
            // if its a bullet and it's not my own bullet
            if ((byte)projectile.Type == (byte)ItemType.Bullet && 
                (actor == null || !actor.ContainsItem(projectile.Id)) )
            {
                //Vector bulletPos = projectile.GetRewindedPos(Peer.RoundTripTime / 2);
                float distanceToBullet2 = (projectile.Position - ship.Position).Len2;
              //  GlobalVars.log.InfoFormat("checkin coll " + projectile.Id.ToString() + "dist: " + distanceToBullet2.ToString());
                if (distanceToBullet2 < shipRadiusSq)
                {
                    GlobalVars.log.InfoFormat(ship.Id + " COLLISION with: " +
                        (ItemType)projectile.Type);
                    //if (removeList == null)
                    //    removeList = new List<Tuple<Item, Region>>();
                    //removeList.Add(new Tuple<Item, Region>(projectile, reg));   // set bullet to be removed

                   // sp.Unreliable = false;

                    var eventInstance = new HpEvent
                    {
                        ItemId = ship.Id,
                        HpChange = GlobalVars.bulletDamage
                    };
                    var eventData = new EventData((byte)EventCode.HpEvent, eventInstance);
                    var message = new ItemEventMessage(ship, eventData, sp);
                    ship.EventChannel.Publish(message);

                    hitPoints -= GlobalVars.bulletDamage;
                    if (hitPoints < 0)
                    {
                        // send itemdestroyed message for player's ship item
                    }
                    ((Bullet)projectile).SetDead();
                  //  projectile.SetPos(new Vector(-60000, -60000));
                   // projectile.UpdateRegionSimple();
                    return true;
                }
            }
            else if ((byte)projectile.Type == (byte)ItemType.Bomb)
            {
                if (actor != null && actor.isSaberOut)
                {
                    Vector saberstartPt = new Vector();
                    Vector saberendPt = new Vector();
                    saberstartPt = ship.Position + ship.Rotation * GlobalVars.saberStart;
                    saberendPt = ship.Position + ship.Rotation * GlobalVars.saberLength;
                    Vector offset = new Vector();
                    if (MathHelper.Intersects(saberstartPt, saberendPt, projectile.Position, shipRadiusSq,
                        ref offset, shipRadius, null, true))
                    {
                        GlobalVars.log.InfoFormat("saber collide with a bomb");
          
                        projectile.SetPos(projectile.Position + offset * 2.1f); // move over instantly to uncollided position

                        // set bomb velocity using 'collision impact' with saber
                        projectile.Velocity = projectile.Velocity + (offset / offset.Len) * ship.GetCumulativeDotProducts(8) * 90f;
                    }
                }
                // if this is not my bomb, check if i collide with it
                if ((actor != null && !actor.ContainsItem(projectile.Id)) || actor == null)
                {
                    Vector difference = (projectile.Position - ship.Position);
                    float distanceToBomb2 = difference.Len2;
                    if (distanceToBomb2 < (shipRadiusSq + GlobalVars.bombBallRadius2))
                    {
                        GlobalVars.log.InfoFormat("player + " + ship.Id.Substring(0, 3) + " collision with: " +
                            projectile.Id.ToString());
                        //if (removeList == null)
                        //    removeList = new List<Tuple<Item, Region>>();
                        //removeList.Add(new Tuple<Item, Region>(projectile, reg));   // set bomb to be removed

                        float t = (GlobalVars.bombRadius2 - distanceToBomb2) / GlobalVars.bombRadius2;
                        int damageToPlayer = (int)Math.Floor(t * GlobalVars.bombDmg);
                        hitPoints -= damageToPlayer;
                        
                     //   sp.Unreliable = false;

                        var eventInstance = new HpEvent
                        {
                            ItemId = ship.Id,
                            HpChange = damageToPlayer,
                        };
                        var eventData = new EventData((byte)EventCode.HpEvent, eventInstance);
                        var message = new ItemEventMessage(ship, eventData, sp);
                        ship.EventChannel.Publish(message);

                        float force = GlobalVars.bombForce * t;
                        difference = difference / (float)Math.Sqrt(distanceToBomb2);
                        ship.wasBursted = true;
                        ship.secSinceBursted = GlobalVars.timeForBurstToLast;
                        ship.Velocity = ship.Velocity + difference * force;

                        return true;
                    }
                }
            }
            return false;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections;
using Photon.MmoDemo.Common;
using Photon.SocketServer;
using Photon.MmoDemo.Server.Events;
using System.Diagnostics;


namespace Photon.MmoDemo.Server.GameSpecific
{
    public enum BotState
    {
        Patroling,
        Chasing,
        Attacking
    };

    public enum BotType
    {
        Swarm,  // majority type, slow but shoot from distance
        Mother, // mother ship
        FastSwarm,  // majority type, fast but low health and melee only
        Strong  // strong and higher damage but much fewer than swarm
    }


    public class Mob
    {
        public Item mobItem;
        public MmoActor target;
        public Vector currTargetPos;
        public BotState state;
        public BotType type;
        protected int hp;
        public bool isDead;
        private float timeTillReload;   // sec until mob can shoot another bullet
        private float timeSinceVelUpdate;   // sec since velocity was updated

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
            else if (type == BotType.FastSwarm)
                hp = GlobalVars.FastSwarmMobHP;
            isDead = false;
            timeTillReload = 0;
            timeSinceVelUpdate = 0;
        }

        // patroling state function for setting random target near the mother mob
        public void ChooseNewTargetPosAndSetVel()
        {
            if (mobItem.Disposed)
                return;

            if (type == BotType.Swarm)
                currTargetPos = mother.mobItem.Position + MathHelper.GetRandomVector(GlobalVars.MaxDistFromMother * .9f);
            else if (type == BotType.FastSwarm)
                currTargetPos = mother.mobItem.Position + MathHelper.GetRandomVector(GlobalVars.MaxDistFromMother);
            SetVelToTarget();
        }

        private void SetVelToTarget()
        {
            if (timeSinceVelUpdate > 0)
                return;
            else
                timeSinceVelUpdate = GlobalVars.SecTillVelocityUpdate;
            Vector moveDir = Vector.Normalized(currTargetPos - mobItem.Position);
            if (type == BotType.Swarm)
                mobItem.Velocity = moveDir * GlobalVars.SwarmSpeed;
            else if (type == BotType.Strong)
                mobItem.Velocity = moveDir * GlobalVars.StrongMobSpeed;
            else if (type == BotType.Mother)
                mobItem.Velocity = moveDir * GlobalVars.MotherSpeed;
            else if (type == BotType.FastSwarm)
                mobItem.Velocity = moveDir * GlobalVars.FastSwarmSpeed;

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
            this.timeSinceVelUpdate -= elapsedSeconds;
            if (mobItem.wasBursted)
            {
                mobItem.secSinceBursted -= elapsedSeconds;
                if (mobItem.secSinceBursted <= 0)
                    mobItem.wasBursted = false;
            }

            if (state == BotState.Patroling)
            {
                float distToTargetSq = (currTargetPos - oldPosition).Len2;
                if (distToTargetSq < 800 || distToTargetSq > GlobalVars.MaxDistFromMotherSq)
                {
                    ChooseNewTargetPosAndSetVel();
                    //  GlobalVars.log.InfoFormat(mobItem.Id + "chose new velo");
                }
                if (mobItem.Velocity.Len2 > GlobalVars.maxShipVelSq + 1)
                {
                    mobItem.Velocity = mobItem.Velocity * GlobalVars.burstDampening;
                    SetVelToTarget();
                }
            }
            if (state == BotState.Chasing)
            {
                Vector diff = target.Avatar.Position - mobItem.Position;
                float distToTarg2 = diff.Len2;
                if (type == BotType.FastSwarm)
                {
                    currTargetPos = target.Avatar.Position;
                    SetVelToTarget();
                    // if bot is colliding with it's targetted player do damage
                    if (distToTarg2 < GlobalVars.swarmShipRadius2)
                    {
                        target.Hitpoints = target.Hitpoints - 1;

                        var eventInstance1 = new HpEvent
                        {
                            ItemId = target.Avatar.Id,
                            HpChange = 1,
                        };
                        var eventData1 = new EventData((byte)EventCode.HpEvent, eventInstance1);
                        var message1 = new ItemEventMessage(target.Avatar, eventData1, sp);
                        target.Avatar.EventChannel.Publish(message1);
                    }
                }

                if (type == BotType.Swarm)
                {
                    if (distToTarg2 > GlobalVars.BotChasingStopDist2)
                    {
                        currTargetPos = target.Avatar.Position;
                        SetVelToTarget();
                    }
                    else
                    {
                        mobItem.Velocity = Vector.Zero;

                    }
                    if (timeTillReload > 0)
                        timeTillReload -= elapsedSeconds;

                    if (timeTillReload <= 0)    // bullet is ready
                    {
                        // check if target is within shoot range
                        if (distToTarg2 < GlobalVars.BotShotSight2)
                        {
                            timeTillReload = GlobalVars.BotReloadTime;
                            mobItem.Rotation = diff / (float)Math.Sqrt(distToTarg2);
                            BotManager.MobFireBullet(mobItem);

                        }
                    }
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
            if ((type == BotType.Swarm) || type == BotType.FastSwarm)
                radius = GlobalVars.swarmShipRadius;
            else if (type == BotType.Mother)
                radius = GlobalVars.motherShipRadius;
            float radiusSq = radius * radius;
            foreach (Item regionItem in mobItem.CurrentWorldRegion.myitems)
            {
                //  if (regionItem.Type == (byte)ItemType.Bullet)
                //       GlobalVars.log.InfoFormat("mob col check w " + regionItem.Id.ToString());
                if (CollisionHelper.CheckItemCollisionAgainstProjectile(mobItem, regionItem,
                    ref this.hp, sp, (MmoActorOperationHandler)BotManager.serverPeer.CurrentOperationHandler, radiusSq, radius))
                {
                    // check if mob is dead
                    if (this.hp <= 0)
                    {
                        GlobalVars.log.InfoFormat("mob killed " + mobItem.Id);
                        DestroyMob();
                    }
                    else
                    {
                        if ((byte)regionItem.Type == (byte)ItemType.Bullet)
                        {
                            if (!regionItem.Owner.isBotMan)
                            {
                                ChangeToChaseState((MmoActor)regionItem.Owner);
                            }

                        }
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

        public void ChangeToChaseState(MmoActor target)
        {
            this.target = target;
            state = BotState.Chasing;
        }
    }


    public class MobMother : Mob
    {
        public List<Mob> childMobs;
        public float timeTillNextSpawn;
        private float HPregenPerSec = 4f;
        private float hpRegainAmount = 10f;
        private float hpAccumulated;

        public MobMother(Item item, BotType type)
            : base(item, null, type)
        {
            childMobs = new List<Mob>();
            this.timeTillNextSpawn = 0;
            hpAccumulated = 0;
        }

        public void Regenerate(float elapsedSec)
        {
            if (hp >= GlobalVars.MotherHP)
                return;
            hpAccumulated += elapsedSec * HPregenPerSec;
            if (hpAccumulated > hpRegainAmount)
            {
                int hpToAdd = (int)hpAccumulated;
                hp += hpToAdd;
                hpAccumulated = hpAccumulated - (float)Math.Floor(hpAccumulated);

                var eventInstance1 = new HpEvent
                {
                    ItemId = this.mobItem.Id,
                    HpChange = -hpToAdd,
                };
                var eventData1 = new EventData((byte)EventCode.HpEvent, eventInstance1);
                var message1 = new ItemEventMessage(target.Avatar, eventData1, MmoActorOperationHandler.GetDefaultSP());
                mobItem.EventChannel.Publish(message1);
            }
        }
    }
}

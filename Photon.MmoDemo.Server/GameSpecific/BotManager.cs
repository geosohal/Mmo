using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using Photon.MmoDemo.Common;
using Photon.SocketServer;
using Photon.MmoDemo.Server.Events;

namespace Photon.MmoDemo.Server.GameSpecific
{
    public enum BotState
    {
        Patroling,
        Attacking
    };

    public struct Bot
    {
        public Item botItem;
        public Item target;
        public Vector guardPos;
        public float guardDist;
        public BotState state;
    }



    public class BotManager
    {
        List<Bot> bots;
        Random r;   // todo deterministic rand

        public BotManager()
        {
            bots = new List<Bot>();
            r = new Random(52);
        }

        public Item AddBot(Vector pos, Vector guardPos, float guardDist, MmoActorOperationHandler handler, World world)
        {
            string botId = "bo" + GlobalVars.botCount.ToString();
            GlobalVars.botCount = (GlobalVars.botCount+1)%99;
            Item botitem = new Item(pos, Vector.Zero, new Hashtable(), handler, botId, (byte)ItemType.Bot, world);
            Bot newBot = new Bot();
            newBot.botItem = botitem;
            newBot.target = null;
            newBot.guardDist = guardDist;
            newBot.guardPos = guardPos;
            newBot.state = BotState.Patroling;

            bots.Add(newBot);
            return botitem;
        }

        public void UpdateBots(float elapsedSeconds, SendParameters sp)
        {
            foreach (Bot bot in bots)
            {
                if (bot.state == BotState.Patroling)
                {
                    bot.botItem.Velocity = bot.botItem.Velocity*.95f + 
                        new Vector((float)r.NextDouble()*.1f-.05f, (float)r.NextDouble() * .1f-.05f);
                }

                Vector oldPosition = bot.botItem.Position;
                bot.botItem.SetPos(bot.botItem.Position + bot.botItem.Velocity * elapsedSeconds);
                bot.botItem.Rotation = bot.botItem.Velocity;
             //   if (r.Next(100) < 3)
             //       GlobalVars.log.InfoFormat("bot pos " + bot.botItem.Position.ToString());

                // send event
                var eventInstance = new ItemMoved
                {
                    ItemId = bot.botItem.Id,
                    OldPosition = oldPosition,
                    Position = bot.botItem.Position,
                    Rotation = bot.botItem.Rotation, // not changing rotation
                    OldRotation = bot.botItem.Rotation
                };
                var eventData = new EventData((byte)EventCode.ItemMoved, eventInstance);
                var message = new ItemEventMessage(bot.botItem, eventData, sp);
                bot.botItem.EventChannel.Publish(message);

            }
        }
    }
}

// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GameEvents.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Game events methods.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Runtime.Remoting.Messaging;

namespace Photon.MmoDemo.Client
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    using ExitGames.Client.Photon;
#if Unity
    using Hashtable = ExitGames.Client.Photon.Hashtable;
#endif


    using Photon.MmoDemo.Common;
    using System.IO;

    //using ExitGames.Logging;

    //   using ExitGames.Logging.Log4Net;

    //   using log4net.Config;
    public partial class Game
    {
       // private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        public void OnEventReceive(EventData eventData)
        {
            string itemId;
            Item item;
         //   log.InfoFormat("client got event {0}", eventData.Code.ToString());
         string line = "client got event " + ((EventCode)eventData.Code).ToString() + "\n";
            
            switch ((EventCode)eventData.Code)
            {
                case EventCode.RadarUpdate:                    
                    this.Listener.OnRadarUpdate(
                        (string)eventData[(byte)ParameterCode.ItemId],
                        (ItemType)(byte)eventData[(byte)ParameterCode.ItemType],                        
                        (Vector)eventData[(byte)ParameterCode.Position],
                        (bool)eventData[(byte)ParameterCode.Remove]
                        );
                    return;

                case EventCode.ItemMoved:
                    itemId = (string)eventData[(byte)ParameterCode.ItemId];

                    // if item is bullet. item is bullet type if it starts with bt
                    if (itemId[0] == 'b' && itemId[1] == 't' && this.TryGetItem(itemId, out item))
                    {
                        // todo optimize we will change this to bulletmoved event so we dont need to pass rotations
                        Vector position = (Vector)eventData[(byte)ParameterCode.Position];
                        Vector oldPosition = (Vector)eventData[(byte)ParameterCode.OldPosition];
                        Vector rotation = (Vector)(eventData[(byte)ParameterCode.Rotation] ?? Vector.Zero);
                        Vector oldRotation = (Vector)(eventData[(byte)ParameterCode.OldRotation] ?? Vector.Zero);
                        item.SetPositions(position, oldPosition, rotation, oldRotation);
                    }
                    else if (this.TryGetItem(itemId, out item))
                    {
                       // if (!item.IsMine) only needed this when we did SetPositionson client with the Move opreation
                        {
                            Vector position = (Vector)eventData[(byte)ParameterCode.Position];
                            Vector oldPosition = (Vector)eventData[(byte)ParameterCode.OldPosition];
                            Vector rotation = (Vector)(eventData[(byte)ParameterCode.Rotation] ?? Vector.Zero);
                            Vector oldRotation = (Vector)(eventData[(byte)ParameterCode.OldRotation] ?? Vector.Zero);
                            item.SetPositions(position, oldPosition, rotation, oldRotation);
                        }
                    }
                    return;

                case EventCode.FireLaser:
                    itemId = (string)eventData[(byte)ParameterCode.ItemId];
                    if (this.TryGetItem(itemId, out item))
                    {
                       // if (!item.IsMine)
                        {
                            item.IsLaserFiring = true;
                        }
                    }
                    return;

                case EventCode.ItemDestroyed:
                    itemId = (string)eventData[(byte)ParameterCode.ItemId];
                    if (this.TryGetItem(itemId, out item))
                    {
                        item.IsDestroyed = this.RemoveItem(item);
                    }
                    return;

                case EventCode.ItemProperties:
                    HandleEventItemProperties(eventData.Parameters);
                    return;

                case EventCode.ItemPropertiesSet:
                    HandleEventItemPropertiesSet(eventData.Parameters);
                    return;

                case EventCode.ItemSubscribed: // item enters our interest area
                    HandleEventItemSubscribed(eventData.Parameters);
                    return;

                case EventCode.ItemUnsubscribed: // item exits our interest area
                    HandleEventItemUnsubscribed(eventData.Parameters);
                    return;

                case EventCode.WorldExited:
                    this.SetConnected();
                    return;

                case EventCode.HpEvent:
                    itemId = (string)eventData[(byte)ParameterCode.ItemId];
                    int hpchange = (int)eventData[(byte)ParameterCode.HpChange];
                    this.listener.OnHpChange(itemId, hpchange);
                    return;

                case EventCode.BulletSpawn:
                    itemId = (string)eventData[(byte)ParameterCode.ItemId];
                    line += " id: " + itemId + "\n";
                    Item newbullet = new Item(this, itemId, ItemType.Bullet);
                    Vector pos = (Vector)eventData[(byte)ParameterCode.Position];
                    Vector rot = (Vector)eventData[(byte)ParameterCode.Rotation];
                    newbullet.SetPositions(pos, pos, rot, rot);
                    AddItem(newbullet);
                    System.IO.File.AppendAllText(@"C:\client-" + Avatar.Id + ".log", line);
                    return;

                case EventCode.BotSpawn:
                    itemId = (string)eventData[(byte)ParameterCode.ItemId];
                    line += " id: " + itemId + "\n";
                    Item newbot = new Item(this, itemId, ItemType.Bot);
                    Vector Pos = (Vector)eventData[(byte)ParameterCode.Position];
                    Vector Rot = (Vector)eventData[(byte)ParameterCode.Rotation];
                    newbot.SetPositions(Pos, Pos, Rot, Rot);
                    AddItem(newbot);
                    System.IO.File.AppendAllText(@"C:\client-" + Avatar.Id + ".log", line);
                    return;
                    /*
                case EventCode.BulletExpire:
                    itemId = (string)eventData[(byte)ParameterCode.ItemId];
                    if (this.TryGetItem(itemId, out item))
                    {
                        item.IsDestroyed = this.RemoveItem(item);
                    }
                    return;*/
            }
            
            this.OnUnexpectedEventReceive(eventData);
        }


        private void HandleEventItemProperties(IDictionary eventData)
        {
            var itemId = (string)eventData[(byte)ParameterCode.ItemId];

            Item item;
            if (this.TryGetItem(itemId, out item))
            {
                item.PropertyRevisionLocal = (int)eventData[(byte)ParameterCode.PropertiesRevision];

                if (!item.IsMine)
                {
                    var propertiesSet = (Hashtable)eventData[(byte)ParameterCode.PropertiesSet];

                    item.SetColor((int)propertiesSet[Item.PropertyKeyColor]);
                    item.SetText((string)propertiesSet[Item.PropertyKeyText]);
                    item.SetInterestAreaAttached((bool)propertiesSet[Item.PropertyKeyInterestAreaAttached]);
                    item.SetInterestAreaViewDistance(
                        (Vector)propertiesSet[Item.PropertyKeyViewDistanceEnter], (Vector)propertiesSet[Item.PropertyKeyViewDistanceExit]);

                }
            }
        }

        private void HandleEventItemPropertiesSet(IDictionary eventData)
        {
            var itemId = (string)eventData[(byte)ParameterCode.ItemId];
            Item item;
            if (this.TryGetItem(itemId, out item))
            {
                item.PropertyRevisionLocal = (int)eventData[(byte)ParameterCode.PropertiesRevision];

                if (!item.IsMine)
                {
                    var propertiesSet = (Hashtable)eventData[(byte)ParameterCode.PropertiesSet];

                    if (propertiesSet.ContainsKey(Item.PropertyKeyColor))
                    {
                        item.SetColor((int)propertiesSet[Item.PropertyKeyColor]);
                    }

                    if (propertiesSet.ContainsKey(Item.PropertyKeyText))
                    {
                        item.SetText((string)propertiesSet[Item.PropertyKeyText]);
                    }

                    if (propertiesSet.ContainsKey(Item.PropertyKeyViewDistanceEnter))
                    {
                        var viewDistanceEnter = (Vector)propertiesSet[Item.PropertyKeyViewDistanceEnter];
                        item.SetInterestAreaViewDistance(viewDistanceEnter, (Vector)propertiesSet[Item.PropertyKeyViewDistanceExit]);
                    }

                    if (propertiesSet.ContainsKey(Item.PropertyKeyInterestAreaAttached))
                    {
                        item.SetInterestAreaAttached((bool)propertiesSet[Item.PropertyKeyInterestAreaAttached]);
                    }
                }
            }
        }

        private void HandleEventItemSubscribed(IDictionary eventData)
        {
            var itemType = (ItemType)(byte)eventData[(byte)ParameterCode.ItemType];
            var itemId = (string)eventData[(byte)ParameterCode.ItemId];
            Vector position = (Vector)eventData[(byte)ParameterCode.Position];
            var cameraId = (byte)eventData[(byte)ParameterCode.InterestAreaId];
            Vector rotation = eventData.Contains((byte)ParameterCode.Rotation) ? (Vector)eventData[(byte)ParameterCode.Rotation] : Vector.Zero;

            Item item;
            if (!this.TryGetItem(itemId, out item)) // register item first time seen 
            {
                item = new Item(this, itemId, itemType);
                string line = "subscribed adding item: " + item.Id + "\n";
                System.IO.File.AppendAllText(@"C:\client-" + Avatar.Id+ ".log", line);
                this.AddItem(item);
                item.GetProperties();
            } 
            if (!item.IsMine)
            {
                item.PropertyRevisionRemote = (int)eventData[(byte)ParameterCode.PropertiesRevision];

                if (item.PropertyRevisionRemote != item.PropertyRevisionLocal)
                {
                    item.GetProperties();
                }
                item.SetPositions(position, position, rotation, rotation);
            }

            item.AddSubscribedInterestArea(cameraId);
        }

        private void HandleEventItemUnsubscribed(IDictionary eventData)
        {
            var itemId = (string)eventData[(byte)ParameterCode.ItemId];
            var cameraId = (byte)eventData[(byte)ParameterCode.InterestAreaId];

            Item item;
            if (this.TryGetItem(itemId, out item))
            {
                item.RemoveSubscribedInterestArea(cameraId);
            }
        }

        
    }
}
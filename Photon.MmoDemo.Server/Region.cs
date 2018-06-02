// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Region.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Represents a region used for region-based interest management.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Photon.MmoDemo.Server
{
    using Photon.MmoDemo.Common;
    using Photon.SocketServer.Concurrency;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Item notifies interest areas via regions this item exits and enters.
    /// </summary>
    public class ItemRegionChangedMessage 
    {
        public ItemRegionChangedMessage(Region r0, Region r1, ItemSnapshot snaphot)
        {
            this.Region0 = r0;
            this.Region1 = r1;
            this.ItemSnapshot = snaphot;
        }
        public Region Region0 { get; private set; }
        public Region Region1 { get; private set; }
        public ItemSnapshot ItemSnapshot { get; private set; }
    };

    /// <summary>
    /// Interest area requests all items in entered region.
    /// </summary>
    public class RequestItemEnterMessage 
    {
        public RequestItemEnterMessage(InterestArea interestArea)
        {
            this.InterestArea = interestArea;
        }
        public InterestArea InterestArea { get; private set; }
    };

    /// <summary>
    /// Interest area requests all items in exited region.
    /// </summary>
    public class RequestItemExitMessage
    {
        public RequestItemExitMessage(InterestArea interestArea)
        {
            this.InterestArea = interestArea;
        }
        public InterestArea InterestArea { get; private set; }
    };

    /// <summary>
    /// Represents a region used for region-based interest management. 
    /// </summary>
    public class Region : IDisposable
    {
        // region needs to keep track of what items it has so we can do per-region collision checks
        public List<Item> myitems;

        public Region(int x , int y)
        {
            this.ItemRegionChangedChannel = new MessageChannel<ItemRegionChangedMessage>(MessageCounters.CounterSend);
            this.RequestItemEnterChannel = new MessageChannel<RequestItemEnterMessage>(MessageCounters.CounterSend);
            this.RequestItemExitChannel = new MessageChannel<RequestItemExitMessage>(MessageCounters.CounterSend);
            this.ItemEventChannel = new MessageChannel<ItemEventMessage>(MessageCounters.CounterSend);
            this.X = x;
            this.Y = y;
            myitems = new List<Item>();
        }

        public MessageChannel<ItemRegionChangedMessage> ItemRegionChangedChannel { get; private set; }
        public MessageChannel<RequestItemEnterMessage> RequestItemEnterChannel { get; private set; }
        public MessageChannel<RequestItemExitMessage> RequestItemExitChannel { get; private set; }
        public MessageChannel<ItemEventMessage> ItemEventChannel { get; private set; }

        // grid cell X (debug only)
        public int X { get; private set; }

        // grid cell Y (debug only)
        public int Y { get; private set; }

        public void EnlistItem(Item newitem)
        {
            if (GlobalVars.IsDebugMode && myitems.Contains(newitem))
            {
                GlobalVars.log.InfoFormat("item was already in region");
            }
            else
            {
                if (GlobalVars.IsDebugMode)
                    GlobalVars.log.InfoFormat("enlisting tiem to region: " + this.ToString());
                myitems.Add(newitem);
            }
        }

        public void DelistItem(Item itemToRemove)
        {
            if (GlobalVars.IsDebugMode)
                GlobalVars.log.InfoFormat("delisting tiem from region " + itemToRemove.Id + ToString());

            if (!myitems.Remove(itemToRemove))
                GlobalVars.log.InfoFormat("requested to remove unlisted item from region " + itemToRemove.Id + ToString());
        }

        public override string ToString()
        {
            return string.Format("Region({0},{1})", base.ToString(), X, Y);
        }

        public void Dispose()
        {
            this.ItemRegionChangedChannel.Dispose();
            this.RequestItemEnterChannel.Dispose();
            this.RequestItemExitChannel.Dispose();
            this.ItemEventChannel.Dispose();
        }

    }
}
// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Operations.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   The operations.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Photon.MmoDemo.Client
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    using Photon.MmoDemo.Common;
#if Unity
    using Hashtable = ExitGames.Client.Photon.Hashtable;
#endif

    public static class Operations
    {
        public static void AddInterestArea(Game game, byte cameraId, Vector position, Vector viewDistanceEnter, Vector viewDistanceExit)
        {
            var data = new Dictionary<byte, object>
                {
                    { (byte)ParameterCode.InterestAreaId, cameraId }, 
                    { (byte)ParameterCode.ViewDistanceEnter, viewDistanceEnter }, 
                    { (byte)ParameterCode.ViewDistanceExit, viewDistanceExit }, 
                    { (byte)ParameterCode.Position, position }
                };

            game.SendOperation(OperationCode.AddInterestArea, data, true, Settings.ItemChannel);
        }

        public static void AttachInterestArea(Game game, string itemId)
        {
            var data = new Dictionary<byte, object>();

            if (!string.IsNullOrEmpty(itemId))
            {
                data.Add((byte)ParameterCode.ItemId, itemId);
            }

            game.SendOperation(OperationCode.AttachInterestArea, data, true, Settings.ItemChannel);
        }

        public static void CounterSubscribe(ExitGames.Client.Photon.PhotonPeer peer, int receiveInterval)
        {
            var data = new Dictionary<byte, object> { { (byte)ParameterCode.CounterReceiveInterval, receiveInterval } };
            peer.OpCustom((byte)OperationCode.SubscribeCounter, data, true, Settings.DiagnosticsChannel);
        }

        public static void CreateWorld(Game game, string worldName, BoundingBox boundingBox, Vector tileDimensions)
        {
            var data = new Dictionary<byte, object>
                {
                    { (byte)ParameterCode.WorldName, worldName }, 
                    { (byte)ParameterCode.BoundingBox, boundingBox }, 
                    { (byte)ParameterCode.TileDimensions, tileDimensions }
                };
            game.SendOperation(OperationCode.CreateWorld, data, true, Settings.OperationChannel);
        }

        public static void DestroyItem(Game game, string itemId)
        {
            var data = new Dictionary<byte, object> { { (byte)ParameterCode.ItemId, itemId } };
            game.SendOperation(OperationCode.DestroyItem, data, true, Settings.ItemChannel);
        }

        public static void DetachInterestArea(Game game)
        {
            game.SendOperation(OperationCode.DetachInterestArea, new Dictionary<byte, object>(), true, Settings.ItemChannel);
        }

        public static void EnterWorld(
            Game game, string worldName, string username, Hashtable properties, Vector position, Vector rotation, Vector viewDistanceEnter, Vector viewDistanceExit)
        {
            var data = new Dictionary<byte, object>
                {
                    { (byte)ParameterCode.WorldName, worldName }, 
                    { (byte)ParameterCode.Username, username }, 
                    { (byte)ParameterCode.Position, position }, 
                    { (byte)ParameterCode.ViewDistanceEnter, viewDistanceEnter }, 
                    { (byte)ParameterCode.ViewDistanceExit, viewDistanceExit }
                };
            if (properties != null)
            {
                data.Add((byte)ParameterCode.Properties, properties);
            }

            if (!rotation.IsZero)
            {
                data.Add((byte)ParameterCode.Rotation, rotation);
            }

            game.SendOperation(OperationCode.EnterWorld, data, true, Settings.OperationChannel);
        }

        public static void ExitWorld(Game game)
        {
            game.SendOperation(OperationCode.ExitWorld, new Dictionary<byte, object>(), true, Settings.OperationChannel);
        }

        public static void GetProperties(Game game, string itemId, int? knownRevision)
        {
            var data = new Dictionary<byte, object> { { (byte)ParameterCode.ItemId, itemId } };
            if (knownRevision.HasValue)
            {
                data.Add((byte)ParameterCode.PropertiesRevision, knownRevision.Value);
            }

            game.SendOperation(OperationCode.GetProperties, data, true, Settings.ItemChannel);
        }

        public static void Move(Game game, string itemId, Vector position, Vector rotation, bool sendReliable)
        {
            var data = new Dictionary<byte, object> { { (byte)ParameterCode.Position, position } };
            if (itemId != null)
            {
                data.Add((byte)ParameterCode.ItemId, itemId);
            }

            if (!rotation.IsZero)
            {
                data.Add((byte)ParameterCode.Rotation, rotation);
            }

            game.SendOperation(OperationCode.Move, data, sendReliable, Settings.ItemChannel);
        }

        public static void VelocityRot(Game game, string itemId, Vector vel, Vector? rot, Vector? mouseFwd, bool isMegaThrust, bool sendReliable)
        {
            var data = new Dictionary<byte, object> { { (byte)ParameterCode.Velocity, vel } };
            if (itemId != null)
                data.Add((byte)ParameterCode.ItemId, itemId);

            if (rot != null)
                data.Add((byte)ParameterCode.Rotation, rot);
            if (mouseFwd != null)
                data.Add((byte)ParameterCode.MouseFwd, mouseFwd);
            if (isMegaThrust == true)
                data.Add((byte)ParameterCode.IsMegaThrust, true);

            game.SendOperation(OperationCode.VelocityRotation, data, sendReliable, Settings.ItemChannel);
        }

        public static void ApplyBurst(Game game, string itemId, bool sendReliable)
        {
            var data = new Dictionary<byte, object>();
            if (itemId != null)
                data.Add((byte)ParameterCode.ItemId, itemId);
            game.SendOperation(OperationCode.Burst, data, sendReliable, Settings.ItemChannel);
        }

        public static void Break(Game game, string itemId, bool sendReliable)
        {
            var data = new Dictionary<byte, object>();
            if (itemId != null)
                data.Add((byte)ParameterCode.ItemId, itemId);
            game.SendOperation(OperationCode.Breaks, data, sendReliable, Settings.ItemChannel);

        }

        public static void FireSaber(Game game, string itemId, bool sendReliable)
        {
            var data = new Dictionary<byte, object>();
            if (itemId != null)
                data.Add((byte)ParameterCode.ItemId, itemId);
            game.SendOperation(OperationCode.FireSaber, data, sendReliable, Settings.ItemChannel);
        }

        public static void StartSuperFast(Game game, string itemId, Vector rot, bool sendReliable)
        {
            var data = new Dictionary<byte, object>();
            if (itemId != null)
                data.Add((byte)ParameterCode.ItemId, itemId);
            
            data.Add((byte)ParameterCode.Rotation, rot);
            game.SendOperation(OperationCode.StartSuperFast, data, sendReliable, Settings.ItemChannel);
        }

        public static void EndSuperFast(Game game, string itemId, bool sendReliable)
        {
            var data = new Dictionary<byte, object>();
            if (itemId != null)
                data.Add((byte)ParameterCode.ItemId, itemId);

            game.SendOperation(OperationCode.EndSuperFast, data, sendReliable, Settings.ItemChannel);
        }

        // todo: probably want to add playerid here to avoid friendly fire
        public static void FireLaser(Game game, string itemId, bool sendReliable)
        {
            var data = new Dictionary<byte, object>();
            if (itemId != null)
                data.Add((byte)ParameterCode.ItemId, itemId);
            game.SendOperation(OperationCode.FireLaser, data, sendReliable, Settings.ItemChannel);
        }

        public static void FireBullet(Game game, Vector position, Vector rotation, float velx, float vely, bool sendReliable)
        {
            var data = new Dictionary<byte, object> { { (byte)ParameterCode.Position, position } };
            if (!rotation.IsZero)
            {
                data.Add((byte)ParameterCode.Rotation, rotation);
              //  data.Add( (byte)ParameterCode.OldRotation, new Vector(velx, vely)); // this is actually avatar velocity
            }
            game.SendOperation(OperationCode.FireBullet, data, sendReliable, Settings.ItemChannel);
        }

        public static void LaunchBomb(Game game, Vector rotation, string itemId, bool sendReliable)
        {
            var data = new Dictionary<byte, object>();
            if (itemId != null)
                data.Add((byte)ParameterCode.ItemId, itemId);
            if (!rotation.IsZero)
            {
                data.Add((byte)ParameterCode.Rotation, rotation);
                //  data.Add( (byte)ParameterCode.OldRotation, new Vector(velx, vely)); // this is actually avatar velocity
            }
            game.SendOperation(OperationCode.ShootBomb, data, sendReliable, Settings.ItemChannel);
        }



        public static void MoveInterestArea(Game game, byte cameraId, Vector position)
        {
            var data = new Dictionary<byte, object> { { (byte)ParameterCode.InterestAreaId, cameraId }, { (byte)ParameterCode.Position, position } };

            game.SendOperation(OperationCode.MoveInterestArea, data, true, Settings.ItemChannel);
        }

        public static void RadarSubscribe(ExitGames.Client.Photon.PhotonPeer peer, string worldName)
        {
            var data = new Dictionary<byte, object> { { (byte)ParameterCode.WorldName, worldName } };
            peer.OpCustom((byte)OperationCode.RadarSubscribe, data, true, Settings.RadarChannel);
        }

        public static void RemoveInterestArea(Game game, byte cameraId)
        {
            var data = new Dictionary<byte, object> { { (byte)ParameterCode.InterestAreaId, cameraId } };

            game.SendOperation(OperationCode.RemoveInterestArea, data, true, Settings.ItemChannel);
        }

        public static void SetProperties(Game game, string itemId, Hashtable propertiesSet, ArrayList propertiesUnset, bool sendReliable)
        {
            var data = new Dictionary<byte, object>();
            if (propertiesSet != null)
            {
                data.Add((byte)ParameterCode.PropertiesSet, propertiesSet);
            }

            if (propertiesUnset != null)
            {
                data.Add((byte)ParameterCode.PropertiesUnset, propertiesUnset);
            }

            if (itemId != null)
            {
                data.Add((byte)ParameterCode.ItemId, itemId);
            }

            game.SendOperation(OperationCode.SetProperties, data, sendReliable, Settings.ItemChannel);
        }

        public static void SetViewDistance(Game game, Vector viewDistanceEnter, Vector viewDistanceExit)
        {
            var data = new Dictionary<byte, object>
                {
                    { (byte)ParameterCode.ViewDistanceEnter, viewDistanceEnter }, { (byte)ParameterCode.ViewDistanceExit, viewDistanceExit } 
                };
            game.SendOperation(OperationCode.SetViewDistance, data, true, Settings.ItemChannel);
        }

        public static void SpawnItem(Game game, string itemId, ItemType itemType, Vector position, Vector rotation, Hashtable properties, bool subscribe)
        {
            var data = new Dictionary<byte, object>
                {
                    { (byte)ParameterCode.Position, position }, 
                    { (byte)ParameterCode.ItemId, itemId }, 
                    { (byte)ParameterCode.ItemType, (byte)itemType }, 
                    { (byte)ParameterCode.Subscribe, subscribe }
                };
            if (properties != null)
            {
                data.Add((byte)ParameterCode.Properties, properties);
            }

            if (!rotation.IsZero)
            {
                data.Add((byte)ParameterCode.Rotation, rotation);
            }

            game.SendOperation(OperationCode.SpawnItem, data, true, Settings.ItemChannel);
        }

        public static void SubscribeItem(Game game, string itemId, ItemType itemType, int? propertiesRevision)
        {
            var data = new Dictionary<byte, object> { { (byte)ParameterCode.ItemId, itemId }, { (byte)ParameterCode.ItemType, (byte)itemType } };
            if (propertiesRevision.HasValue)
            {
                data.Add((byte)ParameterCode.PropertiesRevision, propertiesRevision);
            }

            game.SendOperation(OperationCode.SubscribeItem, data, true, Settings.ItemChannel);
        }

        public static void UnsubscribeItem(Game game, string itemId)
        {
            var data = new Dictionary<byte, object> { { (byte)ParameterCode.ItemId, itemId } };

            game.SendOperation(OperationCode.UnsubscribeItem, data, true, Settings.ItemChannel);
        }
    }
}
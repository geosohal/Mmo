﻿namespace Photon.MmoDemo.Server.Operations
{
    using Photon.MmoDemo.Common;
    using Photon.SocketServer;
    using Photon.MmoDemo.Server;
    using Photon.SocketServer.Rpc;

    /// <summary>
    /// The operation fires a laser starting at the position with a given direction/rotation 
    /// </summary>
    /// <remarks>
    /// This operation is allowed AFTER having entered a World with operation EnterWorld.
    /// </remarks>
    public class FireLaser : Operation
    {
        public FireLaser(IRpcProtocol protocol, OperationRequest request)
            : base(protocol, request)
        {
        }

        // ItemId is the id of the player who fired the laser
        [DataMember(Code = (byte)ParameterCode.ItemId, IsOptional = true)]
        public string ItemId { get; set; }

        [DataMember(Code = (byte)ParameterCode.Position)]
        public Vector Position { get; set; }

        [DataMember(Code = (byte)ParameterCode.Rotation, IsOptional = true)]
        public Vector Rotation { get; set; }

        public OperationResponse GetOperationResponse(short errorCode, string debugMessage)
        {
            var responseObject = new FireLaserResponse { ItemId = this.ItemId };
            return new OperationResponse(this.OperationRequest.OperationCode, responseObject) { ReturnCode = errorCode, DebugMessage = debugMessage };
        }

        public OperationResponse GetOperationResponse(MethodReturnValue returnValue)
        {
            return this.GetOperationResponse(returnValue.Error, returnValue.Debug);
        }
    }


    public class FireLaserResponse
    {
        [DataMember(Code = (byte)ParameterCode.ItemId)]
        public string ItemId { get; set; }
    }
}
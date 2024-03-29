﻿
namespace Photon.MmoDemo.Server.Operations
{
    using Photon.MmoDemo.Common;
    using Photon.SocketServer;
    using Photon.MmoDemo.Server;
    using Photon.SocketServer.Rpc;

    /// <summary>
    /// The operation moves an Item. 
    /// </summary>
    /// <remarks>
    /// This operation is allowed AFTER having entered a World with operation EnterWorld.
    /// </remarks>
    public class VelocityRotation : Operation
    {
        public VelocityRotation(IRpcProtocol protocol, OperationRequest request)
            : base(protocol, request)
        {
        }

        // If not submitted the MmoActor.Avatar is selected.
        [DataMember(Code = (byte)ParameterCode.ItemId, IsOptional = true)]
        public string ItemId { get; set; }

        [DataMember(Code = (byte)ParameterCode.Velocity)]
        public Vector Velocity { get; set; }

        [DataMember(Code = (byte)ParameterCode.Rotation, IsOptional = true)]
        public Vector Rotation { get; set; }

        [DataMember(Code = (byte)ParameterCode.MouseFwd, IsOptional = true)]
        public Vector MouseFwd { get; set; }

        [DataMember(Code = (byte)ParameterCode.IsMegaThrust, IsOptional = true)]
        public bool IsMegaThrust { get; set; }

        public OperationResponse GetOperationResponse(short errorCode, string debugMessage)
        {
            var responseObject = new MoveResponse { ItemId = this.ItemId };
            return new OperationResponse(this.OperationRequest.OperationCode, responseObject) { ReturnCode = errorCode, DebugMessage = debugMessage };
        }

        public OperationResponse GetOperationResponse(MethodReturnValue returnValue)
        {
            return this.GetOperationResponse(returnValue.Error, returnValue.Debug);
        }
    }

    public class VelocityRotResponse
    {
        // If not submitted the MmoActor.Avatar.
        [DataMember(Code = (byte)ParameterCode.ItemId)]
        public string ItemId { get; set; }
    }
}
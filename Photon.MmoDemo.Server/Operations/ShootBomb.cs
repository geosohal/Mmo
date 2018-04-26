namespace Photon.MmoDemo.Server.Operations
{
    using Photon.MmoDemo.Common;
    using Photon.SocketServer;
    using Photon.MmoDemo.Server;
    using Photon.SocketServer.Rpc;

    /// <summary>
    /// The operation fires a bomb
    /// </summary>
    /// <remarks>
    /// This operation is allowed AFTER having entered a World with operation EnterWorld.
    /// </remarks>
    public class ShootBomb : Operation
    {
        public ShootBomb(IRpcProtocol protocol, OperationRequest request)
            : base(protocol, request)
        {
        }

        // ItemId is the id of the player who fired the Saber
        [DataMember(Code = (byte)ParameterCode.ItemId)]
        public string ItemId { get; set; }

        [DataMember(Code = (byte)ParameterCode.Position, IsOptional = true)]
        public Vector Position { get; set; }

        [DataMember(Code = (byte)ParameterCode.Rotation, IsOptional = true)]
        public Vector Rotation { get; set; }

        public OperationResponse GetOperationResponse(short errorCode, string debugMessage)
        {
            var responseObject = new FireSaberResponse { ItemId = this.ItemId };
            return new OperationResponse(this.OperationRequest.OperationCode, responseObject) { ReturnCode = errorCode, DebugMessage = debugMessage };
        }

        public OperationResponse GetOperationResponse(MethodReturnValue returnValue)
        {
            return this.GetOperationResponse(returnValue.Error, returnValue.Debug);
        }
    }


    public class ShootBombResponse
    {
        [DataMember(Code = (byte)ParameterCode.ItemId)]
        public string ItemId { get; set; }
    }
}
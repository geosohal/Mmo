namespace Photon.MmoDemo.Server.Events
{
    using Photon.MmoDemo.Common;
    using Photon.MmoDemo.Server.Operations;
    using Photon.SocketServer.Rpc;

    using Photon.MmoDemo.Server;

    public class BurstEvent
    {
        [DataMember(Code = (byte)ParameterCode.Position)]
        public Vector Position { get; set; }
    }
}

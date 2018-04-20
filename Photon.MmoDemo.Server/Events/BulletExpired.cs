// ItemDestroyed would have worked the same i think but this could be useful
// later for explosion events or something
namespace Photon.MmoDemo.Server.Events
{
    using Photon.MmoDemo.Common;
    using Photon.MmoDemo.Server.Operations;
    using Photon.MmoDemo.Server;
    using Photon.SocketServer.Rpc;

    class BulletExpired
    {
        // bullet carries its own unique id
        [DataMember(Code = (byte)ParameterCode.ItemId)]
        public string BulletId { get; set; }
    }
}

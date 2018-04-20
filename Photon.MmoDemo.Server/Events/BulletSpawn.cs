
namespace Photon.MmoDemo.Server.Events
{
    using Photon.MmoDemo.Common;
    using Photon.MmoDemo.Server.Operations;
    using Photon.SocketServer.Rpc;

    using Photon.MmoDemo.Server;
    // clients receive this event after a bullet is spawned
    class BulletSpawn
    {
        // bullet carries its own unique id
        [DataMember(Code = (byte)ParameterCode.ItemId)]
        public string BulletId { get; set; }
    }
}

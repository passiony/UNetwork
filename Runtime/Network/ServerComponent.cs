using System;
using System.Threading;

namespace UNetwork
{
    /// <summary>
    /// Server 服务器的 业务逻辑 管理器
    /// </summary>
    public class ServerComponent : MonoSingleton<ServerComponent>, INetworkComponent
    {
        public string IP = "127.0.0.1";
        public int Port = 12345;
        public NetworkProtocol protocol;

        public AService Service { get; private set; }
        public SessionServer Session { get; private set; }

        public IMessagePacker MessagePacker { get; set; }
        public IMessageDispatcher MessageDispatcher { get; set; }

        public Action<int> OnConnect { get; set; }
        public Action<int> OnError { get; set; }
        public Action<byte[]> OnMessage { get; set; }
        public bool IsConnecting => this.Service.GetChannel().IsConnected;


        protected override void Init()
        {
            SynchronizationContext.SetSynchronizationContext(OneThreadSynchronizationContext.Instance);

            InitService(protocol);
            //设置消息packer(json,protobuf)
            MessagePacker = new ProtobufPacker();
            //设置消息分发（可选）
            MessageDispatcher = new OuterMessageDispatcher();
        }

        //server
        public void InitService(NetworkProtocol protocol, int packetSize = Packet.PacketSizeLength4)
        {
            switch (protocol)
            {
                default:
                    this.Service = new TServiceServer(packetSize);
                    break;
            }
        }

        public void Start()
        {
            AChannel channel = this.Service.ConnectChannel(NetworkHelper.ToIPEndPoint(IP, Port));
            Session = new SessionServer(channel);
            Session.Start(this);
        }

        public void Update()
        {
            OneThreadSynchronizationContext.Instance.Update();

            if (this.Service == null)
            {
                return;
            }

            this.Service.Update();
        }

        public void Send(byte[] data)
        {
            Session.Send(data);
        }

        public override void Dispose()
        {
            Session.Dispose();
        }
    }
}
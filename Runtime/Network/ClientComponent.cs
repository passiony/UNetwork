﻿using System;
using System.Threading;
using UnityEngine;

namespace UNetwork
{
    /// <summary>
    /// 客户端的 业务逻辑 管理类
    /// </summary>
    public class ClientComponent :MonoBehaviour, INetworkComponent
    {
        public string IP;
        public int Port;
        public NetworkProtocol protocol;
        
        public AService Service { get; private set; }
        public Session Session { get; private set; }

        public IMessagePacker MessagePacker { get; set; }
        public IMessageDispatcher MessageDispatcher { get; set; }

        public Action<int> OnConnect { get; set; }
        public Action<int> OnError { get; set; }
        public Action<byte[]> OnMessage { get; set; }

        protected virtual void Awake()
        {
            SynchronizationContext.SetSynchronizationContext(OneThreadSynchronizationContext.Instance);
            
            InitService(protocol);
            //设置消息packer(json,protobuf)
            MessagePacker = new ProtobufPacker();
            //设置消息分发（可选）
            MessageDispatcher = new OuterMessageDispatcher();
            
            OnConnect += OnConnectMessage;
            OnError += OnErrorMessage;
            OnMessage += OnMessageMessage;
        }
        
        protected virtual void InitService(NetworkProtocol protocol, int packetSize = Packet.PacketSizeLength4)
        {
            switch (protocol)
            {
                case NetworkProtocol.KCP:
                    this.Service = new KService() { };
                    break;
                case NetworkProtocol.TCP:
                    this.Service = new TService(packetSize) { };
                    break;
                case NetworkProtocol.Modbus:
                    this.Service = new ModbusService() { };
                    break;
                case NetworkProtocol.WebSocket:
                    this.Service = new WService() { };
                    break;
            }
        }

        public virtual void Connect()
        {
            AChannel channel = this.Service.ConnectChannel(NetworkHelper.ToIPEndPoint(IP, Port));
            Session = new Session(channel);
            Session.Start(this);
            
            Debug.Log("Start Connecting");
        }
        
        public virtual void Send(byte[] data)
        {
            Session.Send(data);
        }
        
        protected virtual void Update()
        {
            OneThreadSynchronizationContext.Instance.Update();

            if (this.Service == null)
            {
                return;
            }

            this.Service.Update();
        }
        
        protected virtual void OnMessageMessage(byte[] bytes)
        {
            Debug.LogError("接收到消息：" + bytes.Length);
        }

        protected virtual void OnErrorMessage(int e)
        {
            Debug.LogError("网络错误：" + e);
        }

        protected virtual void OnConnectMessage(int c)
        {
            Debug.Log("连接成功");
        }

        protected virtual void OnDestroy()
        {
            Session.Dispose();
        }
    }
}
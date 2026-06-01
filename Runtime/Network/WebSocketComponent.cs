using System;
using System.Text;
using UnityEngine;

namespace UNetwork
{
    /// <summary>
    /// Web Socket通信组件类，继承自ClientComponent，
    /// </summary>
    public class WebSocketComponent : ClientComponent
    {
        public string StationId = "TG004";

        private const string TAG = "<color=green>[WebSocket] </color>";

        public override void Connect()
        {
            var address = $"ws://{IP}:{Port}/webSocket/{StationId}";
            Debug.Log(TAG + " address：" + address);

            AChannel channel = this.Service.ConnectChannel(address);
            Session = new Session(channel);
            Session.Start(this);

            Debug.Log(TAG + "Start Connecting");
        }

        protected override void OnConnectMessage(int c)
        {}

        protected override void OnMessageMessage(byte[] bytes)
        {}
    }
}
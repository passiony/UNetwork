using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using Microsoft.IO;
using UnityEngine;

namespace UNetwork
{
    public class WService : AService
    {
        private readonly HttpListener httpListener;

        private WChannel channel;

        public RecyclableMemoryStreamManager MemoryStreamManager = new RecyclableMemoryStreamManager();

        public WService()
        {
        }

        public override AChannel GetChannel()
        {
            return channel;
        }

        public override AChannel ConnectChannel(IPEndPoint ipEndPoint)
        {
            // string address = "ws://" + ipEndPoint.Address + ":" + ipEndPoint.Port+"/webSocket/";
            string address = "ws://192.168.8.96:8000/webSocket/TG004";
            Debug.Log("WS Connect: " + address);
            return ConnectChannel(address);
        }

        public override AChannel ConnectChannel(string address)
        {
            ClientWebSocket webSocket = new ClientWebSocket();
            channel = new WChannel(webSocket, this);
            channel.ConnectAsync(address);
            return channel;
        }

        public override void Update()
        {
        }

        public override void Dispose()
        {
        }
    }
}
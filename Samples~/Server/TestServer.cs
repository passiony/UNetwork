using System;
using System.Text;
using UNetwork;
using UnityEngine;

public class TestServer : MonoBehaviour
{
    public string ip = "127.0.0.1";
    public int port = 12346;
    
    public string sendMessage = "server";
    public static long starttime = 0;

    void Start()
    {
        //获取Server实例
        ServerManager client = ServerManager.Instance;
        //Tcp方式初始化
        client.InitService(NetworkProtocol.TCP);
        //设置pack方式(json、protobuf)
        client.MessagePacker = new ProtobufPacker();
        //设置消息分发（可选）
        client.MessageDispatcher = new OuterMessageDispatcher();
        //开始服务监听
        client.Connect(ip, port);
        //添加网络事件回调
        client.OnConnect += OnConnect;
        client.OnError += OnError;
        //消息接收（和消息分发，都可以处理消息）
        client.OnMessage += OnMessage;
    }

    private void OnMessage(byte[] obj)
    {
        var msg = Encoding.UTF8.GetString(obj);
        Debug.Log($"Receive=>{obj.Length}:" + msg);
    }

    private void OnError(int e)
    {
        Debug.LogError("网络错误：" + e);
    }

    private void OnConnect(int c)
    {
        Debug.Log("连接成功");
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            var data = Encoding.UTF8.GetBytes(sendMessage);
            Debug.Log($"Send=>{data.Length}:" + sendMessage);

            ServerManager.Instance.Send(data);
            starttime = GetTimeStamp();
        }
    }

    /// <summary>
    /// 获取时间戳
    /// </summary>
    /// <returns></returns>
    public static long GetTimeStamp()
    {
        return new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
    }

    public static void Receive()
    {
        var inteval = GetTimeStamp() - starttime;
        Debug.LogWarning(inteval);
    }
}

using System;
using System.Text;
using UNetwork;
using UnityEngine;

public class TestServer : MonoBehaviour
{
    private ServerComponent server;
    public string sendMessage = "server";
    public static long starttime = 0;

    void Start()
    {
        //获取Server实例
        server = gameObject.GetComponent<ServerComponent>();

        //添加网络事件回调
        server.OnConnect += OnConnect;
        server.OnError += OnError;
        server.OnMessage += OnMessage;

        //开始服务监听
        server.Start();
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

            server.Send(data);
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
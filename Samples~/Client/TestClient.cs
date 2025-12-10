using System;
using System.Text;
using UNetwork;
using UnityEngine;

public class TestClient : MonoBehaviour
{
    private ClientComponent client;
    public string sendMessage = "client";
    public static long starttime = 0;

    void Start()
    {
        //获取实例
        client = gameObject.GetComponent<ClientComponent>();

        //设置网络事件回调
        client.OnConnect += OnConnect;
        client.OnError += OnError;
        client.OnMessage += OnMessage;

        //连接服务器
        client.Connect();
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

            client.Send(data);
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
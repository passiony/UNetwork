## UNetwork

一个通用 的socket 网络插件，安装方便，使用简单。

## 功能

1.含有Server端和Client端，即可做服务器，有可以做客户端

2.内部包括TCP，KCP，WebSocket，三种Socket策略可选

## 示例

1.导入插件
![08b06590-1c37-4a26-b200-6e56b8391d6c](https://github.com/user-attachments/assets/b35a461d-2a43-43cc-8aec-1fd090dfa92a)

2.服务器示例
```csharp
public class TestServer : MonoBehaviour
{
    public string ip = "127.0.0.1";
    public int port = 12346;

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
        //开始连接
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
}
```

客户端示例
```csharp
public class TestClient : MonoBehaviour
{
    public string ip = "127.0.0.1";
    public int port = 12346;

    void Start()
    {
        //获取实例
        ClientManager client = ClientManager.Instance;
        //初始化客户端，以TCP
        client.InitService(NetworkProtocol.TCP);
        //设置消息packer(json,protobuf)
        client.MessagePacker = new ProtobufPacker();
        //设置消息分发（可选）
        client.MessageDispatcher = new OuterMessageDispatcher();

        //连接服务器
        client.Connect(ip,port);
        //设置网络事件回调
        client.OnConnect += OnConnect;
        client.OnError += OnError;
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
}
```

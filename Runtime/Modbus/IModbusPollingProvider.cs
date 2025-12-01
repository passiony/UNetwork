using System.Collections;

namespace UNetwork
{
    /// <summary>
    /// 定义可用的轮询模式
    /// </summary>
    public enum PollingMode { 
        /// <summary>不启用自动轮询</summary>
        None, 
        /// <summary>定时轮询（旧模式）：按频率发送，不等待响应。</summary>
        TimeDriven, 
        /// <summary>顺序轮询（新模式）：发送一个，等待响应/超时，再发送下一个。</summary>
        Sequenced 
    }
    
    /// <summary>
    /// 轮询策略接口
    /// </summary>
    public interface IModbusPollingProvider
    {
        IEnumerator StartPolling();
    }
}
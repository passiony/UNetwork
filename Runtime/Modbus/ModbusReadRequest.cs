namespace UNetwork
{
    /// <summary>
    /// 定义Modbus读取请求的结构体，用于队列管理
    /// </summary>
    public struct ModbusReadRequest
    {
        public enum RequestType { Coil, Register }
        public RequestType Type;
        public ushort StartAddress;
        public ushort Length;

        public ModbusReadRequest(RequestType type, ushort startAddress, ushort length)
        {
            Type = type;
            StartAddress = startAddress;
            Length = length;
        }
    }
}
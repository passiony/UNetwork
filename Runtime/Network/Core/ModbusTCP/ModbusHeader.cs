using System;

namespace UNetwork
{
    public class ModbusHeader
    {
        // MBAP报文头 : [事务元标识符+协议标识符+PDU长度+单元标识符] 7字节
        public ushort transactionId = 0x0001; // 事务ID：0x0001 → [0x00, 0x01]
        public ushort protocolId = 0x0000; // 协议ID：0x0000 → [0x00, 0x00]
        public byte unitId = 0101; // 单元标识符：从站地址 → [0x06]

        private byte[] protocalHeader;

        public ModbusHeader(int headerSize)
        {
            protocalHeader = new byte[headerSize];
        }

        /// <summary>
        /// 获取MBAP报文头 字节流
        /// </summary>
        /// <param name="length">长度：PDU部分（功能码+数据）</param>
        /// <returns>MBAP报文头 字节流</returns>
        public byte[] GetData(ushort length)
        {
            transactionId++;
            if (transactionId > 5000) transactionId = 1;

            Array.Copy(transactionId.ToBigBytes(true), 0, protocalHeader, 0, 2);
            Array.Copy(protocolId.ToBigBytes(true), 0, protocalHeader, 2, 2);
            Array.Copy(length.ToBigBytes(true), 0, protocalHeader, 4, 2);

            return protocalHeader;
        }
    }
}
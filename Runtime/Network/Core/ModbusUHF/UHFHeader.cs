using System;

namespace UNetwork
{
    public class UHFHeader
    {
        private readonly byte[] protocalHeader;

        public UHFHeader(int headerSize)
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
            Array.Copy(length.ToBigBytes(true), 0, protocalHeader, 4, 2);
            return protocalHeader;
        }
    }
}
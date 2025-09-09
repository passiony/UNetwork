using System;
using System.IO;

namespace UNetwork
{
    public class ModbusTCPParser
    {
        private readonly CircularBuffer buffer;
        private ushort transactionId; // 事务ID
        private ushort protocolId; // 协议ID
        private ushort packetSize; // PDU长度

        private ParserState state;
        public MemoryStream memoryStream;
        private bool isOK;
        private readonly int packetSizeLength;
        private byte[] tempBytes = new byte[2];

        public ModbusTCPParser(int packetSizeLength, CircularBuffer buffer, MemoryStream memoryStream)
        {
            this.packetSizeLength = packetSizeLength;
            this.buffer = buffer;
            this.memoryStream = memoryStream;
        }

        public bool Parse()
        {
            if (this.isOK)
            {
                return true;
            }

            bool finish = false;
            while (!finish)
            {
                switch (this.state)
                {
                    case ParserState.PacketSize:
                        if (this.buffer.Length < this.packetSizeLength)
                        {
                            finish = true;
                        }
                        else
                        {
                            this.buffer.Read(tempBytes, 0, 2);
                            this.transactionId = ByteHelper.ReadBigUshort(this.tempBytes);
                            this.buffer.Read(tempBytes, 0, 2);
                            this.protocolId = ByteHelper.ReadBigUshort(this.tempBytes);
                            this.buffer.Read(tempBytes, 0, 2);
                            this.packetSize = ByteHelper.ReadBigUshort(this.tempBytes);

                            if (this.packetSize > ushort.MaxValue || this.packetSize < Packet.MinPacketSize)
                            {
                                throw new Exception($"recv packet size error:, 可能是外网探测端口: {this.packetSize}");
                            }

                            this.state = ParserState.PacketBody;
                        }

                        break;
                    case ParserState.PacketBody:
                        if (this.buffer.Length < this.packetSize)
                        {
                            finish = true;
                        }
                        else
                        {
                            this.memoryStream.Seek(0, SeekOrigin.Begin);
                            this.memoryStream.SetLength(this.packetSize + 2);
                            byte[] bytes = this.memoryStream.GetBuffer();
                            byte[] transition = BitConverter.GetBytes(transactionId);
                            Buffer.BlockCopy(transition, 0, bytes, 0, 2);
                            this.buffer.Read(bytes, 2, this.packetSize);
                            this.isOK = true;
                            this.state = ParserState.PacketSize;
                            finish = true;
                        }

                        break;
                }
            }

            return this.isOK;
        }

        public MemoryStream GetPacket()
        {
            this.isOK = false;
            return this.memoryStream;
        }
    }
}
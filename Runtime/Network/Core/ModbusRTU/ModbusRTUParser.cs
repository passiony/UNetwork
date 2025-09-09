using System;
using System.IO;

namespace UNetwork
{
    public class ModbusRTUParser
    {
        private readonly CircularBuffer buffer;
        private ushort transactionId; // 事务ID
        private ushort protocolId; // 协议ID
        private ushort packetSize; // PDU长度

        private ParserState state;
        public MemoryStream memoryStream;
        private bool isOK;
        private readonly int packetSizeLength;

        public ModbusRTUParser(int packetSizeLength, CircularBuffer buffer, MemoryStream memoryStream)
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
                            packetSize = (ushort)packetSizeLength;
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
                            this.memoryStream.SetLength(this.packetSize);
                            byte[] bytes = this.memoryStream.GetBuffer();
                            this.buffer.Read(bytes, 0, this.packetSize);
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
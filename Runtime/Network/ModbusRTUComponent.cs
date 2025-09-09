using System;
using System.Collections;
using System.Net;
using UnityEngine;
using UnityEngine.Events;

namespace UNetwork
{
    /// <summary>
    /// Modbus RTU通信组件类，继承自ClientComponent，用于实现Modbus协议的客户端通信功能
    /// 支持读写寄存器和线圈操作
    /// </summary>
    public class ModbusRTUComponent : ClientComponent
    {
        public int DevID = 1;
        public string DevName = "PLC#1";

        /// <summary>
        /// 是否自动读取寄存器
        /// </summary>
        public bool AutoReadRegister;

        /// <summary>
        /// 自动读取寄存器的频率
        /// </summary>
        public int AutoReadRegisterFrequency = 1;

        public UnityEvent<int, ushort[]> OnReadRegister;

        // 发送时务
        private byte[] CoilsData;

        protected override void OnConnectMessage(int c)
        {
            Debug.Log("连接成功");
            StopAllCoroutines();

            if (AutoReadRegister)
                StartCoroutine(CoReadRegisters());
        }

        IEnumerator CoReadRegisters()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f / AutoReadRegisterFrequency);
                ReadRTU(0x06, 2);
            }
        }


        /// <summary>
        /// 发送RTU读取命令
        /// </summary>
        /// <param name="startAddr"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        void ReadRTU(ushort startAddr, ushort length)
        {
            try
            {
                byte[] bytes = new byte[8];

                // 写入协议头：设备ID
                bytes[0] = 0x01;
                // 写入协议头：功能码（读寄存器）
                bytes[1] = PDUCode.READ_HOLDING_REGISTER;
                // 写入起始地址（大端序）：将ushort转换为大端字节数组并复制到发送缓冲区
                Buffer.BlockCopy(startAddr.ToBigBytes(true), 0, bytes, 2, 2);
                // 写入寄存器数量（大端序）：将ushort转换为大端字节数组并复制到发送缓冲区
                Buffer.BlockCopy(length.ToBigBytes(true), 0, bytes, 4, 2);
                // CRC校验
                var crc = ByteHelper.CRC16(bytes, 6);
                Buffer.BlockCopy(crc.ToBigBytes(true), 0, bytes, 6, 2);

                // Debug.Log(string.Join(" ", bytes));
                Send(bytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"写入RTU时发生错误: {ex.Message}");
                throw;
            }
        }


        /// <summary>
        /// 处理接收到的消息数据
        /// 根据功能码解析返回的数据并输出到日志
        /// </summary>
        /// <param name="bytes">接收到的原始字节数据</param>
        protected override void OnMessageMessage(byte[] bytes)
        {
            // 创建ByteBuffer用于解析接收到的数据
            ByteBuffer buffer = new ByteBuffer(bytes);

            // 从站地址
            var unit = buffer.ReadByte();
            // 读取功能码
            var cmd = buffer.ReadByte();
            // 根据功能码进行不同的处理
            switch (cmd)
            {
                case PDUCode.READ_HOLDING_REGISTER:
                {
                    // 读取返回的字节数
                    var count = buffer.ReadByte();
                    var result = new ushort[count / 2];
                    var bits = buffer.ReadBytes(count);
                    for (int i = 0; i < result.Length; i++)
                    {
                        result[i] = (ushort)IPAddress.NetworkToHostOrder((short)BitConverter.ToUInt16(bits, i * 2));
                    }

                    var rcvCrc = buffer.ReadShort();
                    var lacCrc = ByteHelper.CRC16(bytes, 7);
                    if (rcvCrc == lacCrc)
                    {
                        // 输出解析后的线圈状态到日志
                        Log("Read RTU:" + string.Join("-", result));
                        OnReadRegister?.Invoke(DevID, result);
                    }
                }
                    break;
                default:
                    break;
            }
        }

        void Log(string message)
        {
#if LOG
            Debug.Log(message);
#endif
        }
    }
}
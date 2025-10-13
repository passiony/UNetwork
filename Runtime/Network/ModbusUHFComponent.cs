using System;
using UnityEngine;
using UnityEngine.Events;

namespace UNetwork
{
    /// <summary>
    /// UHF电子标签读写器
    /// </summary>
    public class ModbusUHFComponent : ClientComponent
    {
        public int DevID = 1;
        public string DevName = "RFID#1";

        public UnityEvent<int, byte[]> OnReadRegister;

        protected override void OnConnectMessage(int c)
        {
            Debug.Log(DevName + "连接成功");
        }

        /// <summary>
        /// 写多个寄存器的核心方法
        /// </summary>
        /// <param name="startAddr">起始寄存器地址（16进制）</param>
        /// <param name="array">要写入的寄存器值数组</param>
        public void WriteCommands(byte startAddr, byte cmd, byte[] array)
        {
            try
            {
                // 参数校验：检查数组是否为空或长度为0
                if (array == null || array.Length == 0)
                    throw new ArgumentException("寄存器数组不能为空");

                // 获取要写入的寄存器数量
                byte length = (byte)array.Length;
                // 创建用于发送的字节数组，包含7字节头部信息和数据部分
                byte[] bytes = new byte[3 + length + 2];

                bytes[0] = (byte)bytes.Length;
                // 写入协议头：单元ID（设备地址）
                bytes[1] = startAddr;
                // 写入协议头：功能码（写多个寄存器）
                bytes[2] = cmd;

                // 直接写入寄存器数据（避免中间集合）
                // 遍历每个寄存器值，将其转换为大端字节数组并写入发送缓冲区
                for (int i = 0; i < array.Length; i++)
                {
                    bytes.WriteTo(3 + i, array[i]);
                }

                // CRC校验
                var crc = ByteHelper.CRC16(bytes, 0, bytes.Length - 2);
                Buffer.BlockCopy(crc.ToBytes(), 0, bytes, bytes.Length - 2, 2);

                Send(bytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"写入寄存器时发生错误: {ex.Message}");
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
            var length = buffer.ReadByte();
            var unit = buffer.ReadByte();
            var cmd = buffer.ReadByte();
            var status = buffer.ReadByte();
            // 根据功能码进行不同的处理
            switch (cmd)
            {
                case 0xEE:
                {
                    var epcID = buffer.ReadBytes(length - 5);
                    var rcvCrc = buffer.ReadShort();
                    var lacCrc = ByteHelper.getCRC_MCRF4(bytes, 16);
                    if (rcvCrc == lacCrc)
                    {
                        OnReadRegister?.Invoke(DevID, epcID);
                    }
                    break;
                }
                default:
                {
                    var error = buffer.ReadByte(); //错误码
                    Debug.LogError($"Modbus Error: {error}");
                    break;
                }
            }
        }
    }
}
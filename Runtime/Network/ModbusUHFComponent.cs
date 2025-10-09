using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
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
        public string DevName = "PLC#1";

        /// <summary>
        /// 是否自动读取寄存器
        /// </summary>
        public bool AutoReadRegister;

        /// <summary>
        /// 自动读取寄存器的频率
        /// </summary>
        public int AutoReadRegisterFrequency = 1;

        public UnityEvent<int, int, ushort[]> OnReadRegister;
        public UnityEvent<int, ushort[]> OnWriteRegister;

        // 默认寄存器起始地址 (D3000)
        private const byte REGISTER_ADDR = 0x00;

        // 最大寄存器数量限制
        public ushort READ_REGISTER_COUNT = 16;

        /// <summary>
        /// 错误码
        /// </summary>
        public static readonly string[] ErrorCode = new string[]
        {
            "未知错误",
            "非法的功能码",
            "非法的数据地址",
            "非法的数据值",
            "服务器故障",
        };

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
                ReadCommands(READ_REGISTER_COUNT);
            }
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
                // 参数校验：检查数组长度是否超出最大限制
                if (array.Length > READ_REGISTER_COUNT)
                    throw new ArgumentException("寄存器数组长度超出限制");

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
        /// 读取多个寄存器的便捷方法，使用默认起始地址
        /// </summary>
        /// <param name="count">要读取的寄存器数量</param>
        public void ReadCommands(ushort length)
        {
            // 使用默认寄存器地址
            ReadCommands(REGISTER_ADDR, length);
        }

        /// <summary>
        /// 读取多个寄存器的核心方法
        /// </summary>
        /// <param name="startAddr">起始寄存器地址（16进制）</param>
        /// <param name="length">要读取的寄存器数量</param>
        public void ReadCommands(ushort startAddr, ushort length)
        {
            try
            {
                // 创建用于发送的字节数组，读取请求固定为6字节
                byte[] bytes = new byte[6];
                // 写入单元ID（设备地址）
                bytes.WriteTo(0, PDUCode.UnitID_READ_REGISTER);
                // 写入功能码（读保持寄存器）
                bytes.WriteTo(1, PDUCode.READ_HOLDING_REGISTER);
                // 写入起始地址（大端序）：将ushort转换为大端字节数组并复制到发送缓冲区
                Buffer.BlockCopy(startAddr.ToBigBytes(true), 0, bytes, 2, 2);
                // 写入寄存器数量（大端序）：将ushort转换为大端字节数组并复制到发送缓冲区
                Buffer.BlockCopy(length.ToBigBytes(true), 0, bytes, 4, 2);

                Send(bytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"读取寄存器时发生错误: {ex.Message}");
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

            // 读取事务ID
            var transitionId = buffer.ReadShort();
            // 读取单元ID（设备地址）
            var unit = buffer.ReadByte();
            // 读取功能码
            var cmd = buffer.ReadByte();
            // 根据功能码进行不同的处理
            switch (cmd)
            {
                case PDUCode.READ_INPUT_REGISTER: //读输入寄存器响应
                case PDUCode.READ_HOLDING_REGISTER: //读保持寄存器响应
                {
                    // 读取返回的字节数
                    var count = buffer.ReadByte();
                    // 读取实际的寄存器数据
                    var bits = buffer.ReadBytes();
                    // 创建结果数组，每个寄存器占2个字节
                    var result = new ushort[count / 2];

                    // 解析寄存器数据
                    for (int i = 0; i < result.Length; i++)
                    {
                        // 将网络字节序转换为主机字节序，并存储到结果数组中
                        result[i] = (ushort)IPAddress.NetworkToHostOrder((short)BitConverter.ToUInt16(bits, i * 2));
                    }

                    // 输出解析后的寄存器值到日志
                    Log("Read Register:" + string.Join("-", result));
                    OnReadRegister?.Invoke(DevID, transitionId, result);
                }
                    break;
                case PDUCode.WRITE_SINGLE_REGISTER: //写单个寄存器响应
                case PDUCode.WRITE_MULTIPLE_REGISTER: //写多个寄存器响应
                {
                    // 读取返回的数据
                    var bits = buffer.ReadBytes();
                    // 创建结果数组，每个寄存器占2个字节
                    var result = new ushort[bits.Length / 2];

                    // 解析返回的数据
                    for (int i = 0; i < result.Length; i++)
                    {
                        // 将网络字节序转换为主机字节序，并存储到结果数组中
                        result[i] = (ushort)IPAddress.NetworkToHostOrder((short)BitConverter.ToUInt16(bits, i * 2));
                    }

                    // 输出写入成功的消息到日志
                    Log($"Write Register:" + string.Join("-", result));
                    OnWriteRegister?.Invoke(DevID, result);
                }
                    break;
                default:
                {
                    var symbol = buffer.ReadByte(); //串行链路或其它总线上连接的远程从站的识别码
                    var code = buffer.ReadByte(); //错误命令码
                    var error = buffer.ReadByte(); //错误码
                    Debug.LogError($"Modbus Error:{symbol} {code} {error} {ErrorCode[error]}");
                }

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
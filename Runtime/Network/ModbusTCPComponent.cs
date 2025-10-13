using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using UnityEngine;
using UnityEngine.Events;

namespace UNetwork
{
    /// <summary>
    /// Modbus TCP通信组件类，继承自ClientComponent，用于实现Modbus协议的客户端通信功能
    /// 支持读写寄存器和线圈操作
    /// </summary>
    public class ModbusTCPComponent : ClientComponent
    {
        public int DevID = 1;
        public string DevName = "PLC#1";

        /// <summary>
        /// 是否自动读取线圈
        /// </summary>
        public bool AutoReadCoil;

        /// <summary>
        /// 自动读取线圈的频率
        /// </summary>
        public int AutoReadCoilFrequency = 1;

        /// <summary>
        /// 是否自动读取寄存器
        /// </summary>
        public bool AutoReadRegister;

        /// <summary>
        /// 自动读取寄存器的频率
        /// </summary>
        public int AutoReadRegisterFrequency = 1;

        public UnityEvent<int, int, byte[]> OnReadCoil;
        public UnityEvent<int, ushort[]> OnWriteCoil;
        public UnityEvent<int, int, ushort[]> OnReadRegister;
        public UnityEvent<int, ushort[]> OnWriteRegister;

        // 默认寄存器起始地址 (D3000)
        public const ushort REGISTER_ADDR = 0x0BB8;

        // 默认写线圈起始地址 (D24576)
        public const ushort WRITE_COIL_ADDR1 = 0x6000;
        public const ushort WRITE_COIL_ADDR2 = 0x6100;

        // 默认读线圈起始地址 (D20480)
        public const ushort READ_COIL_ADDR1 = 0x5000;
        public const ushort READ_COIL_ADDR2 = 0x5100;

        // 默认RTU透传，写入寄存器起始地址 (D4000)
        public const ushort WRITE_RTU_ADDR = 0x0FA0;

        //读取RTU透传数据。默认寄存器起始地址 (D4300)
        public const ushort READE_RTU_ADDR = 0x10CC;
        
        //单个PLC线圈数量
        public const ushort A_PLC_COIL_COUNT = 16;

        // 最大线圈数量限制
        public ushort READ_COIL_COUNT = 16;

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

        public bool CustomRegister;
        public ushort CustomStartAddr = 0x06;
        public ushort CustomLength = 2;

        // 发送时务
        private Dictionary<int, ushort> Transitions;
        private byte[] CoilsData;

        protected override void OnConnectMessage(int c)
        {
            Transitions = new Dictionary<int, ushort>();
            Debug.Log(DevName + "连接成功");

            StopAllCoroutines();

            if (AutoReadCoil)
                StartCoroutine(CoReadCoil(READ_COIL_COUNT));

            if (AutoReadRegister)
                StartCoroutine(CoReadRegisters());
        }

        IEnumerator CoReadCoil(ushort coilCount)
        {
            CoilsData = new byte[coilCount];
            while (true)
            {
                yield return new WaitForSeconds(1f / AutoReadCoilFrequency);
                // 调用带起始地址的重载方法，使用默认读线圈地址
                if (coilCount <= A_PLC_COIL_COUNT)
                {
                    ReadMultipleCoil(READ_COIL_ADDR1, coilCount);
                }
                else
                {
                    ReadMultipleCoil(READ_COIL_ADDR1, A_PLC_COIL_COUNT);
                    yield return new WaitForSeconds(1f / AutoReadCoilFrequency);
                    ReadMultipleCoil(READ_COIL_ADDR2, (ushort)(coilCount - A_PLC_COIL_COUNT));
                }
            }
        }

        IEnumerator CoReadRegisters()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f / AutoReadRegisterFrequency);
                if (CustomRegister)
                {
                    ReadMultipleRegisters(READ_REGISTER_COUNT);
                }
                else
                {
                    ReadMultipleRegisters(CustomStartAddr, CustomLength);
                }
            }
        }

        /// <summary>
        /// 透传RTU寄存器
        /// </summary>
        /// <param name="startAddr"></param>
        /// <param name="length"></param>
        public void RequestRTU(ushort startAddr, ushort length)
        {
            var rtuBytes = GetRTUCmd(startAddr, length);
            WriteRTURegisters(WRITE_RTU_ADDR, rtuBytes);
        }

        /// <summary>
        /// 读取RTU数据
        /// </summary>
        /// <param name="length"></param>
        public void ReadRTU(ushort length)
        {
            ReadMultipleRegisters(READE_RTU_ADDR, length);
        }

        /// <summary>
        /// 获取RTU透传读取命令
        /// </summary>
        /// <param name="startAddr"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        byte[] GetRTUCmd(ushort startAddr, ushort length)
        {
            byte[] bytes = new byte[9];

            // 写入数据长度
            bytes[0] = 8;
            // 写入协议头：设备ID
            bytes[1] = 0x05;
            // 写入协议头：功能码（读寄存器）
            bytes[2] = PDUCode.READ_HOLDING_REGISTER;
            // 写入起始地址（大端序）：将ushort转换为大端字节数组并复制到发送缓冲区
            Buffer.BlockCopy(startAddr.ToBigBytes(true), 0, bytes, 3, 2);
            // 写入寄存器数量（大端序）：将ushort转换为大端字节数组并复制到发送缓冲区
            Buffer.BlockCopy(length.ToBigBytes(true), 0, bytes, 5, 2);
            // CRC校验
            var crc = ByteHelper.CRC16(bytes, 1, 6);
            Buffer.BlockCopy(crc.ToBytes(), 0, bytes, 7, 2);

            return bytes;
        }

        /// <summary>
        /// 透传RTU寄存器的核心方法
        /// </summary>
        /// <param name="startAddr">起始寄存器地址（16进制）</param>
        /// <param name="array">要写入的寄存器值数组</param>
        public void WriteRTURegisters(ushort startAddr, byte[] array)
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
                ushort length = (ushort)array.Length;
                // 计算数据字节数（每个寄存器占2个字节）
                byte byteCount = (byte)(length * 2);
                // 创建用于发送的字节数组，包含7字节头部信息和数据部分
                byte[] bytes = new byte[7 + byteCount];

                // 写入协议头：单元ID（设备地址）
                bytes[0] = PDUCode.UnitID_WRITE_REGISTER;
                // 写入协议头：功能码（写多个寄存器）
                bytes[1] = PDUCode.WRITE_MULTIPLE_REGISTER;

                // 写入起始地址（大端序）：将ushort转换为大端字节数组并复制到发送缓冲区
                Buffer.BlockCopy(startAddr.ToBigBytes(true), 0, bytes, 2, 2);
                // 写入寄存器数量（大端序）：将ushort转换为大端字节数组并复制到发送缓冲区
                Buffer.BlockCopy(length.ToBigBytes(true), 0, bytes, 4, 2);
                // 写入字节计数：数据部分的总字节数
                bytes[6] = byteCount;

                // 直接写入寄存器数据（避免中间集合）
                // 遍历每个寄存器值，将其转换为大端字节数组并写入发送缓冲区
                for (int i = 0; i < array.Length; i++)
                {
                    bytes[7 + i * 2 + 1] = array[i];
                }

                // Debug.Log(string.Join(" ", bytes));
                Send(startAddr, bytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"写入RTU寄存器时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 写多个寄存器的便捷方法，使用默认起始地址
        /// </summary>
        /// <param name="array">要写入的寄存器值数组</param>
        public void WriteMultipleRegisters(ushort[] array)
        {
            // 使用默认寄存器地址
            WriteMultipleRegisters(REGISTER_ADDR, array);
        }

        /// <summary>
        /// 写多个寄存器的核心方法
        /// </summary>
        /// <param name="startAddr">起始寄存器地址（16进制）</param>
        /// <param name="array">要写入的寄存器值数组</param>
        public void WriteMultipleRegisters(ushort startAddr, ushort[] array)
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
                ushort length = (ushort)array.Length;
                // 计算数据字节数（每个寄存器占2个字节）
                byte byteCount = (byte)(length * 2);
                // 创建用于发送的字节数组，包含7字节头部信息和数据部分
                byte[] bytes = new byte[7 + byteCount];

                // 写入协议头：单元ID（设备地址）
                bytes[0] = PDUCode.UnitID_WRITE_REGISTER;
                // 写入协议头：功能码（写多个寄存器）
                bytes[1] = PDUCode.WRITE_MULTIPLE_REGISTER;

                // 写入起始地址（大端序）：将ushort转换为大端字节数组并复制到发送缓冲区
                Buffer.BlockCopy(startAddr.ToBigBytes(true), 0, bytes, 2, 2);
                // 写入寄存器数量（大端序）：将ushort转换为大端字节数组并复制到发送缓冲区
                Buffer.BlockCopy(length.ToBigBytes(true), 0, bytes, 4, 2);
                // 写入字节计数：数据部分的总字节数
                bytes[6] = byteCount;

                // 直接写入寄存器数据（避免中间集合）
                // 遍历每个寄存器值，将其转换为大端字节数组并写入发送缓冲区
                for (int i = 0; i < array.Length; i++)
                {
                    // 将当前寄存器值转换为大端字节数组
                    byte[] registerBytes = array[i].ToBigBytes(true);
                    // 将字节数据复制到发送缓冲区的正确位置（从第7字节开始的数据部分）
                    Buffer.BlockCopy(registerBytes, 0, bytes, 7 + i * 2, 2);
                }

                Send(startAddr, bytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"写入寄存器时发生错误: {ex.Message}");
            }
        }

        public void WriteMultipleRegistersInt(ushort startAddr, int[] array)
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
                ushort length = (ushort)array.Length;
                // 计算数据字节数（每个寄存器占2个字节）
                byte byteCount = (byte)(length * 4);
                // 创建用于发送的字节数组，包含7字节头部信息和数据部分
                byte[] bytes = new byte[7 + byteCount];

                // 写入协议头：单元ID（设备地址）
                bytes[0] = PDUCode.UnitID_WRITE_REGISTER;
                // 写入协议头：功能码（写多个寄存器）
                bytes[1] = PDUCode.WRITE_MULTIPLE_REGISTER;

                // 写入起始地址（大端序）：将ushort转换为大端字节数组并复制到发送缓冲区
                Buffer.BlockCopy(startAddr.ToBigBytes(true), 0, bytes, 2, 2);
                // 写入寄存器数量（大端序）：将ushort转换为大端字节数组并复制到发送缓冲区
                Buffer.BlockCopy(length.ToBigBytes(true), 0, bytes, 4, 2);
                // 写入字节计数：数据部分的总字节数
                bytes[6] = byteCount;

                // 直接写入寄存器数据（避免中间集合）
                // 遍历每个寄存器值，将其转换为大端字节数组并写入发送缓冲区
                for (int i = 0; i < array.Length; i++)
                {
                    // 将当前寄存器值转换为大端字节数组
                    byte[] registerBytes = array[i].ToBigBytes(true);
                    // 将字节数据复制到发送缓冲区的正确位置（从第7字节开始的数据部分）
                    Buffer.BlockCopy(registerBytes, 0, bytes, 7 + i * 4, 4);
                }

                Send(startAddr, bytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"写入寄存器时发生错误: {ex.Message}");
            }
        }

        public void WriteMultipleRegistersBytes(ushort startAddr, byte[] array)
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
                ushort length = (ushort)array.Length;
                // 计算数据字节数（每个寄存器占2个字节）
                byte byteCount = (byte)(length);
                // 创建用于发送的字节数组，包含7字节头部信息和数据部分
                byte[] bytes = new byte[7 + byteCount];

                // 写入协议头：单元ID（设备地址）
                bytes[0] = PDUCode.UnitID_WRITE_REGISTER;
                // 写入协议头：功能码（写多个寄存器）
                bytes[1] = PDUCode.WRITE_MULTIPLE_REGISTER;

                // 写入起始地址（大端序）：将ushort转换为大端字节数组并复制到发送缓冲区
                Buffer.BlockCopy(startAddr.ToBigBytes(true), 0, bytes, 2, 2);
                // 写入寄存器数量（大端序）：将ushort转换为大端字节数组并复制到发送缓冲区
                Buffer.BlockCopy(length.ToBigBytes(true), 0, bytes, 4, 2);
                // 写入字节计数：数据部分的总字节数
                bytes[6] = byteCount;

                // 直接写入寄存器数据（避免中间集合）
                // 遍历每个寄存器值，将其转换为大端字节数组并写入发送缓冲区
                for (int i = 0; i < array.Length; i++)
                {
                    bytes[7 + i] = array[i];
                }

                Send(startAddr, bytes);
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
        public void ReadMultipleRegisters(ushort length)
        {
            // 使用默认寄存器地址
            ReadMultipleRegisters(REGISTER_ADDR, length);
        }

        /// <summary>
        /// 读取多个寄存器的核心方法
        /// </summary>
        /// <param name="startAddr">起始寄存器地址（16进制）</param>
        /// <param name="length">要读取的寄存器数量</param>
        public void ReadMultipleRegisters(ushort startAddr, ushort length)
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

                Send(startAddr, bytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"读取寄存器时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 写入一个或者多个线圈数据的便捷方法，使用默认起始地址
        /// </summary>
        /// <param name="array">要写入的线圈值数组（0或1）</param>
        public void WriteMultipleCoil(ushort[] array)
        {
            if (array.Length <= 14)
            {
                // 使用默认写线圈地址
                WriteMultipleCoil(WRITE_COIL_ADDR1, array);
            }
            else
            {
                // 使用默认写线圈地址
                var array1 = array.Take(14).ToArray();
                WriteMultipleCoil(WRITE_COIL_ADDR1, array1);
                var array2 = array.Skip(14).Take(array.Length - 14).ToArray();
                WriteMultipleCoil(WRITE_COIL_ADDR2, array2);
            }
        }

        /// <summary>
        /// 写入一个或者多个线圈数据的核心方法
        /// </summary>
        /// <param name="startAddr">起始线圈地址（16进制）</param>
        /// <param name="array">要写入的线圈值数组（0或1）</param>
        public void WriteMultipleCoil(ushort startAddr, ushort[] array)
        {
            try
            {
                // 参数校验：检查数组是否为空或长度为0
                if (array == null || array.Length == 0)
                    throw new ArgumentException("线圈数组不能为空");
                // 参数校验：检查数组长度是否超出最大限制
                if (array.Length > READ_COIL_COUNT)
                    throw new ArgumentException("线圈数组长度超出限制");

                // 获取要写入的线圈数量
                ushort length = (ushort)array.Length;
                // 将ushort数组转换为字节数组（多个ushort值压缩到较少的字节中）
                byte[] byteArray = ByteHelper.IntArrayToByteArray(array);
                // 创建用于发送的字节数组，包含7字节头部信息和数据部分
                byte[] bytes = new byte[7 + byteArray.Length];

                // 写入单元ID（设备地址）
                bytes.WriteTo(0, PDUCode.UnitID_Write_COIL);
                // 写入功能码（写多个线圈）
                bytes.WriteTo(1, PDUCode.WRITE_MULTIPLE_COIL);
                // 写入起始地址（大端序）：将ushort转换为大端字节数组并复制到发送缓冲区
                Buffer.BlockCopy(startAddr.ToBigBytes(true), 0, bytes, 2, 2);
                // 写入线圈数量（大端序）：将ushort转换为大端字节数组并复制到发送缓冲区
                Buffer.BlockCopy(length.ToBigBytes(true), 0, bytes, 4, 2);
                // 写入字节计数：数据部分的总字节数
                bytes.WriteTo(6, (byte)byteArray.Length);
                // 将线圈数据复制到发送缓冲区
                Buffer.BlockCopy(byteArray, 0, bytes, 7, byteArray.Length);

                Send(startAddr, bytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"写入多个线圈时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 读取一个或者多个线圈数据的便捷方法，使用默认起始地址
        /// </summary>
        /// <param name="coilCount">要读取的线圈数量</param>
        public void ReadMultipleCoil(ushort coilCount)
        {
            // 调用带起始地址的重载方法，使用默认读线圈地址
            if (coilCount <= A_PLC_COIL_COUNT)
            {
                ReadMultipleCoil(READ_COIL_ADDR1, coilCount);
            }
            else
            {
                ReadMultipleCoil(READ_COIL_ADDR1, A_PLC_COIL_COUNT);
                ReadMultipleCoil(READ_COIL_ADDR2, (ushort)(coilCount - A_PLC_COIL_COUNT));
            }
        }

        /// <summary>
        /// 读取一个或者多个线圈数据的核心方法
        /// </summary>
        /// <param name="startAddr">起始线圈地址（16进制）</param>
        /// <param name="coilCount">要读取的线圈数量</param>
        public void ReadMultipleCoil(ushort startAddr, ushort coilCount)
        {
            try
            {
                // 创建用于发送的字节数组，读取请求固定为6字节
                byte[] bytes = new byte[6];
                // 写入单元ID（设备地址）
                bytes.WriteTo(0, PDUCode.UnitID_READ_COIL);
                // 写入功能码（读线圈状态）
                bytes.WriteTo(1, PDUCode.READ_COIL_STATUS);
                // 写入起始地址（大端序）：将ushort转换为大端字节数组并复制到发送缓冲区
                Buffer.BlockCopy(startAddr.ToBigBytes(true), 0, bytes, 2, 2);
                // 写入线圈数量（大端序）：将ushort转换为大端字节数组并复制到发送缓冲区
                Buffer.BlockCopy(coilCount.ToBigBytes(true), 0, bytes, 4, 2);

                Send(startAddr, bytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"读取多个线圈时发生错误: {ex.Message}");
            }
        }

        public void Send(ushort startAdd, byte[] data)
        {
            Send(data);
            Transitions.Add(ModbusHeader.transactionId, startAdd);
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
                case PDUCode.READ_COIL_STATUS: //读线圈响应
                case PDUCode.READ_INPUT_STATUS: //读离散线圈响应
                {
                    // 读取返回的字节数
                    var count = buffer.ReadByte();
                    // 创建结果数组，每个字节包含8个线圈状态
                    var result = new byte[count * 8];

                    // 解析每个字节中的线圈状态
                    for (int i = 0; i < count; i++)
                    {
                        // 读取一个字节的数据
                        var bit = buffer.ReadByte();
                        // 解析该字节中的每一位（每个线圈状态）
                        for (int j = 0; j < 8; j++)
                        {
                            // 提取第j位的值（0或1）并存储到结果数组中
                            result[i * 8 + j] = (byte)((bit >> j) & 0x01);
                        }
                    }

                    // 输出解析后的线圈状态到日志
                    Log("Read Coil:" + string.Join("-", result));

                    if (Transitions.TryGetValue(transitionId, out ushort startAddr))
                    {
                        if (startAddr == READ_COIL_ADDR1)
                            Buffer.BlockCopy(result, 0, CoilsData, 0, result.Length);

                        if (startAddr == READ_COIL_ADDR2)
                            Buffer.BlockCopy(result, 0, CoilsData, A_PLC_COIL_COUNT, result.Length);

                        OnReadCoil?.Invoke(DevID, startAddr, CoilsData);
                    }
                }
                    break;
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
                case PDUCode.WRITE_SINGLE_COIL: //写单个线圈响应
                case PDUCode.WRITE_MULTIPLE_COIL: //写多个线圈响应
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
                    Log($"Write Coil:" + string.Join("-", result));

                    OnWriteCoil?.Invoke(DevID, result);
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
                    var error = buffer.ReadByte(); //错误命令码
                    Debug.LogError($"Modbus Error: {error} {ErrorCode[error]}");
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
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
        // =================================================================================
        // 公共属性和事件
        // =================================================================================
        [Header("轮询设置")]
        public PollingMode CurrentPollingMode = PollingMode.TimeDriven;
        
        public int DevID = 1;
        public string DevName = "PLC#1";

        /// <summary>
        /// 是否自动读取线圈 (旧模式或作为顺序模式的开关)
        /// </summary>
        public bool AutoReadCoil;

        /// <summary>
        /// 自动读取线圈的频率
        /// </summary>
        public int AutoReadCoilFrequency = 1;

        /// <summary>
        /// 是否自动读取寄存器 (旧模式或作为顺序模式的开关)
        /// </summary>
        public bool AutoReadRegister;

        /// <summary>
        /// 自动读取寄存器的频率
        /// </summary>
        public int AutoReadRegisterFrequency = 1;

        /// <summary>
        /// 字节发送频率
        /// </summary>
        public float SendByteInterval = 0.1f;
        
        /// <summary>
        /// 顺序轮询模式下，等待响应的最大超时时间（秒）。超过此时间将发送下一个请求
        /// </summary>
        public float PollingTimeout = 3f;

        public UnityEvent<int, int, byte[]> OnReadCoil;
        public UnityEvent<int, ushort[]> OnWriteCoil;
        public UnityEvent<int, int, ushort[]> OnReadRegister;
        public UnityEvent<int, ushort[]> OnWriteRegister;

        // 默认寄存器起始地址 (D3000)
        public const ushort REGISTER_ADDR = 0x0BB8;
        // ... (其他常量保持不变) ...
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
        private Queue<byte[]> SendQueue;
        public byte[] CoilsData;
        private float sendByteTime;
        private ModbusHeader Header;

        public bool isWaitingForResponse { get; set; }
        private IModbusPollingProvider currentProvider;

        // =================================================================================
        // 生命周期方法
        // =================================================================================

        protected override void OnConnectMessage(int c)
        {
            Header = ((ModbusTCPChannel)Service.GetChannel()).Header;
            Transitions = new Dictionary<int, ushort>();
            SendQueue = new Queue<byte[]>();
            Debug.Log(DevName + "连接成功");

            StopAllCoroutines();

            // 根据选定的模式实例化并启动 Provider
            switch (CurrentPollingMode)
            {
                case PollingMode.TimeDriven:
                    currentProvider = new ModbusTimePollingProvider(this);
                    StartCoroutine(currentProvider.StartPolling());
                    break;
                case PollingMode.Sequenced:
                    currentProvider = new ModbusSequencedPollingProvider(this);
                    StartCoroutine(currentProvider.StartPolling());
                    break;
                case PollingMode.None:
                    currentProvider = null;
                    Debug.Log("未启用自动轮询模式。");
                    break;
            }
        }

        public void SendEnqueue(ushort startAdd, byte[] data)
        {
            if (IsConnecting)
            {
                var header = Header.GetData((ushort)data.Length);
                var fullData = new byte[header.Length + data.Length];
                Buffer.BlockCopy(header, 0, fullData, 0, header.Length);
                Buffer.BlockCopy(data, 0, fullData, header.Length, data.Length);
                SendQueue.Enqueue(fullData);
                Transitions[Header.transactionId] = startAdd;
                
                // 仅在 Sequenced 模式下，且发送的是读取请求时设置等待标志
                if (CurrentPollingMode == PollingMode.Sequenced && 
                    (data[1] == PDUCode.READ_COIL_STATUS || data[1] == PDUCode.READ_HOLDING_REGISTER))
                {
                    isWaitingForResponse = true;
                }
            }
            else
            {
                Debug.LogWarning(DevName + " 未连接");
            }
        }

        protected override void Update()
        {
            base.Update();
            if (IsConnecting)
            {
                sendByteTime += Time.deltaTime;
                if (sendByteTime > SendByteInterval)
                {
                    if (SendQueue?.Count > 0)
                    {
                        var data = SendQueue.Dequeue();
                        Send(data);
                    }
                    sendByteTime = 0; 
                }
            }
        }

        /// <summary>
        /// 透传RTU寄存器
        /// </summary>
        public void RequestRTU(byte devAddr, ushort startAddr, ushort length)
        {
            var rtuBytes = GetRTUCmd(devAddr, startAddr, length);
            WriteRTURegisters(WRITE_RTU_ADDR, rtuBytes);
        }

        /// <summary>
        /// 读取RTU数据
        /// </summary>
        public void ReadRTU(ushort length)
        {
            ReadMultipleRegisters(READE_RTU_ADDR, length);
        }

        //设备地址
        private byte DEV_ADD = 0x01;

        /// <summary>
        /// 获取RTU透传读取命令
        /// </summary>
        byte[] GetRTUCmd(byte devAddr, ushort startAddr, ushort length)
        {
            byte[] bytes = new byte[9];

            // 写入数据长度
            bytes[0] = 8;
            // 写入协议头：设备ID
            bytes[1] = devAddr;
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

                // 注意：RTU 透传并非常规 Modbus 读写，不设置 isWaitingForResponse
                SendEnqueue(startAddr, bytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"写入RTU寄存器时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 写多个寄存器的便捷方法，使用默认起始地址
        /// </summary>
        public void WriteMultipleRegisters(ushort[] array)
        {
            // 使用默认寄存器地址
            WriteMultipleRegisters(REGISTER_ADDR, array);
        }

        /// <summary>
        /// 写多个寄存器的核心方法
        /// </summary>
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

                // 写入操作不影响轮询队列，不设置 isWaitingForResponse
                SendEnqueue(startAddr, bytes);
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

                SendEnqueue(startAddr, bytes);
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

                SendEnqueue(startAddr, bytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"写入寄存器时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 读取多个寄存器的便捷方法，使用默认起始地址
        /// </summary>
        public void ReadMultipleRegisters(ushort length)
        {
            // 使用默认寄存器地址
            ReadMultipleRegisters(REGISTER_ADDR, length);
        }

        /// <summary>
        /// 读取多个寄存器的核心方法
        /// </summary>
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

                SendEnqueue(startAddr, bytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"读取寄存器时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 写入一个或者多个线圈数据的便捷方法，使用默认起始地址
        /// </summary>
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

                SendEnqueue(startAddr, bytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"写入多个线圈时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 读取一个或者多个线圈数据的便捷方法，使用默认起始地址
        /// </summary>
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

                SendEnqueue(startAddr, bytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"读取多个线圈时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理接收到的消息数据
        /// </summary>
        protected override void OnMessageMessage(byte[] bytes)
        {
            ByteBuffer buffer = new ByteBuffer(bytes);

            var transitionId = buffer.ReadShort();
            var unit = buffer.ReadByte();
            var cmd = buffer.ReadByte();

            switch (cmd)
            {
                case PDUCode.READ_COIL_STATUS:
                case PDUCode.READ_INPUT_STATUS:
                {
                    var count = buffer.ReadByte();
                    var result = new byte[count * 8];
                    for (int i = 0; i < count; i++)
                    {
                        var bit = buffer.ReadByte();
                        for (int j = 0; j < 8; j++)
                        {
                            result[i * 8 + j] = (byte)((bit >> j) & 0x01);
                        }
                    }

                    if (Transitions.TryGetValue(transitionId, out ushort startAddr))
                    {
                        // 处理线圈数据
                        if (startAddr == READ_COIL_ADDR1)
                            Buffer.BlockCopy(result, 0, CoilsData, 0, result.Length);

                        if (startAddr == READ_COIL_ADDR2)
                            Buffer.BlockCopy(result, 0, CoilsData, A_PLC_COIL_COUNT, result.Length);

                        Log("Read Coil:" + string.Join("-", CoilsData));
                        OnReadCoil?.Invoke(DevID, startAddr, CoilsData);
                    }

                    // 【新逻辑】读取响应成功，解除 Sequenced 模式的等待
                    if (CurrentPollingMode == PollingMode.Sequenced)
                    {
                        isWaitingForResponse = false;
                    }

                    break;
                }
                case PDUCode.READ_INPUT_REGISTER:
                case PDUCode.READ_HOLDING_REGISTER:
                {
                    var count = buffer.ReadByte();
                    var bits = buffer.ReadBytes();
                    var result = new ushort[count / 2];

                    for (int i = 0; i < result.Length; i++)
                    {
                        result[i] = (ushort)IPAddress.NetworkToHostOrder((short)BitConverter.ToUInt16(bits, i * 2));
                    }

                    Log("Read Register:" + string.Join("-", result));
                    if (Transitions.TryGetValue(transitionId, out ushort startAddr))
                    {
                        OnReadRegister?.Invoke(DevID, startAddr, result);
                    }

                    // 【新逻辑】读取响应成功，解除 Sequenced 模式的等待
                    if (CurrentPollingMode == PollingMode.Sequenced)
                    {
                        isWaitingForResponse = false;
                    }

                    break;
                }
                case PDUCode.WRITE_SINGLE_COIL:
                case PDUCode.WRITE_MULTIPLE_COIL:
                case PDUCode.WRITE_SINGLE_REGISTER:
                case PDUCode.WRITE_MULTIPLE_REGISTER:
                {
                    // 写入操作响应处理...
                    var bits = buffer.ReadBytes();
                    var result = new ushort[bits.Length / 2];

                    for (int i = 0; i < result.Length; i++)
                    {
                        result[i] = (ushort)IPAddress.NetworkToHostOrder((short)BitConverter.ToUInt16(bits, i * 2));
                    }

                    if (cmd == PDUCode.WRITE_SINGLE_COIL || cmd == PDUCode.WRITE_MULTIPLE_COIL)
                    {
                        Log($"Write Coil:" + string.Join("-", result));
                        OnWriteCoil?.Invoke(DevID, result);
                    }
                    else
                    {
                        Log($"Write Register:" + string.Join("-", result));
                        OnWriteRegister?.Invoke(DevID, result);
                    }

                    // 【注意】写入响应不解除 isWaitingForResponse，保持等待读取响应的状态。
                    break;
                }
                default:
                {
                    // 错误码也视为响应，解除等待状态
                    var error = buffer.ReadByte();
                    Debug.LogWarning($"{gameObject.name} : Error: {error} {ErrorCode[error]}");

                    // 【新逻辑】错误响应，解除 Sequenced 模式的等待
                    if (CurrentPollingMode == PollingMode.Sequenced)
                    {
                        isWaitingForResponse = false;
                    }

                    break;
                }
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
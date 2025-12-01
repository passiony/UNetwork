using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UNetwork
{
    // =================================================================================
    // 2. ModbusSequencedPollingProvider (响应驱动/顺序发送策略)
    // =================================================================================

    /// <summary>
    /// Modbus 顺序轮询策略：发送一个请求，等待响应或超时后，再发送下一个。
    /// </summary>
    public class ModbusSequencedPollingProvider : IModbusPollingProvider
    {
        private ModbusTCPComponent component;
        private Queue<ModbusReadRequest> requestQueue = new Queue<ModbusReadRequest>();

        public ModbusSequencedPollingProvider(ModbusTCPComponent owner)
        {
            component = owner;
        }

        /// <summary>
        /// 初始化并填充自动读取请求队列。
        /// </summary>
        private void InitRequests()
        {
            // 清空现有队列，准备重新填充
            requestQueue.Clear();

            // 1. 自动读取线圈请求
            if (component.AutoReadCoil)
            {
                ushort coilCount = component.READ_COIL_COUNT;
                if (coilCount <= ModbusTCPComponent.A_PLC_COIL_COUNT)
                {
                    requestQueue.Enqueue(new ModbusReadRequest(ModbusReadRequest.RequestType.Coil,
                        ModbusTCPComponent.READ_COIL_ADDR1, coilCount));
                }
                else
                {
                    // 请求 1: READ_COIL_ADDR1 (最大数量)
                    requestQueue.Enqueue(new ModbusReadRequest(ModbusReadRequest.RequestType.Coil,
                        ModbusTCPComponent.READ_COIL_ADDR1, ModbusTCPComponent.A_PLC_COIL_COUNT));
                    // 请求 2: READ_COIL_ADDR2 (剩余数量)
                    requestQueue.Enqueue(new ModbusReadRequest(ModbusReadRequest.RequestType.Coil,
                        ModbusTCPComponent.READ_COIL_ADDR2, (ushort)(coilCount - ModbusTCPComponent.A_PLC_COIL_COUNT)));
                }
            }

            // 2. 自动读取寄存器请求
            if (component.AutoReadRegister)
            {
                if (component.CustomRegister)
                {
                    // 使用默认地址和数量
                    requestQueue.Enqueue(new ModbusReadRequest(ModbusReadRequest.RequestType.Register,
                        ModbusTCPComponent.REGISTER_ADDR, component.READ_REGISTER_COUNT));
                }
                else
                {
                    // 使用自定义地址和长度
                    requestQueue.Enqueue(new ModbusReadRequest(ModbusReadRequest.RequestType.Register,
                        component.CustomStartAddr, component.CustomLength));
                }
            }
        }

        /// <summary>
        /// 启动轮询协程
        /// </summary>
        public IEnumerator StartPolling()
        {
            // 1. 填充初始请求队列
            InitRequests();

            // 2. 轮询循环
            while (true)
            {
                // 尝试获取下一个待发送的请求
                if (requestQueue.Count > 0)
                {
                    ModbusReadRequest request = requestQueue.Dequeue();

                    // 2a. 发送请求
                    switch (request.Type)
                    {
                        case ModbusReadRequest.RequestType.Coil:
                            component.ReadMultipleCoil(request.StartAddress, request.Length);
                            break;
                        case ModbusReadRequest.RequestType.Register:
                            component.ReadMultipleRegisters(request.StartAddress, request.Length);
                            break;
                    }

                    // 2b. 等待响应或超时
                    float startTime = Time.time;
                    yield return new WaitUntil(() => !component.isWaitingForResponse || (Time.time - startTime) > component.PollingTimeout);

                    // 2c. 检查是否为超时结束等待
                    if (component.isWaitingForResponse)
                    {
                        Debug.LogWarning($"Modbus 顺序轮询请求超时（>{component.PollingTimeout}s），强制解除等待并发送下一个请求。");
                        component.isWaitingForResponse = false; // 强制解除等待
                    }
                }
                else
                {
                    // 队列为空，等待一小段时间后重新检查
                    yield return new WaitForSeconds(0.1f);
                    // 重新填充队列以实现循环轮询
                    InitRequests();
                }
            }
        }
    }
}
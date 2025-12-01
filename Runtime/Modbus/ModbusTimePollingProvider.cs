using System.Collections;
using UnityEngine;

namespace UNetwork
{
    // =================================================================================
    // 1. ModbusTimePollingProvider (定时发送策略)
    // =================================================================================

    /// <summary>
    /// Modbus 定时轮询策略：按设定的频率发送，不等待响应。
    /// </summary>
    public class ModbusTimePollingProvider : IModbusPollingProvider
    {
        private ModbusTCPComponent component;

        public ModbusTimePollingProvider(ModbusTCPComponent owner)
        {
            component = owner;
        }

        public IEnumerator StartPolling()
        {
            // 启动旧的线圈定时协程
            if (component.AutoReadCoil)
                component.StartCoroutine(CoReadCoil(component.READ_COIL_COUNT));

            // 启动旧的寄存器定时协程
            if (component.AutoReadRegister)
                component.StartCoroutine(CoReadRegisters());

            // 父协程返回，让两个子协程独立运行
            yield break;
        }

        IEnumerator CoReadCoil(ushort coilCount)
        {
            component.CoilsData = new byte[coilCount];
            while (true)
            {
                // 使用原来的频率等待
                yield return new WaitForSeconds(1f / component.AutoReadCoilFrequency);

                if (!component.IsConnecting) continue;

                // 遵循原有的线圈拆分逻辑
                if (coilCount <= ModbusTCPComponent.A_PLC_COIL_COUNT)
                {
                    component.ReadMultipleCoil(ModbusTCPComponent.READ_COIL_ADDR1, coilCount);
                }
                else
                {
                    component.ReadMultipleCoil(ModbusTCPComponent.READ_COIL_ADDR1, ModbusTCPComponent.A_PLC_COIL_COUNT);
                    component.ReadMultipleCoil(ModbusTCPComponent.READ_COIL_ADDR2,
                        (ushort)(coilCount - ModbusTCPComponent.A_PLC_COIL_COUNT));
                }
            }
        }

        IEnumerator CoReadRegisters()
        {
            while (true)
            {
                // 使用原来的频率等待
                yield return new WaitForSeconds(1f / component.AutoReadRegisterFrequency);

                if (!component.IsConnecting) continue;

                if (component.CustomRegister)
                {
                    component.ReadMultipleRegisters(ModbusTCPComponent.REGISTER_ADDR, component.READ_REGISTER_COUNT);
                }
                else
                {
                    component.ReadMultipleRegisters(component.CustomStartAddr, component.CustomLength);
                }
            }
        }
    }
}
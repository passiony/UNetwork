using System;
using System.Collections;
using UNetwork;
using UnityEngine;

public class ModbusManager : MonoBehaviour
{
    private ModbusComponent m_Modbus;

    void Start()
    {
        m_Modbus = gameObject.GetComponent<ModbusComponent>();
        m_Modbus.AutoReadRegister = true;
        m_Modbus.OnReadRegister.AddListener(OnReadRegister);
        m_Modbus.Connect();
    }

    private void OnReadRegister(ushort[] registers)
    {
        Debug.LogWarning(string.Join("-", registers));
    }
    
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            RequestWenDu();
        }

        if (Input.GetKeyDown(KeyCode.B))
        {
            ReadWenDu();
        }

        // if (Input.GetKeyDown(KeyCode.A))
        // {
        //     m_Modbus.ReadMultipleCoil(16);
        // }
        //
        // if (Input.GetKeyDown(KeyCode.B))
        // {
        //     m_Modbus.ReadMultipleRegisters(8);
        // }
        //
        // if (Input.GetKeyDown(KeyCode.C))
        // {
        //     m_Modbus.WriteMultipleRegisters(new ushort[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        // }
        //
        // if (Input.GetKeyDown(KeyCode.D))
        // {
        //     m_Modbus.WriteMultipleCoil(new ushort[] { 1, 1, 1, 1, 1, 1, 1, 1 });
        // }
    }

    //读取温度
    public void RequestWenDu()
    {
        m_Modbus.RequestRTU(0x0009, 2);
    }

    //请求湿度
    public void RequestShiDu()
    {
        m_Modbus.RequestRTU(0x000A, 2);
    }

    //查询湿度
    public void ReadWenDu()
    {
        m_Modbus.ReadRTU(9);
    }

    //查询湿度
    public void ReadShiDu()
    {
        m_Modbus.ReadRTU(9);
    }
}
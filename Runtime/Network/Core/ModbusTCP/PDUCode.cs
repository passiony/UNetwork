namespace UNetwork
{
    public class PDUCode
    {
        /// <summary>
        /// 单元标识符
        /// </summary>
        public const byte UnitID_READ_COIL = 0x01;
        public const byte UnitID_Write_COIL = 0x01;
        public const byte UnitID_READ_REGISTER = 0x01;
        public const byte UnitID_WRITE_REGISTER = 0x01;
        
        /// <summary>
        /// 读线圈状态
        /// </summary>
        public const byte READ_COIL_STATUS = 0x01;
        /// <summary>
        /// 读离散输入状态
        /// </summary>
        public const byte READ_INPUT_STATUS = 0x02;
        /// <summary>
        /// 读保持寄存器
        /// </summary>
        public const byte READ_HOLDING_REGISTER = 0x03;
        /// <summary>
        /// 读输入寄存器
        /// </summary>
        public const byte READ_INPUT_REGISTER = 0x04;
        /// <summary>
        /// 写单个线圈状态
        /// </summary>
        public const byte WRITE_SINGLE_COIL = 0x05;
        /// <summary>
        /// 写单个保持寄存器
        /// </summary>
        public const byte WRITE_SINGLE_REGISTER = 0x06;
        /// <summary>
        /// 写多个线圈
        /// </summary>
        public const byte WRITE_MULTIPLE_COIL = 0x0F;
        /// <summary>
        /// 写多个保持寄存器
        /// </summary>
        public const byte WRITE_MULTIPLE_REGISTER = 0x10;
    }
}
using System;
using System.Net;
using System.Text;

namespace UNetwork
{
	public static class ByteHelper
	{
		public static string ToHex(this byte b)
		{
			return b.ToString("X2");
		}

		public static string ToHex(this byte[] bytes)
		{
			StringBuilder stringBuilder = new StringBuilder();
			foreach (byte b in bytes)
			{
				stringBuilder.Append(b.ToString("X2"));
			}
			return stringBuilder.ToString();
		}

		public static string ToHex(this byte[] bytes, string format)
		{
			StringBuilder stringBuilder = new StringBuilder();
			foreach (byte b in bytes)
			{
				stringBuilder.Append(b.ToString(format));
			}
			return stringBuilder.ToString();
		}

		public static string ToHex(this byte[] bytes, int offset, int count)
		{
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = offset; i < offset + count; ++i)
			{
				stringBuilder.Append(bytes[i].ToString("X2"));
			}
			return stringBuilder.ToString();
		}

		public static string ToStr(this byte[] bytes)
		{
			return Encoding.Default.GetString(bytes);
		}

		public static string ToStr(this byte[] bytes, int index, int count)
		{
			return Encoding.Default.GetString(bytes, index, count);
		}

		public static string Utf8ToStr(this byte[] bytes)
		{
			return Encoding.UTF8.GetString(bytes);
		}

		public static string Utf8ToStr(this byte[] bytes, int index, int count)
		{
			return Encoding.UTF8.GetString(bytes, index, count);
		}

		public static void WriteTo(this byte[] bytes, int offset, uint num)
		{
			bytes[offset] = (byte)(num & 0xff);
			bytes[offset + 1] = (byte)((num & 0xff00) >> 8);
			bytes[offset + 2] = (byte)((num & 0xff0000) >> 16);
			bytes[offset + 3] = (byte)((num & 0xff000000) >> 24);
		}
		
		public static void WriteTo(this byte[] bytes, int offset, int num)
		{
			bytes[offset] = (byte)(num & 0xff);
			bytes[offset + 1] = (byte)((num & 0xff00) >> 8);
			bytes[offset + 2] = (byte)((num & 0xff0000) >> 16);
			bytes[offset + 3] = (byte)((num & 0xff000000) >> 24);
		}
		
		public static void WriteTo(this byte[] bytes, int offset, byte num)
		{
			bytes[offset] = num;
		}
		
		public static void WriteTo(this byte[] bytes, int offset, short num)
		{
			bytes[offset] = (byte)(num & 0xff);
			bytes[offset + 1] = (byte)((num & 0xff00) >> 8);
		}
		
		public static void WriteTo(this byte[] bytes, int offset, ushort num)
		{
			bytes[offset] = (byte)(num & 0xff);
			bytes[offset + 1] = (byte)((num & 0xff00) >> 8);
		}
		
		/// <summary>
        /// 将 ushort 转换为字节数组，大端
        /// </summary>
        public static byte[] ToBigBytes(this ushort value, bool isBigEndian)
        {
            // 若需大端序且当前系统为小端序，则转换字节序
            byte[] bytes =
                BitConverter.GetBytes(isBigEndian ? (ushort)IPAddress.HostToNetworkOrder((short)value) : value);
            return bytes;
        }

        /// <summary>
        /// 将 int 转换为字节数组，大端
        /// </summary>
        /// <param name="value"></param>
        /// <param name="isBigEndian"></param>
        /// <returns></returns>
        public static byte[] ToBigBytes(this int value, bool isBigEndian)
        {
            // 若需大端序且当前系统为小端序，则转换字节序
            byte[] bytes = BitConverter.GetBytes(isBigEndian ? (uint)IPAddress.HostToNetworkOrder((int)value) : value);
            return bytes;
        }

        // 大端字节数组 → 小端Ushort
        public static ushort ReadBigUshort(byte[] bigEndianBytes)
        {
            // 直接转换为小端序（若输入为大端序）
            return (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(bigEndianBytes, 0));
        }

        // 小端Ushort → 大端字节数组
        public static byte[] ToBigEndianBytes(ushort value)
        {
            // 强制转换为大端序
            return BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)value));
        }

        /// <summary>
        /// int[] 按位，转为byte[]。
        /// 如[0,0,0,0,0,1,0,0] => [4]
        /// </summary>
        public static byte[] IntArrayToByteArray(ushort[] array)
        {
            int byteLength = (array.Length + 7) / 8; // 计算需要的字节数
            int[] intArray = new int[byteLength];
            byte[] byteArray = new byte[byteLength];
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] != 0)
                {
                    int byteIndex = i / 8; // 当前位所属的字节索引
                    int bitPos = i % 8; // 修改：低位在前（左侧补零时从右向左填充）
                    intArray[byteIndex] |= (1 << bitPos); // 设置对应位为1
                }
            }

            for (int i = 0; i < intArray.Length; i++)
            {
                byteArray[i] = (byte)intArray[i];
            }

            return byteArray;
        }
        
        /// <summary>
        /// CRC16
        /// </summary>
        /// <param name="data">数据字节数组</param>
        /// <param name="length">计算数据长度</param>
        /// <returns>crc16值</returns>
        public static ushort CRC16(byte[] data, int length)
        {
	        ushort crc = 0xFFFF; // 初始值
	        for (int i = 0; i < length; i++)
	        {
		        crc ^= data[i]; // 逐字节异或
		        for (int j = 0; j < 8; j++)
		        {
			        if ((crc & 0x0001) != 0) // 检查最低位是否为1
			        {
				        crc = (ushort)((crc >> 1) ^ 0xA001); // 右移并异或多项式
			        }
			        else
			        {
				        crc >>= 1; // 直接右移
			        }
		        }
	        }

	        return crc;
        }
	}
}
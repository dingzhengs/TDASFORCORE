using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TDASDataParser.StdfTypes
{
    public class BaseEntity
    {
        [JsonIgnore]
        //key:stdfid,value: startpartid
        protected static Dictionary<int, int> STARTPARTID { get; set; } = new Dictionary<int, int>();

        [JsonIgnore]
        protected static Dictionary<int, Dictionary<int, int>> SITENUM2PARTID { get; set; } = new Dictionary<int, Dictionary<int, int>>();
        [JsonIgnore]
        public static Dictionary<int, double> PTRIndex { get; set; } = new Dictionary<int, double>();
        [JsonIgnore]
        public static object PartIdLock = new object();
        public int StdfId { get; set; }

        [JsonIgnore]
        public string ENTITYJSON;

        #region 静态解析
        [JsonIgnore]
        public int dataIndex = 0;

        /// <summary>
        /// 解析整型结构
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="length">需解析长度</param>
        /// <returns></returns>
        protected int GetInteger(byte[] data, int length)
        {
            if (dataIndex >= data.Length)
            {
                return 0;
            }
            int value = 0;
            switch (length)
            {
                case 4:
                    value = BitConverter.ToInt32(data, dataIndex);
                    break;
                case 2:
                    value = BitConverter.ToInt16(data, dataIndex);
                    break;
                case 1:
                    value = data[dataIndex];
                    break;

            }

            dataIndex += length;
            return double.IsInfinity(value) || double.IsNaN(value) ? 0 : value; ;
        }

        /// <summary>
        /// 解析浮点型结构
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="length">需解析长度</param>
        /// <returns></returns>
        protected double GetFloat(byte[] data, int length)
        {
            if (dataIndex >= data.Length)
            {
                return 0;
            }

            double value = 0;
            switch (length)
            {
                case 8:
                    value = BitConverter.ToDouble(data, dataIndex);
                    break;
                case 4:
                    value = BitConverter.ToSingle(data, dataIndex);
                    break;
            }

            dataIndex += length;
            return double.IsInfinity(value) || double.IsNaN(value) ? 0 : value;
        }

        /// <summary>
        /// 解析定长bit编码结构
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="length">需解析长度</param>
        /// <returns></returns>
        protected string GetFixedBitEncoded(byte[] data, int length)
        {
            if (dataIndex >= data.Length)
            {
                return "";
            }
            byte[] array = data.Skip(dataIndex).Take(length).ToArray();

            if (array[0] > 255)
            {
                return "";
            }

            string value = "";


            for (int i = 0; i < 8; i++)
            {
                value += (byte)(array[0] & 1);
                array[0] = (byte)(array[0] >> 1);
            }

            dataIndex += length;
            value = value.PadRight(8, '0');
            return value;
        }

        /// <summary>
        /// 解析不定长byte编码结构
        /// </summary>
        /// <param name="data">数据</param>
        /// <returns></returns>
        protected string GetNotFixedByteEncoded(byte[] data)
        {
            if (dataIndex >= data.Length)
            {
                return "";
            }
            byte length = data[dataIndex];
            dataIndex++;

            if (length == 0)
            {
                return "";
            }

            byte[] array = data.Skip(dataIndex).Take(length).ToArray();
            string value = "";
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] != 0)
                {
                    value = array[i].ToString();
                    break;
                }
            }
            dataIndex += length;
            return value;
        }

        /// <summary>
        /// 解析不定长byte编码结构.两位长度
        /// </summary>
        /// <param name="data">数据</param>
        /// <returns></returns>
        protected string GetNotFixedLongByteEncoded(byte[] data)
        {
            if (dataIndex >= data.Length)
            {
                return "";
            }
            int length = BitConverter.ToInt16(data, dataIndex);
            dataIndex += 2;

            if (length == 0)
            {
                return "";
            }

            byte[] array = data.Skip(dataIndex).Take(length).ToArray();
            string value = "";
            for (int i = 0; i < array.Length; i++)
            {
                value += array[i];
            }
            dataIndex += length;
            return value;
        }

        /// <summary>
        /// 解析定长字符串编码结构
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="length">需解析长度</param>
        /// <returns></returns>
        protected string GetFixedString(byte[] data, int length)
        {
            if (dataIndex >= data.Length)
            {
                return "";
            }
            byte[] array = data.Skip(dataIndex).Take(length).ToArray();
            dataIndex += length;
            return Encoding.UTF8.GetString(array);
        }

        /// <summary>
        /// 解析不定长字符串编码结构
        /// </summary>
        /// <param name="data">数据</param>
        /// <returns></returns>
        protected string GetNotFixedString(byte[] data)
        {
            if (dataIndex >= data.Length)
            {
                return "";
            }

            byte length = data[dataIndex];
            dataIndex++;

            if (length == 0)
            {
                return "";
            }

            byte[] array = data.Skip(dataIndex).Take(length).ToArray();
            dataIndex += length;
            return Encoding.UTF8.GetString(array);
        }

        /// <summary>
        /// 获取整型数组
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="itemCount">数组数量</param>
        /// <param name="length">每个元素的字节数</param>
        /// <returns></returns>
        protected string GetIntegerArray(byte[] data, int itemCount, int length)
        {
            int[] value = new int[itemCount];

            for (int i = 0; i < itemCount; i++)
            {
                value[i] = GetInteger(data, length);
            }

            return string.Join(Environment.NewLine, value);
        }

        /// <summary>
        /// 获取浮点型数组
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="itemCount">数组数量</param>
        /// <param name="length">每个元素的字节数</param>
        /// <returns></returns>
        protected string GetFloatArray(byte[] data, int itemCount, int length)
        {
            double[] value = new double[itemCount];

            for (int i = 0; i < itemCount; i++)
            {
                value[i] = GetFloat(data, length);
            }

            return string.Join(Environment.NewLine, value);
        }

        /// <summary>
        /// 解析不定长bit编码结构
        /// </summary>
        /// <param name="data">数据</param>
        /// <returns></returns>
        protected string GetNotFixedBitEncoded(byte[] data)
        {
            if (dataIndex >= data.Length)
            {
                return "";
            }
            int bitLength = BitConverter.ToInt16(data, dataIndex);
            dataIndex += 2;

            if (bitLength == 0)
            {
                return "";
            }

            int dataLength = Convert.ToInt32(Math.Ceiling(bitLength / 8.0));

            if (dataLength > data.Length - dataIndex)
            {
                return "";
            }

            string value = Convert.ToBase64String(data.Skip(dataIndex).Take(dataLength).ToArray());

            dataIndex += dataLength;
            return value;
        }

        /// <summary>
        /// 获取非定长字符串数组
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="itemCount">数组数量</param>
        /// <returns></returns>
        protected string GetNotFixedStringArray(byte[] data, int itemCount)
        {
            string[] value = new string[itemCount];

            for (int i = 0; i < itemCount; i++)
            {
                value[i] = GetNotFixedString(data);
            }

            return string.Join(Environment.NewLine, value);
        }

        /// <summary>
        /// 获取按照比特位分割的数据数组
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="itemCount">长度</param>
        /// <returns></returns>
        protected string GetBitStringArray(byte[] data, int itemCount)
        {
            if (dataIndex >= data.Length)
            {
                return "";
            }

            int dataLength = Convert.ToInt32(Math.Ceiling(itemCount / 2.0));

            if (dataLength == 0)
            {
                return "";
            }

            string value = Convert.ToBase64String(data.Skip(dataIndex).Take(dataLength).ToArray());
            dataIndex += dataLength;
            return value;
        }

        //获取高四位
        protected static int getHeight4(byte data)
        {
            int height;
            height = ((data & 0xf0) >> 4);
            return height;
        }

        //获取低四位
        protected static int getLow4(byte data)
        {
            int low;
            low = (data & 0x0f);
            return low;
        }
        #endregion

        public virtual string ToJson()
        {
            return JToken.FromObject(this).ToString();
        }

        public virtual void LoadData(byte[] data, byte[] args)
        {

        }
        public virtual int CalPartId(int stdfid)
        {
            return 0;
        }


    }
}

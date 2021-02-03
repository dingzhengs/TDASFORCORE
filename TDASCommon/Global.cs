using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace TDASCommon
{
    public static class Global
    {

        /// <summary>
        /// 终端信号,此中断信号会拦截新数据的解析 
        /// </summary>
        public static bool StopSignal { get; set; } = false;

        public static bool AutoClose { get; set; } = true;

        public static object StdfidLock = new object();

        public static RedisService redis = new RedisService();



        static bool isInit = false;
        static readonly object initLock = new object();
        public static Dictionary<string, PropertyInfo[]> DicProps { get; set; } = new Dictionary<string, PropertyInfo[]>();
        public static Dictionary<string, string> DicSQL { get; set; } = new Dictionary<string, string>();

        public static void TypePropsInit()
        {
            lock (initLock)
            {
                if (isInit)
                {
                    return;
                }
                else
                {
                    //byte[] assemblyBuf = File.ReadAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TDASDataParser.dll"));

                    //Assembly asse = Assembly.Load(assemblyBuf);
                    Assembly asse = Assembly.Load("TDASDataParser");
                    Type[] types = asse.GetTypes().Where(p => p.FullName.StartsWith("TDASDataParser.StdfTypes") && !p.Name.Contains("PrivateImplementationDetails")).ToArray();
                    for (int i = 0; i < types.Length; i++)
                    {
                        string insert = $"INSERT INTO {types[i].Name.ToUpper()}(";
                        string value = "VALUES(";
                        foreach (var item in types[i].GetProperties())
                        {
                            insert += item.Name.ToUpper() + ",";
                            value += $":{item.Name.ToUpper()},";
                        }

                        DicProps.Add(types[i].Name, types[i].GetProperties());

                        DicSQL.Add(types[i].Name.ToUpper(), insert.Substring(0, insert.Length - 1) + ")" + value.Substring(0, value.Length - 1) + ")");
                    }

                    isInit = true;

                }
            }
        }

    }
}

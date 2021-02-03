using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TDASCommon
{
    public class Config
    {
        private static JToken jsonToken;
        static Config()
        {
            string json = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            string jsonvar = File.ReadAllText(json, Encoding.UTF8);

            jsonToken = JToken.Parse(jsonvar);

            Address = Get<string>("Tcp:Address");

            Port = Get<int>("Tcp:Port");

            Keepalive = Get<int>("Tcp:Keepalive");

            ReceiveBuffer = Get<int>("Tcp:ReceiveBuffer");

            StaticFileFolder = Get<string>("Parse:StaticFileFolder");

            ParseType = Get<string>("Parse:ParseType").Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            SubmitType = Get<string>("Parse:SubmitType").Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            SubmitLimit = Get<int>("Parse:SubmitLimit");

            CsvFileForlder = Get<string>("Parse:CsvFileForlder");
        }

        public static T Get<T>(string key)
        {
            List<string> keys = key.Split(':').ToList();

            JToken result = jsonToken;
            while (keys.Count > 0)
            {
                result = result[keys[0]];
                keys.RemoveAt(0);
            }
            return result.ToObject<T>();
        }

        public static string Address { get; }
        public static int Port { get; }
        public static int Keepalive { get; }
        public static int ReceiveBuffer { get; }
        public static string[] ParseType { get; }
        public static string[] SubmitType { get; }
        public static int SubmitLimit { get; }
        public static string StaticFileFolder { get; }
        public static string CsvFileForlder { get; }
    }
}

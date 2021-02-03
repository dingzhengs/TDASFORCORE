using StackExchange.Redis;
using System.Linq;
using System.Threading;

namespace TDASCommon
{
    public class RedisConfig
    {
        /// <summary>
        /// 服务名称,启用redis集群的时候必须使用
        /// </summary>
        public string ServerName => Config.Get<string>("Redis:ServerName");
        /// <summary>
        /// redis服务器地址
        /// </summary>
        public string[] ServerHosts => Config.Get<string>("Redis:ServerHosts").Split(',');
        /// <summary>
        /// redis密码,在使用集群时,集群redis服务器要配置相同的密码
        /// </summary>
        public string Password => Config.Get<string>("Redis:Password");

        private ConfigurationOptions options;

        public ConfigurationOptions Options
        {
            get
            {
                if (options == null)
                {
                    ConfigurationOptions option = new ConfigurationOptions
                    {
                        ServiceName = this.ServerName,
                        AbortOnConnectFail = true,
                        Password = this.Password,
                        AllowAdmin = true,
                        SyncTimeout=30000
                    };

                    if (this.ServerHosts?.Length > 0)
                    {
                        foreach (var host in this.ServerHosts)
                        {
                            option.EndPoints.Add(host);
                        }
                    }

                    using (var discoverCnn = ConnectionMultiplexer.Connect(option))
                    {
                        Thread.Sleep(2000);
                        string[] endpoints = discoverCnn.GetEndPoints(false).Select(ep => ep.ToString()).ToArray();
                        option.EndPoints.Clear();
                        foreach (var host in endpoints)
                        {
                            option.EndPoints.Add(host);
                        }
                    }

                    this.options = option;
                }
                return this.options;
            }
        }
    }
}

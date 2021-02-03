using StackExchange.Redis;
using System;

namespace TDASCommon
{
    internal class RedisConnection
    {
        internal static RedisConfig Config { get; set; }

        private static readonly object redisConnectlock = new object();

        private static ConnectionMultiplexer instance;

        /// <summary>
        /// 单例获取数据缓存单例
        /// </summary>
        internal static ConnectionMultiplexer Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (redisConnectlock)
                    {
                        if (instance == null || !instance.IsConnected)
                        {
                            if (Config == null)
                            {
                                throw new ArgumentNullException("未初始化RedisConfig实例对象");
                            }
                            instance = GetDataConnection();
                        }
                    }
                }
                return instance;
            }
        }


        private static ConnectionMultiplexer GetDataConnection()
        {
            return ConnectionMultiplexer.Connect(Config.Options);
        }
    }
}

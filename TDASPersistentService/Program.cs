using System;
using System.Threading;
using Topshelf;

namespace TDASPersistentService
{
    class Program
    {
        static void Main(string[] args)
        {
            ThreadPool.SetMinThreads(200, 200);
            HostFactory.Run(x =>
            {
                x.Service<OracleSubmit>();
                x.StartAutomatically();
                x.EnableServiceRecovery(r => r.RestartService(TimeSpan.FromSeconds(10)));
                x.SetServiceName("TDASPersistentService");
                x.SetDescription("用于缓存数据的持久化服务");
                x.SetDisplayName("解析入库服务");
            });
        }
    }
}

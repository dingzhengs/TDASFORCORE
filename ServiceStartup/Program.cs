using System;
using Topshelf;

namespace ServiceStartup
{
    class Program
    {
        static void Main(string[] args)
        {
            RunParse(RunType.Static);
        }

        static void RunParse(RunType currRunType)
        {
            switch (currRunType)
            {
                case RunType.Realtime:
                    HostFactory.Run(x =>
                    {
                        x.Service<MessageReceiverService.Startup>();
                        x.StartAutomatically();
                        x.EnableServiceRecovery(r => r.RestartService(TimeSpan.FromSeconds(10)));
                        x.SetServiceName("MessageReceiverService");
                        x.SetDescription("用于实时接收机台报文");
                        x.SetDisplayName("机台报文对接服务3");
                    });
                    break;
                case RunType.Static:
                    HostFactory.Run(x =>
                    {
                        x.Service<MessageStaticParseService.Startup>();
                        x.StartAutomatically();
                        x.EnableServiceRecovery(r => r.RestartService(TimeSpan.FromSeconds(10)));
                        x.SetServiceName("MessageStaticParseServiceByRule");
                        x.SetDescription("自动测试文件导入服务(带规则)");
                        x.SetDisplayName("自动测试文件导入服务(带规则)");
                    });
                    break;
                case RunType.Single:
                    HostFactory.Run(x =>
                    {
                        x.Service<MessageSingleService.Startup>();
                        x.StartAutomatically();
                        x.EnableServiceRecovery(r => r.RestartService(TimeSpan.FromSeconds(10)));
                        x.SetServiceName("MessageSingleService");
                        x.SetDescription("IC自动解析服务");
                        x.SetDisplayName("IC自动解析服务");
                    });
                    break;
            }
        }
    }

    enum RunType
    {
        // 实时解析
        Realtime,

        // 静态解析
        Static,

        // 独立解析
        Single
    }
}
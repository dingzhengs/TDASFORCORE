using NLog.Fluent;
using System;
using System.IO;
using TDASCommon;
using Topshelf;

namespace MessageStaticParseService
{
    public class Startup : ServiceControl
    {

        StaticWorker sw;
        public bool Start(HostControl hostControl)
        {
            sw = new StaticWorker();
            // 写库 创建文件 写规则
            sw.Start(true, true, true);
            Logs.Trace("启动文件监听");
            return true;
        }

        public bool Stop(HostControl hostControl)
        {
            sw.Stop();
            Logs.Trace("结束文件监听");
            return true;
        }
    }
}

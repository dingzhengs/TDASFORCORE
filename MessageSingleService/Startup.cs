using System;
using TDASCommon;
using Topshelf;

namespace MessageSingleService
{
    public class Startup : ServiceControl
    {

        SingleWorker sw;
        public bool Start(HostControl hostControl)
        {
            sw = new SingleWorker();
            sw.Start();
            Logs.Trace("启动IC解析");
            return true;
        }

        public bool Stop(HostControl hostControl)
        {
            sw.Stop();
            return true;
        }
    }
}
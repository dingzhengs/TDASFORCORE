using System;
using TcpServer;
using TDASCommon;
using Topshelf;

namespace MessageReceiverService
{
    public class Startup : ServiceControl
    {
        TcpHost th;
        public bool Start(HostControl hostControl)
        {
            Logs.Trace("TDAS数据接收服务启动.");
            th = new TcpHost();

            th.Start();

            return true;
        }

        public bool Stop(HostControl hostControl)
        {
            Logs.Trace("TDAS数据接收服务停止.");
            th.Stop();
            return true;
        }
    }
}

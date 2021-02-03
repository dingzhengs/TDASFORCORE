using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Handlers.Timeout;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TDASCommon;

namespace TcpServer
{
    public class TcpHost
    {
        private IChannel channel;
        private MultithreadEventLoopGroup bossGroup = new MultithreadEventLoopGroup(2);
        private MultithreadEventLoopGroup workerGroup = new MultithreadEventLoopGroup(16);

        public void Start()
        {

            //Environment.SetEnvironmentVariable("io.netty.allocator.numDirectArenas", "0");
            //声明一个服务端Bootstrap，每个Netty服务端程序，都由ServerBootstrap控制，
            //通过链式的方式组装需要的参数
            try
            {
                var bootstrap = new ServerBootstrap();
                bootstrap
                    //.Handler(new LoggingHandler("SRV-LSTN")) //在主线程组上设置一个打印日志的处理器
                    .Group(bossGroup, workerGroup) // 设置主和工作线程组
                    .Channel<TcpServerSocketChannel>() // 设置通道模式为TcpSocket
                    .Option(ChannelOption.SoBacklog, 100) // 设置网络IO参数等，这里可以设置很多参数，当然你对网络调优和参数设置非常了解的话，你可以设置，或者就用默认参数吧
                    .Option(ChannelOption.Allocator, PooledByteBufferAllocator.Default)
                    .ChildOption(ChannelOption.Allocator, PooledByteBufferAllocator.Default)
                    .ChildHandler(new ActionChannelInitializer<IChannel>(channel =>
                    {
                        IChannelPipeline pipeline = channel.Pipeline;
                        pipeline.AddLast("heartbeat", new IdleStateHandler(0, 0, Config.Keepalive));
                        pipeline.AddLast("framing-dec", new LengthFieldBasedFrameDecoder(ByteOrder.LittleEndian, Config.ReceiveBuffer, 4, 4, 0, 0, true));
                        pipeline.AddLast("echo", new ServerHandler());
                    }));

                // bootstrap绑定到指定端口的行为 就是服务端启动服务，同样的Serverbootstrap可以bind到多个端口
                channel = bootstrap.BindAsync(IPAddress.Parse(Config.Address), Config.Port).Result;
                Logs.Trace("开始Tcp侦听");
            }
            catch (Exception ex)
            {
                Logs.Error("监听异常", ex);
            }
        }

        public void Stop()
        {
            Global.StopSignal = true;

            Thread.Sleep(5000);
            Task.WhenAll(
               this.channel.CloseAsync(),
               bossGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)),
               workerGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)));
            Logs.Trace("停止Tcp侦听");
        }
    }
}

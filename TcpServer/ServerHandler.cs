using DotNetty.Buffers;
using DotNetty.Common.Utilities;
using DotNetty.Handlers.Timeout;
using DotNetty.Transport.Channels;
using System;
using System.Net;
using TDASCommon;

namespace TcpServer
{
    public class ServerHandler : ChannelHandlerAdapter
    {
        string ip = string.Empty;
        // 数据接收
        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            IByteBuffer data = message as IByteBuffer;

            if (data.HasArray)
            {
                if (string.IsNullOrEmpty(ip))
                {
                    ip = ((IPEndPoint)context.Channel.RemoteAddress).Address.MapToIPv4().ToString();
                }
                ClientManager.Clients[ip]?.Receive(data);
            }

            ReferenceCountUtil.Release(data);
        }

        public override void ChannelReadComplete(IChannelHandlerContext context)
        {
            context.Flush();
        }

        // 客户端接入事件
        public override void ChannelActive(IChannelHandlerContext context)
        {

            if (string.IsNullOrEmpty(ip))
            {
                ip = ((IPEndPoint)context.Channel.RemoteAddress).Address.MapToIPv4().ToString();
            }

            ClientManager client = null;

            if (!ClientManager.Clients.TryGetValue(ip, out client))
            {
                client = new ClientManager { Context = context, IP = ip };

                ClientManager.Clients.AddOrUpdate(ip, client, (key, oldvalue) => oldvalue);
            }
            else
            {
                client.Context = context;
                client.receiving = false;
            }

            Global.redis.Set($"IdleState:{ip}", DateTime.Now, 60);

            Logs.Trace($"[{client.IP}],{context.Channel.Id.AsShortText()}接入");

            try
            {
                StatusUpdater.ClientConnection(client.IP);
            }
            catch (Exception ex)
            {
                Logs.Error("更新客户端状态异常", ex);
            }
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            if (string.IsNullOrEmpty(ip))
            {
                ip = ((IPEndPoint)context.Channel.RemoteAddress).Address.MapToIPv4().ToString();
            }

            if (ClientManager.Clients.Keys.Contains(ip))
            {
                while (context.Channel.Pipeline.Last() != null)
                {
                    context.Channel.Pipeline.RemoveLast();
                }

                ClientManager.Clients[ip].Context.CloseAsync();

                StatusUpdater.ClientDisConnection(ip);

            }
            Logs.Trace($"[{ip}],断开连接");
            Global.redis.Remove($"IdleState:{ip}");
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            var ex = exception as System.Net.Sockets.SocketException;
            if (ex == null)
            {
                Logs.Error($"[{ip}],Inbound异常", exception);
            }
            else
            {
                if (ex.ErrorCode != 10054)
                {
                    Logs.Error($"[{ip}],Tcp通讯异常", ex);
                }
            }

        }

        public override void UserEventTriggered(IChannelHandlerContext context, object evt)
        {
            if (evt is IdleStateEvent)
            {
                var eventState = evt as IdleStateEvent;

                if (eventState != null)
                {
                    if (eventState.State == IdleState.AllIdle)
                    {
                        if (string.IsNullOrEmpty(ip))
                        {
                            ip = ((IPEndPoint)context.Channel.RemoteAddress).Address.MapToIPv4().ToString();
                        }
                        // 心跳超时 关闭客户端对象 
                        if (ClientManager.Clients.Keys.Contains(ip))
                        {
                            ClientManager.Clients[ip].Context.CloseAsync();

                            Logs.Trace($"[{ip}],心跳检测超时,断开连接");

                            StatusUpdater.ClientDie(ip);
                        }
                        else
                        {
                            Logs.Trace($"[{ip}],断开连接.");
                            StatusUpdater.ClientDisConnection(ip);
                        }
                    }
                }
            }
            else
            {
                base.UserEventTriggered(context, evt);
            }
        }
    }
}

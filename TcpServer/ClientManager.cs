using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TDASCommon;
namespace TcpServer
{
    public enum MessageType
    {
        PING = 1,// 来自客户端的心跳消息
        PONG = 2,// 服务器返回给客户端的心跳响应
        CLIENTDATA = 3,// 来自客户端的业务数据
        SERVERRESULT = 4,// 服务器对业务数据的处理结果
        SENDRULE = 5,// 服务器下发role规则
    }
    public class ClientManager : IDisposable
    {
        // 客户端集合 全局
        public static ConcurrentDictionary<string, ClientManager> Clients = new ConcurrentDictionary<string, ClientManager>();

        public IChannelHandlerContext Context { get; set; }

        // 是否正在接收数据
        public bool receiving = false;

        public string IP { get; set; }

        // 最后一次数据接收时间
        DateTime lastReceive = DateTime.MinValue;

        Dictionary<string, IParse> cacheReader = new Dictionary<string, IParse>();

        public void Receive(IByteBuffer data)
        {
            var version = BitConverter.ToInt16(data.Array, data.ArrayOffset);
            var action = (MessageType)BitConverter.ToInt16(data.Array, data.ArrayOffset + 2);
            var length = BitConverter.ToInt32(data.Array, data.ArrayOffset + 4);


            switch (action)
            {
                case MessageType.PING:
                    SendPong(data.Array.Skip(data.ArrayOffset).Take(8).ToArray());
                    break;
                case MessageType.CLIENTDATA:
                    _ = DealStream(version, data.Array.Skip(data.ArrayOffset + 8).Take(length).ToArray());
                    break;
                default:
                    break;
            }
        }

        private async Task DealStream(short version, byte[] data)
        {
            string fileName = string.Empty;
            string stepMsg = "";
            try
            {
                stepMsg = " DateTime inM = DateTime.Now;";
                DateTime inM = DateTime.Now;
                if (!receiving)
                {
                    receiving = true;

                    StatusUpdater.ClientWorking(IP);
                }

                lastReceive = DateTime.Now;
                stepMsg = "  short fileLength = BitConverter.ToInt16(data, 0);";
                short fileLength = BitConverter.ToInt16(data, 0);
                stepMsg = "   Encoding.UTF8.GetString(data, 2, fileLength);";
                fileName = Encoding.UTF8.GetString(data, 2, fileLength);
                stepMsg = "BitConverter.ToInt64(data, 2 + fileLength);";
                long offset = BitConverter.ToInt64(data, 2 + fileLength);

                if (!cacheReader.ContainsKey(fileName))
                {
                    #region 释放历史reader
                    foreach (var item in cacheReader)
                    {
                        item.Value.Dispose();
                    }
                    cacheReader.Clear();
                    #endregion
                    stepMsg = "cacheReader[fileName] = ReflectHelper.Load<IParse>(\"TDASDataParser.dll\", \"TDASDataParser.RealtimeReader\", null);";
                    cacheReader[fileName] = ReflectHelper.Load<IParse>("TDASDataParser.dll", "TDASDataParser.RealtimeReader", null);
                }
                
                stepMsg = "BitConverter.ToInt64(data, 2 + fileLength + 8)";
                long timestamp = BitConverter.ToInt64(data, 2 + fileLength + 8);

                stepMsg = "data.Skip(2 + fileLength + 8 + 8).ToArray()";
                byte[] stdfData = data.Skip(2 + fileLength + 8 + 8).ToArray();

                if (!Global.StopSignal)
                {
                    stepMsg = "cacheReader[fileName].Read(fileName, offset, stdfData, timestamp, IP);";
                    cacheReader[fileName].Read(fileName, offset, stdfData, timestamp, IP);
                    stepMsg = "await SendResult(version, fileName, offset, true);";
                    await SendResult(version, fileName, offset, true);

                    //_ = Global.redis.db.StringSetAsync($"DOTNETTY:{IP}", $"发送用时:{(DateTime.Now - inM).TotalMilliseconds}ms", new TimeSpan(0, 0, 10));
                }
            }
            catch (Exception ex)
            {
                var st = new StackTrace(ex, true);
                string msg = string.Empty;
                foreach (var item in st.GetFrames())
                {
                    msg += $"{Environment.NewLine}{item.GetMethod()}-{item.GetFileLineNumber()}";
                }
                Logs.Error($"{fileName},DealStream处理异常,{stepMsg},{msg}", ex);
            }

        }

        private Task SendResult(short version, string file_name, double offset, bool success)
        {
            List<byte> result = new List<byte>();

            string json = $"{{\"s\":true,\"file_name\":\"{file_name}\",\"offset\":{offset}}}";

            byte[] data = Encoding.UTF8.GetBytes(json);

            result.AddRange(BitConverter.GetBytes(version));
            result.AddRange(BitConverter.GetBytes((short)MessageType.SERVERRESULT));
            result.AddRange(BitConverter.GetBytes(data.Length));
            result.AddRange(data);

            return Send(result.ToArray());
        }

        /// <summary>
        /// 响应来自客户端的心跳
        /// </summary>
        /// <param name="data"></param>
        private void SendPong(byte[] data)
        {
            // 停止数据超过5分钟 且状态为在运行状态的数据 修改状态为1
            if (receiving && (DateTime.Now - lastReceive).TotalMinutes >= 5)
            {
                lastReceive = DateTime.Now;
                receiving = false;
                StatusUpdater.ClientFree(IP);
            }
            Global.redis.Set($"IdleState:{IP}", DateTime.Now, 60);

            data[2] = (int)MessageType.PONG;
            Send(data);
        }

        private Task Send(byte[] data)
        {
            IByteBuffer buffer = Unpooled.Buffer(data.Length);
            buffer.WriteBytes(data);
            return Context.WriteAndFlushAsync(buffer);
        }

        #region IDisposable Support
        private bool disposedValue = false; // 要检测冗余调用

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: 释放托管状态(托管对象)。
                }

                // TODO: 释放未托管的资源(未托管的对象)并在以下内容中替代终结器。
                // TODO: 将大型字段设置为 null。

                disposedValue = true;
            }
        }

        // TODO: 仅当以上 Dispose(bool disposing) 拥有用于释放未托管资源的代码时才替代终结器。
        // ~ClientManager()
        // {
        //   // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
        //   Dispose(false);
        // }

        // 添加此代码以正确实现可处置模式。
        public void Dispose()
        {
            // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
            Dispose(true);
            // TODO: 如果在以上内容中替代了终结器，则取消注释以下行。
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}

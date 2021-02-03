using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TDASCommon
{
    public class RedisService
    {
        private readonly IConnectionMultiplexer dataConnection;
        //获取redis库
        private IDatabase _db;
        public RedisService()
        {
            RedisConnection.Config = new RedisConfig();
            dataConnection = RedisConnection.Instance;
        }

        public IDatabase db
        {
            get
            {
                if (_db == null)
                {
                    _db = dataConnection.GetDatabase();
                }
                return _db;
            }
        }

        #region 设置/获取Key的超时时间
        /// <summary>
        /// 设置指定key的具体过期时间
        /// </summary>
        /// <param name="Key">key</param>
        /// <param name="ExpireAt">具体过期时间</param>
        public void SetExpireAt(string Key, DateTime ExpireAt)
        {
            db.KeyExpire(Key, ExpireAt);
        }

        /// <summary>
        /// 设置指定key还有多久过期
        /// </summary>
        /// <param name="Key">key</param>
        /// <param name="ExpireIn">持续多久后过期(秒)</param>
        public void SetExpireIn(string Key, int ExpireIn)
        {
            db.KeyExpire(Key, DateTime.Now.AddSeconds(ExpireIn));
        }

        /// <summary>
        /// 获取key的剩余有效时间(秒)
        /// </summary>
        /// <param name="Key">要查询的key</param>
        /// <returns>返回剩余有效秒数,如果是-1则表示不会过期</returns>
        public double KeyTimeToLive(string Key)
        {
            TimeSpan? timeLive = db.KeyTimeToLive(Key);
            if (timeLive.HasValue)
            {
                return timeLive.Value.TotalSeconds;
            }
            else
            {
                return -1;
            }
        }
        #endregion

        #region 验证/删除/序列化
        /// <summary>
        /// 判断指定Key是否存在
        /// </summary>
        /// <param name="Key"></param>
        /// <returns></returns>
        public bool Exists(string Key)
        {
            return db.KeyExists(Key);
        }
        /// <summary>
        /// 删除指定Key
        /// </summary>
        public void Remove(string Key)
        {
            db.KeyDelete(Key);
        }

        /// <summary>
        /// 检测hash子键是否存在
        /// </summary>
        /// <param name="HashId">HashId</param>
        /// <param name="Key">Key</param>
        public bool HashExists(string HashId, string Key)
        {
            return db.HashExists(HashId, Key);
        }

        /// <summary>
        /// 删除指定Key
        /// </summary>
        /// <param name="HashId">HashId</param>
        /// <param name="Key">Key</param>
        public void Remove(string HashId, string Key)
        {
            db.HashDelete(HashId, Key);
        }

        /// <summary>
        /// 保存
        /// </summary>
        public void Save()
        {
            GetServer().Save(SaveType.BackgroundSave);
        }
        /// <summary>
        /// 获取Redis服务对象,默认是MasterHosts的第一个地址
        /// </summary>
        /// <returns></returns>
        public IServer GetServer()
        {
            return dataConnection.GetServer(dataConnection.GetEndPoints()[0]);
        }
        #endregion

        #region 键值对数据结构操作
        #region 同步
        /// <summary>
        /// 写入值(同步)
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="Key">Key</param>
        /// <param name="Value">值</param>
        public bool Set<T>(string Key, T Value)
        {
            return db.StringSet(Key, SerializeValue(Value));
        }
        /// <summary>
        /// 写入值(同步)
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="Key">Key</param>
        /// <param name="Value">值</param>
        /// <param name="ExpireAt">超时时间</param>
        public bool Set<T>(string Key, T Value, DateTime ExpireAt)
        {
            return db.StringSet(Key, SerializeValue(Value), ExpireAt - DateTime.Now);
        }
        /// <summary>
        /// 写入值(同步)
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="Key">Key</param>
        /// <param name="Value">值</param>
        /// <param name="ExpireIn">有效时间(秒)</param>
        public bool Set<T>(string Key, T Value, int ExpireIn)
        {
            return db.StringSet(Key, SerializeValue(Value), new TimeSpan(10000000L * ExpireIn));
        }
        /// <summary>
        /// 获取值(同步)
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="Key">Key</param>
        /// <returns></returns>
        public T Get<T>(string Key)
        {
            return DeserializeValue<T>(db.StringGet(Key));
        }
        #endregion

        #region 异步
        /// <summary>
        /// 写入值(异步)
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="Key">Key</param>
        /// <param name="Value">值</param>
        public Task<bool> SetAsync<T>(string Key, T Value)
        {
            return db.StringSetAsync(Key, SerializeValue(Value));
        }
        /// <summary>
        /// 写入值(异步)
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="Key">Key</param>
        /// <param name="Value">值</param>
        /// <param name="ExpireAt">超时时间</param>
        public Task<bool> SetAsync<T>(string Key, T Value, DateTime ExpireAt)
        {
            return db.StringSetAsync(Key, SerializeValue(Value), ExpireAt - DateTime.Now);
        }
        /// <summary>
        /// 写入值(异步)
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="Key">Key</param>
        /// <param name="Value">值</param>
        /// <param name="ExpireIn">有效时间(秒)</param>
        public Task<bool> SetAsync<T>(string Key, T Value, int ExpireIn)
        {
            return db.StringSetAsync(Key, SerializeValue(Value), new TimeSpan(10000000L * ExpireIn));
        }
        /// <summary>
        /// 获取值(异步)
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="Key">Key</param>
        /// <returns></returns>
        public Task<T> GetAsync<T>(string Key)
        {
            return Task.Run((async () =>
            {
                string StrValue = await db.StringGetAsync(Key);
                return DeserializeValue<T>(StrValue);
            }));
        }
        #endregion
        #endregion

        #region Hash数据结构操作
        #region 同步
        /// <summary>
        /// 写入Hash值(同步)
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="HashId">HashId</param>
        /// <param name="Key">Key</param>
        /// <param name="Value">值</param>
        /// <returns></returns>
        public bool HashSet<T>(string HashId, string Key, T Value)
        {
            return db.HashSet(HashId, Key, SerializeValue(Value));
        }
        /// <summary>
        /// 写入Hash值(同步)
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="HashId">HashId</param>
        /// <param name="Key">Key</param>
        /// <param name="Value">值</param>
        /// <param name="ExpireAt">超时时间</param>
        /// <returns></returns>
        public bool HashSet<T>(string HashId, string Key, T Value, DateTime ExpireAt)
        {
            bool result = db.HashSet(HashId, Key, SerializeValue(Value));
            SetExpireAt(HashId, ExpireAt);
            return result;
        }
        /// <summary>
        /// 写入Hash值(同步)
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="HashId">HashId</param>
        /// <param name="Key">Key</param>
        /// <param name="Value">值</param>
        /// <param name="ExpireIn">有效时间(秒)</param>
        /// <returns></returns>
        public bool HashSet<T>(string HashId, string Key, T Value, int ExpireIn)
        {
            bool result = db.HashSet(HashId, Key, SerializeValue(Value));
            SetExpireIn(HashId, ExpireIn);
            return result;
        }
        /// <summary>
        /// 写入Hash值(同步)
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="HashId">HashId</param>
        /// <param name="Values">键值对数组</param>
        public void HashSet<T>(string HashId, KeyValuePair<string, T>[] Values)
        {
            db.HashSet(HashId, Values.Select(p => { return new HashEntry(p.Key, SerializeValue(p.Value)); }).ToArray());
        }
        /// <summary>
        /// 写入Hash值(同步)
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="HashId">HashId</param>
        /// <param name="Values">键值对数组</param>
        /// <param name="ExpireAt">超时时间</param>
        public void HashSet<T>(string HashId, KeyValuePair<string, T>[] Values, DateTime ExpireAt)
        {
            db.HashSet(HashId, Values.Select(p => { return new HashEntry(p.Key, SerializeValue(p.Value)); }).ToArray());
            SetExpireAt(HashId, ExpireAt);

        }
        /// <summary>
        /// 写入Hash值(同步)
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="HashId">HashId</param>
        /// <param name="Values">键值对数组</param>
        /// <param name="ExpireIn">有效时间(秒)</param>
        public void HashSet<T>(string HashId, KeyValuePair<string, T>[] Values, int ExpireIn)
        {
            db.HashSet(HashId, Values.Select(p => { return new HashEntry(p.Key, SerializeValue(p.Value)); }).ToArray());
            SetExpireIn(HashId, ExpireIn);
        }
        /// <summary>
        /// 获取Hash值(同步)
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="HashId">HashId</param>
        /// <param name="Key">Key</param>
        /// <returns></returns>
        public T HashGet<T>(string HashId, string Key)
        {
            return DeserializeValue<T>(db.HashGet(HashId, Key));
        }
        /// <summary>
        /// 获取全部Hash值(同步)
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="HashId">HashId</param>
        /// <param name="Key">Key</param>
        /// <returns></returns>
        public IDictionary<string, T> HashGetAll<T>(string HashId)
        {
            HashEntry[] Values = db.HashGetAll(HashId);
            return ArrayConvert<T>(Values);
        }
        #endregion

        #region 异步
        /// <summary>
        /// 写入Hash值(异步)
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="HashId">HashId</param>
        /// <param name="Key">Key</param>
        /// <param name="Value">值</param>
        public Task<bool> HashSetAsync<T>(string HashId, string Key, T Value)
        {
            return db.HashSetAsync(HashId, Key, SerializeValue(Value));
        }
        /// <summary>
        /// 写入Hash值(异步)
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="HashId">HashId</param>
        /// <param name="Key">Key</param>
        /// <param name="Value">值</param>
        /// <param name="ExpireAt">超时时间</param>
        /// <returns></returns>
        public Task<bool> HashSetAsync<T>(string HashId, string Key, T Value, DateTime ExpireAt)
        {
            return Task.Run((async () =>
            {
                bool resuht = await HashSetAsync(HashId, Key, SerializeValue(Value));
                SetExpireAt(HashId, ExpireAt);
                return resuht;
            }));
        }
        /// <summary>
        /// 写入Hash值(异步)
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="HashId">HashId</param>
        /// <param name="Key">Key</param>
        /// <param name="Value">值</param>
        /// <param name="ExpireIn">有效时间(秒)</param>
        /// <returns></returns>
        public Task<bool> HashSetAsync<T>(string HashId, string Key, T Value, int ExpireIn)
        {
            return Task.Run((async () =>
            {
                bool resuht = await HashSetAsync(HashId, Key, SerializeValue(Value));
                SetExpireIn(HashId, ExpireIn);
                return resuht;
            }));
        }
        /// <summary>
        /// 写入Hash值(异步)
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="HashId">HashId</param>
        /// <param name="Key">Key</param>
        /// <param name="Values">键值对</param>
        /// <returns></returns>
        public Task HashSetAsync<T>(string HashId, KeyValuePair<string, T>[] Values)
        {
            return Task.Run(() =>
            {
                return db.HashSetAsync(HashId, Values.Select(p => { return new HashEntry(p.Key, SerializeValue(p.Value)); }).ToArray());
            });
        }
        /// <summary>
        /// 写入Hash值(异步)
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="HashId">HashId</param>
        /// <param name="Key">Key</param>
        /// <param name="Values">键值对</param>
        /// <param name="ExpireAt">超时时间</param>
        /// <returns></returns>
        public Task HashSetAsync<T>(string HashId, KeyValuePair<string, T>[] Values, DateTime ExpireAt)
        {
            return Task.Run((async () =>
            {
                await HashSetAsync(HashId, Values);
                SetExpireAt(HashId, ExpireAt);
            }));
        }
        /// <summary>
        /// 写入Hash值(异步)
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="HashId">HashId</param>
        /// <param name="Key">Key</param>
        /// <param name="Values">键值对</param>
        /// <param name="ExpireIn">有效时间(秒)</param>
        /// <returns></returns>
        public Task HashSetAsync<T>(string HashId, KeyValuePair<string, T>[] Values, int ExpireIn)
        {
            return Task.Run((async () =>
            {
                await HashSetAsync(HashId, Values);
                SetExpireIn(HashId, ExpireIn);
            }));
        }
        /// <summary>
        /// 获取Hash值(异步)
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="HashId">HashId</param>
        /// <param name="Key">Key</param>
        /// <returns></returns>
        public Task<T> HashSetAsync<T>(string HashId, string Key)
        {
            return Task.Run((async () =>
            {
                var value = await db.HashGetAsync(HashId, Key);
                return DeserializeValue<T>(value);
            }));
        }
        /// <summary>
        /// 获取全部Hash值(异步)
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="HashId">HashId</param>
        /// <param name="Key">Key</param>
        /// <returns></returns>
        public Task<IDictionary<string, T>> HashGetAllAsync<T>(string HashId)
        {
            return Task.Run((async () =>
            {
                return ArrayConvert<T>(await db.HashGetAllAsync(HashId));
            }));

        }
        #endregion
        #endregion

        #region 序列化
        private string SerializeValue<T>(T Value)
        {
            return Value.FromObject();
        }
        private T DeserializeValue<T>(string StrValue)
        {
            return StrValue.ToObject<T>();
        }
        #endregion

        #region KeyValuePair值类型转换
        private KeyValuePair<RedisKey, RedisValue>[] ArrayConvert<T>(IDictionary<string, T> Values)
        {
            return Values.Select(p => { return new KeyValuePair<RedisKey, RedisValue>(p.Key, SerializeValue(p.Value)); }).ToArray();
        }
        #endregion

        #region HashEntry类型转换
        private IDictionary<string, T> ArrayConvert<T>(HashEntry[] Values)
        {
            IDictionary<string, T> _dict = new Dictionary<string, T>();
            Values.Select(p => { return new KeyValuePair<string, T>(p.Name, DeserializeValue<T>(p.Value)); }).ToList().ForEach(p =>
            {
                _dict.Add(p);
            });
            return _dict;
        }
        #endregion

        #region 发布订阅
        /// <summary>
        /// 发布消息
        /// </summary>
        /// <param name="Channel">要发布的消息通道</param>
        /// <param name="Message">要发布的内容</param>
        /// <returns></returns>
        public long Publish(string Channel, string Message)
        {
            return dataConnection.GetSubscriber().Publish(Channel, Message);

        }

        /// <summary>
        /// 订阅消息
        /// </summary>
        /// <param name="Channel">要订阅的通道</param>
        /// <param name="Callback">接收到消息后的回调方法</param>
        /// <returns></returns>
        public Task Subscribe(string Channel, Action<string> Callback)
        {
            return dataConnection.GetSubscriber().SubscribeAsync(Channel, (channel, message) =>
            {
                Callback?.Invoke(message);
            });
        }
        #endregion
    }
}

using MassTransit.Util;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TDASCommon;
using TDASDataParser.StdfTypes;
using Topshelf;

namespace TDASPersistentService
{
    public class OracleSubmit: ServiceControl
    {
       
        DatabaseManager dmgr = new DatabaseManager();
        RedisService redis = new RedisService();
        LimitedConcurrencyLevelTaskScheduler scheduler = new LimitedConcurrencyLevelTaskScheduler(30);
        LimitedConcurrencyLevelTaskScheduler ptrScheduler = new LimitedConcurrencyLevelTaskScheduler(5);
        TaskFactory factory;
        TaskFactory ptrFactory;

        Dictionary<string, DateTime> notExistsCheck = new Dictionary<string, DateTime>();

        List<Task> taskList = new List<Task>();

        public bool Start(HostControl hostControl)
        {
            Logs.Info("开启数据持久化服务");

            factory = new TaskFactory(scheduler);
            ptrFactory = new TaskFactory(ptrScheduler);
            Global.TypePropsInit();
            Assembly asse = Assembly.Load("TDASDataParser");

            Dictionary<string, object> listType = new Dictionary<string, object>();

            foreach (var item in asse.GetTypes())
            {
                if (!item.Name.Contains("PrivateImplementationDetails") && item.FullName.StartsWith("TDASDataParser.StdfTypes."))
                {
                    listType.Add(item.Name, asse.CreateInstance(item.FullName));
                }
            }

            string[] types = Config.SubmitType[0] == "*" ? listType.Keys.ToArray() : Config.SubmitType;
            // 针对每个类型 启动一个线程,类型取值与appsetting.json中的SubmitType
            foreach (var type in types)
            {
                notExistsCheck[type] = DateTime.Now.AddSeconds(-6);
                taskList.Add(Task.Run(() =>
                {
                    while (!Global.StopSignal)
                    {
                        if (!notExistsCheck.ContainsKey(type) || (DateTime.Now - notExistsCheck[type]).TotalSeconds > (type == "PRR" ? 1 : 3))
                        {
                            Woker(type, (dynamic)(listType[type]));
                            Thread.Sleep(100);
                        }
                        else
                        {
                            Thread.Sleep(100);
                        }
                    }
                }));
            }

            Task.Run(() => {
                RemoveClient();
            });

            Global.redis.Subscribe("STDFWRITESTOP", (msg) =>
            {
                Global.StopSignal = true;
            });

            return true;
        }
        public bool Stop(HostControl hostControl)
        {
            Global.StopSignal = true;
            Logs.Info("停止数据持久化服务");
            Thread.Sleep(10000);
            Task.WaitAll(taskList.ToArray());
            return true;
        }

        public void RemoveClient()
        {
            DateTime lastRun = DateTime.Now;
            Logs.Debug("客户端自动清除功能启动");
            while (true)
            {
                if ((DateTime.Now - lastRun).TotalMinutes > 180)
                {
                    lastRun = DateTime.Now;


                    try
                    {
                        string strsql = "select IP_CODE from sys_eqp_status where status=2 and EQP_NAME like '%E3200-05%'";

                        List<dynamic> iplist = dmgr.ExecuteEntities<dynamic>(strsql);

                        foreach (var item in iplist)
                        {
                            Global.redis.Publish("_KILLCLIENT_", item.IP_CODE.ToString());
                        }
                    }
                    catch (Exception ex)
                    {
                        Logs.Error("移除客户端", ex);
                    }

                }
                Task.Delay(1000).Wait();
            }
        }
        private async void Woker<T>(string type, T entity) where T : BaseEntity, new()
        {
            try
            {
                long count = redis.db.ListLengthAsync(type).Result;

                var tmpfactory = type == "PTR" ? ptrFactory : factory;

                count = Math.Min(count, 30000);//每次最多允许提交数



                if (count > 0)
                {
                    await tmpfactory.StartNew(() =>
                    {
                        #region 创建批量读取命令
                        IBatch batch = redis.db.CreateBatch();
                        List<Task<RedisValue>> values = new List<Task<RedisValue>>();
                        for (int i = 0; i < count; i++)
                        {
                            values.Add(batch.ListRightPopAsync(type));
                        }
                        batch.Execute();
                        #endregion

                        //泛型数据结构,用于给插库提供数据源支持
                        Dictionary<int, List<T>> source = new Dictionary<int, List<T>>();

                        //redis 缓存数据,用于在数据库插入失败的情况下重新持久化到redis中
                        Dictionary<int, List<byte[]>> redisData = new Dictionary<int, List<byte[]>>();

                        foreach (var item in values)
                        {
                            byte[] val = item.Result;

                            if (val == null)
                            {
                                continue;
                            }

                            int key = 0;

                            if (!redisData.ContainsKey(key))
                            {
                                source[key] = new List<T>();
                                redisData[key] = new List<byte[]>();
                            }
                            redisData[key].Add(val);

                            //数据格式   |stdfid(4)|partid(4)|data(N)
                            //格式解释   byte[]总计分3段,第一段4个字节为stdfid,第二段8个字节为附加内容,第三段为剩余所有字节,属于stdf格式数据

                            var t = new T() { StdfId = BitConverter.ToInt32(val, 0) };
                            t.LoadData(val.Skip(12).ToArray(), val.Skip(4).Take(8).ToArray());
                            source[key].Add(t);
                        }

                        string sql = Global.DicSQL[type];

                        if (source.Count < 1)
                        {
                            return;
                        }


                        DataHelper.SubmitEntities(type, source[0].ToArray(), sql, dmgr.ConnectionString, redisData[0]);
                    });

                }
                else
                {
                    notExistsCheck[type] = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                Logs.Error("", ex);
            }
        }
    }

}

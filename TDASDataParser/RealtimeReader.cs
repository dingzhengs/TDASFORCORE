using StackExchange.Redis;
using TDASDataParser.StdfTypes;
using System;
using System.Linq;
using TDASCommon;
using TDASDataParser.Csv;
using TDASDataParser.Rules;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using TDASDataParser.Utils;
using System.Collections.Concurrent;

namespace TDASDataParser
{
    public class RealtimeReader : BaseReader, IDisposable
    {
        IBatch batch;
        long offset = -1;
        int stdfid = -1;
        string ip = string.Empty;
        string fileName = string.Empty;
        WriteToCsv writeToCsv;
        RulesFactory ruleFactory;
        MIR mir = null;
        bool isLostMir = false;
        ConcurrentQueue<byte[]> queueData;
        bool isWatch = true;
        DateTime lastTime = DateTime.MinValue;

        public RealtimeReader()
        {
            batch = Global.redis.db.CreateBatch();
            writeToCsv = new WriteToCsv();
            ruleFactory = new RulesFactory();
            queueData = new ConcurrentQueue<byte[]>();
            Task.Run(() => { QueueWatcher(); });
        }

        public override int Read(string fileName, long streamOffset, byte[] data, long timestamp, string ip)
        {
            if (string.IsNullOrEmpty(this.ip))
            {
                Logs.Debug($"[{ip}]-Version:{this.GetType().Assembly.GetName().Version},{fileName}");
                this.fileName = fileName;
            }

            this.ip = ip;
            
            if (streamOffset > offset)
            {
                if (stdfid == -1)
                {
                    stdfid = base.GetStdfId(fileName, "");
                }
                if (offset == 0)
                {
                    Logs.Info($"[{stdfid}]-开始解析[{fileName}]");
                }

                queueData.Enqueue(data);

            }
            
            offset = streamOffset;
            
            return stdfid;
        }

        private void QueueWatcher()
        {
            int count = 0;
            while (isWatch)
            {
                try
                {
                    if (queueData.TryDequeue(out byte[] data))
                    {
                        count = 0;
                        Parse(data.ToList());
                    }
                    else
                    {
                        count++;

                        Task.Delay(1000).Wait();

                        // 挂起超过2天还没继续操作 直接停止循环 允许GC释放
                        if (count > 60 * 60 * 24 * 2)
                        {
                            isWatch = false;
                        }
                    }

                }
                catch (Exception ex)
                {
                    Logs.Error("QueueWatcher", ex);
                }
            }
        }


        public void Parse(List<byte> data)
        {

            while (data?.Count > 0)
            {
                byte[] header = data.GetRange(0, 4).ToArray();
                data.RemoveRange(0, 4);
                int length = BitConverter.ToInt16(header, 0);
                int typ = header[2];
                int sub = header[3];


                byte[] buffer = data.GetRange(0, length).ToArray();

                BaseEntity entity = DataToEntity(typ, sub, buffer, stdfid, out string type);

                if (type == "MIR")
                {
                    mir = entity as MIR;
                }
                if (type == "SDR" && mir == null && !isLostMir)
                {
                    isLostMir = true;
                    Global.redis.Publish("LOSSMIR", JToken.FromObject(new
                    {
                        FILENAME = fileName
                    }).ToString());
                }
                DynamicDeal(type, entity, buffer, DateHelper.GetTimestamp(DateTime.Now));

                data.RemoveRange(0, length);
                InsertToCache();
            }

        }


        public void DynamicDeal(string type, BaseEntity entity, byte[] data, double timestamp)
        {
            if (entity == null)
            {
                return;
            }
            try
            {
                
                if (type == "SDR")
                {
                    writeToCsv?.SDRINFO(entity as SDR);

                    //比对MES文件名是否一致
                    try
                    {
                        if (mir.SBLOTID.Contains("AFY"))
                        {
                            DateTime now = DateTime.Now;
                            string flowid = "";
                            if (mir.FLOWID != "" && mir.FLOWID != null && mir.FLOWID.Length > 1)
                            {
                                flowid = mir.FLOWID?.Substring(0, 2);
                            }
                            string _mailtitle = "Pause Production_DIFFJOBNAM_" + mir.PARTTYP + "_" + mir.LOTID + "_" + mir.TESTCOD + "_" + mir.SBLOTID + "_" + flowid + "_" + mir.NODENAM + "_" + now.ToString("yyyyMMddHHmmss");

                            Global.redis.Publish("DIFFJOBNAM", JToken.FromObject(new
                            {
                                EQPNAME = mir.NODENAM,
                                JOBNAM = mir.JOBNAM,
                                SBLOTID = mir.SBLOTID,
                                EQPTID = (entity as SDR).HANDID,
                                MAILTITLE = _mailtitle
                            }).ToString());
                        }
                    }
                    catch (Exception ex)
                    {
                        Logs.Error("比对MES文件名", ex);
                    }
                }

                ruleFactory?.Match(type, entity, dmgr);

                if (type == "PTR")
                {
                    writeToCsv?.Analyze(entity);
                    // batch.ListLeftPushAsync(type, BitConverter.GetBytes(entity.StdfId).Concat(BitConverter.GetBytes(timestamp)).Concat(data).ToArray());
                }
                else if (type == "FTR")
                {
                    writeToCsv?.Analyze(entity);
                }
                else if (type == "PRR")
                {
                    writeToCsv?.Analyze(entity);

                    batch.ListLeftPushAsync(type, BitConverter.GetBytes(entity.StdfId).Concat(BitConverter.GetBytes(timestamp)).Concat(data).ToArray());
                }
                else
                {
                    batch.ListLeftPushAsync(type, BitConverter.GetBytes(entity.StdfId).Concat(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 }).Concat(data).ToArray());
                }
                if (type == "MIR")
                {
                    MIR mir1 = entity as MIR;

                    ruleFactory?.Init(mir1, dmgr);
                    writeToCsv?.Init(mir1, dmgr);

                    UpdateLostMRR(mir1);
                }
                if (type == "MRR")
                {
                    isWatch = false;
                    writeToCsv?.End(dmgr, entity as MRR);
                    StatusUpdater.ClientFree(ip);
                    // 回头这个需要更新到数据库
                    /*SELECT STDFID,HARDBIN,SOFTBIN,COUNT(1) COUNT FROM prr WHERE stdfid=426511
                    GROUP BY STDFID,HARDBIN,SOFTBIN*/
                    Logs.Info($"[{stdfid}]-完成解析");
                }

            }
            catch (Exception ex)
            {
                Logs.Error(entity.StdfId.ToString(), ex);
            }
        }

        public Task UpdateLostMRR(MIR mir)
        {
            return Task.Run(() =>
            {
                try
                {
                    string strSQL = $@"select t.STDFID from mir t 
                            where not exists (select 1 from mrr where mrr.stdfid=t.stdfid)
                            and  t.nodenam='{mir.NODENAM}' and t.stdfid<>{mir.StdfId}";
                    var stdfids = dmgr.ExecuteEntities<dynamic>(strSQL);

                    foreach (var item in stdfids)
                    {
                        dmgr.ExecuteNonQuery($"INSERT INTO MRR(STDFID,FINISHT,USRDESC) SELECT {item.STDFID},1,'autocheck' FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM MRR WHERE STDFID={item.STDFID})");
                        Task.Delay(20000).Wait();
                        dmgr.ExecuteNonQuery($"delete MRR where stdfid={item.STDFID} and USRDESC='autocheck' and (select count(1) from mrr where stdfid={item.STDFID} )>1");

                    }
                }
                catch
                {

                }
            });

        }
        public void InsertToCache()
        {
            try
            {
                batch.Execute();
                ParseCount = 0;
            }
            catch (Exception ex)
            {
                Logs.Error("批量写入redis缓存异常", ex);
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // 要检测冗余调用

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    isWatch = false;
                    writeToCsv.Dispose();
                }

                // TODO: 释放未托管的资源(未托管的对象)并在以下内容中替代终结器。
                // TODO: 将大型字段设置为 null。

                disposedValue = true;
            }

        }

        // TODO: 仅当以上 Dispose(bool disposing) 拥有用于释放未托管资源的代码时才替代终结器。
        // ~RealtimeReader()
        // {
        //   // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
        //   Dispose(false);
        // }

        // 添加此代码以正确实现可处置模式。
        public override void Dispose()
        {
            base.Dispose();
            // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
            Dispose(true);
            // TODO: 如果在以上内容中替代了终结器，则取消注释以下行。
            // GC.SuppressFinalize(this);
        }
        #endregion

    }

    public class SYS_REPLACE
    {
        public string FAMLYID { get; set; }
        public string FLOWID_OLD { get; set; }
        public string FLOWID_NEW { get; set; }
        public string TESTCOD_OLD { get; set; }
        public string TESTCOD_NEW { get; set; }

        public void CheckMIR(MIR mir, DatabaseManager dmgr)
        {

            try
            {
                {// 重置FLOWID和TESTCOD 
                    string strSQL = "select * from SYS_REPLACE where FAMLYID=:FAMLYID";
                    List<SYS_REPLACE> lstRep = dmgr.ExecuteEntities<SYS_REPLACE>(strSQL, new { mir.FAMLYID });
                    if (lstRep?.Count > 0)
                    {
                        if (mir.FAMLYID == "AIP")
                        {
                            foreach (var item in lstRep)
                            {
                                if (!string.IsNullOrEmpty(item.TESTCOD_OLD) && !string.IsNullOrEmpty(item.FLOWID_NEW) && !string.IsNullOrEmpty(item.TESTCOD_NEW))
                                {
                                    if (item.TESTCOD_OLD == mir.TESTCOD)
                                    {
                                        mir.FLOWID = item.FLOWID_NEW;
                                        mir.TESTCOD = item.TESTCOD_NEW;
                                    }
                                }
                            }
                        }
                        else
                        {
                            foreach (var item in lstRep)
                            {
                                if (!string.IsNullOrEmpty(item.FLOWID_OLD) && !string.IsNullOrEmpty(item.FLOWID_NEW))
                                {
                                    if (item.FLOWID_OLD == mir.FLOWID)
                                    {
                                        mir.FLOWID = item.FLOWID_NEW;
                                    }
                                }

                                if (!string.IsNullOrEmpty(item.TESTCOD_OLD) && !string.IsNullOrEmpty(item.TESTCOD_NEW))
                                {
                                    if (item.TESTCOD_OLD == mir.TESTCOD)
                                    {
                                        mir.TESTCOD = item.TESTCOD_NEW;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {

            }
        }
    }
}

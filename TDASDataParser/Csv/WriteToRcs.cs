using TDASDataParser.StdfTypes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TDASCommon;
using System.Data;
using TDASDataParser.Utils;

namespace TDASDataParser.Csv
{
    public class WriteToRcs : IDisposable
    {
        FileStream allStream;

        string stdfid = "";

        string fullName = string.Empty;

        List<List<string>> dataCache = new List<List<string>>();

        Dictionary<string, int> TESTTXT = new Dictionary<string, int>();

        List<string> TEXTTXTLIMIT = new List<string>();

        MIR mir;

        int startCol = 5;

        string[] mirinfo;
        string[] sdrinfo;

        // Header
        List<string> row1;

        public void MIRINFO(string[] mirinfo)
        {
            try
            {
                this.mirinfo = mirinfo;
            }
            catch (Exception ex)
            {
                Logs.Error("获取MIR信息", ex);
            }
        }
        public void Init(MIR mir, DatabaseManager dmgr, string fileName)
        {
            try
            {
                this.mir = mir;
                this.stdfid = mir?.StdfId.ToString();


                DataTable dt = dmgr.ExecuteDataTable($"select distinct COLUMVALUE  from fw_rcs_rules  where productcode='{mir.PARTTYP}' AND CUSTCODE='{mir.FAMLYID}'  AND INSERTION='{mir.TESTCOD}' and area like '%PTR%'");

                if (dt != null && dt.Rows.Count > 0)
                {
                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        TEXTTXTLIMIT.Add(dt.Rows[i]["COLUMVALUE"].ToString());
                    }
                }

                if (TEXTTXTLIMIT.Count == 0)
                {
                    return;
                }
                string root = Path.Combine(new DirectoryInfo(Config.CsvFileForlder).Parent.FullName, "RCS", DateTime.Now.ToString("yyyyMMdd"));

                if (!Directory.Exists(root))
                {
                    Directory.CreateDirectory(root);
                }

                fullName = Path.Combine(root, $"{fileName}.rcs.csv");

                if (File.Exists(fullName))
                {
                    this.Dispose();
                    return;
                }

                allStream = new FileStream(fullName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);

                allStream.Seek(0, SeekOrigin.Begin);
            }
            catch (Exception ex)
            {
                Logs.Error("初始化WriteToRcs", ex);
            }


        }
        public void SDRINFO(string[] sdrinfo)
        {
            try
            {
                this.sdrinfo = sdrinfo;
            }
            catch (Exception ex)
            {
                Logs.Error( "", ex);
            }
        }
        public void Analyze(BaseEntity entity)
        {
            if (entity.StdfId.ToString() != stdfid)
            {
                return;
            }
            try
            {
                if (allStream == null)
                {
                    return;
                }
                PTR ptr = entity as PTR;
                PRR prr = entity as PRR;


                if (row1 == null)
                {
                    row1 = new List<string>();
                    // 写入固定项
                    row1.AddRange(new string[] { "PARTID", "SBIN", "HBIN", "SITE", "TIME" });
                    foreach (var item in TEXTTXTLIMIT)
                    {
                        TESTTXT.Add(item, TESTTXT.Count);
                        row1.Add(item);
                    }
                }

                #region ptr数据写入
                if (ptr != null)
                {

                    string key = ptr.TESTTXT;// + ptr.TESTNUM;

                    if (ptr.TESTTXT.Contains("FRC_LOTID1"))
                    {

                    }

                    KeyValuePair<string, int> result = TESTTXT.Where(p => ptr.TESTTXT.Contains(p.Key)).FirstOrDefault();

                    if (result.Key == null)
                    {
                        return;
                    }
                    // 获取ptr数据写入位置
                    int writeIndex = startCol + TESTTXT[result.Key];

                    // 获取到不在TESTNUM列表中的数据 退出
                    if (writeIndex < startCol)
                    {
                        return;
                    }


                    // 根据sitenum值创建数据缓存对象 默认 下标是0
                    while (dataCache.Count < ptr.SITENUM + 1)
                    {
                        dataCache.Add(new List<string>());
                    }

                    // 插入空白字符给PRR数据占位
                    while (dataCache[ptr.SITENUM].Count < startCol)
                    {
                        dataCache[ptr.SITENUM].Add("");
                    }

                    // 在当前ptr之前如果有跳 TESTNUM的 插入空白占位
                    while (dataCache[ptr.SITENUM].Count < writeIndex + 1)
                    {
                        dataCache[ptr.SITENUM].Add("");
                    }

                    // 插入测试项的值 完事~
                    dataCache[ptr.SITENUM][writeIndex] = Math.Round(ptr.RESULT, 11).ToString();


                }
                #endregion

                #region prr数据写入
                if (prr != null)
                {
                    try
                    {
                        while (dataCache.Count < prr.SITENUM + 1)
                        {
                            dataCache.Add(new List<string>());
                        }
                        while (dataCache[prr.SITENUM].Count < startCol)
                        {
                            dataCache[prr.SITENUM].Add("");
                        }
                        dataCache[prr.SITENUM][0] = "PID-" + prr.PARTID;
                        dataCache[prr.SITENUM][1] = prr.SOFTBIN.ToString();
                        dataCache[prr.SITENUM][2] = prr.HARDBIN.ToString();
                        dataCache[prr.SITENUM][3] = prr.SITENUM.ToString();
                        dataCache[prr.SITENUM][4] = Math.Round(prr.TESTT / 1000, 3).ToString();

                        // 因为dataCache的key是根据sitenum增加的 所以sitem的最后一个项目肯定是会有值的
                        if (dataCache.Last()[0] != "")
                        {
                            dataCache.RemoveAll(p => p.Count == 0);
                            Write();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logs.Error( "", ex);
                    }
                }
                #endregion
            }
            catch (Exception ex)
            {
                Logs.Error( "", ex);
            }
        }
        private void Write()
        {
            try
            {
                if (allStream == null)
                {
                    return;
                }
                if (row1 != null && row1.Count > 0)
                {
                    // 写入最终头部数据
                    byte[] buffer = Encoding.UTF8.GetBytes(string.Join(",", mirinfo).Replace("@FinishTime", "") + Environment.NewLine);
                    allStream?.Write(buffer, 0, buffer.Length);
                    buffer = Encoding.UTF8.GetBytes(string.Join(",", sdrinfo) + Environment.NewLine);
                    allStream?.Write(buffer, 0, buffer.Length);
                    buffer = Encoding.UTF8.GetBytes(string.Join(",", row1.ToArray()) + Environment.NewLine);
                    allStream?.Write(buffer, 0, buffer.Length);
                    allStream?.Flush();
                    row1.Clear();
                }

                for (int i = 0; i < dataCache.Count; i++)
                {
                    if (dataCache[i] != null)
                    {
                        byte[] buffer = Encoding.UTF8.GetBytes(string.Join(",", dataCache[i].ToArray()) + Environment.NewLine);
                        allStream.Write(buffer, 0, buffer.Length);
                    }
                }
                allStream.Flush();

                dataCache.Clear();
                dataCache = new List<List<string>>();
            }
            catch (Exception ex)
            {
                Logs.Error( "", ex);
            }
        }
        public void End(DatabaseManager dmgr, MRR mrr)
        {
            try
            {
                if (allStream == null)
                {
                    return;
                }
                allStream?.Flush();
                allStream?.Close();
                allStream?.Dispose();
                allStream = null;
                allStream = null;
            }
            catch (Exception ex)
            {
                if (File.Exists(fullName))
                {
                    File.Delete(fullName);
                }
                Logs.Error( $"in end:{fullName}", ex);
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // 要检测冗余调用
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                this.End(new DatabaseManager(), new MRR { FINISHT = DateHelper.GetTimestamp(DateTime.Now) });
                this.allStream?.Dispose();
                this.allStream = null;
                this.dataCache = null;
                this.TEXTTXTLIMIT = null;
                this.row1 = null;
                this.TESTTXT = null;


                if (disposing)
                {

                }

                // TODO: 释放未托管的资源(未托管的对象)并在以下内容中替代终结器。
                // TODO: 将大型字段设置为 null。

                disposedValue = true;
            }
        }

        // TODO: 仅当以上 Dispose(bool disposing) 拥有用于释放未托管资源的代码时才替代终结器。
        ~WriteToRcs()
        {
            // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
            Dispose(false);
        }

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

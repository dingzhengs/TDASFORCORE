using TDASDataParser.StdfTypes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TDASCommon;
using TDASDataParser.Utils;

namespace TDASDataParser.Csv
{
    public class WriteToCsv : IDisposable
    {
        WriteToRcs rcs = null;

        FileStream contentStream;

        FileStream allStream;

        string stdfid = "";

        string fullName = string.Empty;

        List<List<string>> dataCache = new List<List<string>>();

        Dictionary<string, int> TESTTXTNUM = new Dictionary<string, int>();

        MIR mir;

        int startCol = 5;

        string[] mirinfo;

        string[] sdrinfo;

        // Header
        List<string> row1;
        // Tests#
        List<string> row2;
        // Unit
        List<string> row3;
        // HighL
        List<string> row4;
        // LowL
        List<string> row5;

        private void MIRINFO(MIR mir, string stdfid)
        {
            mirinfo = new string[] {
                "--- Global Info:"+Environment.NewLine+
                "STDFID",stdfid+Environment.NewLine+
                "Date",DateHelper.GetDate(mir.STARTT).ToString()+Environment.NewLine+
                "SetupTime",DateHelper.GetDate(mir.SETUPT).ToString()+Environment.NewLine+
                "StartTime",DateHelper.GetDate(mir.STARTT).ToString()+Environment.NewLine+
                "FinishTime","@FinishTime"+Environment.NewLine+
                "ProgramName",mir.JOBNAM+Environment.NewLine+
                "ProgramRevision",mir.JOBREV+Environment.NewLine+
                "Lot",mir.LOTID+Environment.NewLine+
                "SubLot",mir.SBLOTID+Environment.NewLine+
                "Wafer_Pos_X",""+Environment.NewLine+
                "Wafer_Pos_Y",""+Environment.NewLine+
                "TesterName",mir.NODENAM+Environment.NewLine+
                "TesterType",mir.TSTRTYP+Environment.NewLine+
                "Product",mir.PARTTYP+Environment.NewLine+
                "Operator",mir.OPERNAM+Environment.NewLine+
                "ExecType",mir.EXECTYP+Environment.NewLine+
                "TestCode",mir.TESTCOD+Environment.NewLine+
                "ModeCode",mir.MODECOD+Environment.NewLine+
                "RtstCode",mir.RTSTCOD+Environment.NewLine+
                "Temperature",mir.TSTTEMP+Environment.NewLine+
                "Family",mir.FAMLYID+Environment.NewLine+
                "Facility",mir.FACILID+Environment.NewLine+
                "FlowID",mir.FLOWID+Environment.NewLine+
                "SetupID",mir.SETUPID+Environment.NewLine
            };
        }
        public void Init(MIR mir, DatabaseManager dmgr)
        {
            try
            {
                this.mir = mir;
                this.stdfid = mir?.StdfId.ToString();

                string fileName = dmgr.ExecuteScalar($"select filename from stdffile where stdfid=:stdfid", new { stdfid = this.stdfid })?.ToString();
                if (string.IsNullOrEmpty(fileName))
                {
                    fileName = $"{mir.LOTID}-{mir.StdfId}";
                }

                if (!Directory.Exists(Config.CsvFileForlder))
                {
                    Directory.CreateDirectory(Config.CsvFileForlder);
                }

                fullName = Path.Combine(Config.CsvFileForlder, $"{fileName}.csv");

                if (File.Exists(fullName))
                {
                    this.Dispose();
                    return;
                }

                contentStream = new FileStream(fullName + ".tmp", FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                allStream = new FileStream(fullName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);

                contentStream.Seek(0, SeekOrigin.Begin);
                allStream?.Seek(0, SeekOrigin.Begin);

                this.MIRINFO(mir, stdfid);
                rcs = new WriteToRcs();
                rcs.Init(mir, dmgr, fileName);
                rcs.MIRINFO(mirinfo);


            }
            catch (Exception ex)
            {
                Logs.Error("初始化WriteToCsv", ex);
            }


        }
        public void SDRINFO(SDR sdr)
        {
            try
            {
                sdrinfo = new string[] {
                "--- Site details:"+Environment.NewLine+
                "Site group",sdr.SITEGRP.ToString()+Environment.NewLine+
                "Testing sites","\""+sdr.SITENUM.Replace(Environment.NewLine," ").Replace(","," ")+"\""+Environment.NewLine+
                "Handler ID",sdr.HANDID.Replace(Environment.NewLine," ").Replace(","," ")+Environment.NewLine+
                "Load board ID",sdr.LOADID.Replace(Environment.NewLine," ").Replace(","," ")+Environment.NewLine
            };
                rcs?.SDRINFO(sdrinfo);
            }
            catch (Exception ex)
            {
                Logs.Error("sdr信息获取", ex);
            }
        }
        public void Analyze(BaseEntity entity)
        {
            if (entity.StdfId.ToString() != stdfid)
            {
                return;
            }

            rcs?.Analyze(entity);

            try
            {
                if (contentStream == null)
                {
                    return;
                }
                PTR ptr = entity as PTR;
                FTR ftr = entity as FTR;
                PRR prr = entity as PRR;


                if (row1 == null)
                {
                    row1 = new List<string>();
                    row2 = new List<string>();
                    row3 = new List<string>();
                    row4 = new List<string>();
                    row5 = new List<string>();
                    // 写入固定项
                    row1.AddRange(new string[] { "PARTID", "SBIN", "HBIN", "SITE", "TIME" });
                    row2.AddRange(new string[] { "Tests#", "", "", "", "" });
                    row3.AddRange(new string[] { "Unit", "", "", "", "sec." });
                    row4.AddRange(new string[] { "HighL", "", "", "", "" });
                    row5.AddRange(new string[] { "LowL", "", "", "", "" });
                }

                #region ptr数据写入
                if (ptr != null)
                {
                    string key = ptr.TESTTXT + ptr.TESTNUM;
                    // 记录所有的 TESTNUM  
                    if (!TESTTXTNUM.ContainsKey(key))
                    {
                        TESTTXTNUM.Add(key, TESTTXTNUM.Count);
                        // 新的TESTNUM加入时候初始化数据
                        row1.Add(ptr.TESTTXT);
                        row2.Add(ptr.TESTNUM.ToString());
                        row3.Add(ptr.UNITS);
                        row4.Add(ptr.HILIMIT.ToString());
                        row5.Add(ptr.LOLIMIT.ToString());
                    }


                    // 获取ptr数据写入位置
                    int writeIndex = startCol + TESTTXTNUM[ptr.TESTTXT + ptr.TESTNUM];

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

                #region ftr数据写入
                if (ftr != null)
                {
                    string key = ftr.TESTTXT + ftr.TESTNUM;
                    // 记录所有的 TESTNUM  
                    if (!TESTTXTNUM.ContainsKey(key))
                    {
                        TESTTXTNUM.Add(key, TESTTXTNUM.Count);
                        // 新的TESTNUM加入时候初始化数据
                        row1.Add(ftr.TESTTXT);
                        row2.Add(ftr.TESTNUM.ToString());
                        row3.Add("");
                        row4.Add("");
                        row5.Add("");
                    }


                    // 获取ptr数据写入位置
                    int writeIndex = startCol + TESTTXTNUM[ftr.TESTTXT + ftr.TESTNUM];

                    // 获取到不在TESTNUM列表中的数据 退出
                    if (writeIndex < startCol)
                    {
                        return;
                    }


                    // 根据sitenum值创建数据缓存对象 默认 下标是0
                    while (dataCache.Count < ftr.SITENUM + 1)
                    {
                        dataCache.Add(new List<string>());
                    }

                    // 插入空白字符给PRR数据占位
                    while (dataCache[ftr.SITENUM].Count < startCol)
                    {
                        dataCache[ftr.SITENUM].Add("");
                    }

                    // 在当前ptr之前如果有跳 TESTNUM的 插入空白占位
                    while (dataCache[ftr.SITENUM].Count < writeIndex + 1)
                    {
                        dataCache[ftr.SITENUM].Add("");
                    }

                    // 插入测试项的值 完事~
                    dataCache[ftr.SITENUM][writeIndex] = ftr.VECTNAM;


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
                        Logs.Error("", ex);
                    }
                }
                #endregion
            }
            catch (Exception ex)
            {
                Logs.Error("CSV数据写入", ex);
            }
        }
        private void Write()
        {
            try
            {
                if (contentStream == null)
                {
                    return;
                }
                for (int i = 0; i < dataCache.Count; i++)
                {
                    if (dataCache[i] != null)
                    {
                        byte[] buffer = Encoding.UTF8.GetBytes(string.Join(",", dataCache[i].ToArray()) + Environment.NewLine);
                        contentStream.Write(buffer, 0, buffer.Length);
                    }
                }
                contentStream.Flush();

                dataCache.Clear();
                dataCache = new List<List<string>>();
            }
            catch (Exception ex)
            {
                Logs.Error("", ex);
            }
        }
        public void End(DatabaseManager dmgr, MRR mrr)
        {

            rcs?.End(dmgr, mrr);
            try
            {
                if (contentStream == null)
                {
                    return;
                }
                contentStream?.Flush();
                contentStream?.Close();
                contentStream?.Dispose();
                contentStream = null;

                if (row1 != null)
                {
                    // 写入最终头部数据

                    byte[] buffer = Encoding.UTF8.GetBytes(string.Join(",", mirinfo).Replace("@FinishTime", DateHelper.GetDate(mrr.FINISHT).ToString()) + Environment.NewLine);
                    allStream?.Write(buffer, 0, buffer.Length);

                    buffer = Encoding.UTF8.GetBytes(string.Join(",", sdrinfo) + Environment.NewLine);
                    allStream?.Write(buffer, 0, buffer.Length);

                    buffer = Encoding.UTF8.GetBytes(string.Join(",", row1.ToArray()) + Environment.NewLine);
                    allStream?.Write(buffer, 0, buffer.Length);
                    buffer = Encoding.UTF8.GetBytes(string.Join(",", row2.ToArray()) + Environment.NewLine);
                    allStream?.Write(buffer, 0, buffer.Length);
                    buffer = Encoding.UTF8.GetBytes(string.Join(",", row3.ToArray()) + Environment.NewLine);
                    allStream?.Write(buffer, 0, buffer.Length);
                    buffer = Encoding.UTF8.GetBytes(string.Join(",", row4.ToArray()) + Environment.NewLine);
                    allStream?.Write(buffer, 0, buffer.Length);
                    buffer = Encoding.UTF8.GetBytes(string.Join(",", row5.ToArray()) + Environment.NewLine);
                    allStream?.Write(buffer, 0, buffer.Length);
                    allStream?.Flush();

                    using (FileStream fs = new FileStream(fullName + ".tmp", FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        byte[] contentBuffer = new byte[2048];
                        int length = fs.Read(contentBuffer, 0, contentBuffer.Length);
                        while (length > 0)
                        {
                            allStream?.Write(contentBuffer, 0, length);
                            length = fs.Read(contentBuffer, 0, contentBuffer.Length);
                        }
                    }
                }

                allStream?.Flush();
                allStream?.Close();
                allStream?.Dispose();
                allStream = null;

                File.Delete(fullName + ".tmp");

                string FILEPATH = ZipHelper.CompressFile(fullName, Path.Combine(Config.CsvFileForlder, "ZIP", DateTime.Now.ToString("yyyyMMdd")));

                File.Delete(fullName);

                dmgr.ExecuteNonQuery("INSERT INTO CSVPATH(STDFID,FILEPATH,LOTID)VALUES(:STDFID,:FILEPATH,:LOTID)", new { FILEPATH, mir?.LOTID, STDFID = mir?.StdfId });

            }
            catch (Exception ex)
            {
                if (File.Exists(fullName + ".tmp"))
                {
                    File.Delete(fullName + ".tmp");
                }

                if (File.Exists(fullName))
                {
                    File.Delete(fullName);
                }
                Logs.Error($"in end:{fullName}", ex);
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // 要检测冗余调用
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                this.End(new DatabaseManager(), new MRR { FINISHT = DateHelper.GetTimestamp(DateTime.Now) });
                this.contentStream?.Dispose();
                this.allStream?.Dispose();
                this.contentStream = null;
                this.allStream = null;
                this.dataCache = null;
                this.row1 = null;
                this.row2 = null;
                this.row3 = null;
                this.row4 = null;
                this.row5 = null;
                this.TESTTXTNUM = null;


                if (disposing)
                {
                    try
                    {
                        if (File.Exists(fullName + ".tmp"))
                        {
                            File.Delete(fullName + ".tmp");
                        }
                        if (File.Exists(fullName))
                        {
                            File.Delete(fullName);
                        }
                    }
                    catch
                    {
                    }
                }

                // TODO: 释放未托管的资源(未托管的对象)并在以下内容中替代终结器。
                // TODO: 将大型字段设置为 null。

                disposedValue = true;
            }
        }

        // TODO: 仅当以上 Dispose(bool disposing) 拥有用于释放未托管资源的代码时才替代终结器。
        ~WriteToCsv()
        {
            // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
            Dispose(false);
        }

        // 添加此代码以正确实现可处置模式。
        public void Dispose()
        {
            rcs?.Dispose();
            // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
            Dispose(true);
            // TODO: 如果在以上内容中替代了终结器，则取消注释以下行。
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}

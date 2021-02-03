using Newtonsoft.Json.Linq;
using TDASDataParser.StdfTypes;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using TDASCommon;
using TDASDataParser.Csv;
using System.Linq;
using System.Threading;
using TDASDataParser.Rules;

namespace TDASDataParser
{
    public class StaticReader : BaseReader
    {
        public delegate void ReportProgress(double progress);

        public event ReportProgress OnReportProgress;

        WriteToCsv writeToCsv;

        RulesFactory ruleFactory;

        public bool CreateCsv
        {
            get
            {
                return writeToCsv != null;
            }
            set
            {
                if (value)
                {
                    writeToCsv = new WriteToCsv();
                }
            }
        }

        public bool ToDb { get; set; } = true;

        public bool RuleTest
        {
            get
            {
                return ruleFactory != null;
            }
            set
            {
                if (value)
                {
                    ruleFactory = new RulesFactory();
                }
            }
        }

        double currPercent = 0;

        protected Dictionary<string, List<BaseEntity>> dicEntities { get; set; } = new Dictionary<string, List<BaseEntity>>();


        public StaticReader()
        {
            Global.TypePropsInit();
        }
        public override int Read(string filePath)
        {
            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return Read(filePath, stream);
            }
        }
        int counts = 0;
        public int Read(string filePath, Stream fileStream)
        {
            long offset = LoadOffset(filePath);
            int stdfid = -1;

            try
            {
                fileStream.Seek(offset, SeekOrigin.Begin);

                if (fileStream.Position < fileStream.Length)
                {
                    stdfid = GetStdfId(Path.GetFileName(filePath), filePath);
                
                
                    DataParameters param = new DataParameters();

                    param.Add("v_stdfid", stdfid, DbType.Int32, Direction.Input);
                    dmgr.ExecuteProcedureNonQuery("P_DELETESTDFDATA", param);
                }

                Console.WriteLine($"开始解析:{DateTime.Now.ToString("HH:mm:ss,fff")}");
                Logs.Trace($"开始解析文件:[{stdfid}]-{Path.GetFileName(filePath)}");
                Stopwatch sw = new Stopwatch();
                sw.Start();
                MRR mrr = null;
                while (fileStream.Position < fileStream.Length)
                {

                    byte[] header = new byte[4];
                    fileStream.Read(header, 0, 4);

                    int length = BitConverter.ToInt16(header, 0);
                    int typ = header[2];
                    int sub = header[3];

                    byte[] data = new byte[length];
                    fileStream.Read(data, 0, length);

                    BaseEntity entity = DataToEntity(typ, sub, data, stdfid, out string type);


                    StaticDeal(type, entity);

                    if (type == "MRR")
                    {
                        mrr = entity as MRR;
                    }

                    double percent = Math.Round(Convert.ToDouble(fileStream.Position * 1.000000 / fileStream.Length * 1.000000), 4);

                    if (currPercent != percent)
                    {
                        currPercent = percent;
                        OnReportProgress?.Invoke(currPercent);
                    }

                    counts++;

                    if (ParseCount >= Config.SubmitLimit)
                    {
                        SaveOffset(filePath, stdfid, fileStream);
                    }

                }
                Submit();
                SaveOffset(filePath, stdfid, fileStream);
                sw.Stop();
                writeToCsv?.End(dmgr, mrr);
                Console.WriteLine($"完成解析,解析数据{counts}条,用时{sw.ElapsedMilliseconds / 1000}秒,{DateTime.Now.ToString("HH:mm:ss,fff")}");
                Logs.Trace($"完成解析文件:[{stdfid}]-{Path.GetFileName(filePath)}");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return stdfid;
        }
        /// <summary>
        /// 静态解析前,根据文件信息,获取之前加载的文件流位置
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private long LoadOffset(string filePath)
        {
            DumpItem dump = null;

            string dumpPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dump.json");

            if (File.Exists(dumpPath))
            {
                Dictionary<string, DumpItem> dctItems = JToken.Parse(File.ReadAllText(dumpPath)).ToObject<Dictionary<string, DumpItem>>();
                if (dctItems.ContainsKey(Path.GetFileName(filePath)))
                {
                    dump = dctItems[Path.GetFileName(filePath)];
                }
            }
            if (dump != null)
            {
                return dump.Offset;
            }
            else
            {
                return 0L;
            }

        }

        private void SaveOffset(string filePath, int stdfId, Stream fileStream)
        {
            return;
            //string dumpPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dump.json");
            //Dictionary<string, DumpItem> dicDump = new Dictionary<string, DumpItem>();
            //lock (dumpPath)
            //{
            //    if (File.Exists(dumpPath))
            //    {
            //        dicDump = JToken.Parse(File.ReadAllText(dumpPath)).ToObject<Dictionary<string, DumpItem>>();
            //    }

            //    dicDump[Path.GetFileName(filePath)] = new DumpItem { StdfId = stdfId, Offset = fileStream.Position, Length = fileStream.Length };

            //    File.WriteAllText(dumpPath, JToken.FromObject(dicDump).ToString());
            //}
        }

        private void StaticDeal(string type, BaseEntity entity)
        {
            if (entity == null)
            {
                return;
            }
            if (ToDb)
            {
                if (!dicEntities.ContainsKey(type))
                {
                    dicEntities[type] = new List<BaseEntity>();
                }

                if (Config.SubmitType.Contains(type))
                {
                    dicEntities[type].Add(entity);
                }
            }

            if (type == "MIR")
            {
                MIR mir = entity as MIR;

                SYS_REPLACE sp = new SYS_REPLACE();

                sp.CheckMIR(mir, dmgr);

                var newD = mir.ReplaceData();

                MIR nmir = new MIR();
                nmir.LoadData(newD, null);

                writeToCsv?.Init(mir, dmgr);
                ruleFactory?.Init(mir, dmgr);

            }

            ruleFactory?.Match(type, entity, dmgr);


            if (type == "SDR")
            {
                writeToCsv?.SDRINFO(entity as SDR);
            }

            if (type == "PTR")
            {
                writeToCsv?.Analyze(entity);
            }
            else if (type == "PRR")
            {
                writeToCsv?.Analyze(entity);
            }

            if (type == "MRR")
            {
                writeToCsv?.End(dmgr, entity as MRR);
            }

            if (ParseCount >= Config.SubmitLimit)
            {
                Submit();
            }
        }

        private void Submit()
        {
            if (!ToDb)
            {
                return;
            }
            foreach (var type in dicEntities.Keys)
            {
                if (dicEntities[type].Count == 0)
                {
                    continue;
                }
                string sql = Global.DicSQL[type];

                //if (new string[] { "PTR", "PRR" }.Contains(type))
                //{
                //    sql = sql.Replace(type, $"{type}_{dicEntities[type][0].StdfId}");
                //}

                DataHelper.SubmitEntities(type, dicEntities[type].ToArray(), sql, dmgr.ConnectionString, null);
                dicEntities[type].Clear();
            }
            ParseCount = 0;
        }
    }

    public class DumpItem
    {
        public int StdfId { get; set; }

        public long Offset { get; set; }

        public long Length { get; set; }
    }

}

using System;
using System.Data;
using System.Linq;
using System.Threading;
using TDASCommon;
using TDASDataParser.StdfTypes;

namespace TDASDataParser
{
    public class BaseReader : IParse
    {

        public DatabaseManager dmgr = new DatabaseManager();

        public int ParseCount = 0;

        /// <summary>
        /// 开始解析,获取stdfid
        /// </summary>
        /// <param name="fileName">文件名称</param>
        /// <param name="path">存储路径</param>
        /// <returns></returns>
        public int GetStdfId(string fileName, string path)
        {
            lock (Global.StdfidLock)
            {
                int stdfid = -1;
                int tryCount = 0;
                try
                {
                    DataParameters param = new DataParameters();

                    param.Add("V_FILENAM", fileName, DbType.String, Direction.Input);
                    param.Add("V_PATH", path, DbType.String, Direction.Input);
                    param.Add("V_STDFID", null, DbType.Int32, Direction.Output);
                    // 当获取stdfid失败时候,允许重试3次
                    while (stdfid == -1 && tryCount < 3)
                    {
                        try
                        {
                            stdfid = Convert.ToInt32(dmgr.ExecuteProcedure("P_GET_STDFID", param)["V_STDFID"]);
                        }
                        catch (Exception ex)
                        {
                            Logs.Error($"getStdfid-[{fileName}]", ex);
                            stdfid = -1;
                            tryCount++;
                            Thread.Sleep(2000);
                        }
                    }

                    if (stdfid == -1)
                    {
                        stdfid = dmgr.ExecuteInteger("SELECT MAX(STDFID)*-1 FROM MIR");
                    }
                }
                catch (Exception ex)
                {
                    Logs.Error("getStdfid", ex);
                }
                return stdfid;
            }
        }


        #region byte[]数据转换成对象 & 批量对象数据插入缓存
        public BaseEntity DataToEntity(int typ, int sub, byte[] data, int stdfid, out string type)
        {
            BaseEntity entity = null;
            type = "";

            #region 解析结构
            try
            {
                switch (typ)
                {
                    case 0:
                        switch (sub)
                        {
                            case 10:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("FAR"))
                                {
                                    entity = new FAR(data) { StdfId = stdfid };
                                    type = "FAR";
                                }
                                break;
                            case 20:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("ATR"))
                                {
                                    entity = new ATR(data) { StdfId = stdfid };
                                    type = "ATR";
                                }
                                break;
                        }
                        break;
                    case 1:
                        switch (sub)
                        {
                            case 10:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("MIR"))
                                {
                                    entity = new MIR(data) { StdfId = stdfid };
                                    type = "MIR";
                                }
                                break;
                            case 20:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("MRR"))
                                {
                                    entity = new MRR(data) { StdfId = stdfid };
                                    type = "MRR";
                                }
                                break;
                            case 30:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("PCR"))
                                {
                                    entity = new PCR(data) { StdfId = stdfid };
                                    type = "PCR";
                                }
                                break;
                            case 40:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("HBR"))
                                {
                                    entity = new HBR(data) { StdfId = stdfid };
                                    type = "HBR";
                                }
                                break;
                            case 50:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("SBR"))
                                {
                                    entity = new SBR(data) { StdfId = stdfid };
                                    type = "SBR";
                                }
                                break;
                            case 60:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("PMR"))
                                {
                                    entity = new PMR(data) { StdfId = stdfid };
                                    type = "PMR";
                                }
                                break;
                            case 62:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("PGR"))
                                {
                                    entity = new PGR(data) { StdfId = stdfid };
                                    type = "PGR";
                                }
                                break;
                            case 63:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("PLR"))
                                {
                                    entity = new PLR(data) { StdfId = stdfid };
                                    type = "PLR";
                                }
                                break;
                            case 70:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("RDR"))
                                {
                                    entity = new RDR(data) { StdfId = stdfid };
                                    type = "RDR";
                                }
                                break;
                            case 80:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("SDR"))
                                {
                                    entity = new SDR(data) { StdfId = stdfid };
                                    type = "SDR";
                                }
                                break;
                        }
                        break;
                    case 2:
                        switch (sub)
                        {
                            case 10:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("WIR"))
                                {
                                    entity = new WIR(data) { StdfId = stdfid };
                                    type = "WIR";
                                }
                                break;
                            case 20:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("WRR"))
                                {
                                    entity = new WRR(data) { StdfId = stdfid };
                                    type = "WRR";
                                }
                                break;
                            case 30:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("WCR"))
                                {
                                    entity = new WCR(data) { StdfId = stdfid };
                                    type = "WCR";
                                }
                                break;
                        }
                        break;
                    case 5:
                        switch (sub)
                        {
                            case 10:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("PIR"))
                                {
                                    entity = new PIR(data) { StdfId = stdfid };
                                    type = "PIR";
                                }
                                break;
                            case 20:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("PRR"))
                                {
                                    entity = new PRR(data) { StdfId = stdfid };
                                    entity.CalPartId(stdfid);
                                    type = "PRR";
                                }
                                break;
                        }
                        break;
                    case 10:
                        switch (sub)
                        {
                            case 30:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("TSR"))
                                {
                                    entity = new TSR(data) { StdfId = stdfid };
                                    type = "TSR";
                                }
                                break;
                        }
                        break;
                    case 15:
                        switch (sub)
                        {
                            case 10:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("PTR"))
                                {
                                    entity = new PTR(data) { StdfId = stdfid };
                                    entity.CalPartId(stdfid);
                                    type = "PTR";
                                }
                                break;
                            case 15:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("MPR"))
                                {
                                    entity = new MPR(data) { StdfId = stdfid };
                                    type = "MPR";
                                }
                                break;
                            case 20:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("FTR"))
                                {
                                    entity = new FTR(data) { StdfId = stdfid };
                                    type = "FTR";
                                }
                                break;
                        }
                        break;
                    case 20:
                        switch (sub)
                        {
                            case 10:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("BPS"))
                                {
                                    entity = new BPS(data) { StdfId = stdfid };
                                    type = "BPS";
                                }
                                break;
                            case 20:
                                // new EPS().ToObject(data);
                                break;
                        }
                        break;
                    case 50:
                        switch (sub)
                        {
                            case 10:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("GDR"))
                                {
                                    entity = new GDR(data) { StdfId = stdfid };
                                    type = "GDR";
                                }
                                break;
                            case 30:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("DTR"))
                                {
                                    entity = new DTR(data) { StdfId = stdfid };
                                    type = "DTR";
                                }
                                break;
                        }
                        break;
                    case 180:
                        break;
                    case 181:
                        break;
                }
            }
            catch (Exception ex)
            {
                Logs.Error($"{stdfid}{Environment.NewLine}{typ},{sub}{Environment.NewLine}{string.Join(",", data)}{Environment.NewLine}", ex);
            }
            #endregion

            if (entity != null)
            {
                ParseCount++;
            }
            return entity;
        }

        public string GetDataType(int typ, int sub)
        {
            string type = "";

            #region 解析结构
            try
            {
                switch (typ)
                {
                    case 0:
                        switch (sub)
                        {
                            case 10:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("FAR"))
                                {
                                    type = "FAR";
                                }
                                break;
                            case 20:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("ATR"))
                                {
                                    type = "ATR";
                                }
                                break;
                        }
                        break;
                    case 1:
                        switch (sub)
                        {
                            case 10:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("MIR"))
                                {
                                    type = "MIR";
                                }
                                break;
                            case 20:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("MRR"))
                                {
                                    type = "MRR";
                                }
                                break;
                            case 30:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("PCR"))
                                {
                                    type = "PCR";
                                }
                                break;
                            case 40:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("HBR"))
                                {
                                    type = "HBR";
                                }
                                break;
                            case 50:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("SBR"))
                                {
                                    type = "SBR";
                                }
                                break;
                            case 60:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("PMR"))
                                {
                                    type = "PMR";
                                }
                                break;
                            case 62:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("PGR"))
                                {
                                    type = "PGR";
                                }
                                break;
                            case 63:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("PLR"))
                                {
                                    type = "PLR";
                                }
                                break;
                            case 70:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("RDR"))
                                {
                                    type = "RDR";
                                }
                                break;
                            case 80:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("SDR"))
                                {
                                    type = "SDR";
                                }
                                break;
                        }
                        break;
                    case 2:
                        switch (sub)
                        {
                            case 10:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("WIR"))
                                {
                                    type = "WIR";
                                }
                                break;
                            case 20:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("WRR"))
                                {
                                    type = "WRR";
                                }
                                break;
                            case 30:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("WCR"))
                                {
                                    type = "WCR";
                                }
                                break;
                        }
                        break;
                    case 5:
                        switch (sub)
                        {
                            case 10:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("PIR"))
                                {
                                    type = "PIR";
                                }
                                break;
                            case 20:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("PRR"))
                                {
                                    type = "PRR";
                                }
                                break;
                        }
                        break;
                    case 10:
                        switch (sub)
                        {
                            case 30:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("TSR"))
                                {
                                    type = "TSR";
                                }
                                break;
                        }
                        break;
                    case 15:
                        switch (sub)
                        {
                            case 10:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("PTR"))
                                {
                                    type = "PTR";
                                }
                                break;
                            case 15:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("MPR"))
                                {
                                    type = "MPR";
                                }
                                break;
                            case 20:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("FTR"))
                                {
                                    type = "FTR";
                                }
                                break;
                        }
                        break;
                    case 20:
                        switch (sub)
                        {
                            case 10:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("BPS"))
                                {
                                    type = "BPS";
                                }
                                break;
                            case 20:
                                // new EPS().ToObject(data);
                                break;
                        }
                        break;
                    case 50:
                        switch (sub)
                        {
                            case 10:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("GDR"))
                                {
                                    type = "GDR";
                                }
                                break;
                            case 30:
                                if (Config.ParseType[0] == "*" || Config.ParseType.Contains("DTR"))
                                {
                                    type = "DTR";
                                }
                                break;
                        }
                        break;
                    case 180:
                        break;
                    case 181:
                        break;
                }
            }
            catch (Exception ex)
            {
                Logs.Error("STDF结构解析异常", ex);
            }
            #endregion

            return type;
        }
       
        #endregion

        #region 接口对象
        public virtual int Read(string fileName, long streamOffset, byte[] data, long timestamp, string ip)
        {
            return 0;
        }

        public virtual int Read(string filePath)
        {
            return 0;
        }
        public virtual void Dispose()
        {
            // throw new NotImplementedException();
        }
        #endregion
    }
}

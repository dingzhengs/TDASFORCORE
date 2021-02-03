using TDASDataParser.StdfTypes;
using System;
using System.Collections.Generic;
using System.Data;
using TDASCommon;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;

namespace TDASDataParser.Rules
{
    public class RulesFactory
    {
        List<SYS_RULES_TESTRUN> LstRules;
        List<V_RULES_RCS> LstRcsRules;
        MIR mir;
        PRRRule prrrule;
        PTRRule ptrrule;
        PTRRcsRule ptrrcsrule;
        TouchDown td;

        /// <summary>
        /// 初始化rule信息
        /// </summary>
        /// <param name="mir"></param>
        public void Init(MIR mir, DatabaseManager dmgr)
        {
            try
            {
                switch (mir.FLOWID)
                {
                    case "R0":
                        mir.FLOWID = "P1";
                        break;
                    case "R0.1":
                        mir.FLOWID = "P2";
                        break;
                    case "R0.2":
                        mir.FLOWID = "P3";
                        break;
                    case "R0.3":
                        mir.FLOWID = "P4";
                        break;
                    case "R0.4":
                        mir.FLOWID = "P5";
                        break;
                    case "R0.5":
                        mir.FLOWID = "P6";
                        break;
                    case "R0.6":
                        mir.FLOWID = "P7";
                        break;
                    case "R0.7":
                        mir.FLOWID = "P8";
                        break;
                    case "R0.8":
                        mir.FLOWID = "P9";
                        break;
                    case "R0.9":
                        mir.FLOWID = "P10";
                        break;
                    case "R0.10":
                        mir.FLOWID = "P11";
                        break;
                }



                prrrule = new PRRRule();
                ptrrule = new PTRRule();
                ptrrcsrule = new PTRRcsRule();
                this.mir = mir;
                td = new TouchDown(mir.StdfId);

                LstRcsRules = GetRcsRoles(mir, dmgr);

                DatabaseManager dmgr_new = new DatabaseManager();
                DataTable dt_rcs = dmgr_new.ExecuteDataTable("select * from SYS_RCS_RULES_HEAD where stdfid='" + mir.StdfId + "'");
                DataTable dt_rcs_rule = dmgr_new.ExecuteDataTable(@"select * from sys_rcs_rules_testrun t
where upper(t.product) = upper('" + mir.PARTTYP + "') and upper(t.custcode) = upper('" + mir.FAMLYID + "') and upper(t.insertion) = upper('" + mir.TESTCOD + "')");
                if (dt_rcs.Rows.Count < 1 && dt_rcs_rule.Rows.Count > 0)
                {
                    string filename = dmgr_new.ExecuteScalar("select filename from stdffile where stdfid='" + mir.StdfId + "'").ToString();
                    string rcs_sql = @"insert into SYS_RCS_RULES_HEAD(stdfid, lotid, filename, tstrtyp, testcod, product,FIXED,UNIFORMITY,UNIQUENESS,CALCULATE,CONSECUTIVE,EXIST) 
                                            values ('" + mir.StdfId + "','" + mir.LOTID + "','" + filename + "','" + mir.TSTRTYP + "','" + mir.TESTCOD + "','" + mir.PARTTYP + "','pass','pass','pass','pass','pass','pass')";
                    dmgr_new.ExecuteNonQuery(rcs_sql);
                }

                //if (mir.TESTCOD != null)
                //{
                //    if (!mir.TESTCOD.Contains("FT"))
                //    {
                //        if (!(mir.PARTTYP?.ToUpper() == "HI1151GNCV210" || mir.PARTTYP?.ToUpper() == "HI1151SGNCV208" ||
                //            mir.PARTTYP?.ToUpper() == "HI5620GNCV100" || mir.PARTTYP?.ToUpper() == "HI5621GNCV100" ||
                //            mir.PARTTYP?.ToUpper() == "HI3798MRBCV20100000H" || mir.PARTTYP?.ToUpper() == "HI3798MRBCV2010D000H" ||
                //            mir.PARTTYP?.ToUpper() == "HI3798MRBCV30100000H" || mir.PARTTYP?.ToUpper() == "HI3798MRBCV3010D000H" ||
                //            mir.PARTTYP?.ToUpper() == "HI3798MRBCV31100000" || mir.PARTTYP?.ToUpper() == "HI3798MRBCV3110D000"
                //            ))
                //        {
                //            return;
                //        }
                //    }
                //}
                //else { return; }

                int totalcount = 0;
                int Hfieldcount = 0;
                int Sfieldcount = 0;
                DataTable dt_rules = dmgr.ExecuteDataTable("select * from sys_rules_testrun where upper(product)='" + mir.PARTTYP.ToUpper() + "' and stdtype='1'");
                for (int i = 0; i < dt_rules.Rows.Count; i++)
                {
                    DataTable dt = dmgr.ExecuteDataTable("select stdfid from mir where lotid='" + mir.LOTID + "' and substr(testcod,0,2)='FT' and NODENAM='" + mir.NODENAM + "' and stdfid<>'" + mir.StdfId + "' and substr(flowid,0,1)='" + mir.FLOWID.Substring(1, 1) + "'");
                    for (int j = 0; j < dt.Rows.Count; j++)
                    {
                        totalcount += Convert.ToInt32(dmgr.ExecuteScalar($"select count(*) from prr where stdfid={dt.Rows[j][0].ToString()}"));
                        if (dt_rules.Rows[i]["BINS_TYPE"].ToString() == "HBin")
                        {
                            Hfieldcount += Convert.ToInt32(dmgr.ExecuteScalar($"select count(*) from prr where  stdfid={dt.Rows[j][0].ToString()} and hardbin in ({dt_rules.Rows[i]["BINS"].ToString().Replace("HBin", "").Replace("+", ",")}) and softbin<>1"));
                        }
                        else
                        {
                            Sfieldcount += Convert.ToInt32(dmgr.ExecuteScalar($"select count(*) from prr where stdfid={dt.Rows[j][0].ToString()} and softbin in ({dt_rules.Rows[i]["BINS"].ToString().Replace("SBin", "").Replace("+", ",")})"));
                        }
                    }
                    //判断sys_rules_testrun_lot是否有值
                    DataTable dt_lot = dmgr.ExecuteDataTable("select * from SYS_RULES_TESTRUN_LOT where H_GUID='" + dt_rules.Rows[i]["GUID"].ToString() + "' and LOTID='" + mir.LOTID + "'");
                    if (dt_lot.Rows.Count > 0)
                    {
                        //更新表
                        string update_sql = "";
                        if (dt_rules.Rows[i]["BINS_TYPE"].ToString() == "HBin")
                        {
                            update_sql = @"update SYS_RULES_TESTRUN_LOT set YIELDCOUNT='" + Hfieldcount + "',TOTALCOUNT='" + totalcount + "' where H_GUID='" + dt_rules.Rows[i]["GUID"].ToString() + "' and LOTID='" + mir.LOTID + "'";
                        }
                        else
                        {
                            update_sql = @"update SYS_RULES_TESTRUN_LOT set YIELDCOUNT='" + Sfieldcount + "',TOTALCOUNT='" + totalcount + "' where H_GUID='" + dt_rules.Rows[i]["GUID"].ToString() + "' and LOTID='" + mir.LOTID + "'";
                        }
                        dmgr.ExecuteNonQuery(update_sql);
                    }
                    else
                    {
                        //插入表
                        string insert_sql = @"";
                        if (dt_rules.Rows[i]["BINS_TYPE"].ToString() == "HBin")
                        {
                            insert_sql = @"insert into SYS_RULES_TESTRUN_LOT(H_GUID,LOTID,YIELDCOUNT,TOTALCOUNT) values ('" + dt_rules.Rows[i]["GUID"].ToString() + "','" + mir.LOTID + "','" + Hfieldcount + "','" + totalcount + "')";
                        }
                        else
                        {
                            insert_sql = @"insert into SYS_RULES_TESTRUN_LOT(H_GUID,LOTID,YIELDCOUNT,TOTALCOUNT) values ('" + dt_rules.Rows[i]["GUID"].ToString() + "','" + mir.LOTID + "','" + Sfieldcount + "','" + totalcount + "')";
                        }
                        dmgr.ExecuteNonQuery(insert_sql);
                    }
                }

                LstRules = GetRoles(mir, dmgr);
            }
            catch (Exception ex)
            {
                Logs.Error( "", ex);
            }


        }
        // 消息
        public void Match(string type, BaseEntity entity, DatabaseManager dmgr)
        {

            switch (type)
            {
                case "PTR":
                    td?.AddPTR(entity as PTR);
                    break;
                case "PRR":
                    td?.AddPRR(entity as PRR);
                    break;
            }

            try
            {
                if (LstRules != null && LstRules.Count > 0)
                {
                    //根据MIR数据去找指定文件夹有没有文件
                    //string factory = "";
                    //if (type == "MIR")
                    //{
                    //    try
                    //    {
                    //        factory = dmgr.ExecuteScalar("select device_group from sys_eqp_group where eqp_name='" + mir.NODENAM + "'").ToString();
                    //    }
                    //    catch (Exception)
                    //    {
                    //        factory = "";
                    //    }
                    //}
                    //ptrrule?.WriteTxt(type, entity, factory);
                    ptrrule?.Attach(type, entity, LstRules);
                    prrrule?.Attach(type, entity);
                    switch (type)
                    {
                        case "PTR":
                            ptrrule?.Match(LstRules, entity, mir, dmgr);
                            break;
                        case "PRR":

                            bool losttype = false;
                            var lostRules = LstRules.Where(p => p.TYPE == "LOSTTESTTXTTRIGGER");

                            //判断文件的存在
                            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mould", mir.PARTTYP + "_" + mir.JOBNAM + ".txt");
                            if (File.Exists(folder))
                            {
                                //存在文件
                                losttype = true;
                            }
                            else
                            {
                                //不存在,状态
                                losttype = false;
                            }

                            foreach (var rule in lostRules)
                            {
                                if (rule.DEVICEGROUP?.ToUpper() == "FIRSTPASS")
                                {
                                    if (string.IsNullOrEmpty(mir.FLOWID) || mir.FLOWID?.Substring(0, 1).ToUpper() != "P")
                                    {
                                        continue;
                                    }
                                }
                                ptrrule.LOSTTESTTXTTRIGGER(rule, td, dmgr, mir, losttype);
                            }

                            var aRules = LstRules.Where(p => p.TYPE == "SITETOSITEPARAMETRICTESTSTATISTICDELTATRIGGER");

                            foreach (var rule in aRules)
                            {
                                if (rule.DEVICEGROUP?.ToUpper() == "FIRSTPASS")
                                {
                                    if (string.IsNullOrEmpty(mir.FLOWID) || mir.FLOWID?.Substring(0, 1).ToUpper() != "P")
                                    {
                                        continue;
                                    }
                                }
                                ptrrule.SITETOSITEPARAMETRICTESTSTATISTICDELTATRIGGER(rule, td, dmgr, mir);
                            }

                            var ptsAddRules = LstRules.Where(p => p.TYPE == "PTSADDTRIGGER");

                            foreach (var rule in ptsAddRules)
                            {
                                if (rule.DEVICEGROUP?.ToUpper() == "FIRSTPASS")
                                {
                                    if (string.IsNullOrEmpty(mir.FLOWID) || mir.FLOWID?.Substring(0, 1).ToUpper() != "P")
                                    {
                                        continue;
                                    }
                                }
                                ptrrule.PTSADDTRIGGER(rule, td, dmgr, mir);
                            }

                            var ptsCutRules = LstRules.Where(p => p.TYPE == "PTSCUTTRIGGER");

                            foreach (var rule in ptsCutRules)
                            {
                                if (rule.DEVICEGROUP?.ToUpper() == "FIRSTPASS")
                                {
                                    if (string.IsNullOrEmpty(mir.FLOWID) || mir.FLOWID?.Substring(0, 1).ToUpper() != "P")
                                    {
                                        continue;
                                    }
                                }
                                ptrrule.PTSCUTTRIGGER(rule, td, dmgr, mir);
                            }



                            var bRules = LstRules.Where(p => p.TYPE == "PARAMETRICTESTSTATISTICTRIGGER");

                            foreach (var rule in bRules)
                            {
                                if (rule.DEVICEGROUP?.ToUpper() == "FIRSTPASS")
                                {
                                    if (string.IsNullOrEmpty(mir.FLOWID) || mir.FLOWID?.Substring(0, 1).ToUpper() != "P")
                                    {
                                        continue;
                                    }
                                }
                                ptrrule.PARAMETRICTESTSTATISTICTRIGGERNEW(rule, td, dmgr, mir);
                            }


                            var cRules = LstRules.Where(p => p.TYPE == "SIGMATRIGGER");

                            foreach (var rule in cRules)
                            {
                                if (rule.DEVICEGROUP?.ToUpper() == "FIRSTPASS")
                                {
                                    if (string.IsNullOrEmpty(mir.FLOWID) || mir.FLOWID?.Substring(0, 1).ToUpper() != "P")
                                    {
                                        continue;
                                    }
                                }
                                ptrrule.SIGMATRIGGER(rule, td, dmgr, mir);
                            }

                            var dRules = LstRules.Where(p => p.TYPE == "PTRCONSECUTIVEBINTRIGGER");

                            foreach (var rule in dRules)
                            {
                                if (rule.DEVICEGROUP?.ToUpper() == "FIRSTPASS")
                                {
                                    if (string.IsNullOrEmpty(mir.FLOWID) || mir.FLOWID?.Substring(0, 1).ToUpper() != "P")
                                    {
                                        continue;
                                    }
                                }
                                ptrrule.PTRCONSECUTIVEBINTRIGGER(rule, td, dmgr, mir);
                            }



                            //var specRules = LstRules.Where(p => p.TYPE == "SPEC0-250");

                            //foreach (var rule in specRules)
                            //{
                            //    if (rule.DEVICEGROUP?.ToUpper() == "FIRSTPASS")
                            //    {
                            //        if (string.IsNullOrEmpty(mir.FLOWID) || mir.FLOWID?.Substring(0, 1).ToUpper() != "P")
                            //        {
                            //            continue;
                            //        }
                            //    }
                            //    ptrrule.SPECTRIGGER(rule, td, dmgr, mir);
                            //}

                            prrrule?.Match(LstRules, entity, mir, dmgr);
                            break;
                    }
                }
                if (LstRcsRules != null && LstRcsRules.Count > 0)
                {
                    ptrrcsrule?.Attach(type, entity, LstRcsRules);
                    //ptrrcsrule?.Match(LstRcsRules, entity, mir, dmgr, td);
                    switch (type)
                    {
                        case "PRR":
                            ptrrcsrule?.Match(LstRcsRules, entity, mir, dmgr, td);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logs.Error( $"{mir.StdfId}-RulesFactory.Match>", ex);
            }
        }

        private List<SYS_RULES_TESTRUN> GetRoles(MIR mir, DatabaseManager dmgr)
        {
            return dmgr.ExecuteEntities<SYS_RULES_TESTRUN>(@"SELECT distinct t.GUID,t.U_CODE,t.PRODUCT,t.NAME,t.FLOOR_NAME,t.ACTION,t.TRIGGERLEVEL,t.TYPE,t.BINS_TYPE,t.BINS,t.BYSITE,t.COUNT,t.DEVICEGROUP,t.MAXTYPE,t.MAXSTATUS,t.MAXVALUE,t.MINTYPE,t.MINSTATUS,t.MINVALUE,t.STATUS,t.ISUSED,t.APPROVALUSER,t.APPROVALSTATUS,t.ROLE_COUNT,t.TESTNUMBER,t.COUNTTYPE,t.BASELINE,t.ECID_CHECKTXT,t.ECID_OUTPUTTXT,t.MAIL_TYPE,t.MAIL_LIST,t.STEP,t.NOTES,t.STDTYPE,t.UNLOCKROLE,t.UNLOCKBM,t.REMARK,t.CREATOR,t.CREATE_DATE,t.IP,t.TESTTXT,t.OSPINBEGIN,t.OSPINEND,t.PTSGROUP,t.PTSVALUE,t.TRIGGERNUM,t.DIFNUM
,NVL(t1.YIELDCOUNT,0) YIELDCOUNT,NVL(t1.TOTALCOUNT,0) TOTALCOUNT FROM V_EQ_RULES_TESTRUN t
left join (select h_guid,lotid,yieldcount,totalcount from sys_rules_testrun_lot where UPPER(lotid)=UPPER(:LOTID)) t1 on t.GUID=t1.h_guid
WHERE UPPER(t.PRODUCT)=UPPER(:PARTTYP) and (UPPER(t.EQPNAME)=UPPER(:NODENAM) or t.EQPNAME ='ALL') and INSTR(UPPER(NVL(t.STEP,:TESTCOD)),UPPER(:TESTCOD))>0 and t.ISUSED=1
and UPPER(:NODENAM) NOT IN (select eqpname from sys_rules_testrun_eq_reject where H_GUID=t.GUID)",
                    new { mir.LOTID, mir.PARTTYP, mir.NODENAM, mir.TESTCOD });
        }

        private List<V_RULES_RCS> GetRcsRoles(MIR mir, DatabaseManager dmgr)
        {
            Logs.Debug(JsonConvert.SerializeObject(new { mir.PARTTYP, mir.FAMLYID, mir.TESTCOD }));
            return new DatabaseManager().ExecuteEntities<V_RULES_RCS>(@"select guid,ruletype,team,product,custcode,test_type,columname,columvalue,insertion,passflag,notexist,omitname,TESTNUM,UP_DOWN,BYSITE,DEVICEGROUP,NVL(columtype,'不等于') columtype,action,STATUS,STATUS_NUM,CAL_TYPE from sys_rcs_rules_testrun 
where UPPER(product)=UPPER(:PARTTYP) and UPPER(custcode)=UPPER(:FAMLYID) and UPPER(insertion)=UPPER(:TESTCOD) and isused='1'
union all
select guid,'漏项检查' ruletype,'' team,product,'' custcode,'' test_type,decode(bins_type,'HBin','HARDBIN','SBin','SOFTBIN','TESTTXT') columname,decode(bins_type,'HBin',count,'SBin',count,ecid_checktxt) columvalue,'' insertion,'' passflag,'' notexist,'' omitname,'' TESTNUM,0 UP_DOWN,'' BYSITE,DEVICEGROUP,'' columtype,action,'0' STATUS,0 STATUS_NUM,'' CAL_TYPE
from sys_rules_testrun where type='1TOUCHDOWN' and UPPER(product)=UPPER(:PARTTYP) and isused='1' ",
                        new { mir.PARTTYP, mir.FAMLYID, mir.TESTCOD });
        }

        #region IDisposable Support
        private bool disposedValue = false; // 要检测冗余调用

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    prrrule.Dispose();
                    ptrrule.Dispose();
                }
                LstRules = null;
                // TODO: 释放未托管的资源(未托管的对象)并在以下内容中替代终结器。
                // TODO: 将大型字段设置为 null。

                disposedValue = true;
            }
        }

        // TODO: 仅当以上 Dispose(bool disposing) 拥有用于释放未托管资源的代码时才替代终结器。
        ~RulesFactory()
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

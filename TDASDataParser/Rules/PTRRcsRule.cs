using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TDASCommon;
using TDASDataParser.StdfTypes;

namespace TDASDataParser.Rules
{
    public class PTRRcsRule : IDisposable
    {
        private Dictionary<string, List<PTR>> testTotal = new Dictionary<string, List<PTR>>();

        private Dictionary<string, double> testNumTotal = new Dictionary<string, double>();
        private Dictionary<string, double> maxSiteNum = new Dictionary<string, double>();
        private Dictionary<string, int> batch = new Dictionary<string, int>();
        private Dictionary<string, int> triggeredNum = new Dictionary<string, int>();

        // 用于分项统计各site的测试值 key.sitenum.ptr
        private Dictionary<string, Dictionary<double, List<PTR>>> testsTotal = new Dictionary<string, Dictionary<double, List<PTR>>>();

        //校验site值是否有测试到 key.testNumTotal.sitenum.boolean
        private Dictionary<string, Dictionary<double, Dictionary<double, bool>>> siteExists = new Dictionary<string, Dictionary<double, Dictionary<double, bool>>>();

        private string handler = "";

        // 参照对象

        private Dictionary<string, RCSECIDITEM> cacheLot = new Dictionary<string, RCSECIDITEM>();
        private Dictionary<string, RCSECIDITEM> refLot = new Dictionary<string, RCSECIDITEM>();
        //ECIDITEM refLot = null;

        private Dictionary<string, string[]> checkTxt = new Dictionary<string, string[]>();
        private Dictionary<string, string[]> outputTxt = new Dictionary<string, string[]>();
        //private string[] checkTxt;
        //private string[] outputTxt;


        public void Match(List<V_RULES_RCS> lstRules, BaseEntity entity, MIR mir, DatabaseManager dmgr, TouchDown td)
        {
            if (lstRules != null)
            {
                foreach (V_RULES_RCS rule in lstRules)
                {
                    try
                    {
                        if (rule.DEVICEGROUP?.ToUpper() == "FIRSTPASS")
                        {
                            if (string.IsNullOrEmpty(mir.FLOWID) || mir.FLOWID?.Substring(0, 1).ToUpper() != "P")
                            {
                                continue;
                            }
                        }

                        if (rule.RULETYPE == "固定值检查")
                        {
                            FIXED_RCSNEW(rule, dmgr, mir, rule.TEAM, td);
                        }
                        else if (rule.RULETYPE == "一致性检查")
                        {
                            ECID_RCSNEW(rule, dmgr, mir, rule.TEAM, td);
                        }
                        else if (rule.RULETYPE == "唯一性检查")
                        {
                            WAFER_RCSNEW(rule, dmgr, mir, rule.TEAM, td);
                        }
                        else if (rule.RULETYPE == "公式计算")
                        {
                            CALCULATE_RCSNEW(rule, dmgr, mir, rule.TEAM, td);
                        }
                        else if (rule.RULETYPE == "连续多少次一样")
                        {
                            CONSECUTIVE_RCSNEW(rule, dmgr, mir, rule.TEAM, td);
                        }
                        else if (rule.RULETYPE == "存在性检查")
                        {
                            EXIST_RCSNEW(rule, dmgr, mir, rule.TEAM, td);
                        }
                        else if (rule.RULETYPE == "漏项检查")
                        {
                            LOST_RCS(rule, dmgr, mir, rule.TEAM, td);
                        }
                    }
                    catch (Exception ex)
                    {
                        // 所有规则外面增加一个异常捕捉 防止一个规则有未捕获的异常跳出规则匹配
                        Logs.Error( $"{mir.StdfId}-{rule.RULETYPE}", ex);
                    }
                }
            }
        }

        private Dictionary<string, RCS_LOST_RULE> rcsLost = new Dictionary<string, RCS_LOST_RULE>();
        private void LOST_RCS(V_RULES_RCS rule, DatabaseManager dmgr, MIR mir, string team, TouchDown td)
        {
            if (td.IsTouchDownEnd && td.Index == 1)
            {
                foreach (var item in td.DicPTR)
                {
                    string omit_check = string.Format(rule.RuleKey, td.Index.ToString(), item.Key);
                    if (!rcsLost.Keys.Contains(omit_check))
                    {
                        rcsLost[omit_check] = new RCS_LOST_RULE();
                        //用来判断是不是漏项的
                        if (!string.IsNullOrEmpty(rule.COLUMVALUE))
                        {
                            // 初始化预警校验项
                            foreach (var keyVal in rule.COLUMVALUE.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                            {
                                rcsLost[omit_check].check[keyVal] = keyVal;
                            }
                        }
                    }
                    foreach (var ptr in item.Value)
                    {
                        if (rule.COLUMNAME == "HARDBIN")
                        {
                            if (td.DicPRR[ptr.SITENUM].HARDBIN.ToString() == rule.COLUMVALUE)
                            {
                                //预警漏项的报警
                                Logs.Rule( "漏HARDBIN" + ptr.PARTID);
                                Task.Run(() =>
                                {
                                    INSERTALERT_PTR(rule, mir, ptr, dmgr, "");
                                });
                                return;
                            }
                        }
                        if (rule.COLUMNAME == "SOFTBIN")
                        {
                            if (td.DicPRR[ptr.SITENUM].SOFTBIN.ToString() == rule.COLUMVALUE)
                            {
                                //预警漏项的报警
                                Logs.Rule( "漏SOFTBIN" + ptr.PARTID);
                                Task.Run(() =>
                                {
                                    INSERTALERT_PTR(rule, mir, ptr, dmgr, "");
                                });
                                return;
                            }
                        }

                        //2判断是不是漏项
                        if (rcsLost[omit_check].check.Count > 0)// 如果校验项存在
                        {
                            if (rcsLost[omit_check].check.ContainsKey(ptr.TESTTXT))// 校验项包含当前ptr.TESTTXT
                            {
                                rcsLost[omit_check].check.Remove(ptr.TESTTXT); // 移除符合校验值的校验项
                            }
                        }


                    }

                    if (rcsLost[omit_check].check.Count > 0 && rule.COLUMNAME == "TESTTXT")
                    {
                        //预警漏项的报警
                        Logs.Rule( "漏TESTTXT项" + item.Value[0].PARTID);
                        Task.Run(() =>
                        {
                            INSERTALERT_PTR(rule, mir, item.Value[0], dmgr, "");
                        });
                    }
                }
            }
        }

        private Dictionary<string, RCS_EXIST_RULE> rcsExist = new Dictionary<string, RCS_EXIST_RULE>();
        private Dictionary<string, PTR> rcsExist_ptr = new Dictionary<string, PTR>();
        private void EXIST_RCS(V_RULES_RCS rule, DatabaseManager dmgr, MIR mir, string team, TouchDown td)
        {
            if (td.IsTouchDownEnd)
            {
                foreach (var item in td.DicPTR)
                {
                    string omit_check = string.Format(rule.RuleKey, td.Index.ToString(), item.Key);
                    if (!rcsExist.Keys.Contains(omit_check))
                    {
                        rcsExist[omit_check] = new RCS_EXIST_RULE();
                        //用来判断是不是漏项的
                        if (!string.IsNullOrEmpty(rule.OMITNAME))
                        {
                            // 初始化预警校验项
                            foreach (var keyVal in rule.OMITNAME.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                            {
                                rcsExist[omit_check].check[keyVal] = keyVal;
                            }
                        }
                        //用来判断好品的
                        if (!string.IsNullOrEmpty(rule.PASSFLAG))
                        {
                            foreach (var keyVal in rule.PASSFLAG.Split("#".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                            {
                                string[] kv = keyVal.Split("@".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                                if (kv.Length != 2)
                                {
                                    continue;
                                }
                                if (double.TryParse(kv[1], out double v))
                                {
                                    rcsExist[omit_check].validKey[kv[0]] = v;
                                }
                                else
                                {
                                    continue;
                                }

                            }
                        }
                    }

                    foreach (var ptr in item.Value)
                    {
                        //1判断是不是好品
                        if (rcsExist[omit_check]?.validKey.Count > 0)// 如果校验项存在
                        {
                            if (rcsExist[omit_check].validKey.ContainsKey(ptr.TESTTXT))// 校验项包含当前ptr.TESTTXT
                            {
                                if (rcsExist[omit_check].validKey[ptr.TESTTXT] == ptr.RESULT)// 校验值符合
                                {
                                    rcsExist[omit_check].validKey.Remove(ptr.TESTTXT); // 移除符合校验值的校验项
                                }
                            }
                            else if (rcsExist[omit_check].validKey.ContainsKey("HARDBIN"))
                            {
                                if (rcsExist[omit_check].validKey["HARDBIN"] == td.DicPRR[ptr.SITENUM].HARDBIN)
                                {
                                    rcsExist[omit_check].validKey.Remove("HARDBIN");
                                }
                                else
                                {
                                    continue;
                                }
                            }
                        }
                        //2判断是不是漏项
                        if (rcsExist[omit_check].check.Count > 0)// 如果校验项存在
                        {
                            if (rcsExist[omit_check].check.ContainsKey(ptr.TESTTXT))// 校验项包含当前ptr.TESTTXT
                            {
                                rcsExist[omit_check].check.Remove(ptr.TESTTXT); // 移除符合校验值的校验项
                            }
                        }
                    }

                    if (rcsExist[omit_check].validKey.Count == 0 && rcsExist[omit_check].check.Count > 0)
                    {
                        //预警漏项的报警
                        Logs.Rule( "漏项" + item.Value[0].PARTID);
                    }
                }
            }
        }

        private void EXIST_RCSNEW(V_RULES_RCS rule, DatabaseManager dmgr, MIR mir, string team, TouchDown td)
        {
            string triggeredkey = string.Format(rule.RuleKey, rule.RULETYPE, mir.StdfId);

            if (td.IsTouchDownEnd)
            {
                #region 定义计算相关局部变量,定义标准<性能消耗低 无需做全局变量 局部定义 用完即抛>
                // 漏项校验 纯字符串处理
                string[] lossCheckTxt = rule.OMITNAME?.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                // 校验好品信息 字符串切割 
                Dictionary<string, double> passFlag = null;
                #endregion

                #region 构造好品检验,预警忽略检验数据集合
                try
                {
                    // rule.PASSFLAG 有值的时候遵循rule.PASSFLAG规则解析,没值的时候new一个关联对象,确保passFlag非空
                    passFlag = string.IsNullOrEmpty(rule.PASSFLAG) ? new Dictionary<string, double>() :
                                            rule.PASSFLAG.Split("#".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
                                            ?.Select(p =>
                                            {
                                                return p.Split("@".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                                            })?.ToDictionary(p => p[0], p => Convert.ToDouble(p[1]));
                }
                catch (Exception ex)
                {
                    Logs.Error( $"{mir.StdfId}-好品信息定义维护异常", ex);
                    return;
                }

                #endregion

                foreach (var ptrList in td.DicPTR.Values)
                {
                    if (!triggeredNum.ContainsKey(triggeredkey))
                    {
                        triggeredNum[triggeredkey] = 0;
                    }
                    // 只触发一次,且已经触发过 则不继续检测
                    if (rule.STATUS == "1" && triggeredNum[triggeredkey] >= (rule.STATUS_NUM ?? 1))
                    {
                        return;
                    }

                    // 好品标识
                    bool isPass = false;
                    // 漏项标识
                    bool isLoss = false;

                    #region 好品检测 非好品直接continue
                    isPass =
                                    //p.TESTTXT在好品检测列表中,且result值等于好品值
                                    ptrList.Count(p => passFlag.ContainsKey(p.TESTTXT) && passFlag[p.TESTTXT] == p.RESULT)
                                    //增加hardbin检测
                                    + ((passFlag.ContainsKey("HARDBIN") && passFlag["HARDBIN"] == td.DicPRR[ptrList[0].SITENUM].HARDBIN) ? 1 : 0)
                                    == passFlag.Count;

                    // 如果没检测到好品 则直接下一个
                    if (!isPass)
                    {
                        continue;
                    }
                    #endregion

                    // 获取漏项 如果查询到的项少于漏项列表维护的项,说明漏项了
                    if (lossCheckTxt != null)
                    {
                        isLoss = ptrList.Count(p => lossCheckTxt.Contains(p.TESTTXT)) < lossCheckTxt.Length;
                    }
                    // 如果存在漏项了 则预警
                    if (isLoss)
                    {
                        INSERTALERT(rule, mir, ptrList[0], dmgr, ptrList[0].PARTID);
                        triggeredNum[triggeredkey]++;
                    }
                }
            }
        }


        private Dictionary<string, RCS_CONSECUTIVE_RULE> rcsConsecutive = new Dictionary<string, RCS_CONSECUTIVE_RULE>();
        private Dictionary<string, double?> countAddCache = new Dictionary<string, double?>();
        private Dictionary<string, List<double>> testAddTotal = new Dictionary<string, List<double>>();
        private Dictionary<string, string> resultAddTotal = new Dictionary<string, string>();
        //测试值的连续多少次一样卡控
        private void CONSECUTIVE_RCS(V_RULES_RCS rule, DatabaseManager dmgr, MIR mir, string team, TouchDown td)
        {
            if (td.IsTouchDownEnd)
            {
                foreach (var item in td.DicPTR)
                {
                    string omit_check = string.Format(rule.RuleKey, td.Index.ToString(), item.Key);
                    if (!rcsConsecutive.Keys.Contains(omit_check))
                    {
                        rcsConsecutive[omit_check] = new RCS_CONSECUTIVE_RULE();
                        //用来判断是不是漏项的
                        if (!string.IsNullOrEmpty(rule.OMITNAME))
                        {
                            // 初始化预警校验项
                            foreach (var keyVal in rule.OMITNAME.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                            {
                                rcsConsecutive[omit_check].check[keyVal] = keyVal;
                            }
                        }
                        //用来判断好品的
                        if (!string.IsNullOrEmpty(rule.PASSFLAG))
                        {
                            foreach (var keyVal in rule.PASSFLAG.Split("#".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                            {
                                string[] kv = keyVal.Split("@".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                                if (kv.Length != 2)
                                {
                                    continue;
                                }
                                if (double.TryParse(kv[1], out double v))
                                {
                                    rcsConsecutive[omit_check].validKey[kv[0]] = v;
                                }
                                else
                                {
                                    continue;
                                }

                            }
                        }
                    }

                    foreach (var ptr in item.Value)
                    {
                        if (ptr.PARTID == "61700")
                        {
                        }
                        //1判断是不是好品
                        if (rcsConsecutive[omit_check]?.validKey.Count > 0)// 如果校验项存在
                        {
                            if (rcsConsecutive[omit_check].validKey.ContainsKey(ptr.TESTTXT))// 校验项包含当前ptr.TESTTXT
                            {
                                if (rcsConsecutive[omit_check].validKey[ptr.TESTTXT] == ptr.RESULT)// 校验值符合
                                {
                                    rcsConsecutive[omit_check].validKey.Remove(ptr.TESTTXT); // 移除符合校验值的校验项
                                }
                            }
                            else if (rcsConsecutive[omit_check].validKey.ContainsKey("HARDBIN"))
                            {
                                if (rcsConsecutive[omit_check].validKey["HARDBIN"] == td.DicPRR[ptr.SITENUM].HARDBIN)
                                {
                                    rcsConsecutive[omit_check].validKey.Remove("HARDBIN");
                                }
                                else
                                {
                                    continue;
                                }
                            }
                        }
                        //2判断是不是漏项
                        if (rcsConsecutive[omit_check].check.Count > 0)// 如果校验项存在
                        {
                            if (rcsConsecutive[omit_check].check.ContainsKey(ptr.TESTTXT))// 校验项包含当前ptr.TESTTXT
                            {
                                rcsConsecutive[omit_check].check.Remove(ptr.TESTTXT); // 移除符合校验值的校验项
                            }
                        }

                        string key = string.Format(rule.RuleKey, (rule.BYSITE == 0 ? "NOSITE" : ptr.SITENUM.ToString()), ptr.StdfId);
                        if (ptr.TESTNUM.ToString() == rule.TESTNUM && ptr.TESTTXT == rule.COLUMNAME)
                        {
                            if (!countAddCache.ContainsKey(key))
                            {
                                countAddCache[key] = 0;
                            }

                            if (!testAddTotal.ContainsKey(key))
                            {
                                testAddTotal[key] = new List<double>();
                            }

                            testAddTotal[key].Add(ptr.RESULT);// 统计测试值累计
                            if (testAddTotal[key].Count > 1)
                            {
                                if (ptr.RESULT >= (testAddTotal[key][testAddTotal[key].Count - 2] - (rule.UP_DOWN ?? 0)) &&
                                    ptr.RESULT <= (testAddTotal[key][testAddTotal[key].Count - 2] + (rule.UP_DOWN ?? 0)))
                                {
                                    countAddCache[key]++;
                                }
                                else
                                {
                                    testAddTotal[key] = new List<double>();
                                    testAddTotal[key].Add(ptr.RESULT);
                                    countAddCache[key] = 1;
                                }
                            }

                            if (countAddCache[key] > Convert.ToUInt32(rule.COLUMVALUE) && rcsConsecutive[omit_check].validKey.Count == 0)
                            {
                                Task.Run(() =>
                                {
                                    INSERTALERT(rule, mir, ptr, dmgr, "");
                                });
                            }
                        }
                    }

                    if (rcsConsecutive[omit_check].check.Count > 0 && rcsConsecutive[omit_check].validKey.Count == 0)
                    {
                        //预警漏项的报警
                        Logs.Rule( "漏项" + item.Value[0].PARTID);
                    }
                }
            }
        }



        private void CONSECUTIVE_RCSNEW(V_RULES_RCS rule, DatabaseManager dmgr, MIR mir, string team, TouchDown td)
        {
            string triggeredkey = string.Format(rule.RuleKey, rule.RULETYPE, mir.StdfId);

            if (td.IsTouchDownEnd)
            {
                #region 定义计算相关局部变量,定义标准<性能消耗低 无需做全局变量 局部定义 用完即抛>
                // 漏项校验 纯字符串处理
                string[] lossCheckTxt = rule.OMITNAME?.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                // 校验好品信息 字符串切割 
                Dictionary<string, double> passFlag = null;
                #endregion

                #region 构造好品检验,预警忽略检验数据集合
                try
                {
                    // rule.PASSFLAG 有值的时候遵循rule.PASSFLAG规则解析,没值的时候new一个关联对象,确保passFlag非空
                    passFlag = string.IsNullOrEmpty(rule.PASSFLAG) ? new Dictionary<string, double>() :
                                            rule.PASSFLAG.Split("#".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
                                            ?.Select(p =>
                                            {
                                                return p.Split("@".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                                            })?.ToDictionary(p => p[0], p => Convert.ToDouble(p[1]));
                }
                catch (Exception ex)
                {
                    Logs.Error( $"{mir.StdfId}-好品信息定义维护异常", ex);
                    return;
                }

                #endregion

                foreach (var ptrList in td.DicPTR.Values)
                {
                    if (!triggeredNum.ContainsKey(triggeredkey))
                    {
                        triggeredNum[triggeredkey] = 0;
                    }
                    // 只触发一次,且已经触发过 则不继续检测
                    if (rule.STATUS == "1" && triggeredNum[triggeredkey] >= (rule.STATUS_NUM ?? 1))
                    {
                        return;
                    }

                    // 好品标识
                    bool isPass = false;
                    // 漏项标识
                    bool isLoss = false;

                    #region 好品检测 非好品直接continue
                    isPass =
                                    //p.TESTTXT在好品检测列表中,且result值等于好品值
                                    ptrList.Count(p => passFlag.ContainsKey(p.TESTTXT) && passFlag[p.TESTTXT] == p.RESULT)
                                    //增加hardbin检测
                                    + ((passFlag.ContainsKey("HARDBIN") && passFlag["HARDBIN"] == td.DicPRR[ptrList[0].SITENUM].HARDBIN) ? 1 : 0)
                                    == passFlag.Count;

                    // 如果没检测到好品 则直接下一个
                    if (!isPass)
                    {
                        continue;
                    }
                    #endregion

                    // 获取漏项 如果查询到的项少于漏项列表维护的项,说明漏项了
                    if (lossCheckTxt != null)
                    {
                        isLoss = ptrList.Count(p => lossCheckTxt.Contains(p.TESTTXT)) < lossCheckTxt.Length;
                    }
                    // 如果存在漏项了 则预警
                    if (isLoss)
                    {
                        Logs.Rule( "漏项" + ptrList[0].PARTID);
                        INSERTALERT_LOSS(rule, mir, ptrList[0], dmgr, "漏项/空值：" + ptrList[0].PARTID);
                    }

                    string key = string.Format(rule.RuleKey, (rule.BYSITE == 0 ? "NOSITE" : ptrList[0].SITENUM.ToString()), ptrList[0].StdfId);

                    PTR ptr = ptrList.Where(p => p.TESTNUM.ToString() == rule.TESTNUM && p.TESTTXT == rule.COLUMNAME).FirstOrDefault();

                    if (ptr == null || string.IsNullOrEmpty(ptr.PARTID))
                    {
                        return;
                    }

                    if (!countAddCache.ContainsKey(key))
                    {
                        countAddCache[key] = 0;
                    }

                    if (!testAddTotal.ContainsKey(key))
                    {
                        testAddTotal[key] = new List<double>();
                    }

                    testAddTotal[key].Add(ptr.RESULT);// 统计测试值累计
                    if (testAddTotal[key].Count > 1)
                    {
                        if (ptr.RESULT >= (testAddTotal[key][testAddTotal[key].Count - 2] - (rule.UP_DOWN ?? 0)) &&
                            ptr.RESULT <= (testAddTotal[key][testAddTotal[key].Count - 2] + (rule.UP_DOWN ?? 0)))
                        {
                            countAddCache[key]++;
                        }
                        else
                        {
                            testAddTotal[key] = new List<double>();
                            testAddTotal[key].Add(ptr.RESULT);
                            countAddCache[key] = 1;
                        }
                    }

                    if (countAddCache[key] > Convert.ToUInt32(rule.COLUMVALUE))
                    {
                        Task.Run(() =>
                        {
                            INSERTALERT(rule, mir, ptr, dmgr, ptr.PARTID);
                        });
                        triggeredNum[triggeredkey]++;
                    }
                }
            }
        }

        //公式计算的卡控
        private Dictionary<string, RCS_CALCULATE_RULE> rcsCalculatePass = new Dictionary<string, RCS_CALCULATE_RULE>();
        private Dictionary<string, PTR> rcsCalculate_ptr = new Dictionary<string, PTR>();
        private void CALCULATE_RCS(V_RULES_RCS rule, DatabaseManager dmgr, MIR mir, string team, TouchDown td)
        {
            if (td.IsTouchDownEnd)
            {
                foreach (var item in td.DicPTR)
                {
                    string omit_check = string.Format(rule.RuleKey, td.Index.ToString(), item.Key);
                    if (!rcsCalculatePass.Keys.Contains(omit_check))
                    {
                        rcsCalculatePass[omit_check] = new RCS_CALCULATE_RULE();
                        //用来判断是不是漏项的
                        if (!string.IsNullOrEmpty(rule.OMITNAME))
                        {
                            // 初始化预警校验项
                            foreach (var keyVal in rule.OMITNAME.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                            {
                                rcsCalculatePass[omit_check].check[keyVal] = keyVal;
                            }
                        }
                        //用来判断好品的
                        if (!string.IsNullOrEmpty(rule.PASSFLAG))
                        {
                            foreach (var keyVal in rule.PASSFLAG.Split("#".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                            {
                                string[] kv = keyVal.Split("@".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                                if (kv.Length != 2)
                                {
                                    continue;
                                }
                                if (double.TryParse(kv[1], out double v))
                                {
                                    rcsCalculatePass[omit_check].validKey[kv[0]] = v;
                                }
                                else
                                {
                                    continue;
                                }

                            }
                        }
                    }

                    foreach (var ptr in item.Value)
                    {
                        //1判断是不是好品
                        if (rcsCalculatePass[omit_check]?.validKey.Count > 0)// 如果校验项存在
                        {
                            if (rcsCalculatePass[omit_check].validKey.ContainsKey(ptr.TESTTXT))// 校验项包含当前ptr.TESTTXT
                            {
                                if (rcsCalculatePass[omit_check].validKey[ptr.TESTTXT] == ptr.RESULT)// 校验值符合
                                {
                                    rcsCalculatePass[omit_check].validKey.Remove(ptr.TESTTXT); // 移除符合校验值的校验项
                                }
                            }
                            else if (rcsCalculatePass[omit_check].validKey.ContainsKey("HARDBIN"))
                            {
                                if (rcsCalculatePass[omit_check].validKey["HARDBIN"] == td.DicPRR[ptr.SITENUM].HARDBIN)
                                {
                                    rcsCalculatePass[omit_check].validKey.Remove("HARDBIN");
                                }
                                else
                                {
                                    continue;
                                }
                            }
                        }
                        //2判断是不是漏项
                        if (rcsCalculatePass[omit_check].check.Count > 0)// 如果校验项存在
                        {
                            if (rcsCalculatePass[omit_check].check.ContainsKey(ptr.TESTTXT))// 校验项包含当前ptr.TESTTXT
                            {
                                rcsCalculatePass[omit_check].check.Remove(ptr.TESTTXT); // 移除符合校验值的校验项
                            }
                        }

                        string key = string.Format(rule.RuleKey, ptr.PARTID, ptr.StdfId);
                        // 首次测试 初始化
                        if (!rcsCalculatePass.Keys.Contains(key))
                        {
                            rcsCalculatePass[key] = new RCS_CALCULATE_RULE();
                            rcsCalculate_ptr[key] = new PTR();
                            rcsCalculatePass[key].calculate = rule.COLUMNAME;
                            rcsCalculatePass[key].flag = false;
                            if (!string.IsNullOrEmpty(rule.PASSFLAG))
                            {
                                // 初始化预警校验项
                                foreach (var keyVal in rule.PASSFLAG.Split("#".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                                {
                                    string[] kv = rule.PASSFLAG.Split("@".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                                    if (kv.Length != 2)
                                    {
                                        continue;
                                    }
                                    if (double.TryParse(kv[1], out double v))
                                    {
                                        rcsCalculatePass[key].validKey[kv[0]] = v;
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                }
                            }

                        }

                        //移除passflag检测
                        if (rcsCalculatePass[key]?.validKey.Count > 0)// 如果校验项存在
                        {
                            if (rcsCalculatePass[key].validKey.ContainsKey(ptr.TESTTXT))// 校验项包含当前ptr.TESTTXT
                            {
                                if (rcsCalculatePass[key].validKey[ptr.TESTTXT] == ptr.RESULT)// 校验值符合
                                {
                                    rcsCalculatePass[key].validKey.Remove(ptr.TESTTXT); // 移除符合校验值的校验项
                                }
                            }
                            else if (rcsCalculatePass[key].validKey.ContainsKey("HARDBIN"))
                            {
                                if (rcsCalculatePass[key].validKey["HARDBIN"] == td.DicPRR[ptr.SITENUM].HARDBIN)
                                {
                                    rcsCalculatePass[key].validKey.Remove("HARDBIN");
                                }
                                else
                                {
                                    continue;
                                }
                            }
                        }
                        if (rcsCalculatePass[key].calculate.Contains(ptr.TESTTXT))
                        {
                            //把计算公式栏位替换成值
                            rcsCalculatePass[key].calculate = rcsCalculatePass[key].calculate.Replace(ptr.TESTTXT, ptr.RESULT.ToString());
                            rcsCalculatePass[key].calculate = rcsCalculatePass[key].calculate.Replace("SITE@NUM", ptr.SITENUM.ToString());
                            //判断计算公式中是否还有字母
                            //string pattern = @"[a-zA-Z]";
                            if (Regex.Matches(rcsCalculatePass[key].calculate, "[a-zA-Z]").Count <= 0)
                            {
                                //截取=号2边的数据
                                string left = rcsCalculatePass[key].calculate.Substring(0, rcsCalculatePass[key].calculate.IndexOf("="));
                                string right = rcsCalculatePass[key].calculate.Substring(rcsCalculatePass[key].calculate.IndexOf("=") + 1, rcsCalculatePass[key].calculate.Length - rcsCalculatePass[key].calculate.IndexOf("=") - 1);
                                object result = new DataTable().Compute(left, "");
                                if (result.ToString() == "NaN")
                                {
                                    continue;
                                }
                                if (rule.TEST_TYPE == "向下取整")
                                {
                                    //等式左边的值向下取整等于等式右边的值，不预警
                                    if (Math.Floor(Convert.ToDecimal(result)) != Convert.ToDecimal(right))
                                    {
                                        rcsCalculatePass[key].flag = true;
                                        rcsCalculate_ptr[key] = ptr;
                                    }
                                }
                                else if (rule.TEST_TYPE == "向上取整")
                                {
                                    //等式左边的值向上取整等于等式右边的值，不预警
                                    if (Math.Ceiling(Convert.ToDecimal(result)) != Convert.ToDecimal(right))
                                    {
                                        rcsCalculatePass[key].flag = true;
                                        rcsCalculate_ptr[key] = ptr;
                                    }
                                }
                                else if (rule.TEST_TYPE == "四舍六入五取整")
                                {
                                    //等式左边的值四舍六入五取整等于等式右边的值，不预警
                                    if (Math.Round(Convert.ToDecimal(result)) != Convert.ToDecimal(right))
                                    {
                                        rcsCalculatePass[key].flag = true;
                                        rcsCalculate_ptr[key] = ptr;
                                    }
                                }
                                else
                                {
                                    //等式左边的值等于等式右边的值，不预警
                                    if (Convert.ToDecimal(result) != Convert.ToDecimal(right))
                                    {
                                        rcsCalculatePass[key].flag = true;
                                        rcsCalculate_ptr[key] = ptr;
                                    }
                                }
                            }
                        }

                        if (rcsCalculatePass[key].flag && rcsCalculatePass[key].validKey.Count == 0)
                        {
                            rcsCalculatePass[key].flag = false;
                            Task.Run(() =>
                            {
                                INSERTALERT(rule, mir, rcsCalculate_ptr[key], dmgr, "");
                            });
                        }
                    }

                    if (rcsCalculatePass[omit_check].check.Count > 0 && rcsCalculatePass[omit_check].validKey.Count == 0)
                    {
                        //预警漏项的报警
                        Logs.Rule( "漏项" + item.Value[0].PARTID);
                    }
                }
            }
        }


        private void CALCULATE_RCSNEW(V_RULES_RCS rule, DatabaseManager dmgr, MIR mir, string team, TouchDown td)
        {
            string triggeredkey = string.Format(rule.RuleKey, rule.RULETYPE, mir.StdfId);

            if (td.IsTouchDownEnd)
            {
                #region 定义计算相关局部变量,定义标准<性能消耗低 无需做全局变量 局部定义 用完即抛>
                // 漏项校验 纯字符串处理
                string[] lossCheckTxt = rule.OMITNAME?.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                // 校验好品信息 字符串切割 
                Dictionary<string, double> passFlag = null;
                #endregion

                #region 构造好品检验,预警忽略检验数据集合
                try
                {
                    // rule.PASSFLAG 有值的时候遵循rule.PASSFLAG规则解析,没值的时候new一个关联对象,确保passFlag非空
                    passFlag = string.IsNullOrEmpty(rule.PASSFLAG) ? new Dictionary<string, double>() :
                                            rule.PASSFLAG.Split("#".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
                                            ?.Select(p =>
                                            {
                                                return p.Split("@".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                                            })?.ToDictionary(p => p[0], p => Convert.ToDouble(p[1]));
                }
                catch (Exception ex)
                {
                    Logs.Error( $"{mir.StdfId}-好品信息定义维护异常", ex);
                    return;
                }

                #endregion

                foreach (var ptrList in td.DicPTR.Values)
                {
                    if (!triggeredNum.ContainsKey(triggeredkey))
                    {
                        triggeredNum[triggeredkey] = 0;
                    }
                    // 只触发一次,且已经触发过 则不继续检测
                    if (rule.STATUS == "1" && triggeredNum[triggeredkey] >= (rule.STATUS_NUM ?? 1))
                    {
                        return;
                    }

                    // 好品标识
                    bool isPass = false;
                    // 漏项标识
                    bool isLoss = false;

                    #region 好品检测 非好品直接continue
                    isPass =
                                    //p.TESTTXT在好品检测列表中,且result值等于好品值
                                    ptrList.Count(p => passFlag.ContainsKey(p.TESTTXT) && passFlag[p.TESTTXT] == p.RESULT)
                                    //增加hardbin检测
                                    + ((passFlag.ContainsKey("HARDBIN") && passFlag["HARDBIN"] == td.DicPRR[ptrList[0].SITENUM].HARDBIN) ? 1 : 0)
                                    == passFlag.Count;

                    // 如果没检测到好品 则直接下一个
                    if (!isPass)
                    {
                        continue;
                    }
                    #endregion

                    // 获取漏项 如果查询到的项少于漏项列表维护的项,说明漏项了
                    if (lossCheckTxt != null)
                    {
                        isLoss = ptrList.Count(p => lossCheckTxt.Contains(p.TESTTXT)) < lossCheckTxt.Length;
                    }
                    // 如果存在漏项了 则预警
                    if (isLoss)
                    {
                        Logs.Rule( "漏项" + ptrList[0].PARTID);
                        INSERTALERT_LOSS(rule, mir, ptrList[0], dmgr, "漏项/空值：" + ptrList[0].PARTID);
                    }

                    string calstr = rule.COLUMNAME;

                    // 获取所有会参与计算的ptr数据
                    List<PTR> calPtr = ptrList.Where(p => calstr.Contains(p.TESTTXT)).OrderByDescending(p => p.TESTTXT.Length).ToList();
                    if (calPtr.Count < 1)
                    {
                        continue;
                    }

                    // 替换字符串为四则运算公式字符串
                    calPtr.ForEach(p =>
                    {
                        calstr = calstr.Replace(p.TESTTXT, p.RESULT.ToString());
                    });

                    // 替换sitenum数据
                    calstr = calstr.Replace("SITE@NUM", calPtr.Last().SITENUM.ToString());

                    if (Regex.Matches(calstr, "[a-zA-Z]").Count <= 0)
                    {
                        string left = calstr.Substring(0, calstr.IndexOf("="));
                        string right = calstr.Substring(calstr.IndexOf("=") + 1, calstr.Length - calstr.IndexOf("=") - 1);
                        object result = new DataTable().Compute(left, "");

                        bool shouldAlert = false;

                        if (result.ToString() == "NaN")
                        {
                            continue;
                        }

                        if (rule.CAL_TYPE == "向下取整")
                        {
                            //等式左边的值向下取整等于等式右边的值，不预警
                            if (Math.Floor(Convert.ToDecimal(result)) != Convert.ToDecimal(right))
                            {
                                shouldAlert = true;
                            }
                        }
                        else if (rule.CAL_TYPE == "向上取整")
                        {
                            //等式左边的值向上取整等于等式右边的值，不预警
                            if (Math.Ceiling(Convert.ToDecimal(result)) != Convert.ToDecimal(right))
                            {
                                shouldAlert = true;
                            }
                        }
                        else if (rule.CAL_TYPE == "四舍五入取整")
                        {
                            //等式左边的值四舍六入五取整等于等式右边的值，不预警
                            if (Math.Round(Convert.ToDecimal(result)) != Convert.ToDecimal(right))
                            {
                                shouldAlert = true;
                            }
                        }
                        else
                        {
                            //等式左边的值等于等式右边的值，不预警
                            if (Convert.ToDecimal(result) != Convert.ToDecimal(right))
                            {
                                shouldAlert = true;
                            }
                        }
                        if (shouldAlert)
                        {
                            try
                            {
                                Logs.Debug($"公式计算[COLUMNAME:{rule.COLUMNAME},COLUMTYPE:{rule.COLUMTYPE},COLUMVALUE:{rule.COLUMVALUE},PTRS:{JToken.FromObject(calPtr).ToString()}]");
                            }
                            catch
                            {
                            }
                            Task.Run(() =>
                            {
                                INSERTALERT(rule, mir, calPtr.Last(), dmgr, calPtr.Last().PARTID);
                            });

                            triggeredNum[triggeredkey]++;
                        }
                    }
                    else
                    {
                        continue;
                    }

                }
            }
        }

        private void FIXED_RCSNEW(V_RULES_RCS rule, DatabaseManager dmgr, MIR mir, string team, TouchDown td)
        {
            string triggeredkey = string.Format(rule.RuleKey, rule.RULETYPE, mir.StdfId);

            if (td.IsTouchDownEnd)
            {
                #region 定义计算相关局部变量,定义标准<性能消耗低 无需做全局变量 局部定义 用完即抛>
                // 漏项校验 纯字符串处理
                string[] lossCheckTxt = rule.OMITNAME?.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                // 校验好品信息 字符串切割 
                Dictionary<string, double> fixedPassFlag = null;
                try
                {
                    // rule.PASSFLAG 有值的时候遵循rule.PASSFLAG规则解析,没值的时候new一个关联对象,确保fixedPassFlag非空
                    fixedPassFlag = string.IsNullOrEmpty(rule.PASSFLAG) ? new Dictionary<string, double>() :
                                            rule.PASSFLAG.Split("#".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
                                            ?.Select(p =>
                                            {
                                                return p.Split("@".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                                            })?.ToDictionary(p => p[0], p => Convert.ToDouble(p[1]));
                }
                catch (Exception ex)
                {
                    Logs.Error( $"{mir.StdfId}-好品信息定义维护异常", ex);
                    return;
                }
                #endregion

                foreach (var ptrList in td.DicPTR.Values)
                {
                    if (!triggeredNum.ContainsKey(triggeredkey))
                    {
                        triggeredNum[triggeredkey] = 0;
                    }
                    // 只触发一次,且已经触发过 则不继续检测
                    if (rule.STATUS == "1" && triggeredNum[triggeredkey] >= (rule.STATUS_NUM ?? 1))
                    {
                        return;
                    }
                    // 好品标识
                    bool isPass = false;
                    // 漏项标识
                    bool isLoss = false;
                    // 获取好品
                    isPass =
                        //p.TESTTXT在好品检测列表中,且result值等于好品值
                        ptrList.Count(p => fixedPassFlag.ContainsKey(p.TESTTXT) && fixedPassFlag[p.TESTTXT] == p.RESULT)
                        //增加hardbin检测
                        + ((fixedPassFlag.ContainsKey("HARDBIN") && fixedPassFlag["HARDBIN"] == td.DicPRR[ptrList[0].SITENUM].HARDBIN) ? 1 : 0)
                        == fixedPassFlag.Count;

                    // 如果没检测到好品 则直接下一个
                    if (!isPass)
                    {
                        continue;
                    }

                    // 获取漏项 如果查询到的项少于漏项列表维护的项,说明漏项了
                    if (lossCheckTxt != null)
                    {
                        isLoss = ptrList.Count(p => lossCheckTxt.Contains(p.TESTTXT)) < lossCheckTxt.Length;
                    }
                    // 如果存在漏项了 则预警
                    if (isLoss)
                    {
                        Logs.Rule( "漏项" + ptrList[0].PARTID);
                        INSERTALERT_LOSS(rule, mir, ptrList[0], dmgr, "漏项/空值：" + ptrList[0].PARTID);
                    }
                    // 获取检测值
                    var checkV = ptrList.Where(p =>
                    string.IsNullOrEmpty(rule.TESTNUM)
                    ? p.TESTTXT == rule.COLUMNAME
                    : (p.TESTTXT == rule.COLUMNAME && p.TESTNUM.ToString() == rule.TESTNUM)).FirstOrDefault();
                    if (checkV == null)
                    {
                        return;
                    }

                    string checkValue = checkV.RESULT.ToString();

                    //触发规则判断，临时记录触发的内容

                    if ((rule.COLUMTYPE == "不等于" && checkValue != rule.COLUMVALUE)
                        || (rule.COLUMTYPE == "等于" && checkValue == rule.COLUMVALUE))
                    {
                        Logs.Debug($"固定值预警[COLUMNAME:{rule.COLUMNAME},COLUMTYPE:{rule.COLUMTYPE},COLUMVALUE:{rule.COLUMVALUE},PTR:{checkV.ToJson()}]");
                        Task.Run(() =>
                        {
                            INSERTALERT(rule, mir, checkV, dmgr, checkValue);
                        });
                        triggeredNum[triggeredkey]++;
                    }
                }
            }

        }

        //固定值检查的卡控
        private Dictionary<string, RCS_RULE> rcsPass = new Dictionary<string, RCS_RULE>();
        private Dictionary<string, PTR> rcsFixed_ptr = new Dictionary<string, PTR>();

        private void FIXED_RCS(V_RULES_RCS rule, DatabaseManager dmgr, MIR mir, string team, TouchDown td)
        {
            if (td.IsTouchDownEnd)
            {

                foreach (var item in td.DicPTR)
                {
                    string omit_check = string.Format(rule.RuleKey, td.Index.ToString(), item.Key);
                    if (!rcsPass.Keys.Contains(omit_check))
                    {
                        rcsPass[omit_check] = new RCS_RULE();

                        //用来判断是不是漏项的
                        if (!string.IsNullOrEmpty(rule.OMITNAME))
                        {
                            // 初始化预警校验项
                            foreach (var keyVal in rule.OMITNAME.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                            {
                                rcsPass[omit_check].check[keyVal] = keyVal;
                            }
                        }
                        //用来判断好品的
                        if (!string.IsNullOrEmpty(rule.PASSFLAG))
                        {
                            foreach (var keyVal in rule.PASSFLAG.Split("#".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                            {
                                string[] kv = keyVal.Split("@".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                                if (kv.Length != 2)
                                {
                                    continue;
                                }
                                if (double.TryParse(kv[1], out double v))
                                {
                                    rcsPass[omit_check].validKey[kv[0]] = v;
                                }
                                else
                                {
                                    continue;
                                }

                            }
                        }
                    }

                    foreach (var ptr in item.Value)
                    {
                        //1判断是不是好品
                        if (rcsPass[omit_check]?.validKey.Count > 0)// 如果校验项存在
                        {
                            if (rcsPass[omit_check].validKey.ContainsKey(ptr.TESTTXT))// 校验项包含当前ptr.TESTTXT
                            {
                                if (rcsPass[omit_check].validKey[ptr.TESTTXT] == ptr.RESULT)// 校验值符合
                                {
                                    rcsPass[omit_check].validKey.Remove(ptr.TESTTXT); // 移除符合校验值的校验项
                                }
                            }
                            else if (rcsPass[omit_check].validKey.ContainsKey("HARDBIN"))
                            {
                                if (rcsPass[omit_check].validKey["HARDBIN"] == td.DicPRR[ptr.SITENUM].HARDBIN)
                                {
                                    rcsPass[omit_check].validKey.Remove("HARDBIN");
                                }
                                else
                                {
                                    continue;
                                }
                            }
                        }
                        //2判断是不是漏项
                        if (rcsPass[omit_check].check.Count > 0)// 如果校验项存在
                        {
                            if (rcsPass[omit_check].check.ContainsKey(ptr.TESTTXT))// 校验项包含当前ptr.TESTTXT
                            {
                                rcsPass[omit_check].check.Remove(ptr.TESTTXT); // 移除符合校验值的校验项
                            }
                        }

                        string key = string.Format(rule.RuleKey, ptr.PARTID, ptr.StdfId);
                        // 首次测试 初始化
                        if (!rcsPass.Keys.Contains(key))
                        {
                            rcsPass[key] = new RCS_RULE();
                            rcsFixed_ptr[key] = new PTR();
                            rcsFixed_ptr[key] = ptr;
                            rcsPass[key].flag = false;
                            rcsPass[key].result = 0;
                            if (!string.IsNullOrEmpty(rule.PASSFLAG))
                            {
                                // 初始化预警校验项
                                foreach (var keyVal in rule.PASSFLAG.Split("#".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                                {
                                    string[] kv = rule.PASSFLAG.Split("@".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                                    if (kv.Length != 2)
                                    {
                                        continue;
                                    }
                                    if (double.TryParse(kv[1], out double v))
                                    {
                                        rcsPass[key].validKey[kv[0]] = v;
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                }
                            }
                        }
                        //移除passflag检测
                        if (rcsPass[key]?.validKey.Count > 0)// 如果校验项存在
                        {
                            if (rcsPass[key].validKey.ContainsKey(ptr.TESTTXT))// 校验项包含当前ptr.TESTTXT
                            {
                                if (rcsPass[key].validKey[ptr.TESTTXT] == ptr.RESULT)// 校验值符合
                                {
                                    rcsPass[key].validKey.Remove(ptr.TESTTXT); // 移除符合校验值的校验项
                                }
                            }
                            else if (rcsPass[key].validKey.ContainsKey("HARDBIN"))
                            {
                                if (rcsPass[key].validKey["HARDBIN"] == td.DicPRR[ptr.SITENUM].HARDBIN)
                                {
                                    rcsPass[key].validKey.Remove("HARDBIN");
                                }
                                else
                                {
                                    continue;
                                }
                            }
                        }

                        //触发规则判断，临时记录触发的内容
                        if (ptr.TESTTXT == rule.COLUMNAME)
                        {
                            if (rule.COLUMTYPE == "不等于")
                            {
                                if (ptr.RESULT.ToString() != rule.COLUMVALUE)
                                {
                                    rcsPass[key].flag = true;
                                    rcsPass[key].result = ptr.RESULT;
                                }
                            }
                            else if (rule.COLUMTYPE == "等于")
                            {
                                if (ptr.RESULT.ToString() == rule.COLUMVALUE)
                                {
                                    rcsPass[key].flag = true;
                                    rcsPass[key].result = ptr.RESULT;
                                }
                            }
                            else
                            {

                            }
                        }

                        if (rcsPass[key].flag && rcsPass[key].validKey.Count == 0)
                        {
                            rcsPass[key].flag = false;
                            Logs.Rule( "预警" + ptr.PARTID);
                            Task.Run(() =>
                            {
                                INSERTALERT(rule, mir, rcsFixed_ptr[key], dmgr, rcsPass[key].result.ToString());
                            });
                        }
                    }

                    if (rcsPass[omit_check].check.Count > 0 && rcsPass[omit_check].validKey.Count == 0)
                    {
                        //预警漏项的报警
                        Logs.Rule( "漏项" + item.Value[0].PARTID);
                    }
                }
            }
        }

        // ECID_RCSNEW 跳变参考集合
        Dictionary<string, Dictionary<string, double>> dicRcsEcidRef = new Dictionary<string, Dictionary<string, double>>();
        private void ECID_RCSNEW(V_RULES_RCS rule, DatabaseManager dmgr, MIR mir, string team, TouchDown td)
        {
            string RuleKey = string.Format(rule.RuleKey, "ecid", mir.StdfId);
            string triggeredkey = string.Format(rule.RuleKey, rule.RULETYPE, mir.StdfId);

            if (td.IsTouchDownEnd)
            {
                #region 定义计算相关局部变量,定义标准<性能消耗低 无需做全局变量 局部定义 用完即抛>
                // 漏项校验 纯字符串处理
                string[] lossCheckTxt = rule.OMITNAME?.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                // 校验好品信息 字符串切割 
                Dictionary<string, double> passFlag = null;
                // 预警忽略项信息 字符串切割
                Dictionary<string, double> ignoreFlag = null;

                string[] checkTxt = rule.COLUMNAME?.Split(',');

                string[] outputTxt = rule.COLUMNAME?.Split(',');
                #endregion

                #region 构造好品检验,预警忽略检验数据集合
                try
                {
                    // rule.PASSFLAG 有值的时候遵循rule.PASSFLAG规则解析,没值的时候new一个关联对象,确保passFlag非空
                    passFlag = string.IsNullOrEmpty(rule.PASSFLAG) ? new Dictionary<string, double>() :
                                            rule.PASSFLAG.Split("#".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
                                            ?.Select(p =>
                                            {
                                                return p.Split("@".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                                            })?.ToDictionary(p => p[0], p => Convert.ToDouble(p[1]));
                }
                catch (Exception ex)
                {
                    Logs.Error( $"{mir.StdfId}-好品信息定义维护异常", ex);
                    return;
                }

                try
                {
                    // rule.NOTEXIST 有值的时候遵循rule.NOTEXIST规则解析,没值的时候new一个关联对象,确保ignoreFlag非空
                    ignoreFlag = string.IsNullOrEmpty(rule.NOTEXIST) ? new Dictionary<string, double>() :
                                            rule.NOTEXIST.Split("#".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
                                            ?.Select(p =>
                                            {
                                                return p.Split("@".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                                            })?.ToDictionary(p => p[0], p => Convert.ToDouble(p[1]));
                }
                catch (Exception ex)
                {
                    Logs.Error( $"{mir.StdfId}-预警忽略项信息定义维护异常", ex);
                    return;
                }
                #endregion

                foreach (var ptrList in td.DicPTR.Values)
                {
                    if (!triggeredNum.ContainsKey(triggeredkey))
                    {
                        triggeredNum[triggeredkey] = 0;
                    }
                    // 只触发一次,且已经触发过 则不继续检测
                    if (rule.STATUS == "1" && triggeredNum[triggeredkey] >= (rule.STATUS_NUM ?? 1))
                    {
                        return;
                    }

                    // 好品标识
                    bool isPass = false;
                    // 漏项标识
                    bool isLoss = false;
                    // 忽略项标识
                    bool isIgnore = false;

                    #region 好品检测 非好品直接continue
                    isPass =
                                    //p.TESTTXT在好品检测列表中,且result值等于好品值
                                    ptrList.Count(p => passFlag.ContainsKey(p.TESTTXT) && passFlag[p.TESTTXT] == p.RESULT)
                                    //增加hardbin检测
                                    + ((passFlag.ContainsKey("HARDBIN") && passFlag["HARDBIN"] == td.DicPRR[ptrList[0].SITENUM].HARDBIN) ? 1 : 0)
                                    == passFlag.Count;

                    // 如果没检测到好品 则直接下一个
                    if (!isPass)
                    {
                        continue;
                    }
                    #endregion

                    #region 忽略项检测 如果维护了忽略项 且符合忽略条件 则直接continue
                    isIgnore = ptrList.Count(p => ignoreFlag.ContainsKey(p.TESTTXT) && ignoreFlag[p.TESTTXT] == p.RESULT) == ignoreFlag.Count;

                    if (ignoreFlag.Count > 0 && isIgnore)
                    {
                        continue;
                    }
                    #endregion

                    // 获取漏项 如果查询到的项少于漏项列表维护的项,说明漏项了
                    if (lossCheckTxt != null)
                    {
                        isLoss = ptrList.Count(p => lossCheckTxt.Contains(p.TESTTXT)) < lossCheckTxt.Length;
                    }
                    // 如果存在漏项了 则预警
                    if (isLoss)
                    {
                        Logs.Rule( "漏项" + ptrList[0].PARTID);
                        INSERTALERT_LOSS(rule, mir, ptrList[0], dmgr, "漏项/空值：" + ptrList[0].PARTID);
                    }

                    // 第一个好品数据赋值给参照集合
                    if (!dicRcsEcidRef.ContainsKey(RuleKey))
                    {
                        dicRcsEcidRef[RuleKey] = ptrList.Where(p => checkTxt.Contains(p.TESTTXT) || outputTxt.Contains(p.TESTTXT)).ToDictionary(p => p.TESTTXT, p => p.RESULT);

                    }
                    else
                    {

                        if (dicRcsEcidRef[RuleKey].Count == 0)
                        {
                            return;
                        }

                        var dicCurrEcid = ptrList.Where(p => checkTxt.Contains(p.TESTTXT) || outputTxt.Contains(p.TESTTXT)).ToDictionary(p => p.TESTTXT, p => p.RESULT);


                        if (dicCurrEcid.Count == 0)
                        {
                            return;
                        }

                        // 是否跳变
                        bool isDiff = false;

                        // 和参考项比较
                        foreach (var item in checkTxt)
                        {
                            if (!dicRcsEcidRef[RuleKey].ContainsKey(item))
                            {
                                // 漏项 直接跳出
                                break;
                            }
                            if (dicRcsEcidRef[RuleKey][item] != dicCurrEcid[item])
                            {
                                isDiff = true;
                                break;
                            }
                        }

                        // 有跳变
                        if (isDiff)
                        {
                            string REFS = "";
                            string RESULT = "";
                            foreach (var item in outputTxt)
                            {
                                REFS += "," + dicRcsEcidRef[RuleKey][item];
                                RESULT += "," + dicCurrEcid[item];
                            }

                            Task.Run(() =>
                            {
                                Logs.Rule( $"一致性检查:[COLUMNAME:{rule.COLUMNAME},STDFID:{ptrList[0].StdfId}]");
                                INSERTALERT(rule, mir, ptrList[0], dmgr, REFS.Substring(1) + " 、 " + RESULT.Substring(1) + "|" + ptrList[0].PARTID);
                            });

                            triggeredNum[triggeredkey]++;
                            break;
                        }
                    }


                }
            }
        }
        private void ECID_RCS(V_RULES_RCS rule, DatabaseManager dmgr, MIR mir, string team, TouchDown td)
        {
            if (td.IsTouchDownEnd)
            {
                foreach (var item in td.DicPTR)
                {
                    string omit_check = string.Format(rule.RuleKey, td.Index.ToString(), item.Key);
                    if (!cacheLot.Keys.Contains(omit_check))
                    {
                        cacheLot[omit_check] = new RCSECIDITEM();
                        //用来判断是不是漏项的
                        if (!string.IsNullOrEmpty(rule.OMITNAME))
                        {
                            // 初始化预警校验项
                            foreach (var keyVal in rule.OMITNAME.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                            {
                                cacheLot[omit_check].check[keyVal] = keyVal;
                            }
                        }
                        //用来判断预警了需不要用报警的
                        if (!string.IsNullOrEmpty(rule.NOTEXIST))
                        {
                            // 初始化预警校验项
                            foreach (var keyVal in rule.NOTEXIST.Split("#".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                            {
                                string[] kv = rule.NOTEXIST.Split("@".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                                if (kv.Length != 2)
                                {
                                    continue;
                                }
                                if (double.TryParse(kv[1], out double v))
                                {
                                    cacheLot[omit_check].ignoreKey[kv[0]] = v;
                                }
                                else
                                {
                                    continue;
                                }
                            }
                        }
                        //用来判断好品的
                        if (!string.IsNullOrEmpty(rule.PASSFLAG))
                        {
                            foreach (var keyVal in rule.PASSFLAG.Split("#".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                            {
                                string[] kv = keyVal.Split("@".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                                if (kv.Length != 2)
                                {
                                    continue;
                                }
                                if (double.TryParse(kv[1], out double v))
                                {
                                    cacheLot[omit_check].validKey[kv[0]] = v;
                                }
                                else
                                {
                                    continue;
                                }

                            }
                        }
                    }

                    foreach (var ptr in item.Value)
                    {
                        //1判断是不是好品
                        if (cacheLot[omit_check]?.validKey.Count > 0)// 如果校验项存在
                        {
                            if (cacheLot[omit_check].validKey.ContainsKey(ptr.TESTTXT))// 校验项包含当前ptr.TESTTXT
                            {
                                if (cacheLot[omit_check].validKey[ptr.TESTTXT] == ptr.RESULT)// 校验值符合
                                {
                                    cacheLot[omit_check].validKey.Remove(ptr.TESTTXT); // 移除符合校验值的校验项
                                }
                            }
                            else if (cacheLot[omit_check].validKey.ContainsKey("HARDBIN"))
                            {
                                if (cacheLot[omit_check].validKey["HARDBIN"] == td.DicPRR[ptr.SITENUM].HARDBIN)
                                {
                                    cacheLot[omit_check].validKey.Remove("HARDBIN");
                                }
                                else
                                {
                                    continue;
                                }
                            }
                        }
                        //2判断是不是漏项
                        if (cacheLot[omit_check].check.Count > 0)// 如果校验项存在
                        {
                            if (cacheLot[omit_check].check.ContainsKey(ptr.TESTTXT))// 校验项包含当前ptr.TESTTXT
                            {
                                cacheLot[omit_check].check.Remove(ptr.TESTTXT); // 移除符合校验值的校验项
                            }
                        }
                        //3判断是不是要忽略预警
                        if (cacheLot[omit_check].ignoreKey.Count > 0)
                        {
                            if (cacheLot[omit_check].ignoreKey.ContainsKey(ptr.TESTTXT))
                            {
                                if (cacheLot[omit_check].ignoreKey[ptr.TESTTXT] == ptr.RESULT)
                                {
                                    cacheLot[omit_check].ignoreKey.Remove(ptr.TESTTXT);
                                }
                                else
                                {
                                    cacheLot[omit_check].ignoreKey[ptr.TESTTXT] = -9999999;// 已经校验到 但是不满足的数据 重写值为-9999999
                                }
                            }
                        }

                        string key = string.Format(rule.RuleKey, ptr.SITENUM, ptr.StdfId);
                        string key_txt = string.Format(rule.RuleKey, "", ptr.StdfId);
                        // 首次测试 初始化
                        if (!checkTxt.Keys.Contains(key_txt))
                        {
                            checkTxt[key_txt] = rule.COLUMNAME?.Split(',');
                        }
                        if (!outputTxt.Keys.Contains(key_txt))
                        {
                            outputTxt[key_txt] = rule.COLUMNAME?.Split(',');
                        }
                        if (!cacheLot.Keys.Contains(key))
                        {
                            cacheLot[key] = new RCSECIDITEM();
                        }
                        if (!refLot.Keys.Contains(key))
                        {
                            refLot[key] = new RCSECIDITEM();
                        }
                        //outputTxt = rule.COLUMNAME?.Split(',');


                        //增加判断，如果passflag是HARDBIN，那么直接判断
                        if (cacheLot[key]?.validKey.Count > 0)// 如果校验项存在
                        {
                            if (cacheLot[key].validKey.ContainsKey(ptr.TESTTXT))// 校验项包含当前ptr.TESTTXT
                            {
                                if (cacheLot[key].validKey[ptr.TESTTXT] == ptr.RESULT)// 校验值符合
                                {
                                    cacheLot[key].validKey.Remove(ptr.TESTTXT); // 移除符合校验值的校验项
                                }
                            }
                            else if (cacheLot[key].validKey.ContainsKey("HARDBIN"))
                            {
                                if (cacheLot[key].validKey["HARDBIN"] == td.DicPRR[ptr.SITENUM].HARDBIN)
                                {
                                    cacheLot[key].validKey.Remove("HARDBIN");
                                }
                                else
                                {
                                    continue;
                                }
                            }
                        }

                        if (cacheLot[key].ignoreKey.Count > 0)// 如果忽略项存在
                        {
                            if (cacheLot[key].ignoreKey.ContainsKey(ptr.TESTTXT))// 校验项包含当前ptr.TESTTXT
                            {
                                if (cacheLot[key].ignoreKey[ptr.TESTTXT] == ptr.RESULT)// 校验值符合
                                {
                                    cacheLot[key].ignoreKey.Remove(ptr.TESTTXT); // 移除符合校验值的校验项
                                }
                                else
                                {
                                    cacheLot[key].ignoreKey[ptr.TESTTXT] = -9999999;// 已经校验到 但是不满足的数据 重写值为-9999999
                                }
                            }
                        }
                        // outputTxt 包含所有check项目数据,validKey包含要检测项数据 两个里面都包含的话就是不需要比较的数据 直接忽略
                        //if (!outputTxt[key_txt].Contains(ptr.TESTTXT) && (!cacheLot.ContainsKey(key) ? true : (!cacheLot[key].validKey.ContainsKey(ptr.TESTTXT) && !cacheLot[key].ignoreKey.ContainsKey(ptr.TESTTXT))))
                        //{
                        //    return;
                        //}

                        // 检测到第一个要比较的TESTTXT的时候
                        if (ptr.TESTTXT == checkTxt[key_txt][0])
                        {
                            // 如果以sitenum为key的数据不存在则初始化cacheLot对象
                            if (!cacheLot.ContainsKey(key))
                            {
                                cacheLot[key] = new RCSECIDITEM() { stdfid = ptr.StdfId };
                            }
                            // 初始化 checkTxtResult,outputTxtResult
                            cacheLot[key].checkTxtResult = new List<string> { ptr.RESULT.ToString() };
                            cacheLot[key].outputTxtResult = new List<string> { ptr.RESULT.ToString() };

                            if (!string.IsNullOrEmpty(rule.PASSFLAG))
                            {
                                // 初始化预警校验项
                                foreach (var keyVal in rule.PASSFLAG.Split("#".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                                {
                                    string[] kv = keyVal.Split("@".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                                    if (kv.Length != 2)
                                    {
                                        continue;
                                    }
                                    if (double.TryParse(kv[1], out double v))
                                    {
                                        cacheLot[key].validKey[kv[0]] = v;
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                }
                            }

                            if (!string.IsNullOrEmpty(rule.NOTEXIST))
                            {
                                // 初始忽略校验项
                                foreach (var keyVal in rule.NOTEXIST.Split("#".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                                {
                                    string[] kv = keyVal.Split("@".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                                    if (kv.Length != 2)
                                    {
                                        continue;
                                    }
                                    if (double.TryParse(kv[1], out double v))
                                    {
                                        cacheLot[key].ignoreKey[kv[0]] = v;
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                }
                            }

                        }
                        else if (checkTxt[key_txt].Contains(ptr.TESTTXT))// 如果不是第一个需要比较的TESTTXT值,则插入cacheLot对象
                        {
                            cacheLot[key].checkTxtResult.Add(ptr.RESULT.ToString());
                            cacheLot[key].outputTxtResult.Add(ptr.RESULT.ToString());
                        }

                        // 如果参照 refLot为空,以及cacheLot对象的checkTxtResult数量等于参照字段数量,则给refLot赋值
                        if (refLot[key].stdfid == 0 && cacheLot[key].checkTxtResult.Count == checkTxt[key_txt].Length)//首次检查完毕的测试项作为参照值
                        {
                            refLot[key] = new RCSECIDITEM
                            {
                                checkTxtResult = cacheLot[key].checkTxtResult,
                                outputTxtResult = cacheLot[key].outputTxtResult,
                                stdfid = ptr.StdfId
                            };
                        }

                        // 如果参照对象已经存在 但是输出字段元素数量小于数组字段长度,则继续添加
                        if (refLot[key].stdfid != 0 && refLot[key].outputTxtResult.Count < outputTxt[key_txt].Length)
                        {
                            if (outputTxt[key_txt].Contains(ptr.TESTTXT) && !checkTxt[key_txt].Contains(ptr.TESTTXT))
                            {
                                refLot[key].outputTxtResult.Add(ptr.RESULT.ToString());
                            }
                        }

                        if (refLot[key].stdfid != 0
                            && refLot[key].checkTxtResult.Count == cacheLot[key].checkTxtResult.Count
                            && string.Join(",", refLot[key].checkTxtResult) != string.Join(",", cacheLot[key].checkTxtResult))// 如果比较值不同则跳变预警
                        {
                            // 在跳变预警的时候 看是否已经搜集满要报告的数据信息 如果没有则继续搜集 直到搜集满
                            if (outputTxt[key_txt].Contains(ptr.TESTTXT) && !checkTxt[key_txt].Contains(ptr.TESTTXT))
                            {
                                cacheLot[key].outputTxtResult.Add(ptr.RESULT.ToString());
                            }

                            // 需要报告的数据已经搜集满 且 校验项validKey count为0 且 ignoreKey 全部校验完毕(所有value都变成 -9999999),但是count!=0 则可以触发预警
                            if (
                                cacheLot[key].outputTxtResult.Count == outputTxt[key_txt].Count()  // 输出项已经搜集完
                                && cacheLot[key].validKey.Count == 0 // 检验项全部通过
                                && (cacheLot[key].ignoreKey.Count(p => p.Value == -9999999) == cacheLot[key].ignoreKey.Count() // ignoreKey校验忽略项已经全部校验过
                                && cacheLot[key].ignoreKey.Count() != 0 || string.IsNullOrEmpty(rule.COLUMVALUE)) // 没通过的忽略项数量不为0
                               )
                            {
                                string REFS = string.Join(",", refLot[key].outputTxtResult.Take(checkTxt[key_txt].Length)) + "|" + string.Join(",", refLot[key].outputTxtResult.Skip(checkTxt[key_txt].Length));
                                string RESULT = string.Join(",", cacheLot[key].outputTxtResult.Take(checkTxt[key_txt].Length)) + "|" + string.Join(",", cacheLot[key].outputTxtResult.Skip(checkTxt[key_txt].Length));

                                Task.Run(() =>
                                {
                                    INSERTALERT(rule, mir, ptr, dmgr, REFS + " 、 " + RESULT);
                                });
                                //触发一次后，同一颗可跳出循环
                                break;
                            }
                        }
                    }

                    if (cacheLot[omit_check].check.Count > 0 && cacheLot[omit_check].validKey.Count == 0 &&
                        (cacheLot[omit_check].ignoreKey.Count(p => p.Value == -9999999) == cacheLot[omit_check].ignoreKey.Count()
                        && cacheLot[omit_check].ignoreKey.Count() != 0 || string.IsNullOrEmpty(rule.NOTEXIST)))
                    {
                        //预警漏项的报警
                        Logs.Rule( "漏项" + item.Value[0].PARTID);
                    }
                }
            }
        }

        // WAFER_RCSNEW WAFER信息参考集合
        Dictionary<string, Dictionary<string, string>> dicRcsWaferStore = new Dictionary<string, Dictionary<string, string>>();
        private void WAFER_RCSNEW(V_RULES_RCS rule, DatabaseManager dmgr, MIR mir, string team, TouchDown td)
        {
            string RuleKey = string.Format(rule.RuleKey, "wafer", mir.StdfId);
            string triggeredkey = string.Format(rule.RuleKey, rule.RULETYPE, mir.StdfId);

            if (td.IsTouchDownEnd)
            {
                #region 定义计算相关局部变量,定义标准<性能消耗低 无需做全局变量 局部定义 用完即抛>
                // 漏项校验 纯字符串处理
                string[] lossCheckTxt = rule.OMITNAME?.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                // 校验好品信息 字符串切割 
                Dictionary<string, double> passFlag = null;
                // 预警忽略项信息 字符串切割
                Dictionary<string, double> ignoreFlag = null;

                string[] waferCheckTxt = rule.COLUMNAME?.Split(',');

                #endregion

                #region 构造好品检验,预警忽略检验数据集合
                try
                {
                    // rule.PASSFLAG 有值的时候遵循rule.PASSFLAG规则解析,没值的时候new一个关联对象,确保passFlag非空
                    passFlag = string.IsNullOrEmpty(rule.PASSFLAG) ? new Dictionary<string, double>() :
                                            rule.PASSFLAG.Split("#".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
                                            ?.Select(p =>
                                            {
                                                return p.Split("@".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                                            })?.ToDictionary(p => p[0], p => Convert.ToDouble(p[1]));
                }
                catch (Exception ex)
                {
                    Logs.Error( $"{mir.StdfId}-好品信息定义维护异常", ex);
                    return;
                }

                try
                {
                    // rule.NOTEXIST 有值的时候遵循rule.NOTEXIST规则解析,没值的时候new一个关联对象,确保ignoreFlag非空
                    ignoreFlag = string.IsNullOrEmpty(rule.NOTEXIST) ? new Dictionary<string, double>() :
                                            rule.NOTEXIST.Split("#".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
                                            ?.Select(p =>
                                            {
                                                return p.Split("@".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                                            })?.ToDictionary(p => p[0], p => Convert.ToDouble(p[1]));
                }
                catch (Exception ex)
                {
                    Logs.Error( $"{mir.StdfId}-预警忽略项信息定义维护异常", ex);
                    return;
                }
                #endregion

                try
                {
                    foreach (var ptrList in td.DicPTR.Values)
                    {
                        if (!triggeredNum.ContainsKey(triggeredkey))
                        {
                            triggeredNum[triggeredkey] = 0;
                        }
                        // 只触发一次,且已经触发过 则不继续检测
                        if (rule.STATUS == "1" && triggeredNum[triggeredkey] >= (rule.STATUS_NUM ?? 1))
                        {
                            return;
                        }

                        // 好品标识
                        bool isPass = false;
                        // 漏项标识
                        bool isLoss = false;
                        // 忽略项标识
                        bool isIgnore = false;

                        #region 好品检测 非好品直接continue
                        isPass =
                                        //p.TESTTXT在好品检测列表中,且result值等于好品值
                                        ptrList.Count(p => passFlag.ContainsKey(p.TESTTXT) && passFlag[p.TESTTXT] == p.RESULT)
                                        //增加hardbin检测
                                        + ((passFlag.ContainsKey("HARDBIN") && passFlag["HARDBIN"] == td.DicPRR[ptrList[0].SITENUM].HARDBIN) ? 1 : 0)
                                        == passFlag.Count;

                        // 如果没检测到好品 则直接下一个
                        if (!isPass)
                        {
                            continue;
                        }
                        #endregion

                        #region 忽略项检测 如果维护了忽略项 且符合忽略条件 则直接continue
                        isIgnore = ptrList.Count(p => ignoreFlag.ContainsKey(p.TESTTXT) && ignoreFlag[p.TESTTXT] == p.RESULT) == ignoreFlag.Count;

                        if (ignoreFlag.Count > 0 && isIgnore)
                        {
                            continue;
                        }
                        #endregion

                        // 获取漏项 如果查询到的项少于漏项列表维护的项,说明漏项了
                        if (lossCheckTxt != null)
                        {
                            isLoss = ptrList.Count(p => lossCheckTxt.Contains(p.TESTTXT)) < lossCheckTxt.Length;
                        }
                        // 如果存在漏项了 则预警
                        if (isLoss)
                        {

                            Logs.Rule( "漏项" + ptrList[0].PARTID);
                            INSERTALERT_LOSS(rule, mir, ptrList[0], dmgr, "漏项/空值：" + ptrList[0].PARTID);
                        }

                        string waferInfo = string.Empty;
                        string site = "";

                        // 此处整个循环块可以直接用一个lambda表达式得到 但是为了预防字段顺序错误 还是使用waferCheckTxt 做循环
                        foreach (var item in waferCheckTxt)
                        {
                            if (item.ToUpper() == "SITE")
                            {
                                site = "," + ptrList[0].SITENUM.ToString();
                            }
                            foreach (var ptr in ptrList)
                            {
                                if (ptr.TESTTXT == item)
                                {
                                    waferInfo += "," + ptr.RESULT;
                                    break;
                                }
                            }
                        }

                        if (waferInfo.Length == 0)
                        {
                            continue;
                        }
                        waferInfo = waferInfo.Substring(1) + site;


                        Dictionary<string, string> sameValue = new Dictionary<string, string>();

                        if (dicRcsWaferStore.ContainsKey(RuleKey))
                        {
                            // 和历史数据比较重复项 如果有重复则保存到 sameValue 并跳出比较
                            foreach (var item in dicRcsWaferStore[RuleKey])
                            {
                                if (item.Value == waferInfo)
                                {
                                    sameValue[item.Key] = item.Value;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            dicRcsWaferStore[RuleKey] = new Dictionary<string, string>();
                        }
                        //比较完后 把当前最新的 wafer信息保存到缓存集合里
                        dicRcsWaferStore[RuleKey].Add(ptrList[0].PARTID, waferInfo);

                        // 有相同
                        if (sameValue.Count > 0)
                        {
                            string REFS = sameValue.Values.ToList()[0] + "|" + sameValue.Keys.ToList()[0];
                            string RESULT = waferInfo + "|" + ptrList[0].PARTID;

                            Task.Run(() =>
                            {
                                INSERTALERT(rule, mir, ptrList[0], dmgr, REFS + " 、 " + RESULT);
                            });

                            triggeredNum[triggeredkey]++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logs.Error( $"{mir.StdfId}-{ex.Message}");
                }
            }
        }


        //Dictionary<string, double> validKey = new Dictionary<string, double>();
        private Dictionary<string, string[]> waferCheckTxt = new Dictionary<string, string[]>();
        //string[] waferCheckTxt = null;
        private Dictionary<string, List<RCSECIDWAFERITEM>> waferCache = new Dictionary<string, List<RCSECIDWAFERITEM>>();
        Dictionary<string, RCSECIDWAFERITEM> currwafer = new Dictionary<string, RCSECIDWAFERITEM>();
        private void WAFER_RCS(V_RULES_RCS rule, DatabaseManager dmgr, MIR mir, string team, TouchDown td)
        {
            if (td.IsTouchDownEnd)
            {
                foreach (var item in td.DicPTR)
                {
                    string omit_check = string.Format(rule.RuleKey, td.Index.ToString(), item.Key);
                    if (!currwafer.Keys.Contains(omit_check))
                    {
                        currwafer[omit_check] = new RCSECIDWAFERITEM();
                        //用来判断是不是漏项的
                        if (!string.IsNullOrEmpty(rule.OMITNAME))
                        {
                            // 初始化预警校验项
                            foreach (var keyVal in rule.OMITNAME.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                            {
                                currwafer[omit_check].check[keyVal] = keyVal;
                            }
                        }
                        //用来判断预警了需不要用报警的
                        if (!string.IsNullOrEmpty(rule.NOTEXIST))
                        {
                            // 初始化预警校验项
                            foreach (var keyVal in rule.NOTEXIST.Split("#".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                            {
                                string[] kv = rule.NOTEXIST.Split("@".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                                if (kv.Length != 2)
                                {
                                    continue;
                                }
                                if (double.TryParse(kv[1], out double v))
                                {
                                    currwafer[omit_check].ignoreKey[kv[0]] = v;
                                }
                                else
                                {
                                    continue;
                                }
                            }
                        }
                        //用来判断好品的
                        if (!string.IsNullOrEmpty(rule.PASSFLAG))
                        {
                            foreach (var keyVal in rule.PASSFLAG.Split("#".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                            {
                                string[] kv = keyVal.Split("@".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                                if (kv.Length != 2)
                                {
                                    continue;
                                }
                                if (double.TryParse(kv[1], out double v))
                                {
                                    currwafer[omit_check].validKey[kv[0]] = v;
                                }
                                else
                                {
                                    continue;
                                }

                            }
                        }
                    }
                    foreach (var ptr in item.Value)
                    {
                        //1判断是不是好品
                        if (currwafer[omit_check]?.validKey.Count > 0)// 如果校验项存在
                        {
                            if (currwafer[omit_check].validKey.ContainsKey(ptr.TESTTXT))// 校验项包含当前ptr.TESTTXT
                            {
                                if (currwafer[omit_check].validKey[ptr.TESTTXT] == ptr.RESULT)// 校验值符合
                                {
                                    currwafer[omit_check].validKey.Remove(ptr.TESTTXT); // 移除符合校验值的校验项
                                }
                            }
                            else if (currwafer[omit_check].validKey.ContainsKey("HARDBIN"))
                            {
                                if (currwafer[omit_check].validKey["HARDBIN"] == td.DicPRR[ptr.SITENUM].HARDBIN)
                                {
                                    currwafer[omit_check].validKey.Remove("HARDBIN");
                                }
                                else
                                {
                                    continue;
                                }
                            }
                        }
                        //2判断是不是漏项
                        if (currwafer[omit_check].check.Count > 0)// 如果校验项存在
                        {
                            if (currwafer[omit_check].check.ContainsKey(ptr.TESTTXT))// 校验项包含当前ptr.TESTTXT
                            {
                                currwafer[omit_check].check.Remove(ptr.TESTTXT); // 移除符合校验值的校验项
                            }
                        }
                        //3判断是不是要忽略预警
                        if (currwafer[omit_check].ignoreKey.Count > 0)
                        {
                            if (currwafer[omit_check].ignoreKey.ContainsKey(ptr.TESTTXT))
                            {
                                if (currwafer[omit_check].ignoreKey[ptr.TESTTXT] == ptr.RESULT)
                                {
                                    currwafer[omit_check].ignoreKey.Remove(ptr.TESTTXT);
                                }
                                else
                                {
                                    currwafer[omit_check].ignoreKey[ptr.TESTTXT] = -9999999;// 已经校验到 但是不满足的数据 重写值为-9999999
                                }
                            }
                        }

                        #region 主程序
                        // 就用out,没写错
                        string key = string.Format(rule.RuleKey, ptr.PARTID, ptr.StdfId);
                        string key_txt = string.Format(rule.RuleKey, "", ptr.StdfId);
                        if (!waferCheckTxt.Keys.Contains(key_txt))
                        {
                            waferCheckTxt[key_txt] = rule.COLUMNAME?.Split(',');
                        }
                        if (!currwafer.Keys.Contains(key))
                        {
                            currwafer[key] = new RCSECIDWAFERITEM();
                            currwafer[key].keys = waferCheckTxt[key_txt];
                            currwafer[key].partId = ptr.PARTID;
                            currwafer[key].stdfid = ptr.StdfId;
                        }
                        if (!waferCache.Keys.Contains(key_txt))
                        {
                            waferCache[key_txt] = new List<RCSECIDWAFERITEM>();
                        }

                        if (!string.IsNullOrEmpty(rule.PASSFLAG))
                        {
                            foreach (var keyVal in rule.PASSFLAG.Split("#".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                            {
                                string[] kv = keyVal.Split("@".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                                if (kv.Length != 2)
                                {
                                    continue;
                                }
                                if (double.TryParse(kv[1], out double v))
                                {
                                    currwafer[key].validKey[kv[0]] = v;
                                }
                                else
                                {
                                    continue;
                                }

                            }
                        }

                        if (!string.IsNullOrEmpty(rule.NOTEXIST))
                        {
                            foreach (var keyVal in rule.NOTEXIST.Split("#".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                            {
                                string[] kv = keyVal.Split("@".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                                if (kv.Length != 2)
                                {
                                    continue;
                                }
                                if (double.TryParse(kv[1], out double v))
                                {
                                    currwafer[key].ignoreKey[kv[0]] = v;
                                }
                                else
                                {
                                    continue;
                                }

                            }
                        }

                        //增加判断，如果passflag是HARDBIN，那么直接判断
                        if (currwafer[key]?.validKey.Count > 0)// 如果校验项存在
                        {
                            if (currwafer[key].validKey.ContainsKey(ptr.TESTTXT))// 校验项包含当前ptr.TESTTXT
                            {
                                if (currwafer[key].validKey[ptr.TESTTXT] == ptr.RESULT)// 校验值符合
                                {
                                    currwafer[key].validKey.Remove(ptr.TESTTXT); // 移除符合校验值的校验项
                                }
                            }
                            else if (currwafer[key].validKey.ContainsKey("HARDBIN"))
                            {
                                if (currwafer[key].validKey["HARDBIN"] == td.DicPRR[ptr.SITENUM].HARDBIN)
                                {
                                    currwafer[key].validKey.Remove("HARDBIN");
                                }
                                else
                                {
                                    continue;
                                }
                            }
                        }

                        if (currwafer[key].ignoreKey.Count > 0)// 如果忽略项存在
                        {
                            if (currwafer[key].ignoreKey.ContainsKey(ptr.TESTTXT))// 校验项包含当前ptr.TESTTXT
                            {
                                if (currwafer[key].ignoreKey[ptr.TESTTXT] == ptr.RESULT)// 校验值符合
                                {
                                    currwafer[key].ignoreKey.Remove(ptr.TESTTXT); // 移除符合校验值的校验项
                                }
                                else
                                {
                                    currwafer[key].ignoreKey[ptr.TESTTXT] = -9999999;// 已经校验到 但是不满足的数据 重写值为-9999999
                                }
                            }
                        }

                        int index = Array.IndexOf(currwafer[key].keys, ptr.TESTTXT);
                        if (index != -1)
                        {
                            currwafer[key].values[index] = ptr.RESULT.ToString();
                        }

                        // 校验值满 且 验证项 为0
                        if (currwafer[key].values.Count(p => p != null) == currwafer[key].values.Length
                            && currwafer[key].validKey.Count == 0
                             && (currwafer[key].ignoreKey.Count(p => p.Value == -9999999) == currwafer[key].ignoreKey.Count() // ignoreKey校验忽略项已经全部校验过
                                && currwafer[key].ignoreKey.Count() != 0 || string.IsNullOrEmpty(rule.COLUMVALUE)) // 没通过的忽略项数量不为0
                               )
                        {
                            waferCache[key_txt].Add(currwafer[key]);

                            var refwafer = waferCache[key_txt].FirstOrDefault(p => string.Join(",", p.values) + "|" + p.stdfid.ToString() == string.Join(",", currwafer[key].values) + "|" + currwafer[key].stdfid.ToString());

                            // refwafer 不为null 表示有值
                            if (refwafer != null && refwafer.partId != ptr.PARTID)
                            {
                                string REFS = $"{string.Join(",", refwafer.values)}|{refwafer.partId}";
                                string RESULT = $"{string.Join(",", currwafer[key].values)}|{currwafer[key].partId}";

                                Task.Run(() =>
                                {
                                    INSERTALERT(rule, mir, ptr, dmgr, REFS + " 、 " + RESULT);
                                });
                            }

                            currwafer.Remove(key);
                        }
                        #endregion
                    }

                    //漏项的报警
                    if (currwafer[omit_check].check.Count > 0 && currwafer[omit_check].validKey.Count == 0 &&
                    (currwafer[omit_check].ignoreKey.Count(p => p.Value == -9999999) == currwafer[omit_check].ignoreKey.Count()
                    && currwafer[omit_check].ignoreKey.Count() != 0 || string.IsNullOrEmpty(rule.NOTEXIST)))
                    {
                        //预警漏项的报警
                        Logs.Rule( "漏项" + item.Value[0].PARTID);
                    }
                }
            }
        }

        private void INSERTALERT_LOSS(V_RULES_RCS rule, MIR mir, PTR ptr, DatabaseManager dmgr, string remark = "")
        {
            try
            {
                new DatabaseManager().ExecuteNonQuery(@"insert into sys_rcs_rules_list
                                  (stdfid,
                                   filename,
                                   partid,
                                   ruletype,REMARK,RULE_TIME,RULE_GUID)
                                values
                                  (:stdfid,
                                   :filename,
                                   :partid,
                                   :ruletype,:remark,:rule_time,:rule_guid)
                                ", new
                {
                    stdfid = ptr.StdfId,
                    filename = mir.PARTTYP,
                    partid = ptr.PARTID,
                    ruletype = rule.TEAM,
                    remark = remark,
                    rule_time = DateTime.Now,
                    rule_guid = rule.GUID
                });

                //更新表头信息
                switch (rule.RULETYPE)
                {

                    case "固定值检查":
                        new DatabaseManager().ExecuteNonQuery(@"update sys_rcs_rules_head set FIXED='fail' where stdfid='" + ptr.StdfId + "'");
                        break;
                    case "一致性检查":
                        new DatabaseManager().ExecuteNonQuery(@"update sys_rcs_rules_head set UNIFORMITY='fail' where stdfid='" + ptr.StdfId + "'");
                        break;
                    case "唯一性检查":
                        new DatabaseManager().ExecuteNonQuery(@"update sys_rcs_rules_head set UNIQUENESS='fail' where stdfid='" + ptr.StdfId + "'");
                        break;
                    case "公式计算":
                        new DatabaseManager().ExecuteNonQuery(@"update sys_rcs_rules_head set CALCULATE='fail' where stdfid='" + ptr.StdfId + "'");
                        break;
                    case "连续多少次一样":
                        new DatabaseManager().ExecuteNonQuery(@"update sys_rcs_rules_head set CONSECUTIVE='fail' where stdfid='" + ptr.StdfId + "'");
                        break;
                    case "存在性检查":
                        new DatabaseManager().ExecuteNonQuery(@"update sys_rcs_rules_head set EXIST='fail' where stdfid='" + ptr.StdfId + "'");
                        break;
                }

            }
            catch (Exception ex)
            {
                Logs.Error( ptr.StdfId.ToString(), ex);
            }
        }

        private void INSERTALERT(V_RULES_RCS rule, MIR mir, PTR ptr, DatabaseManager dmgr, string remark = "")
        {
            DateTime now = DateTime.Now;
            string flowid = "";
            if (mir.FLOWID != "" && mir.FLOWID != null && mir.FLOWID.Length > 1)
            {
                flowid = mir.FLOWID?.Substring(0, 2);
                //flowid = mir.FLOWID;
            }
            string _mailtitle = rule.ACTION + "_" + rule.RULETYPE + "_" + rule.PRODUCT + "_" + mir.LOTID + "_" + mir.TESTCOD + "_" + mir.SBLOTID + "_" + mir.FLOWID + "_" + mir.NODENAM + "_" + now.ToString("yyyyMMddHHmmss");

            Global.redis.Publish("RCS", JToken.FromObject(new
            {
                ISSTOP = rule.ACTION == "Pause Production" ? "1" : "0",
                GUID = rule.GUID,
                EQPNAME = mir.NODENAM,
                EQPTID = handler,
                STDFID = ptr.StdfId,
                DATETIME = now.ToString("yyyyMMddHHmmss"),
                SITENUM = ptr.SITENUM,
                PARTID = ptr.PARTID,
                REMARK = string.IsNullOrEmpty(remark) ? "-" : remark,
                MAILTITLE = _mailtitle,
                PRODUCT = rule.PRODUCT
            }).ToString());
            Logs.Rule( $"{rule.RULETYPE}-{ptr.StdfId}-{ptr.PARTID}");

            try
            {
                new DatabaseManager().ExecuteNonQuery(@"insert into sys_rcs_rules_list
                                  (stdfid,
                                   filename,
                                   partid,
                                   ruletype,REMARK,RULE_TIME,RULE_GUID,MAILTITLE)
                                values
                                  (:stdfid,
                                   :filename,
                                   :partid,
                                   :ruletype,:remark,:rule_time,:rule_guid,:mailtitle)
                                ", new
                {
                    stdfid = ptr.StdfId,
                    filename = mir.PARTTYP,
                    partid = ptr.PARTID,
                    ruletype = rule.TEAM,
                    remark = remark,
                    rule_time = DateTime.Now,
                    rule_guid = rule.GUID,
                    mailtitle = _mailtitle
                });

                //更新表头信息
                switch (rule.RULETYPE)
                {

                    case "固定值检查":
                        new DatabaseManager().ExecuteNonQuery(@"update sys_rcs_rules_head set FIXED='fail' where stdfid='" + ptr.StdfId + "'");
                        break;
                    case "一致性检查":
                        new DatabaseManager().ExecuteNonQuery(@"update sys_rcs_rules_head set UNIFORMITY='fail' where stdfid='" + ptr.StdfId + "'");
                        break;
                    case "唯一性检查":
                        new DatabaseManager().ExecuteNonQuery(@"update sys_rcs_rules_head set UNIQUENESS='fail' where stdfid='" + ptr.StdfId + "'");
                        break;
                    case "公式计算":
                        new DatabaseManager().ExecuteNonQuery(@"update sys_rcs_rules_head set CALCULATE='fail' where stdfid='" + ptr.StdfId + "'");
                        break;
                    case "连续多少次一样":
                        new DatabaseManager().ExecuteNonQuery(@"update sys_rcs_rules_head set CONSECUTIVE='fail' where stdfid='" + ptr.StdfId + "'");
                        break;
                    case "存在性检查":
                        new DatabaseManager().ExecuteNonQuery(@"update sys_rcs_rules_head set EXIST='fail' where stdfid='" + ptr.StdfId + "'");
                        break;
                }

            }
            catch (Exception ex)
            {
                Logs.Error( ptr.StdfId.ToString(), ex);
            }
        }

        private void INSERTALERT_PTR(V_RULES_RCS rule, MIR mir, PTR ptr, DatabaseManager dmgr, string remark = "")
        {
            DateTime now = DateTime.Now;
            string flowid = "";
            if (mir.FLOWID != "" && mir.FLOWID != null && mir.FLOWID.Length > 1)
            {
                flowid = mir.FLOWID?.Substring(0, 2);
                //flowid = mir.FLOWID;
            }
            string _mailtitle = "Pause Production_1TOUCHDOWN_" + rule.PRODUCT + "_" + mir.LOTID + "_" + mir.TESTCOD + "_" + mir.SBLOTID + "_" + mir.FLOWID + "_" + mir.NODENAM + "_" + now.ToString("yyyyMMddHHmmss");

            Global.redis.Publish("1TOUCHDOWN", JToken.FromObject(new
            {
                ISSTOP = "1",
                GUID = rule.GUID,
                EQPNAME = mir.NODENAM,
                EQPTID = handler,
                STDFID = ptr.StdfId,
                DATETIME = now.ToString("yyyyMMddHHmmss"),
                SITENUM = ptr.SITENUM,
                PARTID = ptr.PARTID,
                REMARK = string.IsNullOrEmpty(remark) ? "-" : remark,
                MAILTITLE = _mailtitle
            }).ToString());
            Logs.Rule( $"{"1TOUCHDOWN"}-{ptr.StdfId}-{ptr.PARTID}");

            string guid = Guid.NewGuid().ToString("N");
            try
            {
                new DatabaseManager().ExecuteNonQuery(@"insert into sys_rules_show_ptr
                                  (stdfid,
                                   testnum,
                                   headnum,
                                   sitenum,
                                   testflg,
                                   parmflg,
                                   result,
                                   testtxt,
                                   alarmid,
                                   optflag,
                                   resscal,
                                   llmscal,
                                   hlmscal,
                                   lolimit,
                                   hilimit,
                                   units,
                                   cresfmt,
                                   cllmfmt,
                                   chlmfmt,
                                   lospec,
                                   hispec,
                                   stdfindex,
                                   rules_guid,
                                   rules_time,
                                   product,
                                   eqname,
                                   remark,guid,partid,mailtitle)
                                values
                                  (:stdfid,
                                   :testnum,
                                   :headnum,
                                   :sitenum,
                                   :testflg,
                                   :parmflg,
                                   :result,
                                   :testtxt,
                                   :alarmid,
                                   :optflag,
                                   :resscal,
                                   :llmscal,
                                   :hlmscal,
                                   :lolimit,
                                   :hilimit,
                                   :units,
                                   :cresfmt,
                                   :cllmfmt,
                                   :chlmfmt,
                                   :lospec,
                                   :hispec,
                                   :stdfindex,
                                   :rules_guid,
                                   :rules_time,
                                   :product,
                                   :eqname,
                                   :remark,:guid,:partid,:mailtitle)
                                ", new
                {
                    stdfid = ptr.StdfId,
                    testnum = ptr.TESTNUM,
                    headnum = ptr.HEADNUM,
                    sitenum = ptr.SITENUM,
                    testflg = ptr.TESTFLG,
                    parmflg = ptr.PARMFLG,
                    result = ptr.RESULT,
                    testtxt = ptr.TESTTXT,
                    alarmid = ptr.ALARMID,
                    optflag = ptr.OPTFLAG,
                    resscal = ptr.RESSCAL,
                    llmscal = ptr.LLMSCAL,
                    hlmscal = ptr.HLMSCAL,
                    lolimit = ptr.LOLIMIT,
                    hilimit = ptr.HILIMIT,
                    units = ptr.UNITS,
                    cresfmt = ptr.CRESFMT,
                    cllmfmt = ptr.CLLMFMT,
                    chlmfmt = ptr.CHLMFMT,
                    lospec = ptr.LOSPEC,
                    hispec = ptr.HISPEC,
                    stdfindex = ptr.STDFINDEX,
                    rules_guid = rule.GUID,
                    rules_time = now,
                    product = rule.PRODUCT,
                    eqname = mir.NODENAM,
                    remark = remark,
                    guid,
                    partid = ptr.PARTID,
                    mailtitle = _mailtitle
                });
            }
            catch (Exception ex)
            {
                Logs.Error( ptr.StdfId.ToString(), ex);
            }
        }

        string lotid = string.Empty;
        string testcod = string.Empty;
        string flowid = string.Empty;

        public void Attach(string type, BaseEntity entity, List<V_RULES_RCS> LstRcsRules)
        {
            if (type == "SDR")
            {
                handler = (entity as SDR).HANDID;
            }
        }

        public bool TestCheck(PTR ptr, SYS_RULES_TESTRUN rule)
        {
            return (string.IsNullOrEmpty(rule.TESTTXT) ? true : rule.TESTTXT == ptr.TESTTXT)
                && (rule.TESTNUMBER.HasValue ? rule.TESTNUMBER == ptr.TESTNUM : true);
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

                this.batch = null;
                this.cacheLot = null;
                this.checkTxt = null;
                this.handler = null;
                this.maxSiteNum = null;
                this.outputTxt = null;
                this.refLot = null;
                this.siteExists = null;
                this.testNumTotal = null;
                this.testsTotal = null;
                this.testTotal = null;
                disposedValue = true;
            }
        }

        // TODO: 仅当以上 Dispose(bool disposing) 拥有用于释放未托管资源的代码时才替代终结器。
        ~PTRRcsRule()
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
            //GC.SuppressFinalize(this);
        }
        #endregion
    }

    public class RCS_RULE
    {
        public string key { get; set; }

        public bool flag { get; set; }

        public double result { get; set; }

        // #分割两项,@分割值
        public Dictionary<string, double> validKey { get; set; } = new Dictionary<string, double>();

        //,分割项
        public Dictionary<string, string> check { get; set; } = new Dictionary<string, string>();

        // #分割两项,@分割值
        public Dictionary<string, double> ignoreKey { get; set; } = new Dictionary<string, double>();
    }



    public class RCS_CALCULATE_RULE
    {
        public string key { get; set; }

        public string calculate { get; set; }

        public bool flag { get; set; }

        // #分割两项,@分割值
        public Dictionary<string, double> validKey { get; set; } = new Dictionary<string, double>();

        //,分割项
        public Dictionary<string, string> check { get; set; } = new Dictionary<string, string>();
    }

    public class RCS_EXIST_RULE
    {
        public string key { get; set; }

        public string testtxt { get; set; }

        public bool flag { get; set; }

        // #分割两项,@分割值
        public Dictionary<string, double> validKey { get; set; } = new Dictionary<string, double>();

        //,分割项
        public Dictionary<string, string> check { get; set; } = new Dictionary<string, string>();

        //
        public Dictionary<string, double> ignoreKey { get; set; } = new Dictionary<string, double>();
    }

    public class RCS_LOST_RULE
    {
        public string key { get; set; }

        public string testtxt { get; set; }

        public bool flag { get; set; }

        // #分割两项,@分割值
        public Dictionary<string, double> validKey { get; set; } = new Dictionary<string, double>();

        //,分割项
        public Dictionary<string, string> check { get; set; } = new Dictionary<string, string>();

        //
        public Dictionary<string, double> ignoreKey { get; set; } = new Dictionary<string, double>();
    }

    public class RCS_CONSECUTIVE_RULE
    {
        public string key { get; set; }

        public string testtxt { get; set; }

        public bool flag { get; set; }

        // #分割两项,@分割值
        public Dictionary<string, double> validKey { get; set; } = new Dictionary<string, double>();

        //,分割项
        public Dictionary<string, string> check { get; set; } = new Dictionary<string, string>();

        //
        public Dictionary<string, double> ignoreKey { get; set; } = new Dictionary<string, double>();
    }

    public class RCSECIDITEM
    {
        public double stdfid { get; set; }

        public List<string> checkTxtResult { get; set; } = new List<string>();

        public List<string> outputTxtResult { get; set; } = new List<string>();

        // #分割两项,@分割值
        public Dictionary<string, double> validKey { get; set; } = new Dictionary<string, double>();

        public Dictionary<string, double> ignoreKey { get; set; } = new Dictionary<string, double>();

        public Dictionary<string, string> check { get; set; } = new Dictionary<string, string>();

    }

    public class RCSECIDWAFERITEM
    {
        public double w { get; set; }
        public double x { get; set; }
        public double y { get; set; }


        public string[] _keys;
        public string[] keys
        {
            get
            {
                return _keys;
            }
            set
            {
                _keys = value;
                values = new string[value.Length];
            }
        }

        public string[] values { get; set; }


        public string partId { get; set; }
        public Dictionary<string, double> validKey { get; set; } = new Dictionary<string, double>();

        public Dictionary<string, double> ignoreKey { get; set; } = new Dictionary<string, double>();

        public Dictionary<string, string> check { get; set; } = new Dictionary<string, string>();

        public int stdfid { get; set; }
    }
}


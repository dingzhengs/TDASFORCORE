using Newtonsoft.Json.Linq;
using TDASDataParser.StdfTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TDASCommon;
using System.IO;
using System.Xml.Serialization;
using System.Threading;

namespace TDASDataParser.Rules
{
    public class PTRRule : IDisposable
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

        private Dictionary<double, ECIDITEM> cacheLot = new Dictionary<double, ECIDITEM>();
        ECIDITEM refLot = null;

        private string[] checkTxt;
        private string[] outputTxt;

        //public Dictionary<double, double> HILIMIT { get; set; } = new Dictionary<double, double>();
        //public Dictionary<double, double> LOLIMIT { get; set; } = new Dictionary<double, double>();

        public void Match(List<SYS_RULES_TESTRUN> lstRules, BaseEntity entity, MIR mir, DatabaseManager dmgr)
        {
            if (lstRules != null)
            {
                PTR ptr = entity as PTR;

                //if (ptr.HILIMIT != 0 && ptr.LOLIMIT != 0)
                //{
                //    if (!HILIMIT.ContainsKey(ptr.TESTNUM))
                //    {
                //        HILIMIT.Add(ptr.TESTNUM, ptr.HILIMIT);
                //    }
                //    if (!LOLIMIT.ContainsKey(ptr.TESTNUM))
                //    {
                //        LOLIMIT.Add(ptr.TESTNUM, ptr.LOLIMIT);
                //    }
                //}

                foreach (SYS_RULES_TESTRUN rule in lstRules)
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

                        //if (rule.TYPE == "PARAMETRICTESTSTATISTICTRIGGER")
                        //{
                        //    PARAMETRICTESTSTATISTICTRIGGER(rule, ptr, dmgr, mir);
                        //}

                        //if (rule.TYPE == "SIGMATRIGGER")
                        //{
                        //    SIGMATRIGGER(rule, ptr, dmgr, mir);
                        //}
                        //else if (rule.TYPE == "SITETOSITEPARAMETRICTESTSTATISTICDELTATRIGGER")
                        //{
                        //    SITETOSITEPARAMETRICTESTSTATISTICDELTATRIGGER(rule, ptr, dmgr, mir);
                        //}
                        if (rule.TYPE == "ECID")
                        {
                            ECID(rule, ptr, dmgr, mir);
                        }
                        else if (rule.TYPE == "ECIDWAFER")
                        {
                            ECIDWAFER(rule, ptr, dmgr, mir);
                        }
                        else if (rule.TYPE == "ECIDWAFER-AKJ")
                        {
                            ECIDWAFERAKJ(rule, ptr, dmgr, mir);
                        }
                        //else if (rule.TYPE == "PTSADDTRIGGER")
                        //{
                        //    PTSADDTRIGGER(rule, ptr, dmgr, mir);
                        //}
                        //else if (rule.TYPE == "PTSCUTTRIGGER")
                        //{
                        //    PTSCUTTRIGGER(rule, ptr, dmgr, mir);
                        //}
                        else if (rule.TYPE == "OSPINCOUNTTRIGGER")
                        {
                            OSPINCOUNTTRIGGER(rule, ptr, dmgr, mir);
                        }
                        else if (rule.TYPE == "OSPINCONSECUTIVETRIGGER")
                        {
                            OSPINCONSECUTIVETRIGGER(rule, ptr, dmgr, mir);
                        }
                    }
                    catch (Exception ex)
                    {
                        // 所有规则外面增加一个异常捕捉 防止一个规则有未捕获的异常跳出规则匹配
                        Logs.Error( $"{mir.StdfId}-{rule.TYPE}", ex);
                    }
                }
            }
        }

        Dictionary<string, double> lostTexttxtPASSFLAG = new Dictionary<string, double>();
        Dictionary<string, List<string>> mouldList = new Dictionary<string, List<string>>();
        Dictionary<string, List<string>> nextList = new Dictionary<string, List<string>>();
        //Dictionary<string, List<string>> newList = new Dictionary<string, List<string>>();
        Dictionary<string, int> TriggerNum = new Dictionary<string, int>();
        public void LOSTTESTTXTTRIGGER(SYS_RULES_TESTRUN rule, TouchDown td, DatabaseManager dmgr, MIR mir, bool losttype)
        {
            string rulekey = string.Format(rule.RuleKey, "", "PTR", td.Stdfid);

            // 只触发一次,且已经触发过 则不继续检测
            if (!triggeredNum.ContainsKey(rulekey))
            {
                triggeredNum[rulekey] = 0;
            }
            if (rule.STATUS == 1 && triggeredNum[rulekey] >= (rule.TRIGGERNUM ?? 1))
            {
                return;
            }

            // 如果界面上列了第几个touchdown，那从之后才开始走此规则
            if (td.Index < (rule.DIFNUM ?? 0))
            {
                return;
            }

            string key = td.Stdfid.ToString();
            //string.Format(rule.RuleKey, "PTR", "", td.Stdfid);
            if (!mouldList.ContainsKey(key))
            {
                mouldList[key] = new List<string>();
            }
            if (!nextList.ContainsKey(key))
            {
                nextList[key] = new List<string>();
            }
            if (!TriggerNum.Keys.Contains(key))
            {
                TriggerNum[key] = 0;
            }

            //限制插库20条
            if (TriggerNum[key] > 20)
            {
                return;
            }

            //存在文件，读出来，不存在模板跳出
            if (losttype)
            {
                if (mouldList[key].Count == 0)
                {
                    string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mould");
                    string outdata = File.ReadAllText(Path.Combine(folder, mir.PARTTYP + "_" + mir.JOBNAM + ".txt"));
                    string[] str = outdata.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                    mouldList[key] = new List<string>(str);
                }
            }
            else
            {
                return;
            }

            if (!string.IsNullOrEmpty(rule.REMARK) && lostTexttxtPASSFLAG.Count == 0)
            {
                foreach (var keyVal in rule.REMARK.Split("#".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] kv = keyVal.Split("@".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                    if (kv.Length != 2)
                    {
                        continue;
                    }
                    if (double.TryParse(kv[1], out double v))
                    {
                        lostTexttxtPASSFLAG[kv[0]] = v;
                    }
                    else
                    {
                        continue;
                    }

                }
            }

            if (td.IsTouchDownEnd)
            {
                if (td.DicPTR.Count > 0)
                {
                    var item = td.DicPTR[td.DicPTR.Keys.ToArray()[0]];
                    // 获取带有比较项的ptr值
                    List<PTR> checkPtr = item.Where(p => lostTexttxtPASSFLAG.ContainsKey(p.TESTTXT) && lostTexttxtPASSFLAG[p.TESTTXT] == p.RESULT).ToList();

                    // 比较带有比较项的ptr值,并与维护好的要比较的参数比较数量,如果数量一致就表示所有验证通过
                    if (checkPtr.Count + ((lostTexttxtPASSFLAG.ContainsKey("HARDBIN") && lostTexttxtPASSFLAG["HARDBIN"] == td.DicPRR[item[0].SITENUM].HARDBIN) ? 1 : 0) == lostTexttxtPASSFLAG.Count)
                    {
                        nextList[key].Clear();
                        //newList[key].Clear();
                        foreach (var ptr in item)
                        {
                            //存第一次出现的测试名称对应的lo/hi
                            //newList[key].Add(ptr.TESTTXT + "||" + ptr.TESTNUM + "||" + td.LOLIMIT[ptr.TESTTXT + "_" + ptr.TESTNUM.ToString()] + "||" + td.HILIMIT[ptr.TESTTXT + "_" + ptr.TESTNUM.ToString()]);
                            //根据OPTFLAG的值来判读lo/hi，有OPTFLAG取自己，没有OPTFLAG取第一次出现的
                            if (string.IsNullOrEmpty(ptr.OPTFLAG))
                            {
                                nextList[key].Add(ptr.TESTTXT + "||" + ptr.TESTNUM + "||" + getValue(td.LOLIMIT[ptr.TESTTXT + "_" + ptr.TESTNUM.ToString()]) + "||" + getValue(td.HILIMIT[ptr.TESTTXT + "_" + ptr.TESTNUM.ToString()]));
                            }
                            else
                            {
                                double lolimit = getValue(ptr.LOLIMIT);
                                double hilimit = getValue(ptr.HILIMIT);
                                if (lolimit == 0)
                                {
                                    lolimit = getValue(td.LOLIMIT[ptr.TESTTXT + "_" + ptr.TESTNUM.ToString()]);
                                }
                                if (hilimit == 0)
                                {
                                    hilimit = getValue(td.HILIMIT[ptr.TESTTXT + "_" + ptr.TESTNUM.ToString()]);
                                }

                                nextList[key].Add(ptr.TESTTXT + "||" + ptr.TESTNUM + "||" + lolimit + "||" + hilimit);
                            }
                        }
                    }

                    //转成字符串比较
                    string mould = string.Join(",", mouldList[key].OrderBy(p => p).ToList());
                    string next = string.Join(",", nextList[key].OrderBy(p => p).ToList());
                    if (mould != next && nextList[key].Count > 0)
                    {
                        //2者差异存库
                        string remark = "";
                        string pushremark = "";
                        var mould_triggered = mouldList[key].Except(nextList[key]).ToList().Select(p => p + "||Mould");
                        var now_triggered = nextList[key].Except(mouldList[key]).ToList();
                        var triggered = mould_triggered.Union(now_triggered);
                        pushremark = string.Join(",", triggered);
                        if (string.Join(",", triggered).Length > 3500)
                        {
                            remark = string.Join(",", triggered).Substring(0, 3500) + "...等";
                        }
                        else
                        {
                            remark = string.Join(",", triggered);
                        }

                        //触发预警
                        Task.Run(() =>
                        {
                            INSERTALERT(rule, mir, item[0], dmgr, "和模板差异：" + remark, "", pushremark);
                        });

                        //临时处理预警次数
                        TriggerNum[key]++;

                        //预警次数加1
                        triggeredNum[rulekey]++;
                    }

                    //如果后面有新出现的测试项名称，则存为新的模板
                    //if (!losttype && mouldList[key].Count > 0 && newList[key].Count > 0)
                    //{
                    //    mouldList[key] = newList[key].Union(mouldList[key]).ToList();
                    //}

                }
            }


        }

        public double getValue(double value)
        {
            //if (value.ToString().ToUpper().Contains("E"))
            //{
            //    return value;
            //}
            //else
            //{
            //    return Math.Round(value, 2);
            //}

            if (Math.Round(value, 4) == 0)
            {
                if (Math.Round(value, 7) == 0)
                {
                    return Math.Round(value, 10);
                }
                else
                {
                    return Math.Round(value, 7);
                }
            }
            else
            {
                return Math.Round(value, 4);
            }
        }

        /// <summary>
        /// ospin的累计值卡控缓存
        /// </summary>
        private Dictionary<string, double?> ospinCount = new Dictionary<string, double?>();
        private Dictionary<string, bool> ospinTriggered = new Dictionary<string, bool>();
        private Dictionary<string, double?> ospinHilimit = new Dictionary<string, double?>();
        private Dictionary<string, double?> ospinLolimit = new Dictionary<string, double?>();
        private Dictionary<string, List<string>> ospinList = new Dictionary<string, List<string>>();
        private Dictionary<string, string> ospinResult = new Dictionary<string, string>();

        //ospin的累计值卡控
        private void OSPINCOUNTTRIGGER(SYS_RULES_TESTRUN rule, PTR ptr, DatabaseManager dmgr, MIR mir)
        {
            string key = string.Format(rule.RuleOsPinKey, ptr.PARTID, ptr.StdfId);
            string key_limit = string.Format(rule.RuleOsPinKey, ptr.TESTNUM, ptr.StdfId);

            if (ptr.TESTNUM >= rule.OSPINBEGIN && ptr.TESTNUM <= rule.OSPINEND)
            {
                if (!ospinCount.ContainsKey(key))
                {
                    ospinCount.Clear();
                    ospinCount[key] = 0;
                }

                if (!ospinTriggered.ContainsKey(key))
                {
                    ospinTriggered.Clear();
                    ospinTriggered[key] = false;
                }

                if (!ospinList.ContainsKey(key))
                {
                    ospinList.Clear();
                    ospinList[key] = new List<string>();
                }

                if (!ospinHilimit.ContainsKey(key_limit))
                {
                    ospinHilimit[key_limit] = 0;
                    ospinHilimit[key_limit] = ptr.HILIMIT;
                }
                else
                {
                    ospinHilimit[key_limit] = ospinHilimit[key_limit] == 0 ? ptr.HILIMIT : ospinHilimit[key_limit];
                }

                if (!ospinLolimit.ContainsKey(key_limit))
                {
                    ospinLolimit[key_limit] = 0;
                    ospinLolimit[key_limit] = ptr.LOLIMIT;
                }
                else
                {
                    ospinLolimit[key_limit] = ospinLolimit[key_limit] == 0 ? ptr.LOLIMIT : ospinLolimit[key_limit];
                }

                // 只触发一次,且已经触发过 则不继续检测
                if (ospinTriggered[key])
                {
                    return;
                }

                if (ptr.RESULT > ospinHilimit[key_limit] || ptr.RESULT < ospinLolimit[key_limit])
                {
                    ospinCount[key]++;
                    ospinList[key].Add(ptr.TESTNUM.ToString() + "=" +
                        Math.Round(ptr.RESULT, 10).ToString("0.0000000000") +
                        " LowLimit=" + ospinLolimit[key_limit] +
                        " HighLimit=" + ospinHilimit[key_limit]);
                }

                if (ospinCount[key] > rule.COUNT)
                {
                    //值触发一次并清零
                    ospinTriggered[key] = true;

                    ospinResult[key] = "";
                    for (int j = 0; j < ospinList[key].Count; j++)
                    {
                        ospinResult[key] += ospinList[key][j] + ",";
                    }

                    Task.Run(() =>
                    {
                        INSERTALERT(rule, mir, ptr, dmgr, ospinResult[key].TrimEnd(','));
                    });
                }
            }
        }

        /// <summary>
        /// ospin的连续卡控缓存
        /// </summary>
        OSPIN ospin = null;
        private Dictionary<string, bool> ospinConsecutiveTriggered = new Dictionary<string, bool>();
        private Dictionary<string, double?> ospinConsecutiveHilimit = new Dictionary<string, double?>();
        private Dictionary<string, double?> ospinConsecutiveLolimit = new Dictionary<string, double?>();
        private Dictionary<string, string> ospinPartid = new Dictionary<string, string>();
        private Dictionary<string, List<OSPIN>> ospinConsecutive = new Dictionary<string, List<OSPIN>>();
        private Dictionary<string, List<string>> ospinConsecutiveList = new Dictionary<string, List<string>>();
        private Dictionary<string, string> ospinConsecutiveResult = new Dictionary<string, string>();
        //ospin的连续卡控
        private void OSPINCONSECUTIVETRIGGER(SYS_RULES_TESTRUN rule, PTR ptr, DatabaseManager dmgr, MIR mir)
        {
            string key_partid = string.Format(rule.RuleOsPinKey, "", ptr.StdfId);
            string key = string.Format(rule.RuleOsPinKey, ptr.PARTID, ptr.StdfId);
            string key_limit = string.Format(rule.RuleOsPinKey, ptr.TESTNUM, ptr.StdfId);

            if (!ospinPartid.ContainsKey(key_partid))
            {
                ospinPartid.Clear();
                ospinPartid[key_partid] = "0";
            }
            if (ospinPartid[key_partid] != key && ospinPartid[key_partid] != "0")
            {
                // 新的一颗
                List<OSPIN> list = ospinConsecutive[ospinPartid[key_partid]];
                if (list.Count > rule.COUNT)
                {
                    bool flag = false;
                    int i = 0;
                    int index = 0;
                    foreach (var item in list.OrderBy(p => p.testnum))
                    {
                        i++;
                        if (item.result == 0)
                        {
                            flag = true;
                            index++;
                        }
                        else
                        {
                            flag = false;
                            index = 0;
                        }
                        if (index == rule.COUNT + 1)
                        {
                            break;
                        }
                        //如果是最后一个testnum
                        if (i == list.Count)
                        {
                            if (index != rule.COUNT + 1)
                            {
                                flag = false;
                            }
                        }
                    }
                    if (flag)
                    {
                        //值触发一次并清零
                        ospinConsecutiveTriggered[key] = true;
                        ospinConsecutiveResult[key] = "";
                        for (int j = 0; j < ospinConsecutiveList[ospinPartid[key_partid]].Count; j++)
                        {
                            ospinConsecutiveResult[key] += ospinConsecutiveList[ospinPartid[key_partid]][j] + ",";
                        }
                        Task.Run(() =>
                        {
                            INSERTALERT(rule, mir, ptr, dmgr, ospinConsecutiveResult[key].TrimEnd(','));
                        });
                    }
                }
            }

            if (!ospinConsecutiveTriggered.ContainsKey(key))
            {
                ospinConsecutiveTriggered.Clear();
                ospinConsecutiveTriggered[key] = false;
            }

            if (!ospinConsecutive.ContainsKey(key))
            {
                ospinPartid[key_partid] = "0";
                ospinConsecutive.Clear();
                ospinConsecutive[key] = new List<OSPIN>();
                ospin = new OSPIN();
            }

            if (!ospinConsecutiveList.ContainsKey(key))
            {
                ospinConsecutiveList.Clear();
                ospinConsecutiveList[key] = new List<string>();
            }

            if (ptr.TESTNUM >= rule.OSPINBEGIN && ptr.TESTNUM <= rule.OSPINEND)
            {
                ospinPartid[key_partid] = key;
                ospin = new OSPIN();
                if (!ospinConsecutiveHilimit.ContainsKey(key_limit))
                {
                    ospinConsecutiveHilimit[key_limit] = 0;
                    ospinConsecutiveHilimit[key_limit] = ptr.HILIMIT;
                }
                else
                {
                    ospinConsecutiveHilimit[key_limit] = ospinConsecutiveHilimit[key_limit] == 0 ? ptr.HILIMIT : ospinConsecutiveHilimit[key_limit];
                }

                if (!ospinConsecutiveLolimit.ContainsKey(key_limit))
                {
                    ospinConsecutiveLolimit[key_limit] = 0;
                    ospinConsecutiveLolimit[key_limit] = ptr.LOLIMIT;
                }
                else
                {
                    ospinConsecutiveLolimit[key_limit] = ospinConsecutiveLolimit[key_limit] == 0 ? ptr.LOLIMIT : ospinConsecutiveLolimit[key_limit];
                }

                // 只触发一次,且已经触发过 则不继续检测
                if (ospinConsecutiveTriggered[key])
                {
                    return;
                }

                if (ptr.RESULT > ospinConsecutiveHilimit[key_limit] || ptr.RESULT < ospinConsecutiveLolimit[key_limit])
                {
                    ospin.testnum = ptr.TESTNUM;
                    ospin.result = 0;
                    ospinConsecutive[key].Add(ospin);
                    ospinConsecutiveList[key].Add(ptr.TESTNUM.ToString() + "=" +
                        Math.Round(ptr.RESULT, 10).ToString("0.0000000000") +
                        " LowLimit=" + ospinConsecutiveLolimit[key_limit] +
                        " HighLimit=" + ospinConsecutiveHilimit[key_limit]);
                }
                else
                {
                    ospin.testnum = ptr.TESTNUM;
                    ospin.result = 1;
                    ospinConsecutive[key].Add(ospin);
                }
            }
        }

        private Dictionary<string, SPEC_RULE> specPass = new Dictionary<string, SPEC_RULE>();
        private Dictionary<string, PTR> specFixed_ptr = new Dictionary<string, PTR>();
        public void SPECTRIGGER(SYS_RULES_TESTRUN rule, TouchDown td, DatabaseManager dmgr, MIR mir)
        {
            if (td.IsTouchDownEnd)
            {
                foreach (var item in td.DicPTR)
                {
                    foreach (var ptr in item.Value)
                    {
                        string key = string.Format(rule.RuleKey, "", ptr.StdfId, ptr.PARTID);
                        // 首次测试 初始化
                        if (!specPass.Keys.Contains(key))
                        {
                            specPass[key] = new SPEC_RULE();
                            specFixed_ptr[key] = new PTR();
                            specFixed_ptr[key] = ptr;
                            specPass[key].flag = false;
                            if (!string.IsNullOrEmpty(rule.REMARK))
                            {
                                // 初始化预警校验项
                                foreach (var keyVal in rule.REMARK.Split("#".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                                {
                                    string[] kv = rule.REMARK.Split("@".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                                    if (kv.Length != 2)
                                    {
                                        continue;
                                    }
                                    if (double.TryParse(kv[1], out double v))
                                    {
                                        specPass[key].validKey[kv[0]] = v;
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                }
                            }
                        }
                        //移除passflag检测
                        if (specPass[key]?.validKey.Count > 0)// 如果校验项存在
                        {
                            if (specPass[key].validKey.ContainsKey(ptr.TESTTXT))// 校验项包含当前ptr.TESTTXT
                            {
                                if (specPass[key].validKey[ptr.TESTTXT] == ptr.RESULT)// 校验值符合
                                {
                                    specPass[key].validKey.Remove(ptr.TESTTXT); // 移除符合校验值的校验项
                                }
                            }
                            else if (specPass[key].validKey.ContainsKey("HARDBIN"))
                            {
                                if (specPass[key].validKey["HARDBIN"] == td.DicPRR[ptr.SITENUM].HARDBIN)
                                {
                                    specPass[key].validKey.Remove("HARDBIN");
                                }
                                else
                                {
                                    continue;
                                }
                            }
                        }

                        //触发规则判断，临时记录触发的内容
                        if (ptr.TESTTXT == rule.TESTTXT && ptr.TESTNUM == rule.TESTNUMBER)
                        {
                            if (ptr.RESULT != 0)
                            {
                                specPass[key].flag = true;
                            }
                        }

                        if (specPass[key].flag && specPass[key].validKey.Count == 0)
                        {
                            specPass[key].flag = false;
                            Task.Run(() =>
                            {
                                INSERTALERT(rule, mir, specFixed_ptr[key], dmgr, specFixed_ptr[key].RESULT.ToString());
                            });
                        }
                    }
                }
            }
        }

        public class SPEC_RULE
        {
            public string key { get; set; }

            public bool flag { get; set; }

            // #分割两项,@分割值
            public Dictionary<string, double> validKey { get; set; } = new Dictionary<string, double>();

            //,分割项
            public Dictionary<string, string> check { get; set; } = new Dictionary<string, string>();

            // #分割两项,@分割值
            public Dictionary<string, double> ignoreKey { get; set; } = new Dictionary<string, double>();
        }

        /// <summary>
        /// 累计result增大时候的数量缓存
        /// </summary>
        Dictionary<string, double> ptsAddPASSFLAG = new Dictionary<string, double>();
        public Dictionary<string, double?> countAddCache = new Dictionary<string, double?>();
        public Dictionary<string, List<double>> sumAddTotal = new Dictionary<string, List<double>>();
        public Dictionary<string, List<double>> checkAddTotal = new Dictionary<string, List<double>>();
        public Dictionary<string, string> resultAddTotal = new Dictionary<string, string>();
        public void PTSADDTRIGGER(SYS_RULES_TESTRUN rule, TouchDown td, DatabaseManager dmgr, MIR mir)
        {
            if (!string.IsNullOrEmpty(rule.REMARK) && ptsAddPASSFLAG.Count == 0)
            {
                foreach (var keyVal in rule.REMARK.Split("#".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] kv = keyVal.Split("@".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                    if (kv.Length != 2)
                    {
                        continue;
                    }
                    if (double.TryParse(kv[1], out double v))
                    {
                        ptsAddPASSFLAG[kv[0]] = v;
                    }
                    else
                    {
                        continue;
                    }

                }
            }

            if (td.IsTouchDownEnd)
            {
                foreach (var item in td.DicPTR)
                {
                    // 获取带有比较项的ptr值
                    List<PTR> checkPtr = item.Value.Where(p => ptsAddPASSFLAG.ContainsKey(p.TESTTXT) && ptsAddPASSFLAG[p.TESTTXT] == p.RESULT).ToList();

                    // 比较带有比较项的ptr值,并与维护好的要比较的参数比较数量,如果数量一致就表示所有验证通过
                    if (checkPtr.Count + ((ptsAddPASSFLAG.ContainsKey("HARDBIN") && ptsAddPASSFLAG["HARDBIN"] == td.DicPRR[item.Value[0].SITENUM].HARDBIN) ? 1 : 0) == ptsAddPASSFLAG.Count)
                    {
                        foreach (var ptr in item.Value)
                        {
                            string triggeredKey = string.Format(rule.RuleKey, "", "", ptr.StdfId);
                            string key = string.Format(rule.RuleKey, (rule.BYSITE == 0 ? "NOSITE" : ptr.SITENUM.ToString()), ptr.TESTNUM, ptr.StdfId);

                            if (ptr.TESTNUM == rule.TESTNUMBER && ptr.TESTTXT == rule.TESTTXT)
                            {
                                if (!countAddCache.ContainsKey(key))
                                {
                                    countAddCache[key] = 0;
                                }
                                if (!checkAddTotal.ContainsKey(key))
                                {
                                    checkAddTotal[key] = new List<double>();
                                }
                                if (!sumAddTotal.ContainsKey(key))
                                {
                                    sumAddTotal[key] = new List<double>();
                                }

                                // 只触发一次,且已经触发过 则不继续检测
                                if (!triggeredNum.ContainsKey(triggeredKey))
                                {
                                    triggeredNum[triggeredKey] = 0;
                                }
                                if (rule.STATUS == 1 && triggeredNum[triggeredKey] >= (rule.TRIGGERNUM ?? 1))
                                {
                                    return;
                                }

                                sumAddTotal[key].Add(ptr.RESULT);// 统计测试值累计

                                if (sumAddTotal[key].Count == (rule.PTSGROUP ?? 1))
                                {
                                    double avg = sumAddTotal[key].Average();
                                    checkAddTotal[key].Add(avg);
                                    sumAddTotal[key] = new List<double>();

                                    if (checkAddTotal[key].Count > 1)
                                    {
                                        if (avg > (checkAddTotal[key][checkAddTotal[key].Count - 2] + (rule.PTSVALUE ?? 0)))
                                        {
                                            countAddCache[key]++;
                                        }
                                        else
                                        {
                                            checkAddTotal[key] = new List<double>();
                                            checkAddTotal[key].Add(avg);
                                            countAddCache[key] = 1;
                                        }
                                    }
                                }

                                if (countAddCache[key] > rule.COUNT)
                                {
                                    //因为触发后累计清零,所以先不记录是否仅支持一次触发了
                                    rule.Triggered = true;
                                    resultAddTotal[key] = "";
                                    for (int j = 0; j < countAddCache[key]; j++)
                                    {
                                        resultAddTotal[key] += Math.Round(checkAddTotal[key][j], 10).ToString("0.0000000000") + ",";
                                    }

                                    Task.Run(() =>
                                    {
                                        INSERTALERT(rule, mir, ptr, dmgr, resultAddTotal[key].TrimEnd(','));
                                    });
                                    // 触发后清零
                                    checkAddTotal[key] = new List<double>();
                                    countAddCache[key] = 1;

                                    //预警次数加1
                                    triggeredNum[triggeredKey]++;
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 累计result减小时候的数量缓存
        /// </summary>
        Dictionary<string, double> ptsCutPASSFLAG = new Dictionary<string, double>();
        public Dictionary<string, double?> countCutCache = new Dictionary<string, double?>();
        public Dictionary<string, List<double>> sumCutTotal = new Dictionary<string, List<double>>();
        public Dictionary<string, List<double>> checkCutTotal = new Dictionary<string, List<double>>();
        public Dictionary<string, string> resultCutTotal = new Dictionary<string, string>();
        public void PTSCUTTRIGGER(SYS_RULES_TESTRUN rule, TouchDown td, DatabaseManager dmgr, MIR mir)
        {
            if (!string.IsNullOrEmpty(rule.REMARK) && ptsCutPASSFLAG.Count == 0)
            {
                foreach (var keyVal in rule.REMARK.Split("#".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] kv = keyVal.Split("@".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                    if (kv.Length != 2)
                    {
                        continue;
                    }
                    if (double.TryParse(kv[1], out double v))
                    {
                        ptsCutPASSFLAG[kv[0]] = v;
                    }
                    else
                    {
                        continue;
                    }

                }
            }

            if (td.IsTouchDownEnd)
            {
                foreach (var item in td.DicPTR)
                {
                    // 获取带有比较项的ptr值
                    List<PTR> checkPtr = item.Value.Where(p => ptsCutPASSFLAG.ContainsKey(p.TESTTXT) && ptsCutPASSFLAG[p.TESTTXT] == p.RESULT).ToList();

                    // 比较带有比较项的ptr值,并与维护好的要比较的参数比较数量,如果数量一致就表示所有验证通过
                    if (checkPtr.Count + ((ptsCutPASSFLAG.ContainsKey("HARDBIN") && ptsCutPASSFLAG["HARDBIN"] == td.DicPRR[item.Value[0].SITENUM].HARDBIN) ? 1 : 0) == ptsCutPASSFLAG.Count)
                    {
                        foreach (var ptr in item.Value)
                        {
                            string key = string.Format(rule.RuleKey, (rule.BYSITE == 0 ? "NOSITE" : ptr.SITENUM.ToString()), ptr.TESTNUM, ptr.StdfId);
                            string triggeredKey = string.Format(rule.RuleKey, "", "", ptr.StdfId);
                            if (ptr.TESTNUM == rule.TESTNUMBER && ptr.TESTTXT == rule.TESTTXT)
                            {
                                if (!countCutCache.ContainsKey(key))
                                {
                                    countCutCache[key] = 0;
                                }
                                if (!checkCutTotal.ContainsKey(key))
                                {
                                    checkCutTotal[key] = new List<double>();
                                }
                                if (!sumCutTotal.ContainsKey(key))
                                {
                                    sumCutTotal[key] = new List<double>();
                                }

                                // 只触发一次,且已经触发过 则不继续检测
                                if (!triggeredNum.ContainsKey(triggeredKey))
                                {
                                    triggeredNum[triggeredKey] = 0;
                                }
                                if (rule.STATUS == 1 && triggeredNum[triggeredKey] >= (rule.TRIGGERNUM ?? 1))
                                {
                                    return;
                                }

                                sumCutTotal[key].Add(ptr.RESULT);// 统计测试值累计

                                if (sumCutTotal[key].Count == (rule.PTSGROUP ?? 1))
                                {
                                    double avg = sumCutTotal[key].Average();
                                    checkCutTotal[key].Add(avg);
                                    sumCutTotal[key] = new List<double>();

                                    if (checkCutTotal[key].Count > 1)
                                    {
                                        if (avg < (checkCutTotal[key][checkCutTotal[key].Count - 2] - (rule.PTSVALUE ?? 0)))
                                        {
                                            countCutCache[key]++;
                                        }
                                        else
                                        {
                                            checkCutTotal[key] = new List<double>();
                                            checkCutTotal[key].Add(avg);
                                            countCutCache[key] = 1;
                                        }
                                    }
                                }

                                if (countCutCache[key] > rule.COUNT)
                                {
                                    //因为触发后累计清零,所以先不记录是否仅支持一次触发了
                                    rule.Triggered = true;
                                    resultCutTotal[key] = "";
                                    for (int j = 0; j < countCutCache[key]; j++)
                                    {
                                        resultCutTotal[key] += Math.Round(checkCutTotal[key][j], 10).ToString("0.0000000000") + ",";
                                    }

                                    Task.Run(() =>
                                    {
                                        INSERTALERT(rule, mir, ptr, dmgr, resultCutTotal[key].TrimEnd(','));
                                        // 触发后清零
                                        //testAddTotal[key] = new List<double>();
                                    });
                                    // 触发后清零
                                    checkCutTotal[key] = new List<double>();
                                    countCutCache[key] = 1;

                                    //预警次数加1
                                    triggeredNum[triggeredKey]++;
                                }
                            }
                        }
                    }
                }
            }
        }

        //测试值的卡控
        //private void PARAMETRICTESTSTATISTICTRIGGER(SYS_RULES_TESTRUN rule, PTR ptr, DatabaseManager dmgr, MIR mir)
        //{
        //    string key = string.Format(rule.RuleKey, (rule.BYSITE == 0 ? "NOSITE" : ptr.SITENUM.ToString()), ptr.TESTNUM, ptr.StdfId);

        //    if (ptr.TESTNUM == rule.TESTNUMBER)
        //    {
        //        if (HILIMIT.ContainsKey(ptr.TESTNUM))
        //        {
        //            if (ptr.RESULT > HILIMIT[ptr.TESTNUM] * 3 || ptr.RESULT < HILIMIT[ptr.TESTNUM] / 3)
        //            {
        //                return;
        //            }
        //        }

        //        if (!testTotal.ContainsKey(key))
        //        {
        //            testTotal[key] = new List<PTR>();
        //        }


        //        testTotal[key].Add(ptr);// 统计测试值累计


        //        if (testTotal[key].Count < rule.ROLE_COUNT)
        //        {
        //            return;
        //        }
        //        // 只触发一次,且已经触发过 则不继续检测
        //        if (rule.STATUS == 1 && rule.Triggered)
        //        {
        //            return;
        //        }

        //        if (rule.Compare(testTotal[key].Take((rule.ROLE_COUNT ?? rule.BASELINE) ?? testTotal[key].Count).ToList(), out double avg))
        //        {
        //            //因为触发后累计清零,所以先不记录是否仅支持一次触发了
        //            //rule.Triggered = true;
        //            Task.Run(() =>
        //            {
        //                INSERTALERT(rule, mir, ptr, dmgr, $"{avg}");
        //                // 触发后清零
        //                testTotal[key] = new List<PTR>();
        //            });
        //        }
        //        if (testTotal[key].Count > 0)
        //        {
        //            testTotal[key].RemoveAt(0);
        //        }
        //    }
        //}


        public void PARAMETRICTESTSTATISTICTRIGGERNEW(SYS_RULES_TESTRUN rule, TouchDown td, DatabaseManager dmgr, MIR mir)
        {
            if (td.IsTouchDownEnd)
            {
                #region 定义计算相关局部变量,定义标准<性能消耗低 无需做全局变量 局部定义 用完即抛>
                // 校验好品信息 字符串切割 
                Dictionary<string, double> passFlag = null;
                #endregion

                #region 构造好品检验数据集合
                try
                {
                    // rule.PASSFLAG 有值的时候遵循rule.PASSFLAG规则解析,没值的时候new一个关联对象,确保passFlag非空
                    passFlag = string.IsNullOrEmpty(rule.REMARK) ? new Dictionary<string, double>() :
                                            rule.REMARK.Split("#".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
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

                    //增加TargerBin功能
                    if (!string.IsNullOrEmpty(rule.BINS))
                    {
                        if (!(rule.TARGERBINS_SBin.Contains(td.DicPRR[ptrList[0].SITENUM].SOFTBIN) ||
                              rule.TARGERBINS_HBin.Contains(td.DicPRR[ptrList[0].SITENUM].HARDBIN)))
                        {
                            continue;
                        }
                    }

                    // 好品标识
                    bool isPass = false;


                    #region 好品检测 非好品直接continue
                    isPass = ptrList.Count(p => passFlag.ContainsKey(p.TESTTXT) && passFlag[p.TESTTXT] == p.RESULT)
                                     + ((passFlag.ContainsKey("HARDBIN") && passFlag["HARDBIN"] == td.DicPRR[ptrList[0].SITENUM].HARDBIN) ? 1 : 0)
                                    == passFlag.Count;

                    // 如果没检测到好品 则直接下一个
                    if (!isPass)
                    {
                        continue;
                    }
                    #endregion


                    PTR ptr = ptrList.Where(p => TestCheck(p, rule)).FirstOrDefault();

                    if (ptr == null || ptr.StdfId == 0)
                    {
                        continue;
                    }

                    string triggeredKey = string.Format(rule.RuleKey, "", "", ptr.StdfId);
                    string key = string.Format(rule.RuleKey, (rule.BYSITE == 0 ? "NOSITE" : ptr.SITENUM.ToString()), ptr.TESTNUM, ptr.StdfId);

                    if (TestCheck(ptr, rule))
                    {
                        if (rule.ResultCompare(ptr.RESULT, td.LOLIMIT[ptr.TESTTXT + "_" + ptr.TESTNUM.ToString()], td.HILIMIT[ptr.TESTTXT + "_" + ptr.TESTNUM.ToString()]))
                        {
                            continue;
                        }
                        //if (ptr.RESULT > td.HILIMIT[ptr.TESTTXT + "_" + ptr.TESTNUM.ToString()] * 3 || ptr.RESULT < td.LOLIMIT[ptr.TESTTXT + "_" + ptr.TESTNUM.ToString()] / 3)
                        //{
                        //    continue;
                        //}

                        if (!testTotal.ContainsKey(key))
                        {
                            testTotal[key] = new List<PTR>();
                        }


                        testTotal[key].Add(ptr);// 统计测试值累计


                        if (testTotal[key].Count < rule.ROLE_COUNT)
                        {
                            continue;
                        }
                        // 只触发一次,且已经触发过 则不继续检测
                        if (!triggeredNum.ContainsKey(triggeredKey))
                        {
                            triggeredNum[triggeredKey] = 0;
                        }
                        if (rule.STATUS == 1 && triggeredNum[triggeredKey] >= (rule.TRIGGERNUM ?? 1))
                        {
                            return;
                        }

                        if (rule.Compare(testTotal[key].Take((rule.ROLE_COUNT ?? rule.BASELINE) ?? testTotal[key].Count).ToList(), out double avg))
                        {
                            //因为触发后累计清零,所以先不记录是否仅支持一次触发了
                            //rule.Triggered = true;
                            Task.Run(() =>
                            {
                                INSERTALERT(rule, mir, ptr, dmgr, $"{avg}");
                            });
                            // 触发后清零
                            testTotal[key] = new List<PTR>();

                            //预警次数加1
                            triggeredNum[triggeredKey]++;
                        }
                        if (testTotal[key].Count > 0)
                        {
                            testTotal[key].RemoveAt(0);
                        }
                    }
                }
            }
        }

        #region site与site之间测试项的差异 rollingwindow 清零

        Dictionary<string, Dictionary<double, List<PTR>>> S2STRIGGER = new Dictionary<string, Dictionary<double, List<PTR>>>();


        Dictionary<string, double> PASSFLAG = new Dictionary<string, double>();

        //site与site之间测试项的差异 rollingwindow 清零
        public void SITETOSITEPARAMETRICTESTSTATISTICDELTATRIGGER(SYS_RULES_TESTRUN rule, TouchDown td, DatabaseManager dmgr, MIR mir)
        {

            if (!string.IsNullOrEmpty(rule.REMARK) && PASSFLAG.Count == 0)
            {
                foreach (var keyVal in rule.REMARK.Split("#".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] kv = keyVal.Split("@".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                    if (kv.Length != 2)
                    {
                        continue;
                    }
                    if (double.TryParse(kv[1], out double v))
                    {
                        PASSFLAG[kv[0]] = v;
                    }
                    else
                    {
                        continue;
                    }

                }
            }

            if (!td.IsTouchDownEnd)
            {
                return;
            }

            string key = string.Format(rule.RuleKey, "PTR", "", td.Stdfid);

            // 只触发一次,且已经触发过 则不继续检测
            if (!triggeredNum.ContainsKey(key))
            {
                triggeredNum[key] = 0;
            }
            if (rule.STATUS == 1 && triggeredNum[key] >= (rule.TRIGGERNUM ?? 1))
            {
                return;
            }

            if (!S2STRIGGER.ContainsKey(key))
            {
                S2STRIGGER[key] = new Dictionary<double, List<PTR>>();
            }

            foreach (var item in td.DicPTR)
            {
                //增加TargerBin功能
                if (!string.IsNullOrEmpty(rule.BINS))
                {
                    if (!(rule.TARGERBINS_SBin.Contains(td.DicPRR[item.Value[0].SITENUM].SOFTBIN) ||
                        rule.TARGERBINS_HBin.Contains(td.DicPRR[item.Value[0].SITENUM].HARDBIN)))
                    {
                        continue;
                    }
                }

                if (!S2STRIGGER[key].ContainsKey(item.Key))
                {
                    S2STRIGGER[key][item.Key] = new List<PTR>();
                }
                if (PASSFLAG.Count == 0)
                {
                    // 对于未维护 好品信息检测的 满足条件的直接添加

                    PTR ptr = item.Value.Where(p => TestCheck(p, rule) && (!td.HILIMIT.ContainsKey(p.TESTTXT + "_" + p.TESTNUM.ToString()) || !rule.ResultCompare(p.RESULT, td.LOLIMIT[p.TESTTXT + "_" + p.TESTNUM.ToString()], td.HILIMIT[p.TESTTXT + "_" + p.TESTNUM.ToString()]))).FirstOrDefault();
                    //PTR ptr = item.Value.Where(p => TestCheck(p, rule) && (!td.HILIMIT.ContainsKey(p.TESTTXT + "_" + p.TESTNUM.ToString()) || (p.RESULT <= td.HILIMIT[p.TESTTXT + "_" + p.TESTNUM.ToString()] * 3 && p.RESULT >= td.LOLIMIT[p.TESTTXT + "_" + p.TESTNUM.ToString()] / 3))).FirstOrDefault();

                    S2STRIGGER[key][item.Key].Add(ptr);
                }
                else
                {

                    // 获取带有比较项的ptr值
                    List<PTR> checkPtr = item.Value.Where(p => PASSFLAG.ContainsKey(p.TESTTXT) && PASSFLAG[p.TESTTXT] == p.RESULT).ToList();



                    // 比较带有比较项的ptr值,并与维护好的要比较的参数比较数量,如果数量一致就表示所有验证通过
                    if (checkPtr.Count + ((PASSFLAG.ContainsKey("HARDBIN") && PASSFLAG["HARDBIN"] == td.DicPRR[item.Value[0].SITENUM].HARDBIN) ? 1 : 0) == PASSFLAG.Count)
                    {
                        PTR ptr = item.Value.Where(p => TestCheck(p, rule) && (!td.HILIMIT.ContainsKey(p.TESTTXT + "_" + p.TESTNUM.ToString()) || !rule.ResultCompare(p.RESULT, td.LOLIMIT[p.TESTTXT + "_" + p.TESTNUM.ToString()], td.HILIMIT[p.TESTTXT + "_" + p.TESTNUM.ToString()]))).FirstOrDefault();
                        //PTR ptr = item.Value.Where(p => TestCheck(p, rule) && (!td.HILIMIT.ContainsKey(p.TESTTXT + "_" + p.TESTNUM.ToString()) || (p.RESULT <= td.HILIMIT[p.TESTTXT + "_" + p.TESTNUM.ToString()] * 3 && p.RESULT >= td.LOLIMIT[p.TESTTXT + "_" + p.TESTNUM.ToString()] / 3))).FirstOrDefault();
                        S2STRIGGER[key][item.Key].Add(ptr);
                    }
                    else
                    {
                        S2STRIGGER[key][item.Key].Add(null);// 补偿一个空的 是为了确保一个touchdown所有site中ptr的数量保持一致
                    }
                }
            }

            foreach (var item in S2STRIGGER[key])
            {
                while (item.Value.Count < S2STRIGGER[key].Max(v => v.Value.Count))
                {
                    item.Value.Add(null);
                }
            }

            if (S2STRIGGER[key].First().Value.Count >= rule.ROLE_COUNT)
            {
                double[] keys = S2STRIGGER[key].Keys.ToArray();

                #region 此处创建符合形参的数据结构,并移除无效的PTR(stdfid=0的)
                Dictionary<string, Dictionary<double, List<PTR>>> _result = new Dictionary<string, Dictionary<double, List<PTR>>>();

                _result[key] = new Dictionary<double, List<PTR>>();

                foreach (var item in S2STRIGGER[key])
                {
                    if (!_result[key].ContainsKey(item.Key))
                    {
                        _result[key][item.Key] = new List<PTR>();
                    }
                    _result[key][item.Key].AddRange(item.Value.Where(p => p != null && p.StdfId > 0));
                }
                #endregion

                if (rule.DiffCompare(rule.ROLE_COUNT, _result, key, out string maxSite, out string minSite, out double maxAvg, out double minAvg, out double avg, out string sitetositeInfo))
                {
                    rule.Triggered = true;
                    Task.Run(() =>
                    {

                        // 写入的ptr为最后一个site的最后一个ptr
                        INSERTALERT(rule, mir, GetLastPTR(S2STRIGGER[key]) ?? td.LastPTR, dmgr, $"maxValue={maxAvg},minValue={minAvg},maxSite={maxSite},minSite={minSite},avg={avg}", sitetositeInfo);
                    });
                    // 清零
                    S2STRIGGER[key].Clear();

                    //预警次数加1
                    triggeredNum[key]++;
                }
                else
                {
                    // 移除顶部

                    for (int i = 0; i < keys.Length; i++)
                    {
                        if (S2STRIGGER[key] != null && S2STRIGGER[key][keys[i]]?.Count > 0)
                        {
                            S2STRIGGER[key][keys[i]].RemoveAt(0);
                        }
                    }

                }
            }
        }
        #endregion


        public void SIGMATRIGGER(SYS_RULES_TESTRUN rule, TouchDown td, DatabaseManager dmgr, MIR mir)
        {

            if (td.IsTouchDownEnd)
            {
                #region 定义计算相关局部变量,定义标准<性能消耗低 无需做全局变量 局部定义 用完即抛>
                // 校验好品信息 字符串切割 
                Dictionary<string, double> passFlag = null;
                #endregion

                #region 构造好品检验数据集合
                try
                {
                    // rule.PASSFLAG 有值的时候遵循rule.PASSFLAG规则解析,没值的时候new一个关联对象,确保passFlag非空
                    passFlag = string.IsNullOrEmpty(rule.REMARK) ? new Dictionary<string, double>() :
                                            rule.REMARK.Split("#".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
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
                    //增加TargerBin功能
                    if (!string.IsNullOrEmpty(rule.BINS))
                    {
                        if (!(rule.TARGERBINS_SBin.Contains(td.DicPRR[ptrList[0].SITENUM].SOFTBIN) ||
                            rule.TARGERBINS_HBin.Contains(td.DicPRR[ptrList[0].SITENUM].HARDBIN)))
                        {
                            continue;
                        }
                    }

                    // 好品标识
                    bool isPass = false;


                    #region 好品检测 非好品直接continue
                    isPass = ptrList.Count(p => passFlag.ContainsKey(p.TESTTXT) && passFlag[p.TESTTXT] == p.RESULT)
                                    + ((passFlag.ContainsKey("HARDBIN") && passFlag["HARDBIN"] == td.DicPRR[ptrList[0].SITENUM].HARDBIN) ? 1 : 0)
                                    == passFlag.Count;

                    // 如果没检测到好品 则直接下一个
                    if (!isPass)
                    {
                        continue;
                    }
                    #endregion


                    PTR ptr = ptrList.Where(p => TestCheck(p, rule)).FirstOrDefault();

                    if (ptr == null || ptr.StdfId == 0)
                    {
                        continue;
                    }

                    string triggeredKey = string.Format(rule.RuleKey, "", "", ptr.StdfId);
                    string key = string.Format(rule.RuleKey, (rule.BYSITE == 0 ? "NOSITE" : ptr.SITENUM.ToString()), ptr.TESTNUM, ptr.StdfId);

                    if (TestCheck(ptr, rule))
                    {
                        if (rule.ResultCompare(ptr.RESULT, td.LOLIMIT[ptr.TESTTXT + "_" + ptr.TESTNUM.ToString()], td.HILIMIT[ptr.TESTTXT + "_" + ptr.TESTNUM.ToString()]))
                        {
                            continue;
                        }
                        //if (ptr.RESULT > td.HILIMIT[ptr.TESTTXT + "_" + ptr.TESTNUM.ToString()] * 3 || ptr.RESULT < td.LOLIMIT[ptr.TESTTXT + "_" + ptr.TESTNUM.ToString()] / 3)
                        //{
                        //    continue;
                        //}
                        //if (HILIMIT.ContainsKey(ptr.TESTNUM))
                        //{
                        //    if (ptr.RESULT > HILIMIT[ptr.TESTNUM] * 3 || ptr.RESULT < HILIMIT[ptr.TESTNUM] / 3)
                        //    {
                        //        continue;
                        //    }
                        //}

                        if (!testTotal.ContainsKey(key))
                        {
                            testTotal[key] = new List<PTR>();
                        }


                        testTotal[key].Add(ptr);// 统计测试值累计


                        if (testTotal[key].Count < rule.ROLE_COUNT)
                        {
                            continue;
                        }
                        // 只触发一次,且已经触发过 则不继续检测
                        if (!triggeredNum.ContainsKey(triggeredKey))
                        {
                            triggeredNum[triggeredKey] = 0;
                        }
                        if (rule.STATUS == 1 && triggeredNum[triggeredKey] >= (rule.TRIGGERNUM ?? 1))
                        {
                            return;
                        }

                        if (rule.Sigma(testTotal[key].Take((rule.ROLE_COUNT ?? rule.BASELINE) ?? testTotal[key].Count).ToList(), out double sigma))
                        {
                            //因为触发后累计清零,所以先不记录是否仅支持一次触发了
                            //rule.Triggered = true;
                            Task.Run(() =>
                            {
                                INSERTALERT(rule, mir, ptr, dmgr, $"{sigma}");
                            });

                            // 触发后清零
                            testTotal[key] = new List<PTR>();
                            //预警次数加1
                            triggeredNum[triggeredKey]++;
                        }
                        testTotal[key].RemoveAt(0);

                    }
                }
            }
        }

        public void PTRCONSECUTIVEBINTRIGGER(SYS_RULES_TESTRUN rule, TouchDown td, DatabaseManager dmgr, MIR mir)
        {

            if (td.IsTouchDownEnd)
            {
                #region 定义计算相关局部变量,定义标准<性能消耗低 无需做全局变量 局部定义 用完即抛>
                // 校验好品信息 字符串切割 
                Dictionary<string, double> passFlag = null;
                #endregion

                #region 构造好品检验数据集合
                try
                {
                    // rule.PASSFLAG 有值的时候遵循rule.PASSFLAG规则解析,没值的时候new一个关联对象,确保passFlag非空
                    passFlag = string.IsNullOrEmpty(rule.REMARK) ? new Dictionary<string, double>() :
                                            rule.REMARK.Split("#".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
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

                    // 好品标识
                    bool isPass = false;


                    #region 好品检测 非好品直接continue
                    isPass = ptrList.Count(p => passFlag.ContainsKey(p.TESTTXT) && passFlag[p.TESTTXT] == p.RESULT)
                                    + ((passFlag.ContainsKey("HARDBIN") && passFlag["HARDBIN"] == td.DicPRR[ptrList[0].SITENUM].HARDBIN) ? 1 : 0)
                                    == passFlag.Count;

                    // 如果没检测到好品 则直接下一个
                    if (!isPass)
                    {
                        continue;
                    }
                    #endregion


                    PTR ptr = ptrList.Where(p => TestCheck(p, rule)).FirstOrDefault();

                    if (ptr == null || ptr.StdfId == 0)
                    {
                        continue;
                    }


                    string key = string.Format(rule.RuleKey, (rule.BYSITE == 0 ? "NOSITE" : ptr.SITENUM.ToString()), ptr.TESTNUM, ptr.StdfId);
                    string triggeredKey = string.Format(rule.RuleKey, "", "", ptr.StdfId);
                    if (TestCheck(ptr, rule))
                    {

                        if (!testTotal.ContainsKey(key))
                        {
                            testTotal[key] = new List<PTR>();
                        }


                        testTotal[key].Add(ptr);// 统计测试值累计


                        if (testTotal[key].Count < rule.ROLE_COUNT)
                        {
                            continue;
                        }
                        // 只触发一次,且已经触发过 则不继续检测
                        if (!triggeredNum.ContainsKey(triggeredKey))
                        {
                            triggeredNum[triggeredKey] = 0;
                        }
                        if (rule.STATUS == 1 && triggeredNum[triggeredKey] >= (rule.TRIGGERNUM ?? 1))
                        {
                            return;
                        }

                        if (testTotal[key].Max(p => p.RESULT) == testTotal[key].Min(p => p.RESULT))
                        {
                            //因为触发后累计清零,所以先不记录是否仅支持一次触发了
                            //rule.Triggered = true;
                            Task.Run(() =>
                            {
                                INSERTALERT(rule, mir, ptr, dmgr, $"PTRCONSECUTIVEBINTRIGGER:{testTotal[key].Last().PARTID}");
                            });
                            // 触发后清零
                            testTotal[key] = new List<PTR>();
                            //预警次数加1
                            triggeredNum[triggeredKey]++;
                        }
                        testTotal[key].RemoveAt(0);

                    }
                }
            }
        }




        Dictionary<double, Dictionary<double, Dictionary<double, ECIDWAFERITEM>>> wafer = new Dictionary<double, Dictionary<double, Dictionary<double, ECIDWAFERITEM>>>();

        Dictionary<string, double> validKey = new Dictionary<string, double>();

        string[] waferCheckTxt = null;

        List<ECIDWAFERITEM> waferCache = new List<ECIDWAFERITEM>();
        Dictionary<string, ECIDWAFERITEM> currwafer = new Dictionary<string, ECIDWAFERITEM>();
        private void ECIDWAFER(SYS_RULES_TESTRUN rule, PTR ptr, DatabaseManager dmgr, MIR mir)
        {
            // 如果 waferCheckTxt 为null 则加载比较对象
            if (waferCheckTxt == null)
            {
                // 就用out,没写错
                waferCheckTxt = rule.ECID_OUTPUTTXT?.Split(',');

                if (waferCheckTxt?.Length >= 3)
                {
                    // waferCheckTxt = waferCheckTxt.Skip(waferCheckTxt.Length - 3).ToArray();
                    if (!string.IsNullOrEmpty(rule.REMARK))
                    {
                        foreach (var keyVal in rule.REMARK.Split("#".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                        {
                            string[] kv = keyVal.Split("@".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                            if (kv.Length != 2)
                            {
                                continue;
                            }
                            if (double.TryParse(kv[1], out double v))
                            {
                                validKey[kv[0]] = v;
                            }
                            else
                            {
                                continue;
                            }

                        }
                    }
                }
                else
                {
                    return;
                }
            }

            string key = string.Format(rule.RuleKey, "PTR", "", ptr.StdfId);
            // 只触发一次,且已经触发过 则不继续检测
            if (!triggeredNum.ContainsKey(key))
            {
                triggeredNum[key] = 0;
            }
            if (rule.STATUS == 1 && triggeredNum[key] >= (rule.TRIGGERNUM ?? 1))
            {
                return;
            }

            // 如果尝试加载了比较对象waferCheckTxt还是null 则跳出
            if (waferCheckTxt == null)
            {
                return;
            }

            if (!waferCheckTxt.Contains(ptr.TESTTXT) && !validKey.ContainsKey(ptr.TESTTXT))
            {
                return;
            }

            if (!currwafer.ContainsKey(ptr.PARTID))
            {
                currwafer[ptr.PARTID] = new ECIDWAFERITEM { keys = waferCheckTxt, partId = ptr.PARTID, stdfid = ptr.StdfId, validKey = new Dictionary<string, double>(validKey) };
            }

            int index = Array.IndexOf(currwafer[ptr.PARTID].keys, ptr.TESTTXT);
            if (index != -1)
            {
                currwafer[ptr.PARTID].values[index] = ptr.RESULT.ToString();
            }

            if (currwafer[ptr.PARTID]?.validKey.Count > 0)// 如果校验项存在
            {
                if (currwafer[ptr.PARTID].validKey.ContainsKey(ptr.TESTTXT))// 校验项包含当前ptr.TESTTXT
                {
                    if (currwafer[ptr.PARTID].validKey[ptr.TESTTXT] == ptr.RESULT)// 校验值符合
                    {
                        currwafer[ptr.PARTID].validKey.Remove(ptr.TESTTXT); // 移除符合校验值的校验项
                    }
                    else
                    {
                        currwafer.Remove(ptr.PARTID);
                        return;
                    }
                }
            }


            // 校验值满 且 验证项 为0
            if (currwafer[ptr.PARTID].values.Count(p => p != null) == currwafer[ptr.PARTID].values.Length && currwafer[ptr.PARTID].validKey.Count == 0)
            {
                var refwafer = waferCache.FirstOrDefault(p => string.Join(",", p.values.Skip(p.values.Length - 3)) + "|" + p.stdfid.ToString() == string.Join(",", currwafer[ptr.PARTID].values.Skip(currwafer[ptr.PARTID].values.Length - 3)) + "|" + currwafer[ptr.PARTID].stdfid.ToString());

                // refwafer 不为null 表示有值
                if (refwafer != null && refwafer.partId != ptr.PARTID)
                {
                    DateTime now = DateTime.Now;
                    string flowid = "";
                    if (mir.FLOWID != "" && mir.FLOWID != null && mir.FLOWID.Length > 1)
                    {
                        flowid = mir.FLOWID?.Substring(0, 2);
                        //flowid = mir.FLOWID;
                    }
                    string _mailtitle = rule.ACTION + "_" + rule.TYPE + "_" + rule.PRODUCT + "_" + mir.LOTID + "_" + mir.TESTCOD + "_" + mir.SBLOTID + "_" + mir.FLOWID + "_" + mir.NODENAM + "_" + now.ToString("yyyyMMddHHmmss");

                    //Global.redis.Publish("ECIDWAFER", JToken.FromObject(new
                    //{
                    //    REFS = $"{string.Join(",", refwafer.values.Take(refwafer.values.Length - 3))}|{string.Join(",", refwafer.values.Skip(refwafer.values.Length - 3))}|{refwafer.partId}",
                    //    RESULT = $"{string.Join(",", currwafer[ptr.PARTID].values.Take(currwafer[ptr.PARTID].values.Length - 3))}|{string.Join(",", currwafer[ptr.PARTID].values.Skip(currwafer[ptr.PARTID].values.Length - 3))}|{currwafer[ptr.PARTID].partId}",
                    //    STDFID = mir.StdfId,
                    //    EQPTID = handler,
                    //    FACTORY = rule.FACTORY,
                    //    PARTTYP = mir.PARTTYP,
                    //    TESTCOD = mir.TESTCOD,
                    //    NODENAM = mir.NODENAM,
                    //    GUID = rule.GUID,
                    //    LOTID = mir.LOTID,
                    //    SBLOTID = mir.SBLOTID,
                    //    FLOWID = flowid,
                    //    DATETIME = now.ToString("yyyyMMddHHmmss"),
                    //    MAILTITLE = _mailtitle
                    //}).ToString());
                    Logs.Rule($"{rule.TYPE}-{ptr.StdfId},{string.Join(",", waferCheckTxt)}");

                    Task.Run(() =>
                    {
                        ECIDINSERTALERT(rule, ptr, dmgr, now, mir.NODENAM, _mailtitle, "");
                    });

                    //预警次数加1
                    triggeredNum[key]++;
                }
            }


            if (currwafer[ptr.PARTID]?.validKey.Count == 0)
            {
                waferCache.Add(currwafer[ptr.PARTID]);
                currwafer.Remove(ptr.PARTID);
            }

        }

        List<ECIDCOMMON> ecidCache = new List<ECIDCOMMON>();

        Dictionary<string, ECIDCOMMON> currecid = new Dictionary<string, ECIDCOMMON>();

        private void ECIDWAFERAKJ(SYS_RULES_TESTRUN rule, PTR ptr, DatabaseManager dmgr, MIR mir)
        {

            if (string.IsNullOrEmpty(rule.ECID_OUTPUTTXT))
            {
                return;
            }
            string[] checkKeys = rule.ECID_OUTPUTTXT.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            if (!checkKeys.Contains(ptr.TESTTXT))
            {
                return;
            }

            string key = string.Format(rule.RuleKey, "PTR", "", ptr.StdfId);
            // 只触发一次,且已经触发过 则不继续检测
            if (!triggeredNum.ContainsKey(key))
            {
                triggeredNum[key] = 0;
            }
            if (rule.STATUS == 1 && triggeredNum[key] >= (rule.TRIGGERNUM ?? 1))
            {
                return;
            }

            //如果值为0就跳过
            if (ptr.RESULT == 0)
            {
                return;
            }

            if (!currecid.ContainsKey(ptr.PARTID))
            {
                currecid[ptr.PARTID] = new ECIDCOMMON { keys = checkKeys, partid = ptr.PARTID, stdfid = ptr.StdfId };
            }

            int index = Array.IndexOf(currecid[ptr.PARTID].keys, ptr.TESTTXT);
            if (index != -1)
            {
                currecid[ptr.PARTID].values[index] = ptr.RESULT.ToString();
            }

            // 表示所有数据已经搜集完毕
            if (currecid[ptr.PARTID].values.Count(p => p != null) == currecid[ptr.PARTID].values.Length)
            {
                var refecid = ecidCache.FirstOrDefault(p => string.Join(",", p.values) + "|" + p.stdfid.ToString() == string.Join(",", currecid[ptr.PARTID].values) + "|" + currecid[ptr.PARTID].stdfid.ToString());
                if (refecid != null && refecid.partid != ptr.PARTID)
                {
                    DateTime now = DateTime.Now;

                    string flowid = "";
                    if (mir.FLOWID != "" && mir.FLOWID != null && mir.FLOWID.Length > 1)
                    {
                        flowid = mir.FLOWID?.Substring(0, 2);
                        //flowid = mir.FLOWID;
                    }
                    string _mailtitle = rule.ACTION + "_" + rule.TYPE + "_" + rule.PRODUCT + "_" + mir.LOTID + "_" + mir.TESTCOD + "_" + mir.SBLOTID + "_" + mir.FLOWID + "_" + mir.NODENAM + "_" + now.ToString("yyyyMMddHHmmss");

                    Global.redis.Publish("ECIDWAFERAKJ", JToken.FromObject(new
                    {
                        REFS = $"{string.Join(",", refecid.values)}|{refecid.partid}",
                        RESULT = $"{string.Join(",", currecid[ptr.PARTID].values)}|{currecid[ptr.PARTID].partid}",
                        STDFID = mir.StdfId,
                        EQPTID = handler,
                        FACTORY = rule.FACTORY,
                        PARTTYP = mir.PARTTYP,
                        TESTCOD = mir.TESTCOD,
                        NODENAM = mir.NODENAM,
                        GUID = rule.GUID,
                        LOTID = mir.LOTID,
                        SBLOTID = mir.SBLOTID,
                        FLOWID = flowid,
                        DATETIME = now.ToString("yyyyMMddHHmmss"),
                        MAILTITLE = _mailtitle
                    }).ToString());
                    Logs.Rule( $"{rule.TYPE}-{ptr.StdfId},{string.Join(",", checkKeys)}");

                    Task.Run(() =>
                    {
                        ECIDINSERTALERT(rule, ptr, dmgr, now, mir.NODENAM, _mailtitle, "");
                    });

                    //预警次数加1
                    triggeredNum[key]++;
                }
                ecidCache.Add(currecid[ptr.PARTID]);

                currecid.Remove(ptr.PARTID);
            }
        }
        private void ECID(SYS_RULES_TESTRUN rule, PTR ptr, DatabaseManager dmgr, MIR mir)
        {
            if (mir.PARTTYP.ToUpper() == rule.PRODUCT.ToUpper())
            {
                //if (mir.TESTCOD.ToUpper() != (rule.STEP ?? "").ToUpper())
                //{
                //    if (!(mir.PARTTYP?.ToUpper() == "HI1151GNCV210" || mir.PARTTYP?.ToUpper() == "HI1151SGNCV208" ||
                //            mir.PARTTYP?.ToUpper() == "HI5620GNCV100" || mir.PARTTYP?.ToUpper() == "HI5621GNCV100" ||
                //            mir.PARTTYP?.ToUpper() == "HI3798MRBCV20100000H" || mir.PARTTYP?.ToUpper() == "HI3798MRBCV2010D000H" ||
                //            mir.PARTTYP?.ToUpper() == "HI3798MRBCV30100000H" || mir.PARTTYP?.ToUpper() == "HI3798MRBCV3010D000H" ||
                //            mir.PARTTYP?.ToUpper() == "HI3798MRBCV31100000" || mir.PARTTYP?.ToUpper() == "HI3798MRBCV3110D000"))
                //    {
                //        return;
                //    }
                //}
                // 初始化比较对象
                if (checkTxt == null)
                {
                    checkTxt = rule.ECID_CHECKTXT?.Split(',');
                    outputTxt = rule.ECID_OUTPUTTXT?.Split(',');
                }
            }

            string key = string.Format(rule.RuleKey, "PTR", "", ptr.StdfId);
            // 只触发一次,且已经触发过 则不继续检测
            if (!triggeredNum.ContainsKey(key))
            {
                triggeredNum[key] = 0;
            }
            if (rule.STATUS == 1 && triggeredNum[key] >= (rule.TRIGGERNUM ?? 1))
            {
                return;
            }

            if (checkTxt == null || outputTxt == null)
            {
                return;
            }

            // outputTxt 包含所有check项目数据,validKey包含要检测项数据 两个里面都包含的话就是不需要比较的数据 直接忽略
            if (!outputTxt.Contains(ptr.TESTTXT) && (!cacheLot.ContainsKey(ptr.SITENUM) ? true : !cacheLot[ptr.SITENUM].validKey.ContainsKey(ptr.TESTTXT)))
            {
                return;
            }

            // 检测到第一个要比较的TESTTXT的时候
            if (ptr.TESTTXT == checkTxt[0])
            {
                // 如果以sitenum为key的数据不存在则初始化cacheLot对象
                if (!cacheLot.ContainsKey(ptr.SITENUM))
                {
                    cacheLot[ptr.SITENUM] = new ECIDITEM() { stdfid = ptr.StdfId };
                }
                // 初始化 checkTxtResult,outputTxtResult
                cacheLot[ptr.SITENUM].checkTxtResult = new List<string> { ptr.RESULT.ToString() };
                cacheLot[ptr.SITENUM].outputTxtResult = new List<string> { ptr.RESULT.ToString() };

                if (!string.IsNullOrEmpty(rule.REMARK))
                {
                    // 初始化预警校验项
                    foreach (var keyVal in rule.REMARK.Split("#".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                    {
                        string[] kv = keyVal.Split("@".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                        if (kv.Length != 2)
                        {
                            continue;
                        }
                        if (double.TryParse(kv[1], out double v))
                        {
                            cacheLot[ptr.SITENUM].validKey[kv[0]] = v;
                        }
                        else
                        {
                            continue;
                        }

                    }
                }
            }
            else if (checkTxt.Contains(ptr.TESTTXT))// 如果不是第一个需要比较的TESTTXT值,则插入cacheLot对象
            {
                cacheLot[ptr.SITENUM].checkTxtResult.Add(ptr.RESULT.ToString());
                cacheLot[ptr.SITENUM].outputTxtResult.Add(ptr.RESULT.ToString());
            }

            if (cacheLot[ptr.SITENUM].validKey.Count > 0)// 如果校验项存在
            {
                if (cacheLot[ptr.SITENUM].validKey.ContainsKey(ptr.TESTTXT))// 校验项包含当前ptr.TESTTXT
                {
                    if (cacheLot[ptr.SITENUM].validKey[ptr.TESTTXT] == ptr.RESULT)// 校验值符合
                    {
                        cacheLot[ptr.SITENUM].validKey.Remove(ptr.TESTTXT); // 移除符合校验值的校验项
                    }
                }
            }

            // 如果参照 refLot为空,以及cacheLot对象的checkTxtResult数量等于参照字段数量,则给refLot赋值
            if (refLot == null && cacheLot[ptr.SITENUM].checkTxtResult.Count == checkTxt.Length && cacheLot[ptr.SITENUM].validKey.Count == 0)//首次检查完毕的测试项作为参照值
            {
                if (rule.STDTYPE == "1")
                {
                    string _lotid = mir.LOTID;
                    string _testcod = mir.TESTCOD;
                    string _flowid = mir.FLOWID?.Substring(0, 1);
                    string fileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ECID", _lotid, "_", _testcod, "_", _flowid, ".json");

                    if (File.Exists(fileName))
                    {
                        refLot = JToken.Parse(File.ReadAllText(fileName)).ToObject<ECIDITEM>();
                    }
                    else
                    {
                        refLot = new ECIDITEM
                        {
                            checkTxtResult = cacheLot[ptr.SITENUM].checkTxtResult,
                            outputTxtResult = cacheLot[ptr.SITENUM].outputTxtResult,
                            stdfid = ptr.StdfId
                        };
                    }
                }
                else
                {
                    refLot = new ECIDITEM
                    {
                        checkTxtResult = cacheLot[ptr.SITENUM].checkTxtResult,
                        outputTxtResult = cacheLot[ptr.SITENUM].outputTxtResult,
                        stdfid = ptr.StdfId
                    };
                }
            }

            // 如果参照对象已经存在 但是输出字段元素数量小于数组字段长度,则继续添加
            if (refLot != null && refLot.outputTxtResult.Count < outputTxt.Length)
            {
                if (outputTxt.Contains(ptr.TESTTXT) && !checkTxt.Contains(ptr.TESTTXT))
                {
                    refLot.outputTxtResult.Add(ptr.RESULT.ToString());
                }
            }



            if (refLot != null
                && refLot.checkTxtResult.Count == cacheLot[ptr.SITENUM].checkTxtResult.Count
                && string.Join(",", refLot.checkTxtResult) != string.Join(",", cacheLot[ptr.SITENUM].checkTxtResult))// 如果比较值不同则跳变预警
            {
                // 在跳变预警的时候 看是否已经搜集满要报告的数据信息 如果没有则继续搜集 直到搜集满
                if (outputTxt.Contains(ptr.TESTTXT) && !checkTxt.Contains(ptr.TESTTXT))
                {
                    cacheLot[ptr.SITENUM].outputTxtResult.Add(ptr.RESULT.ToString());
                }

                if (cacheLot[ptr.SITENUM].validKey.Count > 0)// 如果校验项存在
                {
                    if (cacheLot[ptr.SITENUM].validKey.ContainsKey(ptr.TESTTXT))// 校验项包含当前ptr.TESTTXT
                    {
                        if (cacheLot[ptr.SITENUM].validKey[ptr.TESTTXT] == ptr.RESULT)// 校验值符合
                        {
                            cacheLot[ptr.SITENUM].validKey.Remove(ptr.TESTTXT); // 移除符合校验值的校验项
                        }
                    }
                }

                // 需要报告的数据已经搜集满 且 校验项count为0 则可以触发预警
                if (cacheLot[ptr.SITENUM].outputTxtResult.Count == outputTxt.Count() && cacheLot[ptr.SITENUM].validKey.Count == 0)
                {
                    DateTime now = DateTime.Now;
                    string flowid = "";
                    if (mir.FLOWID != "" && mir.FLOWID != null && mir.FLOWID.Length > 1)
                    {
                        flowid = mir.FLOWID?.Substring(0, 2);
                        //flowid = mir.FLOWID;
                    }
                    string _mailtitle = rule.ACTION + "_" + rule.TYPE + "_" + rule.PRODUCT + "_" + mir.LOTID + "_" + mir.TESTCOD + "_" + mir.SBLOTID + "_" + mir.FLOWID + "_" + mir.NODENAM + "_" + now.ToString("yyyyMMddHHmmss");

                    Global.redis.Publish("ECID", JToken.FromObject(new
                    {
                        REFS = string.Join(",", refLot.outputTxtResult.Take(checkTxt.Length)) + "|" + string.Join(",", refLot.outputTxtResult.Skip(checkTxt.Length)),
                        RESULT = string.Join(",", cacheLot[ptr.SITENUM].outputTxtResult.Take(checkTxt.Length)) + "|" + string.Join(",", cacheLot[ptr.SITENUM].outputTxtResult.Skip(checkTxt.Length)),
                        LOTID = mir.LOTID,
                        SBLOTID = mir.SBLOTID,
                        EQPTID = handler,
                        FACTORY = rule.FACTORY,
                        STDFID = mir.StdfId,
                        PARTTYP = mir.PARTTYP,
                        TESTCOD = mir.TESTCOD,
                        NODENAM = mir.NODENAM,
                        FLOWID = flowid,
                        GUID = rule.GUID,
                        DATETIME = now.ToString("yyyyMMddHHmmss"),
                        MAILTITLE = _mailtitle
                    }).ToString());
                    Logs.Rule( $"{rule.TYPE}-{ptr.StdfId}");
                    rule.Triggered = true;

                    Task.Run(() =>
                    {
                        ECIDINSERTALERT(rule, ptr, dmgr, now, mir.NODENAM, _mailtitle, "");
                    });

                    //预警次数加1
                    triggeredNum[key]++;
                }
            }
        }

        public Dictionary<string, int> rule_sum = new Dictionary<string, int>();
        private void INSERTALERT(SYS_RULES_TESTRUN rule, MIR mir, PTR ptr, DatabaseManager dmgr, string remark = "", string sitetositeInfo = "", string pushremark = "")
        {
            DateTime now = DateTime.Now;
            string flowid = "";
            if (mir.FLOWID != "" && mir.FLOWID != null && mir.FLOWID.Length > 1)
            {
                flowid = mir.FLOWID?.Substring(0, 2);
                //flowid = mir.FLOWID;
            }
            string _mailtitle = rule.ACTION + "_" + rule.TYPE + "_" + rule.PRODUCT + "_" + mir.LOTID + "_" + mir.TESTCOD + "_" + mir.SBLOTID + "_" + mir.FLOWID + "_" + mir.NODENAM + "_" + now.ToString("yyyyMMddHHmmss");

            if (rule.UNLOCKROLE == "JSCC")
            {
                Thread.Sleep(200);
                string key = string.Format(rule.RuleOsPinKey, ptr.StdfId, "");
                if (!rule_sum.Keys.Contains(key))
                {
                    rule_sum[key] = 0;
                }
                rule_sum[key]++;
                //限制插库10条
                if (rule_sum[key] > 10 && rule.ACTION == "Send Email")
                {
                    return;
                }
                _mailtitle = rule.ACTION + "_" + rule.TYPE + "_" + rule.PRODUCT + "_" + mir.LOTID + "_" + mir.TESTCOD + "_" + mir.SBLOTID + "_" + mir.FLOWID + "_" + mir.NODENAM + "_" + now.ToString("yyyyMMddHHmmss") + "_" + rule_sum[key].ToString();
            }

            Global.redis.Publish("PTR", JToken.FromObject(new
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
                PUSHREMARK = string.IsNullOrEmpty(pushremark) ? "-" : pushremark,
                SITETOSITEINFO = string.IsNullOrEmpty(sitetositeInfo) ? "-" : sitetositeInfo,
                MAILTITLE = _mailtitle
            }).ToString());
            Logs.Rule( $"{rule.TYPE}-{ptr.StdfId}-{ptr.PARTID}");

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

        private void ECIDINSERTALERT(SYS_RULES_TESTRUN rule, PTR ptr, DatabaseManager dmgr, DateTime now, string _eqpname, string _mailtitle, string remark = "")
        {
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
                    stdfid = ptr?.StdfId,
                    testnum = ptr?.TESTNUM,
                    headnum = ptr?.HEADNUM,
                    sitenum = ptr?.SITENUM,
                    testflg = ptr?.TESTFLG,
                    parmflg = ptr?.PARMFLG,
                    result = ptr?.RESULT,
                    testtxt = ptr?.TESTTXT,
                    alarmid = ptr?.ALARMID,
                    optflag = ptr?.OPTFLAG,
                    resscal = ptr?.RESSCAL,
                    llmscal = ptr?.LLMSCAL,
                    hlmscal = ptr?.HLMSCAL,
                    lolimit = ptr?.LOLIMIT,
                    hilimit = ptr?.HILIMIT,
                    units = ptr?.UNITS,
                    cresfmt = ptr?.CRESFMT,
                    cllmfmt = ptr?.CLLMFMT,
                    chlmfmt = ptr?.CHLMFMT,
                    lospec = ptr?.LOSPEC,
                    hispec = ptr?.HISPEC,
                    stdfindex = ptr?.STDFINDEX,
                    rules_guid = rule?.GUID,
                    rules_time = now,
                    product = rule?.PRODUCT,
                    eqname = _eqpname,
                    remark = remark,
                    guid,
                    partid = ptr?.PARTID,
                    mailtitle = _mailtitle
                });
            }
            catch (Exception ex)
            {
                Logs.Error( "ecid 插库", ex);
            }
            Logs.Rule( $"{rule?.TYPE}-{ptr?.StdfId}-{ptr?.PARTID}");
        }

        string lotid = string.Empty;
        string testcod = string.Empty;
        string flowid = string.Empty;

        public void Attach(string type, BaseEntity entity, List<SYS_RULES_TESTRUN> lstRules)
        {
            if (type == "SDR")
            {
                handler = (entity as SDR).HANDID;
            }
            //if (type == "MIR")
            //{
            //    _eqpname = (entity as MIR).NODENAM;
            //    _lotid = (entity as MIR).LOTID;
            //    _testcod = (entity as MIR).TESTCOD;
            //    _sblotid = (entity as MIR).SBLOTID;
            //    _flowid = (entity as MIR).FLOWID?.Substring(0, 2);
            //}
            if (lstRules != null)
            {
                foreach (SYS_RULES_TESTRUN rule in lstRules)
                {
                    if (rule.TYPE == "ECID" && rule.STDTYPE == "1")
                    {
                        if (type == "MIR")
                        {
                            lotid = (entity as MIR).LOTID;
                            testcod = (entity as MIR).TESTCOD;
                            flowid = (entity as MIR).FLOWID?.Substring(0, 1);
                        }
                        if (type == "MRR")
                        {
                            //序列化变量到文件
                            string fileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ECID", lotid, "_", testcod, "_", flowid, ".json");
                            File.WriteAllText(fileName, JToken.FromObject(refLot).ToString());
                        }
                    }
                }
            }

        }

        //string txtPARTTYP = string.Empty;
        //string txtJOBNAM = string.Empty;
        //string txtStdfId = string.Empty;
        //public void WriteTxt(string type, BaseEntity entity, string factory)
        //{
        //    if (type == "MIR")
        //    {
        //        txtPARTTYP = (entity as MIR).PARTTYP;
        //        txtJOBNAM = (entity as MIR).JOBNAM;
        //        txtStdfId = (entity as MIR).StdfId.ToString();
        //    }
        //    if (type == "MRR")
        //    {
        //        if (mouldList.ContainsKey(txtStdfId))
        //        {
        //            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mould", factory);
        //            if (!Directory.Exists(folder))
        //            {
        //                Directory.CreateDirectory(folder);
        //            }
        //            File.WriteAllText(Path.Combine(folder, txtPARTTYP + "_" + txtJOBNAM + ".txt"), string.Join(",", mouldList[txtStdfId]));
        //        }
        //    }
        //}


        public PTR GetLastPTR(Dictionary<double, List<PTR>> dicPTR)
        {
            PTR tmp = null;
            PTR result = null;
            double[] keys = dicPTR.Keys.ToArray();
            for (int i = 0; i < dicPTR.Keys.Count; i++)
            {
                tmp = dicPTR[keys[i]].Last(p => p != null);
                if (result == null)
                {
                    result = tmp;
                }
                else
                {
                    if (int.Parse(tmp.PARTID) > int.Parse(result.PARTID))
                    {
                        result = tmp;
                    }
                }
            }
            return result;
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
                this.wafer = null;
                disposedValue = true;
            }
        }

        // TODO: 仅当以上 Dispose(bool disposing) 拥有用于释放未托管资源的代码时才替代终结器。
        ~PTRRule()
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

    public class LOSTTESTTXT
    {
        public double TESTNUM { get; set; }
        public string TESTTXT { get; set; }
        public double LOLIMIT { get; set; }
        public double HILIMIT { get; set; }
    }

    public class ECIDITEM
    {
        public double stdfid { get; set; }

        public List<string> checkTxtResult { get; set; } = new List<string>();

        public List<string> outputTxtResult { get; set; } = new List<string>();

        // #分割两项,@分割值
        public Dictionary<string, double> validKey { get; set; } = new Dictionary<string, double>();

        public Dictionary<string, double> ignoreKey { get; set; } = new Dictionary<string, double>();

        public Dictionary<string, string> check { get; set; } = new Dictionary<string, string>();

    }

    public class ECIDWAFERITEM
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

    public class ECIDCOMMON
    {
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

        public string partid { get; set; }

        public int stdfid { get; set; }

    }

    public class OSPIN
    {
        public double testnum { get; set; }

        public double result { get; set; }
    }
}


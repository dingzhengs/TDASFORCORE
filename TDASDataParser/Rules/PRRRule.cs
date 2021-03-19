using Newtonsoft.Json.Linq;
using TDASDataParser.StdfTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TDASCommon;
using System.Threading;

namespace TDASDataParser.Rules
{
    public class PRRRule : IDisposable
    {
        /// <summary>
        /// 总颗数 于所有规则调用 不应该在规则里手动修改
        /// </summary>
        private long total = 0L;

        private Dictionary<string, double> testNumTotal = new Dictionary<string, double>();
        private Dictionary<string, double> maxSiteNum = new Dictionary<string, double>();
        private Dictionary<string, int> batch = new Dictionary<string, int>();
        private Dictionary<string, int> triggeredNum = new Dictionary<string, int>();

        // 用于分项统计各site的测试值 key.sitenum.ptr
        private Dictionary<string, Dictionary<double, List<PRR>>> testsTotal = new Dictionary<string, Dictionary<double, List<PRR>>>();

        //校验site值是否有测试到 key.testNumTotal.sitenum.boolean
        private Dictionary<string, Dictionary<double, Dictionary<double, bool>>> siteExists = new Dictionary<string, Dictionary<double, Dictionary<double, bool>>>();

        private string handler = "";
        //private string _eqpname = "";
        //private string _lotid = "";
        //private string _testcod = "";
        //private string _sblotid = "";
        //private string _flowid = "";

        public void Match(List<SYS_RULES_TESTRUN> lstRules, BaseEntity entity, MIR mir, DatabaseManager dmgr)
        {
            if (lstRules != null)
            {
                #region 总数统计
                total++;

                PRR prr = entity as PRR;
                #endregion

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

                        if (rule.TYPE == "BINCOUNTTRIGGER")
                        {
                            if (rule.STDTYPE == "1")
                            {
                                BINCOUNTSTDTRIGGER(rule, mir, prr, dmgr);
                            }
                            else
                            {
                                BINCOUNTTRIGGER(rule, mir, prr, dmgr);
                            }
                        }
                        else if (rule.TYPE == "CONSECUTIVEBINTRIGGER")
                        {
                            CONSECUTIVEBINTRIGGER(rule, mir, prr, dmgr);
                        }
                        else if (rule.TYPE == "SITETOSITEYIELDTRIGGER")
                        {
                            SITETOSITEYIELDTRIGGER(rule, mir, prr, dmgr);
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

        #region 累计不良
        /// <summary>
        /// 累计不良时候的数量缓存
        /// </summary>
        private Dictionary<string, double?> countCache = new Dictionary<string, double?>();

        /// <summary>
        /// 累计不良数量
        /// </summary>
        private Dictionary<string, double?> failedCount = new Dictionary<string, double?>();

        /// <summary>
        /// 统计每次rolling中 最后一个不良 
        /// </summary>
        private Dictionary<string, PRR> lastFailed = new Dictionary<string, PRR>();

        /// <summary>
        /// PRR数据集缓存
        /// </summary>
        private Dictionary<string, List<PRR>> entityCache = new Dictionary<string, List<PRR>>();

        private void BINCOUNTSTDTRIGGER(SYS_RULES_TESTRUN rule, MIR mir, PRR prr, DatabaseManager dmgr)
        {
            if (rule.BINFAILEDBINS_SBin == null && rule.BINFAILEDBINS_HBin == null)
            {
                return;
            }

            string key = string.Format(rule.RuleKey, (rule.BYSITE == 0 ? "NOSITE" : prr.SITENUM.ToString()), "PRR", prr.StdfId);
            string triggeredKey = string.Format(rule.RuleKey, "", "PRR", prr.StdfId);
            // 只触发一次,且已经触发过 则不继续检测
            if (!triggeredNum.ContainsKey(triggeredKey))
            {
                triggeredNum[triggeredKey] = 0;
            }
            if (rule.STATUS == 1 && triggeredNum[triggeredKey] >= (rule.TRIGGERNUM ?? 1))
            {
                return;
            }

            bool isfailed = false;// 本次是否不良

            var a = failedCount;// 添加对象引用 防止变量被GC;

            // 首次测试 初始化
            if (!entityCache.Keys.Contains(key))
            {
                entityCache[key] = new List<PRR>();
                failedCount[key] = 0;
            }

            entityCache[key].Add(prr);

            // 记录不良数据
            if ((rule.BINFAILEDBINS_SBin.Contains(prr.SOFTBIN) ||
                rule.BINFAILEDBINS_HBin.Contains(prr.HARDBIN)) &&
                (rule.COUNTTYPE == 0 ? prr.SOFTBIN != 1 : true))
            {
                failedCount[key]++;
                lastFailed[key] = prr;
                isfailed = true;
            }

            // 比较baseline 不满足baseline直接返回
            if (entityCache[key].Count() < rule.BASELINE)
            {
                return;
            }

            //当首次满足baseline的时 或者 本次测试是不良的时候 才比较判断
            if (isfailed)
            {
                if (rule.COUNTTYPE == 0)//统计个数
                {
                    // 不良数量大于卡控数量 则尝试报警
                    if (failedCount[key] + rule.YIELDCOUNT > rule.COUNT)
                    {
                        rule.Triggered = true;
                        Task.Run(() =>
                        {
                            INSERTALERT(rule, mir, prr, dmgr, DateTime.Now, $"{failedCount[key]}");
                        });

                        //预警次数加1
                        triggeredNum[triggeredKey]++;
                    }
                }
                else// 统计百分比
                {
                    // 不良/总数量 
                    var per = (failedCount[key] + rule.YIELDCOUNT) / (entityCache[key].Count() + rule.TOTALCOUNT) * 100;

                    if (rule.PRRPercentCompare(per))
                    {
                        // 记录最后一次满足预警的颗数位置
                        rule.Triggered = true;
                        Task.Run(() =>
                        {
                            INSERTALERT(rule, mir, prr, dmgr, DateTime.Now, $"{per}%");
                        });

                        //预警次数加1
                        triggeredNum[triggeredKey]++;

                        //涉及百分比的都要清零
                        // 移除已经统计的总不良数量
                        failedCount[key] = 0;
                        // 移除最后一次不良以及之前的数据
                        entityCache[key].RemoveAll(p => Convert.ToInt32(p.PARTID) <= Convert.ToInt32(lastFailed[key].PARTID));
                        // 移除最后一次不良的登记信息
                        lastFailed.Remove(key);
                    }
                }
            }
        }

        private void BINCOUNTTRIGGER(SYS_RULES_TESTRUN rule, MIR mir, PRR prr, DatabaseManager dmgr)
        {
            if (rule.BINFAILEDBINS_SBin == null && rule.BINFAILEDBINS_HBin == null)
            {
                return;
            }

            // 有rolling的 走rolling规则
            if ((rule.ROLE_COUNT ?? 0) > 0)
            {
                this.ROLLINGWINDOW(rule, mir, prr, dmgr);
                return;
            }

            string key = string.Format(rule.RuleKey, (rule.BYSITE == 0 ? "NOSITE" : prr.SITENUM.ToString()), "PRR", prr.StdfId);
            string triggeredKey = string.Format(rule.RuleKey, "", "PRR", prr.StdfId);
            // 只触发一次,且已经触发过 则不继续检测
            if (!triggeredNum.ContainsKey(triggeredKey))
            {
                triggeredNum[triggeredKey] = 0;
            }
            if (rule.STATUS == 1 && triggeredNum[triggeredKey] >= (rule.TRIGGERNUM ?? 1))
            {
                return;
            }

            bool isfailed = false;// 本次是否不良

            // 首次测试 初始化
            if (!entityCache.Keys.Contains(key))
            {
                entityCache[key] = new List<PRR>();
                failedCount[key] = 0;
            }

            entityCache[key].Add(prr);

            // 记录不良数据
            if ((rule.BINFAILEDBINS_SBin.Contains(prr.SOFTBIN) ||
                rule.BINFAILEDBINS_HBin.Contains(prr.HARDBIN)) &&
                (rule.COUNTTYPE == 0 ? prr.SOFTBIN != 1 : true))
            {
                failedCount[key]++;
                lastFailed[key] = prr;
                isfailed = true;
            }

            // 比较baseline 不满足baseline直接返回
            if (entityCache[key].Count() < rule.BASELINE)
            {
                return;
            }

            //当首次满足baseline的时 或者 本次测试是不良 且basline>=测试数量
            if (entityCache[key].Count() == (rule.BASELINE ?? 0) || (isfailed && entityCache[key].Count() >= (rule.BASELINE ?? 0)))
            {
                if (rule.COUNTTYPE == 0)//统计个数
                {
                    // 不良数量大于卡控数量 则尝试报警
                    if (failedCount[key] > rule.COUNT)
                    {
                        rule.Triggered = true;
                        Task.Run(() =>
                        {
                            INSERTALERT(rule, mir, prr, dmgr, DateTime.Now, $"{failedCount[key]}");
                        });
                        //预警次数加1
                        triggeredNum[triggeredKey]++;
                    }
                }
                else// 统计百分比
                {
                    // 不良/总数量 
                    var per = failedCount[key] / entityCache[key].Count() * 100.00;

                    if (rule.PRRPercentCompare(per))
                    {
                        // 记录最后一次满足预警的颗数位置
                        rule.Triggered = true;
                        Task.Run(() =>
                        {
                            INSERTALERT(rule, mir, prr, dmgr, DateTime.Now, $"{per}%");
                        });
                        //预警次数加1
                        triggeredNum[triggeredKey]++;

                        //涉及百分比的都要清零
                        //if (rule.BASELINE > 0)
                        //{
                        // 移除已经统计的总不良数量
                        failedCount[key] = 0;
                        // 移除最后一次不良以及之前的数据
                        entityCache[key].RemoveAll(p => Convert.ToInt32(p.PARTID) <= Convert.ToInt32(lastFailed[key].PARTID));
                        // 移除最后一次不良的登记信息
                        lastFailed.Remove(key);
                        //}
                    }
                }

            }

            //统计个数
            if (rule.COUNTTYPE == 0)
            {
                // 当测试的总数量比baseline大2的是时候,每次检测完毕,移除集合的第一个值
                // 一方面减少程序资源压力,一方面大于2才移除数量可以保证满足BASELINE
                if (entityCache[key].Count() - rule.BASELINE > 2)
                {
                    entityCache[key].RemoveAt(0);
                }
            }



        }

        private void ROLLINGWINDOW(SYS_RULES_TESTRUN rule, MIR mir, PRR prr, DatabaseManager dmgr)
        {
            string key = string.Format(rule.RuleKey, (rule.BYSITE == 0 ? "NOSITE" : prr.SITENUM.ToString()), "PRR", prr.StdfId);


            //首次 初始化缓存数据集合
            if (!entityCache.Keys.Contains(key))
            {
                entityCache[key] = new List<PRR>();
            }

            // 加载数据
            entityCache[key].Add(prr);

            // 记录最后一个不良记录
            if ((rule.BINFAILEDBINS_SBin.Contains(prr.SOFTBIN) ||
                rule.BINFAILEDBINS_HBin.Contains(prr.HARDBIN)) &&
                prr.SOFTBIN != 1)
            //if (rule.BINFAILEDBINS.Contains(rule.BINS_TYPE == "HBin" ? prr.HARDBIN : prr.SOFTBIN) && prr.SOFTBIN != 1)
            {
                lastFailed[key] = prr;
            }

            // 满足baseline后验证是否触发预警
            if (entityCache[key].Count >= rule.BASELINE)
            {
                // 不良数量
                int failedCount = entityCache[key].Count(p => (rule.BINFAILEDBINS_SBin.Contains(prr.SOFTBIN) || rule.BINFAILEDBINS_HBin.Contains(prr.HARDBIN)) && p.SOFTBIN != 1);

                //满足触发预警条件
                if (failedCount > rule.COUNT)
                {
                    Task.Run(() =>
                    {
                        INSERTALERT(rule, mir, prr, dmgr, DateTime.Now, $"{failedCount}");
                    });

                    // 移除最后一个不良以及之前的所有数据
                    entityCache[key].RemoveAll(p => Convert.ToInt32(p.PARTID) <= Convert.ToInt32(lastFailed[key].PARTID));
                }
                else
                {
                    // 没触发报警 但是满足rolling数量 移除第一个数量
                    if (entityCache[key].Count == rule.ROLE_COUNT)
                    {
                        entityCache[key].RemoveAt(0);
                    }
                }
            }
        }

        #endregion
        //连续累计不良
        private void CONSECUTIVEBINTRIGGER(SYS_RULES_TESTRUN rule, MIR mir, PRR prr, DatabaseManager dmgr)
        {

            string key = string.Format(rule.RuleKey, (rule.BYSITE == 0 ? "NOSITE" : prr.SITENUM.ToString()), "PRR", prr.StdfId);
            string triggeredKey = string.Format(rule.RuleKey, "", "PRR", prr.StdfId);
            string resultKey = string.Format(rule.RuleKey, (rule.BYSITE == 0 ? "NOSITE" : prr.SITENUM.ToString()), "PRR", prr.StdfId) + ".result";

            // 只触发一次,且已经触发过 则不继续检测
            if (!triggeredNum.ContainsKey(triggeredKey))
            {
                triggeredNum[triggeredKey] = 0;
            }
            if (rule.STATUS == 1 && triggeredNum[triggeredKey] >= (rule.TRIGGERNUM ?? 1))
            {
                return;
            }

            // 考虑性能 尽量就不用反射了 直接判断
            if (rule.BINFAILEDBINS_SBin != null || rule.BINFAILEDBINS_HBin != null)
            //if (rule.BINFAILEDBINS != null)
            {
                double count = 0;

                if (countCache.ContainsKey(key))
                {
                    count = countCache[key].Value;
                }
                else
                {
                    countCache[key] = 0;
                }
                if (rule.BINS_TYPE == "HBin" || rule.BINS_TYPE == "SBin" || rule.BINS_TYPE == "HBin+SBin")
                {
                    if ((rule.BINFAILEDBINS_SBin.Contains(prr.SOFTBIN) ||
                        rule.BINFAILEDBINS_HBin.Contains(prr.HARDBIN)) &&
                        prr.SOFTBIN != 1)
                    //if (rule.BINFAILEDBINS.Contains(rule.BINS_TYPE == "HBin" ? prr.HARDBIN : prr.SOFTBIN) && prr.SOFTBIN != 1)
                    {
                        // 检测sbin的连续不良,如果存在连续,则count增加计数,并添加缓存实体,如果出现不连续,则重置计数和缓存
                        count++;
                        if (!entityCache.ContainsKey(resultKey))
                        {
                            entityCache[resultKey] = new List<PRR>();
                        }
                        entityCache[resultKey].Add(prr);
                    }
                    else
                    {
                        count = 0;
                        entityCache.Remove(resultKey);
                    }
                }
                countCache[key] = count;
                if (count > rule.COUNT)
                {
                    // 满足连续failed条件后,从缓存抓取为一个局部变量(防止异步插入导致的脏缓存数据)
                    List<PRR> lstPrr = entityCache[resultKey];
                    // 清空已经有的缓存(防止异步插入导致的脏缓存数据)
                    entityCache.Remove(resultKey);

                    if (total < (rule.BASELINE ?? 0))
                    {
                        return;
                    }

                    rule.Triggered = true;
                    DateTime now = DateTime.Now;
                    string flowid = "";
                    if (mir.FLOWID != "" && mir.FLOWID != null && mir.FLOWID.Length > 1)
                    {
                        flowid = mir.FLOWID?.Substring(0, 2);
                        //flowid = mir.FLOWID;
                    }
                    string _mailtitle = rule.ACTION + "_" + rule.TYPE + "_" + rule.PRODUCT + "_" + mir.LOTID + "_" + mir.TESTCOD + "_" + mir.SBLOTID + "_" + mir.FLOWID + "_" + mir.NODENAM + "_" + now.ToString("yyyyMMddHHmmss");

                    Global.redis.Publish("PRR", JToken.FromObject(new
                    {
                        ISSTOP = rule.ACTION == "Pause Production" ? "1" : "0",
                        EQPTID = handler,
                        GUID = rule.GUID,
                        EQPNAME = mir.NODENAM,
                        STDFID = prr.StdfId,
                        DATETIME = now.ToString("yyyyMMddHHmmss"),
                        SITENUM = prr.SITENUM,
                        REMARK = $"{count}",
                        MAILTITLE = _mailtitle
                    }).ToString());
                    Logs.Rule($"{rule.TYPE}-{prr.StdfId}");

                    Task.Run(() =>
                    {
                        foreach (var item in lstPrr)
                        {
                            INSERTALERT(rule, mir, item, dmgr, now, $"{count}");
                        }
                    });

                    //预警次数加1
                    triggeredNum[triggeredKey]++;
                    //清零
                    countCache[key] = 0;
                }
            }


        }

        private void SITETOSITEYIELDTRIGGER(SYS_RULES_TESTRUN rule, MIR mir, PRR prr, DatabaseManager dmgr)
        {
            string key = string.Format(rule.RuleKey, "PRR", "SITETOSITEYIELD", prr.StdfId);

            // 只触发一次,且已经触发过 则不继续检测
            if (!triggeredNum.ContainsKey(key))
            {
                triggeredNum[key] = 0;
            }
            if (rule.STATUS == 1 && triggeredNum[key] >= (rule.TRIGGERNUM ?? 1))
            {
                return;
            }

            if (!testsTotal.ContainsKey(key))
            {
                testsTotal[key] = new Dictionary<double, List<PRR>>();
                siteExists[key] = new Dictionary<double, Dictionary<double, bool>>();
                testNumTotal[key] = 0;
                maxSiteNum[key] = -1;
                batch[key] = 1;
            }

            if (prr.SITENUM > maxSiteNum[key])
            {
                maxSiteNum[key] = prr.SITENUM;
            }
            else
            {
                maxSiteNum[key] = prr.SITENUM;
                testNumTotal[key]++;
            }

            if (testNumTotal[key] == rule.ROLE_COUNT)
            {
                if (rule.DiffPRRCompare(testsTotal, key, out string maxSite, out string minSite, out double maxYield, out double minYield, out double diffYield))
                {
                    rule.Triggered = true;
                    Task.Run(() =>
                    {
                        INSERTALERT(rule, mir, prr, dmgr, DateTime.Now, $"maxYield={maxYield},minYield={minYield},maxSite={maxSite},minSite={minSite},gap={diffYield}");
                    });

                    //预警次数加1
                    triggeredNum[key]++;

                    //清零
                    testsTotal[key] = new Dictionary<double, List<PRR>>();
                    siteExists[key] = new Dictionary<double, Dictionary<double, bool>>();
                    testNumTotal[key] = 0;
                    maxSiteNum[key] = -1;
                    batch[key] = 1;

                    goto ADDITEM;
                }


                foreach (List<PRR> item in testsTotal[key].Values)
                {
                    if (item.Count == 0)
                    {
                        continue;
                    }
                    if (siteExists[key][batch[key]].ContainsKey(item[0].SITENUM))
                    {
                        item.RemoveAt(0);
                    }
                }
                siteExists[key].Remove(batch[key]);

                testNumTotal[key]--;
                batch[key]++;
            }
        ADDITEM:
            if (!testsTotal[key].ContainsKey(prr.SITENUM))
            {
                testsTotal[key][prr.SITENUM] = new List<PRR>();
            }
            // 因为当前批次数+已经累计数量
            if (!siteExists[key].ContainsKey(batch[key] + testNumTotal[key]))
            {
                siteExists[key][batch[key] + testNumTotal[key]] = new Dictionary<double, bool>();
            }
            if (!siteExists[key][batch[key] + testNumTotal[key]].ContainsKey(prr.SITENUM))
            {
                siteExists[key][batch[key] + testNumTotal[key]][prr.SITENUM] = true;
            }

            testsTotal[key][prr.SITENUM].Add(prr); // 统计sitenum的累计值
        }

        public Dictionary<string, int> rule_sum = new Dictionary<string, int>();
        private void INSERTALERT(SYS_RULES_TESTRUN rule, MIR mir, PRR prr, DatabaseManager dmgr, DateTime now, string remark = "")
        {
            string flowid = "";
            if (mir.FLOWID != "" && mir.FLOWID != null && mir.FLOWID.Length > 1)
            {
                flowid = mir.FLOWID?.Substring(0, 2);
                //flowid = mir.FLOWID;
            }
            string _mailtitle = rule.ACTION + "_" + rule.TYPE + "_" + rule.PRODUCT + "_" + mir.LOTID + "_" + mir.TESTCOD + "_" + mir.SBLOTID + "_" + mir.FLOWID + "_" + mir.NODENAM + "_" + now.ToString("yyyyMMddHHmmss");

            //针对JSCC
            if (rule.UNLOCKROLE == "JSCC")
            {
                Thread.Sleep(200);
                string key = string.Format(rule.RuleOsPinKey, prr.StdfId, "");
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

            
            if (rule.TYPE != "CONSECUTIVEBINTRIGGER")
            {
                Global.redis.Publish("PRR", JToken.FromObject(new
                {
                    ISSTOP = rule.ACTION == "Pause Production" ? "1" : "0",
                    EQPTID = handler,
                    GUID = rule.GUID,
                    EQPNAME = mir.NODENAM,
                    STDFID = prr.StdfId,
                    DATETIME = now.ToString("yyyyMMddHHmmss"),
                    SITENUM = prr.SITENUM,
                    REMARK = string.IsNullOrEmpty(remark) ? "-" : remark,
                    MAILTITLE = _mailtitle
                }).ToString());
                Logs.Rule($"{rule.TYPE}-{prr.StdfId}");
            }

            string guid = Guid.NewGuid().ToString("N");
            new DatabaseManager().ExecuteNonQuery(@"INSERT INTO SYS_RULES_SHOW
                                                                  (PRODUCT,
                                                                   STDFID,
                                                                   HEADNUM,
                                                                   SITENUM,
                                                                   PARTFLG,
                                                                   NUMTEST,
                                                                   HARDBIN,
                                                                   SOFTBIN,
                                                                   XCOORD,
                                                                   YCOORD,
                                                                   TESTT,
                                                                   PARTID,
                                                                   PARTTXT,
                                                                   PARTFIX,
                                                                   STDFINDEX,
                                                                   WORKINGT,
                                                                   CREATEDATE,
                                                                   RULES_GUID,
                                                                   GROUPID,
                                                                   REMARK,
                                                                   EQNAME,RULES_TIME,GUID,MAILTITLE)
                                                                VALUES
                                                                  (:PRODUCT,
                                                                   :STDFID,
                                                                   :HEADNUM,
                                                                   :SITENUM,
                                                                   :PARTFLG,
                                                                   :NUMTEST,
                                                                   :HARDBIN,
                                                                   :SOFTBIN,
                                                                   :XCOORD,
                                                                   :YCOORD,
                                                                   :TESTT,
                                                                   :PARTID,
                                                                   :PARTTXT,
                                                                   :PARTFIX,
                                                                   :STDFINDEX,
                                                                   :WORKINGT,
                                                                   :CREATEDATE,
                                                                   :RULES_GUID,
                                                                   :GROUPID,
                                                                   :REMARK,
                                                                   :EQNAME,:RULES_TIME,:GUID,:MAILTITLE)
                                                                ", new
            {
                PRODUCT = rule.PRODUCT,
                STDFID = prr.StdfId,
                HEADNUM = prr.HEADNUM,
                SITENUM = prr.SITENUM,
                PARTFLG = prr.PARTFLG,
                NUMTEST = prr.NUMTEST,
                HARDBIN = prr.HARDBIN,
                SOFTBIN = prr.SOFTBIN,
                XCOORD = prr.XCOORD,
                YCOORD = prr.YCOORD,
                TESTT = prr.TESTT,
                PARTID = prr.PARTID,
                PARTTXT = prr.PARTTXT,
                PARTFIX = prr.PARTFIX,
                STDFINDEX = prr.STDFINDEX,
                WORKINGT = prr.WORKINGT,
                CREATEDATE = now,
                RULES_GUID = rule.GUID,
                GROUPID = 1,
                REMARK = remark,
                EQNAME = mir.NODENAM,
                RULES_TIME = now,
                GUID = guid,
                MAILTITLE = _mailtitle
            });
        }

        public void Attach(string type, BaseEntity entity)
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
                this.entityCache = null;
                this.failedCount = null;
                this.handler = null;
                this.lastFailed = null;
                this.maxSiteNum = null;
                this.siteExists = null;
                this.testNumTotal = null;
                this.testsTotal = null;

                disposedValue = true;
            }
        }

        // TODO: 仅当以上 Dispose(bool disposing) 拥有用于释放未托管资源的代码时才替代终结器。
        ~PRRRule()
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

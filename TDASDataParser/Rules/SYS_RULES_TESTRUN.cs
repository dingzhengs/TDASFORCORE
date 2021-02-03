using TDASDataParser.StdfTypes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TDASDataParser.Rules
{
    public class SYS_RULES_TESTRUN
    {
        public string GUID { get; set; }

        public string EQPNAME { get; set; }

        public string PRODUCT { get; set; }

        public string DEVICEGROUP { get; set; }

        //TYPE类型为BINCOUNTTRIGGER，代表累积   
        public string TYPE { get; set; }

        public string ACTION { get; set; }

        //是否多份STD文件(0：否，1：是)
        public string STDTYPE { get; set; }

        public int? YIELDCOUNT { get; set; }

        public int? TOTALCOUNT { get; set; }

        //HBin或者SBin
        public string BINS_TYPE { get; set; }

        //如果BINS_TYPE里面是HBin，那这个值是HBin1+HBin2+HBin3这种格式；如果BINS_TYPE里面是SBin，那这个值是SBin1+SBin2+SBin3这种格式
        public string BINS { get; set; }

        //代表累积的值，当达到这个值的时候就触发规则
        public double? COUNT { get; set; }

        //0代表持续触发，1代表规则只触发一次。
        public int? STATUS { get; set; }

        //0：否，1：是
        public int? BYSITE { get; set; }

        // rollingwindow时候的记录范围
        public int? ROLE_COUNT { get; set; }

        // count值类型, 0 标识数值,1 标识百分比
        public int? COUNTTYPE { get; set; }

        // rule 开始统计
        public int? BASELINE { get; set; }

        public string MAXTYPE { get; set; }

        public int? MAXSTATUS { get; set; }

        public double? MAXVALUE { get; set; }

        public string MINTYPE { get; set; }

        public int? MINSTATUS { get; set; }

        public double? MINVALUE { get; set; }

        public double? TESTNUMBER { get; set; }

        public string ECID_CHECKTXT { get; set; }

        public string ECID_OUTPUTTXT { get; set; }

        public string FACTORY { get; set; }

        public string STEP { get; set; }

        public string REMARK { get; set; }

        public string TESTTXT { get; set; }

        public string UNLOCKROLE { get; set; }

        public double? OSPINBEGIN { get; set; }

        public double? OSPINEND { get; set; }

        public double? PTSGROUP { get; set; }

        public double? PTSVALUE { get; set; }

        public double? TRIGGERNUM { get; set; }

        public double? DIFNUM { get; set; }

        // 规则比较 比较平均值和测试值的差别
        public bool Compare(List<PTR> total, out double avg)
        {
            bool? maxValue = null;

            bool? minValue = null;

            avg = Math.Round(total.Sum(p => p.RESULT) / total.Count, 10);

            if (!string.IsNullOrEmpty(MAXTYPE))
            {
                if (MAXTYPE.Trim() == ">")
                {
                    if (MAXVALUE < avg)
                    {
                        maxValue = true;
                    }
                }
                else
                {
                    if (MAXVALUE <= avg)
                    {
                        maxValue = true;
                    }
                }
            }

            if (!string.IsNullOrEmpty(MINTYPE))
            {
                if (MINTYPE.Trim() == "<")
                {
                    if (MINVALUE > avg)
                    {
                        minValue = true;
                    }
                }
                else
                {
                    if (MINVALUE >= avg)
                    {
                        minValue = true;
                    }
                }
            }

            // 都没值 说明没满足预警条件,直接返回false
            if (!maxValue.HasValue && !minValue.HasValue)
            {
                return false;
            }
            else
            {
                // 如果最大值和最小值有值 且值一致
                if (MAXVALUE.HasValue && MINVALUE.HasValue && MAXVALUE.Value == MINVALUE.Value)
                {
                    // 则必须同时满足maxValue和minValue 都是true
                    return (maxValue ?? false) && (minValue ?? false);
                }
                else // 否则
                {
                    // 有一个值为true则返回
                    return (maxValue ?? false) || (minValue ?? false);
                }
            }
        }

        // 规则比较 比较最大良率与最小良率之差和测试值的差别
        public bool DiffPRRCompare(Dictionary<string, Dictionary<double, List<PRR>>> result, string key, out string maxSite, out string minSite, out double maxYield, out double minYield, out double diffYield)
        {
            maxYield = 0;
            minYield = 10000000;
            diffYield = 0;

            maxSite = "";
            minSite = "";

            var ger = result[key].GetEnumerator();

            while (ger.MoveNext())
            {
                double pass = result[key][ger.Current.Key].Count(p => p.SOFTBIN == 1);
                double count = result[key][ger.Current.Key].Count;
                double currYield = pass / count * 1.00000;

                if (count == 0)
                {
                    continue;
                }

                if (currYield > maxYield)
                {
                    maxSite = ger.Current.Key.ToString();
                    maxYield = currYield;
                }

                if (currYield < minYield)
                {
                    minSite = ger.Current.Key.ToString();
                    minYield = currYield;
                }
            }

            diffYield = Math.Round((maxYield - minYield), 10);

            if (diffYield > this.COUNT / 100)
            {
                return true;
            }
            return false;
        }

        // 规则比较 比较最大平均值与最小平均值之差和测试值的差别
        public bool DiffCompare(int? role_count, Dictionary<string, Dictionary<double, List<PTR>>> result, string key, out string maxSite, out string minSite, out double maxAvg, out double minAvg, out double diffAvg, out string sitetositeInfo)
        {
            bool? maxValue = null;
            bool? minValue = null;

            maxAvg = double.MinValue;
            minAvg = double.MaxValue;
            diffAvg = 0;

            maxSite = "";
            minSite = "";

            sitetositeInfo = "";

            var ger = result[key].GetEnumerator();

            while (ger.MoveNext())
            {
                double sum = result[key][ger.Current.Key].Sum(p => p.RESULT);
                double count = result[key][ger.Current.Key].Count;
                double currAvg = Math.Round(sum / count, 10);

                //if (count < role_count / 2)
                //{
                //    continue;
                //}

                if (count == 0)
                {
                    continue;
                }
                else
                {
                    sitetositeInfo += "SITE" + result[key][ger.Current.Key][0].SITENUM + "=" + currAvg + ";   ";
                }

                if (currAvg > maxAvg)
                {
                    maxSite = ger.Current.Key.ToString();
                    maxAvg = currAvg;
                }

                if (currAvg < minAvg)
                {
                    minSite = ger.Current.Key.ToString();
                    minAvg = currAvg;
                }
            }

            if (maxAvg == double.MinValue || minAvg == double.MaxValue)
            {
                return false;
            }

            diffAvg = maxAvg - minAvg;

            if (!string.IsNullOrEmpty(MAXTYPE))
            {
                if (MAXTYPE.Trim() == ">")
                {
                    if (MAXVALUE < diffAvg)
                    {
                        maxValue = true;
                    }
                }
                else
                {
                    if (MAXVALUE <= diffAvg)
                    {
                        maxValue = true;
                    }
                }
            }

            if (!string.IsNullOrEmpty(MINTYPE))
            {
                if (MINTYPE.Trim() == "<")
                {
                    if (MINVALUE > diffAvg)
                    {
                        minValue = true;
                    }
                }
                else
                {
                    if (MINVALUE >= diffAvg)
                    {
                        minValue = true;
                    }
                }
            }

            // 都没值 说明没满足预警条件,直接返回false
            if (!maxValue.HasValue && !minValue.HasValue)
            {
                return false;
            }
            else
            {
                // 如果最大值和最小值有值 且值一致
                if (MAXVALUE.HasValue && MINVALUE.HasValue && MAXVALUE.Value == MINVALUE.Value)
                {
                    // 则必须同时满足maxValue和minValue 都是true
                    return (maxValue ?? false) && (minValue ?? false);
                }
                else // 否则
                {
                    // 有一个值为true则返回
                    return (maxValue ?? false) || (minValue ?? false);
                }
            }
        }

        // 测试比较 只有满roll_count的才比较
        public bool DiffComparetmp1(Dictionary<string, Dictionary<double, List<PTR>>> result, string key, out string maxSite, out string minSite, out double maxAvg, out double minAvg, out double diffAvg)
        {
            bool? maxValue = null;
            bool? minValue = null;

            maxAvg = 0;
            minAvg = 10000000;
            diffAvg = 0;

            maxSite = "";
            minSite = "";



            var ger = result[key].GetEnumerator();

            while (ger.MoveNext())
            {
                double sum = result[key][ger.Current.Key].Sum(p => p.RESULT);
                double count = result[key][ger.Current.Key].Count;
                double currAvg = Math.Round(sum / count, 10);

                if (count == 0)
                {
                    continue;
                }

                if (currAvg > maxAvg)
                {
                    maxSite = ger.Current.Key.ToString();
                    maxAvg = currAvg;
                }

                if (currAvg < minAvg)
                {
                    minSite = ger.Current.Key.ToString();
                    minAvg = currAvg;
                }
            }




            diffAvg = maxAvg - minAvg;

            if (!string.IsNullOrEmpty(MAXTYPE))
            {
                if (MAXTYPE.Trim() == "<")
                {
                    if (MAXVALUE < diffAvg)
                    {
                        maxValue = true;
                    }
                }
                else
                {
                    if (MAXVALUE <= diffAvg)
                    {
                        maxValue = true;
                    }
                }
            }

            if (!string.IsNullOrEmpty(MINTYPE))
            {
                if (MINTYPE.Trim() == ">")
                {
                    if (MINVALUE > diffAvg)
                    {
                        minValue = true;
                    }
                }
                else
                {
                    if (MINVALUE >= diffAvg)
                    {
                        minValue = true;
                    }
                }
            }

            // 都没值 说明没满足预警条件,直接返回false
            if (!maxValue.HasValue && !minValue.HasValue)
            {
                return false;
            }
            else
            {
                // 如果最大值和最小值有值 且值一致
                if (MAXVALUE.HasValue && MINVALUE.HasValue && MAXVALUE.Value == MINVALUE.Value)
                {
                    // 则必须同时满足maxValue和minValue 都是true
                    return (maxValue ?? false) && (minValue ?? false);
                }
                else // 否则
                {
                    // 有一个值为true则返回
                    return (maxValue ?? false) || (minValue ?? false);
                }
            }

        }

        public bool DiffComparetmp(Dictionary<string, Dictionary<double, List<PTR>>> result, string key, out string maxSite, out string minSite, out double maxAvg, out double minAvg, out double diffAvg)
        {
            bool? maxValue = null;
            bool? minValue = null;

            maxAvg = 0;
            minAvg = 10000000;
            diffAvg = 0;

            maxSite = "";
            minSite = "";



            var ger = result[key].GetEnumerator();

            while (ger.MoveNext())
            {
                double sum = result[key][ger.Current.Key].Take(50).Sum(p => p.RESULT);
                double count = 50;
                double currAvg = Math.Round(sum / count, 10);

                if (currAvg == 0)
                {
                    continue;
                }

                if (currAvg > maxAvg)
                {
                    maxSite = ger.Current.Key.ToString();
                    maxAvg = currAvg;
                }

                if (currAvg < minAvg)
                {
                    minSite = ger.Current.Key.ToString();
                    minAvg = currAvg;
                }
            }




            diffAvg = maxAvg - minAvg;

            if (!string.IsNullOrEmpty(MAXTYPE))
            {
                if (MAXTYPE.Trim() == "<")
                {
                    if (MAXVALUE < diffAvg)
                    {
                        maxValue = true;
                    }
                }
                else
                {
                    if (MAXVALUE <= diffAvg)
                    {
                        maxValue = true;
                    }
                }
            }

            if (!string.IsNullOrEmpty(MINTYPE))
            {
                if (MINTYPE.Trim() == ">")
                {
                    if (MINVALUE > diffAvg)
                    {
                        minValue = true;
                    }
                }
                else
                {
                    if (MINVALUE >= diffAvg)
                    {
                        minValue = true;
                    }
                }
            }

            // 都没值 说明没满足预警条件,直接返回false
            if (!maxValue.HasValue && !minValue.HasValue)
            {
                return false;
            }
            else
            {
                // 如果最大值和最小值有值 且值一致
                if (MAXVALUE.HasValue && MINVALUE.HasValue && MAXVALUE.Value == MINVALUE.Value)
                {
                    // 则必须同时满足maxValue和minValue 都是true
                    return (maxValue ?? false) && (minValue ?? false);
                }
                else // 否则
                {
                    // 有一个值为true则返回
                    return (maxValue ?? false) || (minValue ?? false);
                }
            }
        }

        // 规则比较 sigma 计算
        public bool Sigma(List<PTR> total, out double sigmaValue)
        {
            if (total.Count < 2)
            {
                sigmaValue = 0;
                return false;
            }
            bool? maxValue = null;

            bool? minValue = null;

            // 获取平均值
            double avg = Math.Round(total.Sum(p => p.RESULT) / total.Count, 10);
            double sum = total.Sum(p => Math.Pow((p.RESULT - avg), 2));
            sigmaValue = Math.Round(Math.Sqrt(sum / (total.Count - 1)), 10);

            if (!string.IsNullOrEmpty(MAXTYPE))
            {
                if (MAXTYPE.Trim() == ">")
                {
                    if (MAXVALUE < sigmaValue)
                    {
                        maxValue = true;
                    }
                }
                else
                {
                    if (MAXVALUE <= sigmaValue)
                    {
                        maxValue = true;
                    }
                }
            }

            if (!string.IsNullOrEmpty(MINTYPE))
            {
                if (MINTYPE.Trim() == "<")
                {
                    if (MINVALUE > sigmaValue)
                    {
                        minValue = true;
                    }
                }
                else
                {
                    if (MINVALUE >= sigmaValue)
                    {
                        minValue = true;
                    }
                }
            }

            // 都没值 说明没满足预警条件,直接返回false
            if (!maxValue.HasValue && !minValue.HasValue)
            {
                return false;
            }
            else
            {
                // 如果最大值和最小值有值 且值一致
                if (MAXVALUE.HasValue && MINVALUE.HasValue && MAXVALUE.Value == MINVALUE.Value)
                {
                    // 则必须同时满足maxValue和minValue 都是true
                    return (maxValue ?? false) && (minValue ?? false);
                }
                else // 否则
                {
                    // 有一个值为true则返回
                    return (maxValue ?? false) || (minValue ?? false);
                }
            }
        }

        public bool PRRPercentCompare(double? pre)
        {
            bool? maxValue = null;
            bool? minValue = null;

            if (!string.IsNullOrEmpty(MAXTYPE))
            {
                if (MAXTYPE.Trim() == ">")
                {
                    if (MAXVALUE < pre)
                    {
                        maxValue = true;
                    }
                }
                else
                {
                    if (MAXVALUE <= pre)
                    {
                        maxValue = true;
                    }
                }
            }

            if (!string.IsNullOrEmpty(MINTYPE))
            {
                if (MINTYPE.Trim() == "<")
                {
                    if (MINVALUE > pre)
                    {
                        minValue = true;
                    }
                }
                else
                {
                    if (MINVALUE >= pre)
                    {
                        minValue = true;
                    }
                }
            }

            // 都没值 说明没满足预警条件,直接返回false
            if (!maxValue.HasValue && !minValue.HasValue)
            {
                return false;
            }
            else
            {
                // 如果最大值和最小值有值 且值一致
                if (MAXVALUE.HasValue && MINVALUE.HasValue && MAXVALUE.Value == MINVALUE.Value)
                {
                    // 则必须同时满足maxValue和minValue 都是true
                    return (maxValue ?? false) && (minValue ?? false);
                }
                else // 否则
                {
                    // 有一个值为true则返回
                    return (maxValue ?? false) || (minValue ?? false);
                }
            }
        }

        // 离散值的判断
        public bool ResultCompare(double result, double lolimit, double hilimit)
        {
            double loresult = lolimit >= 0 ? lolimit / 3 : lolimit * 3;
            double hiresult = hilimit >= 0 ? hilimit * 3 : hilimit / 3;
            if (result > hiresult || result < loresult)
            {
                return true;
            }
            else
            {
                return false;
            }
        }


        //public bool DiffCompare(Dictionary<string, Dictionary<double, double?>> count, Dictionary<string, Dictionary<double, double?>> total, string key, out string maxSite, out string minSite, out double maxAvg, out double minAvg, out double diffAvg)
        //{
        //    bool? maxValue = null;
        //    bool? minValue = null;

        //    maxAvg = 0;
        //    minAvg = 10000000;
        //    diffAvg = 0;

        //    maxSite = "";
        //    minSite = "";


        //    var ger = count[key].GetEnumerator();

        //    while (ger.MoveNext())
        //    {

        //        if ((total[key][ger.Current.Key].Value / count[key][ger.Current.Key].Value) > maxAvg)
        //        {
        //            maxSite = ger.Current.Key.ToString();
        //            maxAvg = (total[key][ger.Current.Key].Value / count[key][ger.Current.Key].Value);
        //        }

        //        if ((total[key][ger.Current.Key].Value / count[key][ger.Current.Key].Value) < minAvg)
        //        {
        //            minSite = ger.Current.Key.ToString();
        //            minAvg = (total[key][ger.Current.Key].Value / count[key][ger.Current.Key].Value);
        //        }
        //    }




        //    diffAvg = maxAvg - minAvg;

        //    if (!string.IsNullOrEmpty(MAXTYPE))
        //    {
        //        if (MAXTYPE.Trim() == "<")
        //        {
        //            if (MAXVALUE < diffAvg)
        //            {
        //                maxValue = true;
        //            }
        //        }
        //        else
        //        {
        //            if (MAXVALUE <= diffAvg)
        //            {
        //                maxValue = true;
        //            }
        //        }
        //    }

        //    if (!string.IsNullOrEmpty(MINTYPE))
        //    {
        //        if (MINTYPE.Trim() == ">")
        //        {
        //            if (MINVALUE > diffAvg)
        //            {
        //                minValue = true;
        //            }
        //        }
        //        else
        //        {
        //            if (MINVALUE >= diffAvg)
        //            {
        //                minValue = true;
        //            }
        //        }
        //    }

        //    // 都没值 说明没满足预警条件,直接返回false
        //    if (!maxValue.HasValue && !minValue.HasValue)
        //    {
        //        return false;
        //    }
        //    // 都有值,则两个值取与,只有都为true时候才满足条件,针对于>=和<= 相同值时候
        //    if (maxValue.HasValue && minValue.HasValue)
        //    {
        //        return maxValue.Value && minValue.Value;
        //    }

        //    // 其中一个有值,另一个没值 就返回true
        //    if (maxValue.HasValue && !minValue.HasValue)
        //    {
        //        return true;
        //    }
        //    // 其中一个有值,另一个没值 就返回true
        //    if (!maxValue.HasValue && minValue.HasValue)
        //    {
        //        return true;
        //    }

        //    // 不明真相,返回false
        //    return false;
        //}



        //如果是TYPE是BINCOUNTTRIGGER类型,则转换为int数组
        //public double[] BINFAILEDBINS
        //{
        //    get
        //    {
        //        if (!string.IsNullOrEmpty(BINS_TYPE))
        //        {
        //            return BINS.Replace(BINS_TYPE, "").Split("+".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(P => Convert.ToDouble(P)).ToArray();
        //        }
        //        else
        //        {
        //            return null;
        //        }
        //    }
        //}

        public double[] BINFAILEDBINS_HBin
        {
            get
            {
                if (!string.IsNullOrEmpty(BINS_TYPE))
                {
                    return BINS.Replace("HBin", "").Split("+".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Where(O => O.IndexOf("SBin") < 0).Select(P => Convert.ToDouble(P)).ToArray();
                }
                else
                {
                    return null;
                }
            }
        }

        public double[] BINFAILEDBINS_SBin
        {
            get
            {
                if (!string.IsNullOrEmpty(BINS_TYPE))
                {
                    return BINS.Replace("SBin", "").Split("+".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Where(O => O.IndexOf("HBin") < 0).Select(P => Convert.ToDouble(P)).ToArray();
                }
                else
                {
                    return null;
                }
            }
        }

        public double[] TARGERBINS_HBin
        {
            get
            {
                return BINS.Replace("HBin", "").Split("+".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Where(O => O.IndexOf("SBin") < 0).Select(P => Convert.ToDouble(P)).ToArray();
            }
        }

        public double[] TARGERBINS_SBin
        {
            get
            {
                return BINS.Replace("SBin", "").Split("+".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Where(O => O.IndexOf("HBin") < 0).Select(P => Convert.ToDouble(P)).ToArray();
            }
        }


        //是否已经触发过
        public bool Triggered { get; set; }

        string keypre = Guid.NewGuid().ToString("N");

        public string RuleKey
        {
            get
            {
                /// EQPNAME.PRODUCT.BINCOUNTTRIGGER.HBin.BYSITE.PRR.123
                return $"{this.keypre}-{EQPNAME}.{PRODUCT}.{GUID}.{{0}}.{{1}}_{{2}}";
            }
        }

        public string RuleOsPinKey
        {
            get
            {
                /// EQPNAME.PRODUCT.BINCOUNTTRIGGER.HBin.BYSITE.PRR.123
                return $"{this.keypre}-{EQPNAME}.{PRODUCT}.{GUID}.{{0}}.{{1}}";
            }
        }
    }
}

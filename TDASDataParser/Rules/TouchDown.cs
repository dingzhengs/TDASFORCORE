using System;
using System.Collections.Generic;
using System.Linq;
using TDASCommon;
using TDASDataParser.StdfTypes;

namespace TDASDataParser.Rules
{
    public class TouchDown
    {
        public TouchDown(int Stdfid)
        {
            this.Stdfid = Stdfid;
        }
        public Dictionary<int, List<PTR>> DicPTR = new Dictionary<int, List<PTR>>();

        public Dictionary<int, PRR> DicPRR = new Dictionary<int, PRR>();

        /// <summary>
        /// 当前touchdown数
        /// </summary>
        public int Index { get; private set; } = 1;

        /// <summary>
        /// 第一个site编号
        /// </summary>
        public int FirstSite { get; private set; }

        /// <summary>
        /// 最后一个site编号
        /// </summary>
        public int LastSite { get; private set; }

        /// <summary>
        /// 每个touchdown的最后一个ptr
        /// </summary>
        public PTR LastPTR { get; private set; }

        public Dictionary<string, double> HILIMIT { get; set; } = new Dictionary<string, double>();

        public Dictionary<string, double> LOLIMIT { get; set; } = new Dictionary<string, double>();

        public int Stdfid { get; }

        public bool IsTouchDownEnd { get; private set; } = false;

        public void AddPTR(PTR ptr)
        {
            this.Clear();
            if (!DicPTR.Keys.Contains(ptr.SITENUM))
            {
                DicPTR[ptr.SITENUM] = new List<PTR>();
            }

            if (!HILIMIT.ContainsKey(ptr.TESTTXT + "_" + ptr.TESTNUM.ToString()))
            {
                HILIMIT.Add(ptr.TESTTXT + "_" + ptr.TESTNUM.ToString(), ptr.HILIMIT);
            }
            if (!LOLIMIT.ContainsKey(ptr.TESTTXT + "_" + ptr.TESTNUM.ToString()))
            {
                LOLIMIT.Add(ptr.TESTTXT + "_" + ptr.TESTNUM.ToString(), ptr.LOLIMIT);
            }
            //if (ptr.HILIMIT != 0 && ptr.LOLIMIT != 0)
            //{
                
            //}

            LastPTR = ptr;

            DicPTR[ptr.SITENUM].Add(ptr);
        }

        public void AddPRR(PRR prr)
        {
            // PTR集合无数据 但是PRR有数据 这是有问题的 直接抛弃这个
            if (DicPTR.Keys.Count == 0)
            {
                return;
            }

            DicPRR[prr.SITENUM] = prr;
            // site最大值一致 则认为是一个touchdown结束
            if (DicPRR?.Keys?.Max() == DicPTR?.Keys?.Max())
            {
                // 最大site值一致 但是site数量不一致,说明数据异常,直接抛弃这个touchdown
                // 此逻辑暂定
                if (DicPRR?.Keys?.Count() != DicPTR?.Keys?.Count())
                {
                    DicPTR.Clear();
                    DicPRR.Clear();
                    this.IsTouchDownEnd = false;
                    Index++;
                    return;
                }

                try
                {
                    int[] keys = DicPRR?.Keys?.ToArray();

                    for (int i = 0; i < keys?.Length; i++)
                    {
                        if (DicPTR.ContainsKey(keys[i]))
                        {
                            for (int j = 0; j < DicPTR[keys[i]]?.Count; j++)
                            {
                                if (DicPTR[keys[i]][j]?.SITENUM == DicPRR[keys[i]]?.SITENUM)
                                {
                                    DicPTR[keys[i]][j].PARTID = DicPRR[keys[i]]?.PARTID;
                                }
                            }
                        }
                    }

                    FirstSite = keys[0];
                    LastSite = keys[keys.Length - 1];
                }
                catch (Exception ex)
                {
                    Logs.Error("", ex);
                }
                this.IsTouchDownEnd = true;
                return;
            }

            // 如果PRR的最大SITE大于PTR的最大SITE,说明数据异常,直接抛弃这个touchdown
            // 此逻辑暂定
            if (DicPRR?.Keys?.Max() > DicPTR?.Keys?.Max())
            {
                DicPTR.Clear();
                DicPRR.Clear();
                this.IsTouchDownEnd = false;
                Index++;
            }
        }

        private void Clear()
        {
            if (this.IsTouchDownEnd == true)
            {
                DicPTR.Clear();
                DicPRR.Clear();
                this.IsTouchDownEnd = false;
                Index++;
            }
        }

    }
}

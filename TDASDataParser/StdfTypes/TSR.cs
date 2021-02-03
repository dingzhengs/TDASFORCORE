using System;
using System.Collections.Generic;
using System.Text;

namespace TDASDataParser.StdfTypes
{
    public class TSR : BaseEntity
    {
        public TSR() { }
        public double HEADNUM { get; set; }
        public double SITENUM { get; set; }
        public string TESTTYP { get; set; }
        public double TESTNUM { get; set; }
        public double EXECCNT { get; set; }
        public double FAILCNT { get; set; }
        public double ALRMCNT { get; set; }
        public string TESTNAM { get; set; }
        public string SEQNAME { get; set; }
        public string TESTLBL { get; set; }
        public string OPTFLAG { get; set; }
        public double TESTTIM { get; set; }
        public double TESTMIN { get; set; }
        public double TESTMAX { get; set; }
        public double TSTSUMS { get; set; }
        public double TSTSQRS { get; set; }

        public TSR(byte[] data)
        {
            this.LoadData(data, null);
        }

        public override void LoadData(byte[] data, byte[] args)
        {
            HEADNUM = GetInteger(data, 1);
            SITENUM = GetInteger(data, 1);
            TESTTYP = GetFixedString(data, 1);
            TESTNUM = GetInteger(data, 4);
            EXECCNT = GetInteger(data, 4);
            FAILCNT = GetInteger(data, 4);
            ALRMCNT = GetInteger(data, 4);
            TESTNAM = GetNotFixedString(data);
            SEQNAME = GetNotFixedString(data);
            TESTLBL = GetNotFixedString(data);
            OPTFLAG = GetFixedBitEncoded(data, 1);
            TESTTIM = GetFloat(data, 4);
            TESTMIN = GetFloat(data, 4);
            TESTMAX = GetFloat(data, 4);
            TSTSUMS = GetFloat(data, 4);
            TSTSQRS = GetFloat(data, 4);
            if (Math.Abs(TESTTIM) > 100000000000)
            {
                TESTTIM = -999999999;
            }

            TESTMIN = Math.Round(GetFloat(data, 4), 11);
            if (Math.Abs(TESTMIN) > 100000000000)
            {
                TESTMIN = -999999999;
            }

            TESTMAX = Math.Round(GetFloat(data, 4), 11);
            if (Math.Abs(TESTMAX) > 100000000000)
            {
                TESTMAX = 999999999;
            }

            TSTSUMS = Math.Round(GetFloat(data, 4), 11);
            if (Math.Abs(TSTSUMS) > 100000000000)
            {
                TSTSUMS = 999999999;
            }

            TSTSQRS = Math.Round(GetFloat(data, 4), 11);
            if (Math.Abs(TSTSQRS) > 100000000000)
            {
                TSTSQRS = 999999999;
            }
            if (OPTFLAG.Length == 8)
            {
                if (OPTFLAG[2] == '1')
                {
                    TESTTIM = 0;
                }
                if (OPTFLAG[0] == '1')
                {
                    TESTMIN = 0;
                }
                if (OPTFLAG[1] == '1')
                {
                    TESTMAX = 0;
                }
                if (OPTFLAG[4] == '1')
                {
                    TSTSUMS = 0;
                }
                if (OPTFLAG[5] == '1')
                {
                    TSTSQRS = 0;
                }
            }
        }
    }
}

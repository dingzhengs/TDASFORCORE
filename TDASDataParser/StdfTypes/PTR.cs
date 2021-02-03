using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace TDASDataParser.StdfTypes
{
    public class PTR : BaseEntity
    {
        public PTR() { }
        private readonly object indexLock = new object();
        public double TESTNUM { get; set; }
        public double HEADNUM { get; set; }
        public int SITENUM { get; set; }
        public string TESTFLG { get; set; }
        public string PARMFLG { get; set; }
        public double RESULT { get; set; }
        public string TESTTXT { get; set; }
        public string ALARMID { get; set; }
        public string OPTFLAG { get; set; }
        public double RESSCAL { get; set; }
        public double LLMSCAL { get; set; }
        public double HLMSCAL { get; set; }
        public double LOLIMIT { get; set; }
        public double HILIMIT { get; set; }
        public string UNITS { get; set; }
        public string CRESFMT { get; set; }
        public string CLLMFMT { get; set; }
        public string CHLMFMT { get; set; }
        public double LOSPEC { get; set; }
        public double HISPEC { get; set; }
        public string PARTID { get; set; }
        private double _stdfIndex = 0;
        public double STDFINDEX
        {
            get
            {
                return 0;
            }
            set
            {
                _stdfIndex = value;
            }
        }
        public string LOTID { get; set; }
        public string PARTTYP { get; set; }
        public PTR(byte[] data)
        {
            this.LoadData(data, null);

        }



        public override string ToJson()
        {
            return $@"{{PARTID:{PARTID}, TESTNUM:{TESTNUM},HEADNUM:{HEADNUM},SITENUM:{SITENUM},TESTFLG:'{TESTFLG}',PARMFLG:'{PARMFLG}',RESULT:{RESULT},TESTTXT:'{TESTTXT}',ALARMID:'{ALARMID}',OPTFLAG:'{OPTFLAG}',RESSCAL:{RESSCAL},LLMSCAL:{LLMSCAL},HLMSCAL:{HLMSCAL},LOLIMIT:{LOLIMIT},HILIMIT:{HILIMIT},UNITS:'{UNITS}',CRESFMT:'{CRESFMT}',CLLMFMT:'{CLLMFMT}',CHLMFMT:'{CHLMFMT}',LOSPEC:{LOSPEC}, HISPEC:{HISPEC},STDFINDEX:{STDFINDEX},StdfId:{StdfId}}}";
        }

        public override void LoadData(byte[] data, byte[] args)
        {
            TESTNUM = GetInteger(data, 4);
            HEADNUM = GetInteger(data, 1);
            SITENUM = GetInteger(data, 1);
            TESTFLG = GetFixedBitEncoded(data, 1);
            PARMFLG = GetFixedBitEncoded(data, 1);
            RESULT = Math.Round(GetFloat(data, 4), 11);
            TESTTXT = GetNotFixedString(data);
            ALARMID = GetNotFixedString(data);
            OPTFLAG = GetFixedBitEncoded(data, 1);
            RESSCAL = GetInteger(data, 1);
            LLMSCAL = GetInteger(data, 1);
            HLMSCAL = GetInteger(data, 1);
            LOLIMIT = GetFloat(data, 4);
            HILIMIT = GetFloat(data, 4);
            UNITS = GetNotFixedString(data);
            CRESFMT = GetNotFixedString(data);
            CLLMFMT = GetNotFixedString(data);
            CHLMFMT = GetNotFixedString(data);

            if (OPTFLAG.Length == 8)
            {
                if (OPTFLAG[4] == '1' || OPTFLAG[6] == '1')
                {
                    LLMSCAL = 0;
                    LOLIMIT = 0;
                }
                if (OPTFLAG[5] == '1' || OPTFLAG[7] == '1')
                {
                    HLMSCAL = 0;
                    HILIMIT = 0;
                }
                if (OPTFLAG[2] == '1')
                {
                    LOSPEC = 0;
                }
                else
                {
                    LOSPEC = GetFloat(data, 4);
                }
                if (OPTFLAG[3] == '1')
                {
                    HISPEC = 0;
                }
                else
                {
                    HISPEC = GetFloat(data, 4);
                }
            }
            else
            {
                LOSPEC = GetFloat(data, 4);
                HISPEC = GetFloat(data, 4);
            }

            if (args != null && args.Length > 0)
            {
                PARTID = BitConverter.ToInt32(args, 0).ToString();
            }

            var a = STDFINDEX;
        }


        public override int CalPartId(int stdfid)
        {
            lock (PartIdLock)
            {

                try
                {
                    if (!STARTPARTID.ContainsKey(stdfid))
                    {
                        STARTPARTID[stdfid] = 0;
                        SITENUM2PARTID[stdfid] = new Dictionary<int, int>();
                    }

                    if (!SITENUM2PARTID[stdfid].ContainsKey(SITENUM))
                    {
                        SITENUM2PARTID[stdfid][SITENUM] = ++STARTPARTID[stdfid];
                    }

                    if (SITENUM2PARTID[stdfid].ContainsKey(SITENUM))
                    {
                        this.PARTID = SITENUM2PARTID[stdfid][SITENUM].ToString();
                    }

                    return SITENUM2PARTID[stdfid][SITENUM];
                }
                catch
                {
                    return 0;
                }
            }
        }
    }
}

namespace TDASDataParser.StdfTypes
{
    public class MPR : BaseEntity
    {
        public MPR() { }
        public double TESTNUM { get; set; }
        public double HEADNUM { get; set; }
        public double SITENUM { get; set; }
        public string TESTFLG { get; set; }
        public string PARMFLG { get; set; }
        public int RTNICNT { get; set; }
        public int RSLTCNT { get; set; }
        public string RTNSTAT { get; set; }
        public string RTNRSLT { get; set; }
        public string TESTTXT { get; set; }
        public string ALARMID { get; set; }
        public string OPTFLAG { get; set; }
        public double RESSCAL { get; set; }
        public double LLMSCAL { get; set; }
        public double HLMSCAL { get; set; }
        public double LOLIMIT { get; set; }
        public double HILIMIT { get; set; }
        public double STARTIN { get; set; }
        public double INCRIN { get; set; }
        public string RTNINDX { get; set; }
        public string UNITS { get; set; }
        public string UNITSIN { get; set; }
        public string CRESFMT { get; set; }
        public string CLLMFMT { get; set; }
        public string CHLMFMT { get; set; }
        public double LOSPEC { get; set; }
        public double HISPEC { get; set; }
        public string STARTTESTNUM
        {
            get
            {
                if (TESTNUM.ToString().Length < 4)
                {
                    return TESTNUM.ToString();
                }
                return TESTNUM.ToString().Substring(0, 4);
            }
            set
            {
            }
        }

        public MPR(byte[] data)
        {
            this.LoadData(data,null);
           
        }

        public override void LoadData(byte[] data, byte[] args)
        {
            TESTNUM = GetInteger(data, 4);
            HEADNUM = GetInteger(data, 1);
            SITENUM = GetInteger(data, 1);
            TESTFLG = GetFixedBitEncoded(data, 1);
            PARMFLG = GetFixedBitEncoded(data, 1);
            RTNICNT = GetInteger(data, 2);
            RSLTCNT = GetInteger(data, 2);
            RTNSTAT = GetBitStringArray(data, RTNICNT);
            RTNRSLT = GetFloatArray(data, RSLTCNT, 4);
            TESTTXT = GetNotFixedString(data);
            ALARMID = GetNotFixedString(data);
            OPTFLAG = GetFixedBitEncoded(data, 1);
            RESSCAL = GetInteger(data, 1);
            LLMSCAL = GetInteger(data, 1);
            HLMSCAL = GetInteger(data, 1);
            LOLIMIT = GetFloat(data, 4);
            HILIMIT = GetFloat(data, 4);
            STARTIN = GetFloat(data, 4);
            INCRIN = GetFloat(data, 4);
            RTNINDX = GetIntegerArray(data, RTNICNT, 2);
            UNITS = GetNotFixedString(data);
            UNITSIN = GetNotFixedString(data);
            CRESFMT = GetNotFixedString(data);
            CLLMFMT = GetNotFixedString(data);
            CHLMFMT = GetNotFixedString(data);
            LOSPEC = GetFloat(data, 4);
            HISPEC = GetFloat(data, 4);
        }
    }
}

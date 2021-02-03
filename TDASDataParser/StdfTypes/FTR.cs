namespace TDASDataParser.StdfTypes
{
    public class FTR : BaseEntity
    {
        public FTR() { }
        public double TESTNUM { get; set; }
        public double HEADNUM { get; set; }
        public int SITENUM { get; set; }
        public string TESTFLG { get; set; }
        public string OPTFLAG { get; set; }
        public double CYCLCNT { get; set; }
        public double RELVADR { get; set; }
        public double REPTCNT { get; set; }
        public double NUMFAIL { get; set; }
        public double XFAILAD { get; set; }
        public double YFAILAD { get; set; }
        public double VECTOFF { get; set; }
        public int RTNICNT { get; set; }
        public int PGMICNT { get; set; }
        public string RTNINDX { get; set; }
        public string RTNSTAT { get; set; }
        public string PGMINDX { get; set; }
        public string PGMSTAT { get; set; }
        public string FAILPIN { get; set; }
        public string VECTNAM { get; set; }
        public string TIMESET { get; set; }
        public string OPCODE { get; set; }
        public string TESTTXT { get; set; }
        public string ALARMID { get; set; }
        public string PROGTXT { get; set; }
        public string RSLTTXT { get; set; }
        public double PATGNUM { get; set; }
        public string SPINMAP { get; set; }

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



        public FTR(byte[] data)
        {
            this.LoadData(data,null);
        }

        public override void LoadData(byte[] data, byte[] args)
        {
            TESTNUM = GetInteger(data, 4);
            HEADNUM = GetInteger(data, 1);
            SITENUM = GetInteger(data, 1);
            TESTFLG = GetFixedBitEncoded(data, 1);
            OPTFLAG = GetFixedBitEncoded(data, 1);
            CYCLCNT = GetInteger(data, 4);
            RELVADR = GetInteger(data, 4);
            REPTCNT = GetInteger(data, 4);
            NUMFAIL = GetInteger(data, 4);
            XFAILAD = GetInteger(data, 4);
            YFAILAD = GetInteger(data, 4);
            VECTOFF = GetInteger(data, 2);
            RTNICNT = GetInteger(data, 2);
            PGMICNT = GetInteger(data, 2);
            RTNINDX = GetIntegerArray(data, RTNICNT, 2);
            RTNSTAT = GetBitStringArray(data, RTNICNT);
            PGMINDX = GetIntegerArray(data, PGMICNT, 2);
            PGMSTAT = GetBitStringArray(data, PGMICNT);
            FAILPIN = GetNotFixedBitEncoded(data);
            VECTNAM = GetNotFixedString(data);
            TIMESET = GetNotFixedString(data);
            OPCODE = GetNotFixedString(data);
            TESTTXT = GetNotFixedString(data);
            ALARMID = GetNotFixedString(data);
            PROGTXT = GetNotFixedString(data);
            RSLTTXT = GetNotFixedString(data);
            PATGNUM = GetInteger(data, 1);
            SPINMAP = GetNotFixedBitEncoded(data);
        }
    }
}

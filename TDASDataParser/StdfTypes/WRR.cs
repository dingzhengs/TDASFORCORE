namespace TDASDataParser.StdfTypes
{
    public class WRR : BaseEntity
    {
        public WRR() { }
        public double HEADNUM { get; set; }
        public double SITEGRP { get; set; }
        public double FINISHT { get; set; }
        public double PARTCNT { get; set; }
        public double ABRTCNT { get; set; }
        public double GOODCNT { get; set; }
        public double FUNCCNT { get; set; }
        public string WAFERID { get; set; }
        public string FABWFID { get; set; }
        public string FRAMEID { get; set; }
        public string MASKID { get; set; }
        public string USRDESC { get; set; }
        public string EXCDESC { get; set; }

        public WRR(byte[] data)
        {
            this.LoadData(data,null);
        }

        public override void LoadData(byte[] data, byte[] args)
        {
            HEADNUM = GetInteger(data, 1);
            SITEGRP = GetInteger(data, 1);
            FINISHT = GetInteger(data, 4);
            PARTCNT = GetInteger(data, 4);
            ABRTCNT = GetInteger(data, 4);
            GOODCNT = GetInteger(data, 4);
            FUNCCNT = GetInteger(data, 4);
            WAFERID = GetNotFixedString(data);
            FABWFID = GetNotFixedString(data);
            FRAMEID = GetNotFixedString(data);
            MASKID = GetNotFixedString(data);
            USRDESC = GetNotFixedString(data);
            EXCDESC = GetNotFixedString(data);
        }
    }
}

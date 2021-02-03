namespace TDASDataParser.StdfTypes
{
    public class WCR : BaseEntity
    {
        public WCR() { }
        public double WAFRSIZ { get; set; }
        public double DIEHT { get; set; }
        public double DIEWID { get; set; }
        public double WFUNITS { get; set; }
        public string WFFLAT { get; set; }
        public double CENTERX { get; set; }
        public double CENTERY { get; set; }
        public string POSX { get; set; }
        public string POSY { get; set; }
        public WCR(byte[] data)
        {
            this.LoadData(data,null);
           
        }

        public override void LoadData(byte[] data, byte[] args)
        {
            WAFRSIZ = GetFloat(data, 4);
            DIEHT = GetFloat(data, 4);
            DIEWID = GetFloat(data, 4);
            WFUNITS = GetInteger(data, 1);
            WFFLAT = GetFixedString(data, 1);
            CENTERX = GetInteger(data, 2);
            CENTERY = GetInteger(data, 2);
            POSX = GetFixedString(data, 1);
            POSY = GetFixedString(data, 1);

        }
    }
}

namespace TDASDataParser.StdfTypes
{
    public class SBR : BaseEntity
    {
        public SBR() { }
        public double HEADNUM { get; set; }
        public double SITENUM { get; set; }
        public double SBINNUM { get; set; }
        public double SBINCNT { get; set; }
        public string SBINPF { get; set; }
        public string SBINNAM { get; set; }

        public SBR(byte[] data)
        {
            this.LoadData(data,null);
          
        }

        public override void LoadData(byte[] data, byte[] args)
        {
            HEADNUM = GetInteger(data, 1);
            SITENUM = GetInteger(data, 1);
            SBINNUM = GetInteger(data, 2);
            SBINCNT = GetInteger(data, 4);
            SBINPF = GetFixedString(data, 1);
            SBINNAM = GetNotFixedString(data);
        }
    }
}

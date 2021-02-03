namespace TDASDataParser.StdfTypes
{
    public class PIR : BaseEntity
    {
        public PIR() { }
        public double HEADNUM { get; set; }
        public double SITENUM { get; set; }

        public PIR(byte[] data)
        {
            this.LoadData(data,null);
           
        }

        public override void LoadData(byte[] data, byte[] args)
        {
            HEADNUM = GetInteger(data, 1);
            SITENUM = GetInteger(data, 1);
        }
    }
}

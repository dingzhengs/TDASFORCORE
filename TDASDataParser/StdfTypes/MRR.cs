using System;

namespace TDASDataParser.StdfTypes
{
    public class MRR : BaseEntity
    {
        public MRR() { }
        public double FINISHT { get; set; }
        public string DISPCOD { get; set; }
        public string USRDESC { get; set; }
        public string EXCDESC { get; set; }

        public MRR(byte[] data)
        {
            this.LoadData(data,null);
           
        }
        public override void LoadData(byte[] data, byte[] args)
        {
            FINISHT = GetInteger(data, 4);
            DISPCOD = GetFixedString(data, 1);
            USRDESC = GetNotFixedString(data);
            EXCDESC = GetNotFixedString(data);
        }
    }
}

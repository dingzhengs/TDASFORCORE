using System;

namespace TDASDataParser.StdfTypes
{
    public class PGR : BaseEntity
    {
        public PGR() { }
        public double GRPINDX { get; set; }
        public string GRPNAM { get; set; }
        public int INDXCNT { get; set; }
        public string PMRINDX { get; set; }

        public PGR(byte[] data)
        {
            this.LoadData(data,null);
           
        }

        public override void LoadData(byte[] data, byte[] args)
        {
            GRPINDX = GetInteger(data, 2);
            GRPNAM = GetNotFixedString(data);
            INDXCNT = GetInteger(data, 2);
            PMRINDX = GetIntegerArray(data, INDXCNT, 2);
        }
    }
}

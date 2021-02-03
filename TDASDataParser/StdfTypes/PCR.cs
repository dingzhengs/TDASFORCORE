using System;
using System.Collections.Generic;
using System.Text;

namespace TDASDataParser.StdfTypes
{
    public class PCR : BaseEntity
    {
        public PCR() { }
        public double HEADNUM { get; set; }
        public double SITENUM { get; set; }
        public double PARTCNT { get; set; }
        public double RTSTCNT { get; set; }
        public double ABRTCNT { get; set; }
        public double GOODCNT { get; set; }
        public double FUNCCNT { get; set; }

        public PCR(byte[] data)
        {
            this.LoadData(data,null);
          
        }

        public override void LoadData(byte[] data, byte[] args)
        {
            HEADNUM = GetInteger(data, 1);
            SITENUM = GetInteger(data, 1);
            PARTCNT = GetInteger(data, 4);
            RTSTCNT = GetInteger(data, 4);
            ABRTCNT = GetInteger(data, 4);
            GOODCNT = GetInteger(data, 4);
            FUNCCNT = GetInteger(data, 4);
        }
    }
}

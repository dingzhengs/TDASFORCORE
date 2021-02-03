using System;
using System.Collections.Generic;
using System.Text;

namespace TDASDataParser.StdfTypes
{
    public class HBR : BaseEntity
    {
        public HBR() { }
        public double HEADNUM { get; set; }
        public double SITENUM { get; set; }
        public double HBINNUM { get; set; }
        public double HBINCNT { get; set; }
        public string HBINPF { get; set; }
        public string HBINNAM { get; set; }

        public HBR(byte[] data)
        {
            this.LoadData(data,null);
           
        }
        public override void LoadData(byte[] data, byte[] args)
        {
            HEADNUM = GetInteger(data, 1);
            SITENUM = GetInteger(data, 1);
            HBINNUM = GetInteger(data, 2);
            HBINCNT = GetInteger(data, 4);
            HBINPF = GetFixedString(data, 1);
            HBINNAM = GetNotFixedString(data);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace TDASDataParser.StdfTypes
{
    public class WIR : BaseEntity
    {
        public WIR() { }
        public double HEADNUM { get; set; }
        public double SITEGRP { get; set; }
        public double STARTT { get; set; }
        public string WAFERID { get; set; }

        public WIR(byte[] data)
        {
            this.LoadData(data,null);
            
        }

        public override void LoadData(byte[] data, byte[] args)
        {
            HEADNUM = GetInteger(data, 1);
            SITEGRP = GetInteger(data, 1);
            STARTT = GetInteger(data, 4);
            WAFERID = GetNotFixedString(data);
        }
    }
}

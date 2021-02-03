using System;
using System.Collections.Generic;
using System.Text;

namespace TDASDataParser.StdfTypes
{
    public class PMR : BaseEntity
    {
        public PMR() { }
        public double PMRINDX { get; set; }
        public double CHANTYP { get; set; }
        public string CHANNAM { get; set; }
        public string PHYNAM { get; set; }
        public string LOGNAM { get; set; }
        public double HEADNUM { get; set; }
        public double SITENUM { get; set; }

        public PMR(byte[] data)
        {
            this.LoadData(data,null);
           
        }

        public override void LoadData(byte[] data, byte[] args)
        {
            PMRINDX = GetInteger(data, 2);
            CHANTYP = GetInteger(data, 2);
            CHANNAM = GetNotFixedString(data);
            PHYNAM = GetNotFixedString(data);
            LOGNAM = GetNotFixedString(data);
            HEADNUM = GetInteger(data, 1);
            SITENUM = GetInteger(data, 1);
        }
    }

}

using System;
using System.Collections.Generic;
using System.Text;

namespace TDASDataParser.StdfTypes
{
    public class GDR : BaseEntity
    {
        public GDR() { }
        public double FLDCNT { get; set; }

        public string GENDATA { get; set; }

        public GDR(byte[] data)
        {
            this.LoadData(data,null);
          
        }

        public override void LoadData(byte[] data, byte[] args)
        {
            FLDCNT = GetInteger(data, 2);
            GENDATA = "";
        }
    }

}

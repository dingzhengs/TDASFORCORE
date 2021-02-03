using System;
using System.Collections.Generic;
using System.Text;

namespace TDASDataParser.StdfTypes
{
    public class RDR : BaseEntity
    {
        public RDR() { }
        public int NUMBINS { get; set; }
        public string RTSTBIN { get; set; }

        public RDR(byte[] data)
        {
            this.LoadData(data,null);
         
        }

        public override void LoadData(byte[] data, byte[] args)
        {
            NUMBINS = GetInteger(data, 2);
            RTSTBIN = GetIntegerArray(data, NUMBINS, 2);
        }
    }
}

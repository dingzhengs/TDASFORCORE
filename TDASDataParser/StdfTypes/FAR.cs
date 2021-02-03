using System;
using System.Collections.Generic;
using System.Text;

namespace TDASDataParser.StdfTypes
{
    public class FAR : BaseEntity
    {
        public FAR() { }
        public double CPUTYPE { get; set; }

        public double STDFVER { get; set; }
        public FAR(byte[] data)
        {
            this.LoadData(data,null);
           
        }
        public override void LoadData(byte[] data, byte[] args)
        {
            CPUTYPE = GetInteger(data, 1);
            STDFVER = GetInteger(data, 1);
        }
        
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace TDASDataParser.StdfTypes
{
    public class DTR : BaseEntity
    {
        public DTR() { }
        public string TEXTDAT { get; set; }
        public DTR(byte[] data)
        {
            this.LoadData(data,null);
        }
        public override void LoadData(byte[] data, byte[] args)
        {
            TEXTDAT = GetNotFixedString(data);
        }
        

    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace TDASDataParser.StdfTypes
{
    public class BPS : BaseEntity
    {
        public BPS() { }
        public string SEQNAME { get; set; }
        public BPS(byte[] data)
        {
            this.LoadData(data,null);
        }

        public override void LoadData(byte[] data, byte[] args)
        {
            SEQNAME = GetNotFixedString(data);
        }
    }
}

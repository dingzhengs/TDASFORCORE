using System;
using System.Collections.Generic;
using System.Text;

namespace TDASDataParser.StdfTypes
{
    //REC_TYP:0
    //REC_SUB:20
    public class ATR : BaseEntity
    {
        public ATR() { }
        public double MODTIM { get; set; }

        public string CMDLINE { get; set; }
       
        public ATR(byte[] data)
        {
            this.LoadData(data,null);
        }

        public override void LoadData(byte[] data, byte[] args)
        {
            MODTIM = GetInteger(data, 4);
            CMDLINE = GetNotFixedString(data);
        }
    }
}

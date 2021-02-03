using System;
using System.Collections.Generic;
using System.Text;

namespace TDASDataParser.StdfTypes
{
    public class PLR : BaseEntity
    {
        public PLR() { }
        public int GRPCNT { get; set; }
        public string GRPINDX { get; set; }
        public string GRPMODE { get; set; }
        public string GRPRADX { get; set; }
        public string PGMCHAR { get; set; }
        public string RTNCHAR { get; set; }
        public string PGMCHAL { get; set; }
        public string RTNCHAL { get; set; }
        public PLR(byte[] data)
        {
            this.LoadData(data,null);
           
        }

        public override void LoadData(byte[] data, byte[] args)
        {
            GRPCNT = GetInteger(data, 2);
            GRPINDX = GetIntegerArray(data, GRPCNT, 2);
            GRPMODE = GetIntegerArray(data, GRPCNT, 2);
            GRPRADX = GetIntegerArray(data, GRPCNT, 1);
            PGMCHAR = GetNotFixedStringArray(data, GRPCNT);
            RTNCHAR = GetNotFixedStringArray(data, GRPCNT);
            PGMCHAL = GetNotFixedStringArray(data, GRPCNT);
            RTNCHAL = GetNotFixedStringArray(data, GRPCNT);
        }
    }
}

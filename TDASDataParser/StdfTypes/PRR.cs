using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace TDASDataParser.StdfTypes
{
    public class PRR : BaseEntity
    {
        public PRR() { }
        private readonly object indexLock = new object();
        public double HEADNUM { get; set; }
        public int SITENUM { get; set; }
        public string PARTFLG { get; set; }
        public double NUMTEST { get; set; }
        public double HARDBIN { get; set; }
        public double SOFTBIN { get; set; }
        public double XCOORD { get; set; }
        public double YCOORD { get; set; }
        public double TESTT { get; set; }
        public string PARTID { get; set; }
        public string PARTTXT { get; set; }
        public string PARTFIX { get; set; }

        private double _stdfIndex = 0;
        public double STDFINDEX
        {
            get
            {
                return 0;
            }
            set
            {
                _stdfIndex = value;
            }
        }
        //return 
        public DateTime? WORKINGT { get; set; }

        public PRR(byte[] data)
        {
            this.LoadData(data, null);

        }

        public override string ToJson()
        {
            return $"{{HEADNUM:{HEADNUM},SITENUM:{SITENUM},PARTFLG:'{PARTFLG}',NUMTEST:{NUMTEST},HARDBIN:{HARDBIN},SOFTBIN:{SOFTBIN},XCOORD:{XCOORD},YCOORD:{YCOORD},TESTT:{TESTT},PARTID:'{PARTID}', PARTTXT:'{PARTTXT}',PARTFIX:'{PARTFIX}',STDFINDEX:{STDFINDEX},StdfId:{StdfId}}}";
        }

        public override void LoadData(byte[] data, byte[] args)
        {
            HEADNUM = GetInteger(data, 1);
            SITENUM = GetInteger(data, 1);
            PARTFLG = GetFixedBitEncoded(data, 1);
            NUMTEST = GetInteger(data, 2);
            HARDBIN = GetInteger(data, 2);
            SOFTBIN = GetInteger(data, 2);
            XCOORD = GetInteger(data, 2);
            YCOORD = GetInteger(data, 2);
            TESTT = GetInteger(data, 4);
            PARTID = GetNotFixedString(data);
            PARTTXT = GetNotFixedString(data);
            PARTFIX = GetNotFixedByteEncoded(data);

            WORKINGT = DateTime.Now;

            var a = STDFINDEX;
        }


        public override int CalPartId(int stdfid)
        {
            lock (PartIdLock)
            {
                SITENUM2PARTID[stdfid] = new Dictionary<int, int>();
                STARTPARTID[stdfid] = Convert.ToInt32(PARTID);
                return 0;
            }
        }

    }
}

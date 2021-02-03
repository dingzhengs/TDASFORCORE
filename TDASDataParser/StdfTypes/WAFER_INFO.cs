using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TDASDataParser.StdfTypes
{
    public class WAFER_INFO
    {
        public double StdfId { get; set; }
        public string LOTID { get; set; }
        public double? WAFERID { get; set; }
        public double SOFTBIN { get; set; }
        public double? DIEX { get; set; }
        public double? DIEY { get; set; }
        public double PARTID { get; set; }
        public string TESTCOD { get; set; }

    }
}

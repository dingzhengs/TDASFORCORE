using System;
using System.Collections.Generic;
using System.Text;

namespace TDASDataParser.StdfTypes
{
    public class STDFFILE : BaseEntity
    {
        public string FILENAME { get; set; }
        public string PATH { get; set; }
        public string SLOT { get; set; }
        public DateTime? BEGINDATE { get; set; }
        public DateTime? ENDDATE { get; set; }
    }
}

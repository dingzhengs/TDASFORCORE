using TDASDataParser.StdfTypes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TDASDataParser.Rules
{
    public class V_RULES_RCS
    {
        public string GUID { get; set; }

        public string TEAM { get; set; }

        public string PRODUCT { get; set; }

        public string ACTION { get; set; }

        public string CUSTCODE { get; set; }

        public string TEST_TYPE { get; set; }

        public string COLUMNAME { get; set; }

        public string COLUMTYPE { get; set; }

        public string COLUMVALUE { get; set; }

        public string INSERTION { get; set; }

        public string RULETYPE { get; set; }

        public string PASSFLAG { get; set; }

        public string NOTEXIST { get; set; }

        public string OMITNAME { get; set; }

        public string TESTNUM { get; set; }

        public double? UP_DOWN { get; set; }

        public string DEVICEGROUP { get; set; }

        public string STATUS { get; set; }

        public string CAL_TYPE { get; set; }

        public double? STATUS_NUM { get; set; }

        //0：否，1：是
        public int? BYSITE { get; set; }


        string keypre = Guid.NewGuid().ToString("N");
        public string RuleKey
        {
            get
            {
                return $"{this.keypre}-{PRODUCT}.{GUID}.{{0}}_{{1}}";
            }
        }
    }
}

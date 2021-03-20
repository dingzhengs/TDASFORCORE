using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TDASDataParser.StdfTypes
{
    public class MIR : BaseEntity
    {
        public MIR() { }
        public double SETUPT { get; set; }
        public double STARTT { get; set; }
        public double STATNUM { get; set; }
        public string MODECOD { get; set; }
        public string RTSTCOD { get; set; }
        public string PROTCOD { get; set; }
        public double BURNTIM { get; set; }
        public string CMODCOD { get; set; }
        public string LOTID { get; set; }
        public string PARTTYP { get; set; }
        public string NODENAM { get; set; }
        public string TSTRTYP { get; set; }
        public string JOBNAM { get; set; }
        public string JOBREV { get; set; }
        public string SBLOTID { get; set; }
        public string OPERNAM { get; set; }
        public string EXECTYP { get; set; }
        public string EXECVER { get; set; }
        public string TESTCOD { get; set; }
        public string TSTTEMP { get; set; }
        public string USERTXT { get; set; }
        public string AUXFILE { get; set; }
        public string PKGTYP { get; set; }
        public string FAMLYID { get; set; }
        public string DATECOD { get; set; }
        public string FACILID { get; set; }
        public string FLOORID { get; set; }
        public string PROCID { get; set; }
        public string OPERFRQ { get; set; }
        public string SPECNAM { get; set; }
        public string SPECVER { get; set; }
        public string FLOWID { get; set; }
        public string SETUPID { get; set; }
        public string DSGNREV { get; set; }
        public string ENGID { get; set; }
        public string ROMCOD { get; set; }
        public string SERLNUM { get; set; }
        public string SUPRNAM { get; set; }
        [JsonIgnore]
        private int TESTCODINDEX;
        [JsonIgnore]
        private int FLOWIDINDEX;
        [JsonIgnore]
        private int TESTCODLEN;
        [JsonIgnore]
        private int FLOWIDLEN;
        [JsonIgnore]
        private byte[] TYPEDATA;

        public MIR(byte[] data)
        {
            this.LoadData(data, null);


        }
        public override void LoadData(byte[] data, byte[] args)
        {
            TYPEDATA = new byte[data.Length];
            Array.Copy(data,TYPEDATA, data.Length);
            SETUPT = GetInteger(data, 4);
            STARTT = GetInteger(data, 4);
            STATNUM = GetInteger(data, 1);
            MODECOD = GetFixedString(data, 1);
            RTSTCOD = GetFixedString(data, 1);
            PROTCOD = GetFixedString(data, 1);
            BURNTIM = GetInteger(data, 2);
            CMODCOD = GetFixedString(data, 1);
            LOTID = GetNotFixedString(data);
            PARTTYP = GetNotFixedString(data);
            NODENAM = GetNotFixedString(data);
            TSTRTYP = GetNotFixedString(data);
            JOBNAM = GetNotFixedString(data);
            JOBREV = GetNotFixedString(data);
            SBLOTID = GetNotFixedString(data);
            OPERNAM = GetNotFixedString(data);
            EXECTYP = GetNotFixedString(data);
            EXECVER = GetNotFixedString(data);
            TESTCODINDEX = dataIndex;
            TESTCOD = GetNotFixedString(data);
            TESTCODLEN = dataIndex - TESTCODINDEX;
            TSTTEMP = GetNotFixedString(data);
            USERTXT = GetNotFixedString(data);
            AUXFILE = GetNotFixedString(data);
            PKGTYP = GetNotFixedString(data);
            FAMLYID = GetNotFixedString(data);
            DATECOD = GetNotFixedString(data);
            FACILID = GetNotFixedString(data);
            FLOORID = GetNotFixedString(data);
            PROCID = GetNotFixedString(data);
            OPERFRQ = GetNotFixedString(data);
            SPECNAM = GetNotFixedString(data);
            SPECVER = GetNotFixedString(data);
            FLOWIDINDEX = dataIndex;
            FLOWID = GetNotFixedString(data);
            FLOWIDLEN = dataIndex - FLOWIDINDEX;
            SETUPID = GetNotFixedString(data);
            DSGNREV = GetNotFixedString(data);
            ENGID = GetNotFixedString(data);
            ROMCOD = GetNotFixedString(data);
            SERLNUM = GetNotFixedString(data);
            SUPRNAM = GetNotFixedString(data);

            //原FAMLYID赋值给SUPRNAM，原FACILID赋值给FAMLYID
            SUPRNAM = FAMLYID;
            FAMLYID = FACILID;

            //if (SBLOTID?.Length >= 3)
            //{
            //    FAMLYID = SBLOTID?.Substring(0, 3);
            //}

            switch (FLOWID)
            {
                case "R0":
                    FLOWID = "P1";
                    break;
                case "R0.1":
                    FLOWID = "P2";
                    break;
                case "R0.2":
                    FLOWID = "P3";
                    break;
                case "R0.3":
                    FLOWID = "P4";
                    break;
                case "R0.4":
                    FLOWID = "P5";
                    break;
                case "R0.5":
                    FLOWID = "P6";
                    break;
                case "R0.6":
                    FLOWID = "P7";
                    break;
                case "R0.7":
                    FLOWID = "P8";
                    break;
                case "R0.8":
                    FLOWID = "P9";
                    break;
                case "R0.9":
                    FLOWID = "P10";
                    break;
                case "R0.10":
                    FLOWID = "P11";
                    break;
            }
        }

        public byte[] ReplaceData()
        {
            List<byte> dd = new List<byte>(TYPEDATA);

            dd.RemoveRange(FLOWIDINDEX, FLOWIDLEN);

            byte[] flowIdarr = Encoding.UTF8.GetBytes(this.FLOWID);

            dd.InsertRange(FLOWIDINDEX, new byte[1] { Convert.ToByte(flowIdarr.Length) }.Concat(flowIdarr));

            dd.RemoveRange(TESTCODINDEX, TESTCODLEN);

            byte[] testCodearr = Encoding.UTF8.GetBytes(this.TESTCOD);

            dd.InsertRange(TESTCODINDEX, new byte[1] { Convert.ToByte(testCodearr.Length) }.Concat(testCodearr));

            return dd.ToArray();
        }
    }


}

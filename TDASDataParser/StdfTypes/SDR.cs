using System;

namespace TDASDataParser.StdfTypes
{
    public class SDR : BaseEntity
    {
        public SDR() { }
        public int HEADNUM { get; set; }
        public int SITEGRP { get; set; }
        public int SITECNT { get; set; }
        public string SITENUM { get; set; }
        public string HANDTYP { get; set; }
        public string HANDID { get; set; }
        public string CARDTYP { get; set; }
        public string CARDID { get; set; }
        public string LOADTYP { get; set; }
        public string LOADID { get; set; }
        public string DIBTYP { get; set; }
        public string DIBID { get; set; }
        public string CABLTYP { get; set; }
        public string CABLID { get; set; }
        public string CONTTYP { get; set; }
        public string CONTID { get; set; }
        public string LASRTYP { get; set; }
        public string LASRID { get; set; }
        public string EXTRTYP { get; set; }
        public string EXTRID { get; set; }

        public SDR(byte[] data)
        {
            this.LoadData(data,null);
           
        }

        public override void LoadData(byte[] data, byte[] args)
        {
            HEADNUM = GetInteger(data, 1);
            SITEGRP = GetInteger(data, 1);
            SITECNT = GetInteger(data, 1);
            SITENUM = GetIntegerArray(data, SITECNT, 1);
            HANDTYP = GetNotFixedString(data);
            HANDID = GetNotFixedString(data);
            CARDTYP = GetNotFixedString(data);
            CARDID = GetNotFixedString(data);
            LOADTYP = GetNotFixedString(data);
            LOADID = GetNotFixedString(data);
            DIBTYP = GetNotFixedString(data);
            DIBID = GetNotFixedString(data);
            CABLTYP = GetNotFixedString(data);
            CABLID = GetNotFixedString(data);
            CONTTYP = GetNotFixedString(data);
            CONTID = GetNotFixedString(data);
            LASRTYP = GetNotFixedString(data);
            LASRID = GetNotFixedString(data);
            EXTRTYP = GetNotFixedString(data);
            EXTRID = GetNotFixedString(data);
        }
    }
}

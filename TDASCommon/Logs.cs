using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace TDASCommon
{
    public class Logs
    {
        private static readonly Logger logger = LogManager.GetLogger("TDAS");

        public static void Info(string msg)
        {
            logger.Info(msg);
            ToConsole(msg);
        }
        public static void Rule(string msg)
        {
            logger.Warn(msg);
            ToConsole(msg);
        }
        public static void Debug(string msg, Exception ex = null)
        {
            logger.Debug(ex, msg);
            ToConsole(msg, ex);
        }
        public static void Error(string msg, Exception ex = null)
        {
            logger.Error(ex, msg);
            ToConsole(msg, ex);
        }
        public static void Trace(string msg)
        {
            logger.Trace(msg);
            ToConsole(msg);
        }

        public static void ToConsole(string msg, Exception ex = null)
        {
#if (DEBUG)
            {
                Console.WriteLine(msg + ex?.Message);
            }
#endif
        }
    }
}

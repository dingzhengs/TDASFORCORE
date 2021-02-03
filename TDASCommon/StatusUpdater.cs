namespace TDASCommon
{
    public static class StatusUpdater
    {
        // 更新机台连接状态
        public static void ClientConnection(string IP)
        {
            new DatabaseManager().ExecuteNonQueryTmp("UPDATE SYS_EQP_STATUS SET STATUS=1,IDLE=1,CHANGEDATE=SYSDATE,LASTALIVETIME=NULL WHERE IP_CODE=:IP_CODE", new { IP_CODE = IP });
        }

        // 更新机台断开状态
        public static void ClientDisConnection(string IP)
        {
            new DatabaseManager().ExecuteNonQueryTmp("UPDATE SYS_EQP_STATUS SET STATUS=0,IDLE=0,CHANGEDATE=SYSDATE WHERE IP_CODE=:IP_CODE", new { IP_CODE = IP });
        }

        // 更新机台工作状态
        public static void ClientWorking(string IP)
        {
            int result = new DatabaseManager().ExecuteNonQueryTmp("UPDATE SYS_EQP_STATUS SET STATUS=2,CHANGEDATE=SYSDATE WHERE IP_CODE=:IP_CODE", new { IP_CODE = IP });
            //Logs.Debug($"[{IP}],开始传输数据,{result}");
        }

        // 更新机台空闲状态
        public static void ClientFree(string IP)
        {
            int result = new DatabaseManager().ExecuteNonQueryTmp("UPDATE SYS_EQP_STATUS SET STATUS=1,CHANGEDATE=SYSDATE WHERE IP_CODE=:IP_CODE", new { IP_CODE = IP });
            //Logs.Debug($"[{IP}],空闲超过5分钟,{result}");
        }

        public static void ClientDie(string IP)
        {
            new DatabaseManager().ExecuteNonQueryTmp("UPDATE SYS_EQP_STATUS SET LASTALIVETIME=sysdate WHERE IP_CODE=:IP_CODE", new { IP_CODE = IP });
        }
    }
}

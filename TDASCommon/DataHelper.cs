using Oracle.ManagedDataAccess.Client;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
namespace TDASCommon
{
    public static class DataHelper
    {
        /// <summary>
        /// 批量提交数据到数据库
        /// </summary>
        /// <param name="type"></param>
        /// <param name="source"></param>
        /// <param name="sql"></param>
        /// <param name="connectionString"></param>
        public static void SubmitEntities<T>(string type, T[] source, string sql, string connectionString, List<byte[]> redisData)
        {
            using (OracleConnection ocon = new OracleConnection(connectionString))
            {
                try
                {
                    ocon.Open();
                }
                catch (Exception ex)
                {
                    Logs.Error("打开数据库连接异常:"+ connectionString, ex);
                    CacheError(type, redisData);
                }

                using (OracleCommand cmd = ocon.CreateCommand())
                {
                    cmd.ArrayBindCount = source.Length;
                    cmd.CommandText = sql;

                    for (int i = 0; i < Global.DicProps[type].Length; i++)
                    {
                        object[] value = new object[source.Length];
                        for (int j = 0; j < source.Length; j++)
                        {
                            try
                            {
                                value[j] = Global.DicProps[type][i].GetValue(source[j]);
                            }
                            catch (Exception ex)
                            {
                                Logs.Error("构建插入参数异常", ex);
                            }
                        }

                        OracleParameter param = CreateParameter(Global.DicProps[type][i]);
                        param.Direction = ParameterDirection.Input;
                        param.Value = value;
                        cmd.Parameters.Add(param);
                    }

                    try
                    {

                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        if (!ex.Message.ToString().Contains("ORA-00942"))
                        {
                            Logs.Error($"{(source[0] as dynamic).StdfId}-{type},数据提交异常", ex);
                            CacheError(type, redisData);
                        }
                    }
                    finally
                    {
                    }
                }
            }
        }

        private static void CacheError(string type, List<byte[]> redisData)
        {
            if (redisData != null)
            {
                //IBatch batch = Global.redis.db.CreateBatch();
                //foreach (var item in redisData)
                //{
                //    _ = batch.ListLeftPushAsync(type, item);
                //}
                //batch.Execute();
            }
        }

        private static OracleParameter CreateParameter(PropertyInfo prop)
        {
            if (prop.Name.ToUpper() == "WORKINGT")
            {
                return new OracleParameter(prop.Name.ToUpper(), OracleDbType.Date);
            }
            return new OracleParameter(prop.Name.ToUpper(), OracleDbType.Varchar2);
        }
    }
}

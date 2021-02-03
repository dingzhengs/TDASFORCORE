using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace TDASCommon
{
    public class ReflectHelper
    {
        public static T Load<T>(string libName, string fullName, object[] param, string loadPath = "") where T : class
        {

            string path = Path.Combine(string.IsNullOrEmpty(loadPath) ? AppDomain.CurrentDomain.BaseDirectory : loadPath, libName);

            if (File.Exists(path))
            {
                byte[] assemblyBuf = File.ReadAllBytes(path);

                Assembly assembly = Assembly.Load(assemblyBuf);

                //string version = assembly.FullName.Split(',')[1].Replace("version=", "");

                foreach (var item in assembly.GetTypes())
                {
                    if (item.FullName == fullName)
                    {
                        return Activator.CreateInstance(item, param) as T;
                    }
                }
                return default(T);
            }
            else
            {
                return default(T);
            }
        }
    }
}

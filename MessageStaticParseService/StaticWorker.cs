using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TDASCommon;
using TDASDataParser;

namespace MessageStaticParseService
{
    public class StaticWorker
    {
        bool todb = false;
        bool csv = false;
        bool rule = false;
        private ConcurrentQueue<string> queue = new ConcurrentQueue<string>();
        private Hashtable fileMap = new Hashtable();

        public void Start(bool todb, bool csv, bool rule)
        {
            this.todb = todb;
            this.csv = csv;
            this.rule = rule;
            string staticFolder = Config.StaticFileFolder;
            try
            {
                if (!Directory.Exists(staticFolder))
                {
                    Directory.CreateDirectory(staticFolder);
                }
            }
            catch (Exception ex)
            {
                Logs.Error("", ex);
            }

            LoadFiles(staticFolder);
            Task.Run(() => { WorkAsync(staticFolder); });

            Logs.Info("启动静态文件监控.");
        }

        private void LoadFiles(string staticFolder)
        {
            string[] file = Directory.GetFiles(staticFolder);
            for (int i = 0; i < file.Length; i++)
            {
                lock (fileMap.SyncRoot)
                {
                    if (!fileMap.ContainsValue(file[i]))
                    {
                        fileMap.Add(Path.GetFileName(file[i]), file[i]);
                        queue.Enqueue(file[i]);
                    }
                }
            }
        }

        private void WorkAsync(string staticFolder)
        {
            while (true)
            {
                string file = "";
                try
                {
                    if (queue.TryDequeue(out file))
                    {
                        if (Path.GetExtension(file).ToUpper() == ".ZIP")
                        {
                            StaticReader a = new StaticReader();
                            a.ToDb = this.todb;
                            a.CreateCsv = this.csv;
                            a.RuleTest = this.rule;
                            a.OnReportProgress += A_OnReportProgress;
                            a.Read(file, ZipHelper.DeCompressFile(file));
                            deleteFile(file);
                        }
                        else if (Path.GetExtension(file).ToUpper() == ".CSV")
                        {
                        }
                        else
                        {
                            var a = new StaticReader();
                            a.ToDb = this.todb;
                            a.CreateCsv = this.csv;
                            a.RuleTest = this.rule;
                            a.OnReportProgress += A_OnReportProgress;
                            a.Read(file);

                            deleteFile(file);
                        }
                    }
                    else
                    {
                        Console.WriteLine("等待文件写入");
                        LoadFiles(staticFolder);
                        Thread.Sleep(1000);
                    }
                }
                catch (Exception ex)
                {
                    lock (fileMap.SyncRoot)
                    {
                        if (fileMap.ContainsKey(Path.GetFileName(file)))
                        {
                            queue.Enqueue(file);
                        }
                    }

                    Logs.Error("解析异常", ex);
                    Thread.Sleep(10000);
                }
            }

            // ReSharper disable once FunctionNeverReturns
        }

        private void deleteFile(string file)
        {
            lock (fileMap.SyncRoot)
            {
                fileMap.Remove(Path.GetFileName(file));
                try
                {
                    File.Delete(file);
                    Logs.Info($"释放缓存,删除文件:{Path.GetFileName(file)}");
                }
                catch (Exception e)
                {
                    Logs.Error(Path.GetFileName(file), e);
                }
            }
        }

        private void A_OnReportProgress(double progress)
        {
            Console.WriteLine($"{(progress * 100).ToString("N2")}%");
        }

        public void Stop()
        {
            Logs.Info("停止静态文件监控.");
        }
    }
}
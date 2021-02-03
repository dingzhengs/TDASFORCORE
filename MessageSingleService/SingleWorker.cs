using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentFTP;
using SharpCompress.Readers;
using TDASCommon;
using TDASDataParser;

namespace MessageSingleService
{
    public class SingleWorker
    {
        readonly FtpClient _icFtp = new FtpClient("192.1.1.172", "icupload", "icuploadxccdkj");
        readonly FtpClient _csvFtpClient = new FtpClient("172.17.129.109", "csv", "csv123");
        readonly DatabaseManager _dmgr = new DatabaseManager();
        readonly string _unZipDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UnZip");
        private bool _working = true;

        public void Start()
        {
            if (!Directory.Exists(_unZipDir))
            {
                Directory.CreateDirectory(_unZipDir);
            }

            Task.Run(() =>
            {
                while (_working)
                {
                    try
                    {
                        foreach (var dirRes in GetFtpDir())
                        {
                            if (string.IsNullOrEmpty(dirRes.dir))
                            { 
                                _dmgr.ExecuteNonQuery($@"UPDATE MES_TDAS_OFFLINE SET FLAG='Y', REMARK='FTP文件路径无效' WHERE ROWID='{dirRes.rowid}'");
                                continue;
                            }
                            
                            foreach (var file in GetFileList(dirRes.dir))
                            {
                                foreach (var filePath in Unzip(file))
                                {
                                    ImportStdf(filePath);
                                }
                            }

                            _dmgr.ExecuteNonQuery($"UPDATE MES_TDAS_OFFLINE SET FLAG='Y' WHERE ROWID='{dirRes.rowid}'");
                        }
                    }
                    catch (Exception e)
                    {
                        Logs.Error("异常", e);
                    }


                    Task.Delay(1000 * 60 * 60).Wait();
                }

                // ReSharper disable once FunctionNeverReturns
            });
        }

        IEnumerable<(string dir, string rowid)> GetFtpDir()
        {
            dynamic paths = _dmgr.ExecuteEntities<dynamic>($"SELECT ROWID, PATH FROM MES_TDAS_OFFLINE WHERE FLAG='N'");
            foreach (var item in paths)
            {
                if (_icFtp.DirectoryExists(item.PATH))
                {
                    yield return (item.PATH, item.ROWID.ToString());
                }
                else
                {
                    yield return (null, item.ROWID.ToString());
                }
            }
        }

        IEnumerable<FtpListItem> GetFileList(string path)
        {
            foreach (var file in _icFtp.GetListing(path, FtpListOption.AllFiles))
            {
                //if (file.Modified > DateTime.Now.AddDays(-2)) // 两天内上传的数据
                {
                    string stdfFile = Path.GetFileNameWithoutExtension(file.Name);
                    string exten = Path.GetExtension(stdfFile);
                    if (exten != null && new[] {".STDF", ".STD"}.Contains(exten.ToUpper())) // 包含STDF或者STD
                    {
                        if (_dmgr.ExecuteInteger($"SELECT COUNT(1) FROM STDFFILE WHERE FILENAME='{stdfFile}'") == 0)
                        {
                            yield return file;
                        }
                    }
                }
            }
        }

        IEnumerable<string> Unzip(FtpListItem fileListItem)
        {
            using (MemoryStream mstream = new MemoryStream())
            {
                if (_icFtp.Download(mstream, fileListItem.FullName))
                {
                    mstream.Position = 0;
                    using (IReader reader = ReaderFactory.Open(mstream))
                    {
                        while (reader.MoveToNextEntry())
                        {
                            if (!reader.Entry.IsDirectory)
                            {
                                string filePath = Path.Combine(_unZipDir, reader.Entry.Key);
                                using (Stream stream = File.Open(filePath, FileMode.Create, FileAccess.Write))
                                {
                                    reader.WriteEntryTo(stream);
                                }

                                yield return filePath;
                            }
                        }
                    }
                }
            }
        }

        void ImportStdf(string filePath)
        {
            StaticReader sr = new StaticReader {CreateCsv = true, RuleTest = true, ToDb = true};

            int stdfid = sr.Read(filePath);

            File.Delete(filePath);

            ToFtp(stdfid);
        }

        // 压缩完成的csv文件上传至ftp
        void ToFtp(int stdfid)
        {
            string fileName = _dmgr.ExecuteScalar("select filename from stdffile where stdfid=:stdfid", new {stdfid}).ToString();

            string lotid = _dmgr.ExecuteScalar("select lotid from mir where stdfid=:stdfid", new {stdfid}).ToString();

            FileInfo[] files =
                new DirectoryInfo(Config.Get<string>("Parse:CsvFileForlder")).GetFiles(Path.GetFileNameWithoutExtension(fileName) + "*",
                    SearchOption.AllDirectories);

            if (files.Length > 0)
            {
                string data = DateTime.Now.ToString("yyyyMMdd");

                string disk = $@"D:\shuxi\csv\ZIP\{data}";

                try
                {
                    _csvFtpClient.UploadFile(files[0].FullName, $"ZIP/{data}/{files[0].Name}", FtpRemoteExists.Overwrite, true);
                }
                catch (Exception ex)
                {
                    throw ex;
                }

                _dmgr.ExecuteNonQuery(@"DELETE CSVPATH WHERE STDFID=:stdfid", new {stdfid});

                _dmgr.ExecuteNonQuery(@"INSERT INTO CSVPATH(STDFID,FILEPATH,LOTID,INPUTDATE)VALUES(:stdfid,:filename,:lotid,sysdate)",
                    new {stdfid, filename = Path.Combine(disk, files[0].Name), lotid});

                files[0].Delete();
            }
        }

        public void Stop()
        {
            _working = false;
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using ICSharpCode.SharpZipLib.Checksum;
using ICSharpCode.SharpZipLib.Zip;

namespace TDASCommon
{
    /// <summary>
    /// 压缩文件工具类
    /// </summary>
    public class ZipHelper
    {
        /// <summary>
        /// 压缩文件或文件夹
        /// </summary>
        /// <param name="filename">要压缩的文件名称或文件夹名称</param>
        /// <param name="zipPath">保存zip文件的文件地址</param>
        /// <returns>压缩文件地址</returns>
        public static string CompressFile(string filename, string zipPath)
        {
            //如果目标地址非文件,且末尾不是\结尾,则补全\
            if (!filename.IsFile() && filename[filename.Length - 1] != Path.DirectorySeparatorChar)
            {
                filename += Path.DirectorySeparatorChar;
            }


            //验证压缩文件保存路径,如果非zip结尾,则以被压缩文件的最后一个文件夹作为压缩文件的名称
            if (!zipPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                if(!Directory.Exists(zipPath))
                {
                    Directory.CreateDirectory(zipPath);
                }
                string[] dirList = filename.Split(@"\\".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                zipPath = Path.Combine(zipPath, Path.GetFileNameWithoutExtension(dirList[dirList.Length - 1]) + ".zip");
            }

            using (ZipOutputStream outputStream = new ZipOutputStream(File.Create(zipPath)))
            {
                outputStream.SetLevel(6); // 0 - store only to 9 - means best compression
                Compress(filename, outputStream, filename);
                outputStream.Finish();
                outputStream.Close();
            }

            return zipPath;
        }

        /// <summary>
        /// 压缩文件或文件夹
        /// </summary>
        /// <param name="filename">要压缩的文件名称或文件夹名称</param>
        /// <param name="zipPath">保存zip文件的文件地址</param>
        /// <returns>压缩文件地址</returns>
        public static string CompressFile(string[] filenames, string zipPath)
        {
            //验证压缩文件保存路径,如果非zip结尾,则以被压缩文件的最后一个文件夹作为压缩文件的名称
            if (!zipPath.EndsWith("zip", StringComparison.OrdinalIgnoreCase))
            {
                zipPath = Path.Combine("temp.zip");
            }

            using (ZipOutputStream outputStream = new ZipOutputStream(File.Create(zipPath)))
            {
                outputStream.SetLevel(6); // 0 - store only to 9 - means best compression

                foreach (var filename in filenames)
                {
                    Compress(filename, outputStream, filename);
                }

                outputStream.Finish();
                outputStream.Close();
            }

            return zipPath;
        }

        public static string CompressFile(FileInfo[] files, string zipPath)
        {
            //验证压缩文件保存路径,如果非zip结尾,则以被压缩文件的最后一个文件夹作为压缩文件的名称
            if (!zipPath.EndsWith("zip", StringComparison.OrdinalIgnoreCase))
            {
                zipPath = Path.Combine("temp.zip");
            }

            using (ZipOutputStream outputStream = new ZipOutputStream(File.Create(zipPath)))
            {
                outputStream.SetLevel(6); // 0 - store only to 9 - means best compression

                foreach (var filename in files)
                {
                    Compress(filename.FullName, outputStream, filename.FullName);
                }

                outputStream.Finish();
                outputStream.Close();
            }

            return zipPath;
        }

        private static void Compress(string filename, ZipOutputStream outputStream, string staticFile)
        {
            //如果目标地址非文件,且末尾不是\结尾,则补全\
            if (!filename.IsFile() && filename[filename.Length - 1] != Path.DirectorySeparatorChar)
            {
                filename += Path.DirectorySeparatorChar;
            }
            Crc32 crc = new Crc32();

            string[] filenames;
            if (filename.IsFile())
            {
                filenames = new string[] { filename };
            }
            else
            {
                filenames = Directory.GetFileSystemEntries(filename);
            }

            foreach (string file in filenames)
            {

                if (Directory.Exists(file))
                {
                    Compress(file, outputStream, staticFile);
                }

                else // 否则直接压缩文件
                {
                    //打开要压缩的文件
                    using (FileStream fs = File.OpenRead(file))
                    {
                        long bufferSize = 4194304;
                        long block = (fs.Length / bufferSize) + 1;
                        byte[] buffer = null;

                        string tempfile = file.Substring(staticFile.LastIndexOf("\\") + 1);

                        ZipEntry entry = new ZipEntry(tempfile)
                        {
                            Size = fs.Length,
                            DateTime = DateTime.Now
                        };
                        outputStream.PutNextEntry(entry);

                        for (long i = 1; i <= block; i += 1)
                        {
                            if ((bufferSize * i) < fs.Length)
                            {
                                buffer = new byte[bufferSize];
                                fs.Seek(bufferSize * (i - 1), SeekOrigin.Begin);
                            }
                            else if (fs.Length < bufferSize)
                            {
                                buffer = new byte[fs.Length];
                            }
                            else
                            {
                                buffer = new byte[fs.Length - (bufferSize * (i - 1L))];
                                fs.Seek(bufferSize * (i - 1L), SeekOrigin.Begin);
                            }
                            fs.Read(buffer, 0, buffer.Length);
                            crc.Reset();
                            crc.Update(buffer);
                            outputStream.Write(buffer, 0, buffer.Length);
                            outputStream.Flush();
                        }
                    }

                }
            }
        }

        /// <summary>
        /// 解压ZIP文件
        /// </summary>
        /// <param name="zipFilename">要解压的ZIP文件</param>
        /// <param name="path">解压到指定的文件夹</param>
        /// <returns></returns>
        public static string[] DeCompressFile(string zipFilename, string fileDir)
        {
            List<string> lstPath = new List<string>();
            string rootFile = " ";
            try
            {
                //读取压缩文件(zip文件),准备解压缩
                ZipInputStream zipStream = new ZipInputStream(File.OpenRead(zipFilename.Trim()));
                ZipEntry theEntry;
                string path = fileDir;
                string rootDir = " ";
                //根目录下的第一个子文件夹的名称
                while ((theEntry = zipStream.GetNextEntry()) != null)
                {
                    rootDir = Path.GetDirectoryName(theEntry.Name);
                    //得到根目录下的第一级子文件夹的名称
                    if (rootDir.IndexOf("\\") >= 0)
                    {
                        rootDir = rootDir.Substring(0, rootDir.IndexOf("\\") + 1);
                    }
                    string dir = Path.GetDirectoryName(theEntry.Name);
                    //根目录下的第一级子文件夹的下的文件夹的名称
                    string fileName = Path.GetFileName(theEntry.Name);
                    //根目录下的文件名称
                    if (dir != " ")
                    //创建根目录下的子文件夹,不限制级别
                    {
                        if (!Directory.Exists(fileDir + "\\" + dir))
                        {
                            path = fileDir + "\\" + dir;
                            //在指定的路径创建文件夹
                            Directory.CreateDirectory(path);
                        }
                    }
                    else if (dir == " " && fileName != "")
                    //根目录下的文件
                    {
                        path = fileDir;
                        rootFile = fileName;
                    }
                    else if (dir != " " && fileName != "")
                    //根目录下的第一级子文件夹下的文件
                    {
                        if (dir.IndexOf("\\") > 0)
                        //指定文件保存的路径
                        {
                            path = fileDir + "\\" + dir;
                        }
                    }

                    if (dir == rootDir)
                    //判断是不是需要保存在根目录下的文件
                    {
                        path = fileDir + "\\" + rootDir;
                    }

                    //以下为解压缩zip文件的基本步骤
                    //遍历压缩文件里的所有文件,创建一个相同的文件。
                    if (fileName != string.Empty)
                    {
                        lstPath.Add(path + "\\" + fileName);
                        FileStream streamWriter = File.Create(path + "\\" + fileName);

                        int size = 2048;
                        byte[] data = new byte[2048];
                        while (true)
                        {
                            size = zipStream.Read(data, 0, data.Length);
                            if (size > 0)
                            {
                                streamWriter.Write(data, 0, size);
                            }
                            else
                            {
                                break;
                            }
                        }

                        streamWriter.Close();
                    }
                }
                zipStream.Close();

                return lstPath?.ToArray() ;
            }
            catch (Exception ex)
            {
                return new string[] { "1; " + ex.Message };
            }
        }

        /// <summary>
        /// 解压ZIP文件
        /// </summary>
        /// <param name="zipFilename">要解压的ZIP文件</param>
        /// <param name="path">解压到指定的文件夹</param>
        /// <returns></returns>
        public static MemoryStream DeCompressFile(string zipFilename)
        {
            try
            {
                //读取压缩文件(zip文件),准备解压缩
                ZipInputStream zipStream = new ZipInputStream(File.OpenRead(zipFilename.Trim()));
                ZipEntry theEntry = zipStream.GetNextEntry();

                MemoryStream stream = new MemoryStream();

                int size = 2048;
                byte[] data = new byte[2048];
                while (true)
                {
                    size = zipStream.Read(data, 0, data.Length);
                    if (size > 0)
                    {
                        stream.Write(data, 0, size);
                    }
                    else
                    {
                        break;
                    }
                }
                return stream;
            }
            catch (Exception ex)
            {
                Logs.Error("DeCompressFile解压异常", ex);
                return null;
            }
        }
    }

    public static class FileHelper
    {
        public static bool IsFile(this string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
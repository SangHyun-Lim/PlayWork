using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MsgFileMultSocketServer
{
    class FileClient : IDisposable
    {
        private TcpClient Sock = null;

        private NetworkStream Stream { set; get; }

        private FileData fileData { set; get; }


        private string filePath { get { return Config.Instance().filePath; } }
        private int ReadTimeout { get { return Config.Instance().Timetout; } }
        private DirectoryInfo di { set; get; }


        public FileClient(TcpClient tcp)
        {
            this.Sock = tcp;

            if (Sock != null)
            {
                Stream = Sock.GetStream();
                Stream.ReadTimeout = ReadTimeout * 1000;
            }

            di = new System.IO.DirectoryInfo(filePath);
        }

        public void Run()
        {
            if(HelloSync() == true)
            {
                FileTransfer();
            }
        }

        private bool HelloSync()
        {
            bool result = false;

            try
            {
                FileModel msg = new FileModel();

                msg.SetCmd((int)FileCmdType.FILE_HELLO);
                msg.SetStrData($"FILE TRANSFER CONN COMPLETE. {DateTime.Now.ToString()}");

                Stream.Write(msg.GetSendData(), 0, 1024);
                Stream.Flush();

                byte[] buffer = new byte[1024];

                if (Stream.Read(buffer, 0, 1024) > 0)
                {
                    int err = msg.RawModelSet(buffer);

                    if (err > 0 && msg.GetCmd() == (int)FileCmdType.FILE_HELLO_ACK)
                    {
                        result = true;
                    }
                }
            }catch(Exception ex)
            {
                Console.WriteLine("# FILE TRANSFER HELLO FAILED :: " + ex.Message);
            }

            return result;
        }

        private void FileTransfer()
        {
            if (Stream == null)
                return;

            byte[] buff = new byte[1024];

            if(Stream.Read(buff, 0, 1024) > 0)
            {
                FileModel fm = new FileModel();

                if(fm.RawModelSet(buff) > 0)
                {
                    if (fm.GetCmd() == (int)FileCmdType.FILE_SEND_INIT) {
                        string strGuid = Guid.NewGuid().ToString("N");
                        fm.SetCmd((int)FileCmdType.FILE_SEND_READY);
                        fm.SetGuid(strGuid);

                        FileData fd = new FileData(fm.GetStrData().Trim(), 0, strGuid);
                        FileStream f = File.Open(filePath + fd.Guid, FileMode.Create);

                        Stream.Write(fm.GetSendData(), 0, 1024);
                        int fileSeq = -1;

                        using (BinaryWriter wr = new BinaryWriter(f))
                        {
                            while (Stream.Read(buff, 0, 1024) > 0)
                            {
                                int fomt = fm.RawModelSet(buff);
                                
                                if (fomt > 0)
                                {
                                    if (fm.GetStrGuid() != strGuid)
                                    {
                                        fm.SetGuid(strGuid);
                                        fm.SetCmd((int)FileCmdType.FILE_UPLOAD_ERR);
                                        fm.SetStrData("Invalid GUID.");

                                        Stream.Write(fm.GetSendData(), 0, 1024);
                                        Stream.Flush();

                                        break;
                                    }

                                    if(fm.GetCmd() == (int)FileCmdType.FILE_SENDING)
                                    {
                                        if (fileSeq < 0)
                                            fileSeq = fm.GetSeq();
                                        
                                        if(++fileSeq != fm.GetSeq())
                                        {
                                            continue;
                                        }

                                        fd.Size += fm.GetLength();

                                        wr.Write(fm.GetData());
                                        wr.Flush();

                                        fm.SetCmd((int)FileCmdType.FILE_SEND_ACK);

                                        Stream.Write(fm.GetSendData(), 0, 1024);
                                        Stream.Flush();
                                    }
                                    else if(fm.GetCmd() == (int)FileCmdType.FILE_FINISH)
                                    {
                                        fm.SetCmd((int)FileCmdType.FILE_FINISHED);
                                        wr.Close();
                                        Stream.Write(fm.GetSendData(), 0, 1024);
                                        Stream.Flush();

                                        OnSavedFile(fd);

                                        break;
                                    }
                                }
                            }
                        }

                    }
                    else if(fm.GetCmd() == (int)FileCmdType.FILE_REQ)
                    {
                        string strGuid = fm.GetStrGuid();
                        var fd = OnGetFileInfo(strGuid);
                        
                        if(fd == null)
                        {
                            fm.SetCmd((int)FileCmdType.FILE_DWLOAD_ERR);
                            fm.SetStrData("Unregistered File.(Or Invalid GUID)");

                            Stream.Write(fm.GetSendData(), 0, 1024);
                            return;
                        }

                        fm.SetGuid(fd.Guid);
                        fm.SetStrData(fd.Name);
                        fm.SetCmd((int)FileCmdType.FILE_REQ_INFO);

                        Stream.Write(fm.GetSendData(), 0, 1024);
                        Stream.Flush();

                        if(Stream.Read(buff, 0, 1024) > 0)
                        {
                            int len = fm.RawModelSet(buff);

                            if (len > 0)
                            {
                                if(fm.GetCmd() == (int)FileCmdType.FILE_REQ_ACK)
                                {
                                    var fi = new FileInfo(filePath + fd.Name);

                                    if (fi.Exists)
                                    {
                                        var fs = fi.OpenRead();
                                        int fileSeq = 0;
                                        int leng = 0;

                                        while((leng = fs.Read(fm.Data, 0, fm.Data.Length)) > 0)
                                        {
                                            fm.SetSize(leng);
                                            fm.SetSeq(fileSeq++);
                                            fm.SetCmd((int)FileCmdType.FILE_RES_DATA);

                                            Stream.Write(fm.GetSendData(), 0, 1024);
                                            Stream.Flush();
#if de
                                            if(//Debeg){ 모드 설정.
                                                if(Stream.Read(buff, 0, 1024) > 0)
                                                {
                                                    if(fm.RawModelSet(buff) > 0)
                                                    {
                                                        //리스폰스 채크 또는 Ack 체크
                                                    }
                                                }
                                            }
#endif
                                        }

                                        fm.SetCmd((int)FileCmdType.FILE_REQ_FINISH);
                                        fm.SetStrData("FILE TRANSFER(Download) COMPLETE");

                                        Stream.Write(fm.GetSendData(), 0, 1024);
                                        Stream.Flush();
                                    }
                                    else
                                    {
                                        fm.SetCmd((int)FileCmdType.FILE_DWLOAD_ERR);
                                        fm.SetStrData("No File.");

                                        Stream.Write(fm.GetSendData(), 0, 1024);
                                        return;
                                    }
                                }
                                else
                                {
                                    fm.SetCmd((int)FileCmdType.FILE_DWLOAD_ERR);
                                    fm.SetStrData("Invalid Command.");

                                    Stream.Write(fm.GetSendData(), 0, 1024);
                                    return;
                                }
                            }
                            else
                            {
                                fm.SetCmd((int)FileCmdType.FILE_DWLOAD_ERR);
                                switch (len)
                                {
                                    case -401:
                                        fm.SetStrData("Formet length does not correct.");
                                        break;
                                    case -402:
                                        fm.SetStrData("STX/ETX does not correct.");
                                        break;
                                    case -403:
                                        fm.SetStrData("FCS FAILED");
                                        break;
                                    default:
                                        fm.SetStrData("Invalid Foramt.");
                                        break;
                                }

                                Stream.Write(fm.GetSendData(), 0, 1024);
                                return;
                            }
                        }

                    }
                }
            }
        }

        public void WipeFile()
        {
            DirectoryInfo di = new System.IO.DirectoryInfo(filePath);

            foreach (FileInfo f in di.GetFiles())
            {
                f.Delete();
            }
        }

        public void Dispose()
        {
#if lin
            if (false)
            {
                DirectoryInfo di = new System.IO.DirectoryInfo(filePath);

                foreach (FileInfo f in di.GetFiles())
                {
                    f.Delete();
                }
            }

#endif
            if (Stream != null)
            {
                Stream.Close();
                Stream.Dispose();
            }

            if (Sock != null)
            {
                Sock.Close();
                Sock.Dispose();
            }

            Console.WriteLine("## FILE TRANSFER FREE.");
        }

        public delegate void SavedFile(FileData fd);
        public event SavedFile OnSavedFile;

        public delegate FileData GetFileInfo(string guid);
        public event GetFileInfo OnGetFileInfo;
    }
}

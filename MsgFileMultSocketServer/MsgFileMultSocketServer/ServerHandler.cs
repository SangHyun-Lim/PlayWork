using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MsgFileMultSocketServer
{
    class ServerHandler
    {
        /// <summary>
        /// Critical Section - ClientLIst
        /// </summary>
        private object lockerClient = new object();

        /// <summary>
        /// Critical Section - MsgQueue
        /// </summary>
        private object lockerMsg = new object();

        /// <summary>
        /// Critical Section - FileList
        /// </summary>
        private object lockerFile = new object();

        //서버 메인 스레드
        private Thread SocketThread = null;
        //파일 서버 메인 스레드
        private Thread FileThread = null;

        //소켓 서버
        private TcpListener SocketService = null;
        //파일 소켓 서버
        private TcpListener FileService = null;

        //클라이언트 풀
        private static Dictionary<string, Client> clients = new Dictionary<string, Client>();
        //메시지 큐
        private Queue<MsgModel> MsgQueue = new Queue<MsgModel>();

        //파일 디비..(디비안쓰고 하기 넘힘들당...)
        private Dictionary<string, FileData> FileDB = new Dictionary<string, FileData>();

        private int SocketPort { get { return Config.Instance().SocketPort; } }
        private int ConnectPool { get { return Config.Instance().SocketPool; } }
        private int TimeoutSec { get { return Config.Instance().Timetout; } }
        private bool AliveEnable { get { return Config.Instance().AliveFlag; } }
        private int FileSocketPort { get { return Config.Instance().FilePort; } }
        private string FilePath { get { return Config.Instance().filePath; } }


        /// <summary>
        /// 서비스 동작 flag
        /// </summary>
        public bool ServiceFlag = false;


        public ServerHandler()
        {
            Init();
        }

        void Init()
        {
            Console.WriteLine("Server Init Start.");


            //Socket Server Init....
            SocketService = new TcpListener(IPAddress.Any, SocketPort);

            SocketThread = new Thread(new ThreadStart(ServiceOperation));
            SocketThread.IsBackground = true;

            ServiceFlag = true;

            clients.Clear();
            MsgQueue.Clear();


            //File Socket Server Init...
            //작업 예정

            FileService = new TcpListener(IPAddress.Any, FileSocketPort);

            FileThread = new Thread(new ThreadStart(FileServiceOperation));
            FileThread.IsBackground = true;

            FileDB.Clear();

            DirectoryInfo di = new DirectoryInfo(FilePath);
            if (di.Exists == false)
                di.Create();


            Console.WriteLine("Server Init Finished!");
        }

       
        public void ProcessRun()
        {
            Console.WriteLine("######### Server Start #########");

            //TcpListener Start!
            SocketService.Start();

            //통신 스래드 시작
            SocketThread.Start();

            //파일 스레드 시작
            FileThread.Start();

            //서버 Accept 반복문.
            while (ServiceFlag)
            {
                TcpClient tc = SocketService.AcceptTcpClient();
                string strGuid = Guid.NewGuid().ToString("N");

                Client cl = new Client(tc, strGuid);
                
                cl.OnClosed += ClientListRemove;
                cl.OnReceived += OnRecviedToClient;

                ClientListAppend(cl.Guid, cl);
            }
        }

        /// <summary>
        /// 통신 서비스 함수.
        /// </summary>
        private void ServiceOperation()
        {
            Console.WriteLine("******** Service Start ******** ");

            while (ServiceFlag)
            {
                Thread.Sleep(300);

                if (GetMsgQueue().Count > 0)
                {
                    var msgModel = GetMsgQueue().Dequeue();

                    switch (msgModel.GetCmd())
                    {
                        case CmdType.HELLO_ACK:
                            Console.WriteLine($"## ONLINE CLIENT - #{msgModel.Guid}");
                            break;
                        case CmdType.CLIENT_MSG://클라이언트로 메시지 수신
                            {
                                Console.WriteLine($"# SENDING MSG : [{msgModel.SendFormMsg()}]");
                                for (int i = 0; i < GetClientList().Count; i++)
                                {
                                    Client cl = GetClientList().ElementAt(i).Value;
                                    if (cl.Guid == msgModel.Guid)
                                    {
                                        MsgModel res = msgModel;
                                        res.SetCmd(CmdType.CLIENT_ACK);

                                        cl.WriteMsg(res);
                                    }
                                    else
                                    {
                                        MsgModel res = msgModel;
                                        res.SetCmd(CmdType.BROAD_MSG);

                                        cl.WriteMsg(res);
                                    }
                                }
                                break;
                            }
                        case CmdType.BROAD_ACK:
                            break;
                        case CmdType.FILE_INFO_REQ:
                            {

                                if (GetClientList().TryGetValue(msgModel.Guid, out Client cl))
                                {
                                    if(GetFileList().Count < 1)
                                    {
                                        MsgModel res = msgModel;
                                        res.UserName = "SERVER";
                                        res.SetCmd(CmdType.FILE_INFO_REQ);
                                        res.Msg = "[WARN] There are NO Registered Files!";
                                        cl.WriteMsg(res);
                                    }
                                    else
                                    {
                                        foreach (var file in GetFileList().Values)
                                        {
                                            MsgModel res = msgModel;
                                            res.UserName = "SERVER";
                                            res.SetCmd(CmdType.FILE_INFO_REQ);
                                            res.Msg = $"[{file.Guid}] {file.Name} / {file.Size} Byte";
                                            cl.WriteMsg(res);
                                        }
                                    }
                                }
                                
                                break;
                            }
                        case CmdType.CLOSE_REQ:
                            {
                                if (GetClientList().TryGetValue(msgModel.Guid, out Client cl))
                                {
                                    MsgModel res = msgModel;
                                    res.SetCmd(CmdType.CLOSE_ACK);

                                    cl.WriteMsg(res);

                                    cl.Dispose();
                                }

                                break;
                            }
                        case CmdType.ALIVE:
                            {

                                if (GetClientList().TryGetValue(msgModel.Guid, out Client cl))
                                {
                                    MsgModel res = msgModel;
                                    res.SetCmd(CmdType.ALIVE_ACK);

                                    cl.WriteMsg(res);
                                }

                                break;
                            }
                        case CmdType.ERROR_ACK:
                        case CmdType.UNKNOWN:
                        default:
                            break;

                    }
                }
            }

            Console.WriteLine("******** Service Finished ******** ");

        }

        /// <summary>
        /// 파일전송 함수
        /// </summary>
        private void FileServiceOperation()
        {
            Console.WriteLine("******** File Service Start ******** ");

            FileService.Start();


            while (ServiceFlag)
            {
                try
                {
                    TcpClient tc = FileService.AcceptTcpClient();

                    FileClient fc = new FileClient(tc);

                    fc.OnGetFileInfo += GetFileInfo;
                    fc.OnSavedFile += SavedFile;

                    fc.Run();

                }
                catch(Exception ex)
                {
                    Console.WriteLine("FILE ACCEPT ERROR :: " + ex.Message);
                }
            }
            
        }


        public void OnRecviedToClient(MsgModel msg)
        {
            Console.WriteLine($"MSG PUSH Q  [{msg.SendFormMsg()}]");

            GetMsgQueue().Enqueue(msg);
        }

        public void ClientListAppend(string guid, Client cl)
        {
            GetClientList().Add(guid, cl);

            Console.WriteLine($"APPEND CLIENT LIST[{GetClientList().Count}] -- {guid}");
        }

        public void ClientListRemove(string guid)
        {
            
            try
            {
                if(GetClientList().TryGetValue(guid, out Client cl))
                {
                    if (GetClientList().Remove(guid) == true) {
                        Console.WriteLine($"CLIENT LIST REMOVED [{guid}]");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CLIENT REMOVED ERROR :: {ex.Message}");
            }

        }

        public void SavedFile(FileData file)
        {
            GetFileList().Add(file.Guid, file);

            Console.WriteLine($"FILE SAVED [{file.Guid}] {file.Name}");
        }

        public FileData GetFileInfo(string guid)
        {
            if (GetFileList().TryGetValue(guid, out FileData data))
                return data;
            else
                return null;
        }
        /// <summary>
        /// 현재 접속 유저 리스트에 접근합니다.
        /// </summary>
        /// <returns>List.client</returns>
        public Dictionary<string, Client> GetClientList()
        {
            lock (lockerClient)
            {
                return clients;
            }
        }

        /// <summary>
        /// 통합메시지 큐에 접근합니다.
        /// </summary>
        /// <returns></returns>
        public Queue<MsgModel> GetMsgQueue()
        {
            lock (lockerMsg)
            {
                if (MsgQueue == null)
                    MsgQueue = new Queue<MsgModel>();
                return MsgQueue;
            }
        }

        /// <summary>
        /// FIleDB에 접근합니다.
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, FileData> GetFileList()
        {
            lock (lockerFile)
            {
                return FileDB;
            }
        }

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MsgFileMultSocketServer
{
    class Client : IDisposable
    {
        private Thread clientThread = null;

        private TcpClient Sock { get; set; }
        public string IPaddr { get; set; }
        public string Guid { private set; get; }

        private int BufferSize { get { return Config.Instance().ReadBufferSize; } }
        public NetworkStream Stream { get; set; }

        private bool RunFlag = false;

        public Client(TcpClient tcp, string guid)
        {
            this.Sock = tcp;
            this.Guid = guid;
            IPEndPoint ip_point = (IPEndPoint)Sock.Client.RemoteEndPoint;

            Stream = tcp.GetStream();

            this.IPaddr = ip_point.Address.ToString();

            clientThread = new Thread(new ThreadStart(ThreadRun))
            {
                IsBackground = true
            };
            RunFlag = true;

            clientThread.Start();
        }

        public void ThreadRun()
        {
            try
            {
                //소켓등록 후 서버에서 Hello Sign 전송.
                Hello();

                while (RunFlag)
                {
                    byte[] buffer = new byte[Config.Instance().ReadBufferSize];
                    int readSize = Stream.Read(buffer, 0, buffer.Length);

                    if (readSize < 0)
                        break ;

                    if (RunFlag == false)
                        break;

                    string strMsg = Config.Instance().encod.GetString(buffer, 0, readSize);
                    MsgModel msg = new MsgModel(strMsg.Trim());

                    OnReceived(msg);
                }

            }catch(Exception ex)
            {
                Console.WriteLine("TCP READ SOCKET ERR : " + ex.Message);
            }

            Dispose();
        }

        public int WriteMsg(MsgModel msg)
        {
            int result = -9999;

            if (Sock == null)
            {
                result = -504;
                return result;
            }

            if(Sock.Connected == false)
            {
                result = -503;
                return result;
            }

            try
            {
                string strMsg = msg.SendFormMsg();
                Console.WriteLine($"-- WRITE_MSG[{strMsg}]");
                byte[] buffer = Config.Instance().encod.GetBytes(strMsg);

                Stream.Write(buffer, 0, buffer.Length);
                Stream.Flush();
                result = buffer.Length;
            }
            catch(Exception ex)
            {
                Console.WriteLine("TCP WRITE SOCKET ERR : " + ex.Message);
            }

            return result;
        }

        public void Hello()
        {
            MsgModel msg = new MsgModel();
            msg.SetCmd(CmdType.HELLO_MSG);
            msg.Guid = this.Guid;
            msg.UserName = "SERVER";
            msg.Msg = this.Guid;

            WriteMsg(msg);
        }

        public void Dispose()
        {
            try
            {
                OnClosed(Guid);

                Stream.Close();
                Stream.Dispose();

                Sock.Close();
                Sock.Dispose();

                clientThread.Abort();
                clientThread.Join();
            }
            catch (ThreadAbortException ex)
            {
                string exMsg = string.Empty;

                exMsg = System.Reflection.Assembly.GetExecutingAssembly().ManifestModule.Name + ":" +
                             System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name + "." + ex.StackTrace + Environment.NewLine +
                             System.Reflection.MethodBase.GetCurrentMethod().Name + "():" + ex.Message;
                Console.WriteLine("THREAD ABORT ERROR :: " + exMsg);
            }
            catch (Exception ex)
            {
                string exMsg = string.Empty;

                exMsg = System.Reflection.Assembly.GetExecutingAssembly().ManifestModule.Name + ":" +
                             System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name + "." + ex.StackTrace + Environment.NewLine +
                             System.Reflection.MethodBase.GetCurrentMethod().Name + "():" + ex.Message;
                Console.WriteLine("Dispose ERROR :: " + exMsg);
            }
        }

        public delegate void recvMsg(MsgModel msg);
        public event recvMsg OnReceived;

        public delegate void Closed(string guid);
        public event Closed OnClosed;
    }
}

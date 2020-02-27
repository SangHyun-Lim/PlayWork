using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MsgFileMultSocketServer
{
    class MsgModel
    {
        public string Cmd { set; get; }
        public string Guid { set; get; }
        public string UserName { set; get; }
        public string Msg { set; get; }
        public string TimeStemp { set; get; }

        public string SendFormMsg()
        {
            return string.Format("{0}|{1}|{2}|{3}|{4}", Cmd, Guid, UserName, Msg, TimeStemp);
        }

        public MsgModel()
        {
            Cmd = string.Empty;
            UserName = string.Empty;
            Guid = string.Empty;
            Msg = string.Empty;
            TimeStemp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff");
        }

        public MsgModel(string rawStr)
        {
            RecvMsg(rawStr);
        }

        public bool RecvMsg(string rawStr)
        {
            bool result = false;
            var strlist = rawStr.Split('|');

            if (strlist.Length == 5)
            {
                Cmd = strlist[0];
                Guid = strlist[1];
                UserName = strlist[2];
                Msg = strlist[3];
                TimeStemp = strlist[4];

                result = true;
            }

            return result;
        }

        public CmdType GetCmd()
        {
            CmdType cmd;
            if (int.TryParse(Cmd, out int i))
            {
                switch (i)
                {
                    case 1:
                        cmd = CmdType.HELLO_MSG;
                        break;
                    case 2:
                        cmd = CmdType.HELLO_ACK;
                        break;
                    case 3:
                        cmd = CmdType.CLIENT_MSG;
                        break;
                    case 4:
                        cmd = CmdType.CLIENT_ACK;
                        break;
                    case 5:
                        cmd = CmdType.BROAD_MSG;
                        break;
                    case 6:
                        cmd = CmdType.BROAD_ACK;
                        break;
                    case 7:
                        cmd = CmdType.FILE_INFO_REQ;
                        break;
                    case 8:
                        cmd = CmdType.FILE_INFO_ACK;
                        break;
                    case 9:
                        cmd = CmdType.CLOSE_REQ;
                        break;
                    case 10:
                        cmd = CmdType.CLOSE_ACK;
                        break;
                    case 21:
                        cmd = CmdType.ALIVE;
                        break;
                    case 22:
                        cmd = CmdType.ALIVE_ACK;
                        break;
                    case -1:
                        cmd = CmdType.ERROR;
                        break;
                    case -2:
                        cmd = CmdType.ERROR_ACK;
                        break;
                    default:
                        cmd = CmdType.UNKNOWN;
                        break;
                }
            }
            else
            {
                cmd = CmdType.UNKNOWN;
            }

            return cmd;
        }

        public void SetCmd(CmdType type)
        {

            Cmd = ((int)type).ToString();
        }
    }

    public enum CmdType
    {
        HELLO_MSG = 1,  // 서버 전송
        HELLO_ACK,      // 클라이언트 전송

        CLIENT_MSG,     // 클라이언트 전송
        CLIENT_ACK,     // 서버 전송

        BROAD_MSG,      // 서버전송
        BROAD_ACK,      // 클라이언트 전송

        FILE_INFO_REQ,  // 파일 정보 요청
        FILE_INFO_ACK,  // 파일 정보 응답

        CLOSE_REQ,      // 서버 클로즈 메시지
        CLOSE_ACK,      // 서버 수신

        ALIVE = 20,     // 클라이언트 전송
        ALIVE_ACK = 21, // 서버 전송

        ERROR = -1,     // 서버 전송
        ERROR_ACK = -2,  // 클라이언트 전송

        UNKNOWN = -99
    }
}

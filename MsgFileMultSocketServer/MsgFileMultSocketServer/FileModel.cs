using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MsgFileMultSocketServer
{
    class FileModel
    {
        //Data Format--------------------------------------
        //FORMAT - STX[1]/CMD[1]/GUID[16]/SEQ[2]/SIZE[2]/DATA[1000]/FCC[1]/ETX[1] 
        //TOTAL LEN : 1024byte
        //-------------------------------------------------
        private readonly byte Stx = 0x02;                //1Byte
        public byte[] Cmd = new byte[1];      //1Byte
        public byte[] Guid = new byte[16];     //16Byte
        public byte[] Seq = new byte[2];      //2Byte
        public byte[] Size = new byte[2];      //2Byte
        public byte[] Data = new byte[1000];   //1000Byte
        public byte[] Fcs = new byte[1];        //1Byte  - FCS는 stx,etx를 제외한 모든 데이터를 XOR연산으로 산출
        private readonly byte Etx = 0x03;                //1Byte


        //Total 1024Byte == 1KB

        public FileModel()
        {
            Init();
        }

        public FileModel(string guid)
        {
            Init();
            SetGuid(guid);
        }

        public void Init()
        {
            Array.Clear(Cmd, 0, Cmd.Length);
            Array.Clear(Guid, 0, Guid.Length);
            Array.Clear(Seq, 0, Seq.Length);
            Array.Clear(Size, 0, Size.Length);
            Array.Clear(Data, 0, Data.Length);
            Array.Clear(Fcs, 0, Fcs.Length);
        }

        public int RawModelSet(byte[] raw)
        {
            int result = -1;

            if (raw.Length != 1024)
            {
                result = -401;// 응답데이터 길이 안맞음. length error
                return result;
            }

            if (raw[0] != Stx || raw[1023] != Etx)
            {
                result = -402; //STX/ETX가 없음. format error
                return result;
            }

            byte fcs = 0x00; //fcs Checker

            //Raw[1] - CMD, Raw[1022] - FCS
            for (int i = 1; i < (raw.Length - 2); i++)
            {
                fcs ^= raw[i];
            }

            if (fcs != raw[1022])
            {
                result = -403;// FCS Error
                return result;
            }
            else
                Fcs[0] = fcs;

            int indx = 1;

            Array.Copy(raw, indx, Cmd, 0, Cmd.Length);
            indx += Cmd.Length;

            Array.Copy(raw, indx, Guid, 0, Guid.Length);
            indx += Guid.Length;

            Array.Copy(raw, indx, Seq, 0, Seq.Length);
            indx += Seq.Length;

            Array.Copy(raw, indx, Size, 0, Size.Length);
            indx += Size.Length;

            Array.Copy(raw, indx, Data, 0, Data.Length);
            indx += Data.Length;

            result = 1;
            return result;
        }

        /// <summary>
        /// 프로토콜 타입 설정
        /// </summary>
        /// <param name="cmd">타입</param>
        public void SetCmd(int cmd)
        {
            Cmd[0] = (byte)cmd;
        }

        /// <summary>
        /// 프로토콜 GUID 설정
        /// </summary>
        /// <param name="guid">GUID</param>
        public void SetGuid(string guid)
        {
            string uid = guid;
            var rawGuid = Enumerable.Range(0, uid.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(uid.Substring(x, 2), 16))
                             .ToArray();

            if (rawGuid.Length == 16)
                Array.Copy(rawGuid, Guid, 16);
        }

        /// <summary>
        /// 데이터 사이즈 설정
        /// </summary>
        /// <param name="size">데이터 설정</param>
        public void SetSize(int size)
        {
            if (size < 0 || size > 1000)
                return;

            int i, j;
            i = size / 256;
            j = size % 256;

            Size[0] = (byte)i;
            Size[1] = (byte)j;
        }

        public void SetSeq(int seq)
        {
            if (seq < 0)
                return;

            int i, j;
            i = seq / 256;
            j = seq % 256;

            Seq[0] = (byte)i;
            Seq[1] = (byte)j;
        }

        /// <summary>
        /// 파일 파싱 후 바디 데이터
        /// </summary>
        /// <param name="raw"></param>
        public void SetData(byte[] raw)
        {
            if (raw.Length <= 1000)
                Array.Copy(raw, Data, raw.Length);

            SetSize(raw.Length);
        }

        public void SetStrData(string strData)
        {
            byte[] strBuffer = Config.Instance().encod.GetBytes(strData);

            if (strBuffer.Length <= 1000)
                Array.Copy(strBuffer, Data, strBuffer.Length);

            SetSize(strBuffer.Length);
        }

        /// <summary>
        /// RawData Utf-8 디코딩 String 출력
        /// </summary>
        /// <returns>Raw Data</returns>
        public string GetStrData()
        {
            return Config.Instance().encod.GetString(Data);
        }

        /// <summary>
        /// GUID String 출력.
        /// </summary>
        /// <returns>Guid</returns>
        public string GetStrGuid()
        {
            return BitConverter.ToString(Guid);
        }

        /// <summary>
        /// 데이터 포멧 시퀀스
        /// </summary>
        /// <returns>Seq</returns>
        public int GetSeq()
        {
            return BitConverter.ToInt16(Seq, 0);
        }

        /// <summary>
        /// 바디 데이터 실 길이
        /// </summary>
        /// <returns>Length</returns>
        public int GetLength()
        {
            return BitConverter.ToInt16(Size, 0);
        }

        /// <summary>
        /// 파일 프로토콜 커맨드 타입 출력
        /// </summary>
        /// <returns>CMD</returns>
        public int GetCmd()
        {
            return BitConverter.ToInt16(Cmd, 0);
        }

        public byte[] GetData()
        {
            byte[] result = new byte[GetLength()];

            Array.Copy(Data, result, result.Length);

            return result;
        }

        public byte[] GetSendData()
        {
            var msgList = new List<byte>();

            msgList.AddRange(Cmd);
            msgList.AddRange(Guid);
            msgList.AddRange(Seq);
            msgList.AddRange(Size);
            msgList.AddRange(Data);

            foreach (var f in msgList)
            {
                Fcs[0] ^= f;
            }

            msgList.AddRange(Fcs);

            msgList.Insert(0, Stx);
            msgList.Add(Etx);

            return msgList.ToArray();
        }

    }
    public enum FileCmdType
    {
        FILE_HELLO = 0,
        FILE_HELLO_ACK,
        FILE_SEND_INIT, //클라이언트 전송(서버로 업로드) - 요청
        FILE_SEND_READY,    //서버 준비 완료
        FILE_SENDING,       // 파일데이터 전송
        FILE_SEND_ACK,      // 파일데이터 수신완료
        FILE_FINISH,        // 파일 데이터 완료
        FILE_FINISHED,      // 프로세스 완료 

        FILE_REQ = 10, //클라이언트 전송 (다운로드) - 요청
        FILE_REQ_INFO,      //파일 정보 전송
        FILE_REQ_ACK,       //수신 완료 응답.
        FILE_RES_DATA,      //서버 데이터 전송
        FILE_RES_ACK,       //데이터 수신 응답.
        FILE_REQ_FINISH,    //파일 완료 
        FILE_REQ_FINISHED,  // 파일 완료 응답.

        FILE_UPLOAD_ERR = -1, // Error Msg 확인
        FILE_DWLOAD_ERR = -2, // Error Msg 확인
        FILE_FAILED = -9
    }
}

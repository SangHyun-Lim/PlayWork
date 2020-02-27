using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;


namespace MsgFileMultSocketServer
{
    class Config
    {
        private static Config _instance = null;

        public int SocketPort = 20000;
        public int SocketPool = 100;
        public int Timetout = 30;
        public int ReadBufferSize = 2000;
        public bool AliveFlag = true;
        public int FilePort = 20001;
        public bool FileInstantMode = true;

        public string filePath = "./Download/";

        public UTF8Encoding encod = new UTF8Encoding(); // 통신시 Default Char-set : utf-8

        public Config()
        {
            if (int.TryParse(ConfigurationManager.AppSettings["ConnectionSocketPort"], out int param))
                SocketPort = param;

            if (int.TryParse(ConfigurationManager.AppSettings["ConnectionPoolSize"], out param))
                SocketPool = param;

            if (int.TryParse(ConfigurationManager.AppSettings["ConnectionTimeout"], out param))
                Timetout = param;

            if (int.TryParse(ConfigurationManager.AppSettings["ReadBufferSize"], out param))
                ReadBufferSize = param;

            if (bool.TryParse(ConfigurationManager.AppSettings["AliveCheckMode"], out bool flag))
                AliveFlag = flag;

            if (int.TryParse(ConfigurationManager.AppSettings["FileSocketPort"], out param))
                FilePort = param;

            if (bool.TryParse(ConfigurationManager.AppSettings["FileInstantMode"], out flag))
                FileInstantMode = flag;
        }
        
        public static Config Instance()
        {
            if (_instance == null)
                _instance = new Config();

            return _instance;
        }
    }
}

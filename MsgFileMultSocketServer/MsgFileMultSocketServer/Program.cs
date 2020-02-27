using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MsgFileMultSocketServer
{
    class Program
    {
        private static bool success = true;
        static void Main(string[] args)
        {
            Console.WriteLine("Press the 'q' key to exit");
            Console.WriteLine(); 

            new Program();


            while (success)
            {

                string cmd = Console.ReadLine();
                if ("q".Equals(cmd, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }
        }

        Program()
        {
            const string mtxName = "KoreanBibleUniversity_Since1952_DepartmentOfComputerSoftware_Since2002_ISTARTEDCLASSOFDOUBLE_1";

            Mutex mtx = new Mutex(true, mtxName, out success);

            if (success == false)
            {
                Console.WriteLine("이미 프로그램이 실행중입니다.");

                return;
            }

            ServerHandler server = new ServerHandler();

            Task t1 = new Task(new Action(server.ProcessRun));
            t1.Start();
        }

        
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
//using log4net;

namespace SharpFtpServer
{
    class Program
    {
        //void Program.Main(string[] args)
        //Основная функция программы
      static void Main(string[] args)
        {
            FtpServer s = new FtpServer();
            s.Start();//запуск сервера
            Console.WriteLine("Press any key to stop...");
            Console.ReadKey(true);
            s.Stop();//остановка сервера
            return;
        }
    }
}



        
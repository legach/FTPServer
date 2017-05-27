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
      static void Main(string[] args)
        {
        SharpFtpServer.FtpServer s = new SharpFtpServer.FtpServer();
           
                s.Start();
                Console.WriteLine("Press any key to stop...");
                Console.ReadKey(true);
            return;
        }

     /* static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
          {
              _log.Fatal((Exception)e.ExceptionObject);
          }
      */
    }
}



        
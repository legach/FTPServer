using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Net.Sockets;
using System.IO;
using System.Threading;

namespace SharpFtpServer
{
    //class SharpFtpServer.FtpServer
    //Позволяет организовать работу и завершение работы сервера
    public class FtpServer
    {
        //FtpServer._listener
        //TcpListener
        //Поле - основной объект для прослушки управляющего соединения
        private TcpListener _listener;

        //Конструктор класса
        public FtpServer()
        {
        }

        //void FtpServer.Start()
        //запуск прослушивания управляющего соединения
        //используется поле _listener
        public void Start()
        {
            _listener = new TcpListener(IPAddress.Any, 21);
            _listener.Start();
            //ожидание попытки входящего подключения
            _listener.BeginAcceptTcpClient(HandleAcceptTcpClient, _listener);
        }

        //void FtpServer.Stop()
        //остановка прослушивания управляющего соединения
        //используется поле _listener
        public void Stop()
        {
            if (_listener != null)
            {
                _listener.Stop();
            }
        }

        //void FtpSerever.HandleAcceptTcpClient(IAsyncResult result)
        //обрабатывает TCP соединение с клиентом
        private void HandleAcceptTcpClient(IAsyncResult result)
        {
            _listener.BeginAcceptTcpClient(HandleAcceptTcpClient, _listener);
            //создание нового объекта для связи с клиентом
            TcpClient client = _listener.EndAcceptTcpClient(result);
            ClientConnection connection = new ClientConnection(client);
            //Помещение выполнения метода ClientConnection.HandleClient в очередь
            ThreadPool.QueueUserWorkItem(connection.HandleClient, client);
        }
    }
}


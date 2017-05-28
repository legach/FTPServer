using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Net.Sockets;
using System.Net;
//using log4net;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;

namespace SharpFtpServer
{
    //class SharpFtpServer.ClientConnection
    //Позволяет обработать входящие подключения
    public class ClientConnection
    {
        //enum ClientConnection.TransferType
        //Возможные типы передачи
        private enum TransferType
        {
            Ascii,
            Ebcdic,
            Image,
            Local,
        }

        // enum ClientConnection.FormatControlType
        //Возможные форматы управления ипом передачи
        private enum FormatControlType
        {
            NonPrint,
            Telnet,
            CarriageControl,
        }

        // enum ClientConnection.DataConnectionType
        //Возможные режимы соединения
        private enum DataConnectionType
        {
            Passive,
            Active,
        }


        //TcpClient ClientConnection._controlClient
        //Управляющее соединение
        private TcpClient _controlClient;
        //TcpClient ClientConnection._dataClient
        //Соединение для передачи данных
        private TcpClient _dataClient;
        //NetworkStream ClientConnection._controlStream
        //Основной поток для управляющего соединения
        private NetworkStream _controlStream;
        //StreamReader ClientConnection._controlReader
        //Поток для чтения управляющих данных
        private StreamReader _controlReader;
        //StreamWriter ClientConnection._controlWriter
        //Поток для записи управляющих данных
        private StreamWriter _controlWriter;
        //TcpListener ClientConnection._passiveListener
        //Слушатель для соединения пассивного режима
        private TcpListener _passiveListener;
        //string ClientConnection._username
        //Содержит имя подключенного пользователя
        public string _username = "admin";
        //int ClientConnection._userid
        //Содержит идентификатор пройденной аутентификации пользователя
        private int _userid = 0;
        //bool ClientConnection._usertrue
        //Содержит флаг пройденной проверки имени пользователя
        private bool _usertrue = false;
        //string ClientConnection._password
        //Содержит пароль подключенного пользователя
        public string _password = "admin";
        //string ClientConnection._root
        //Содержит путь к корневой папке пользователя
        public string _root = "F:\\";
        //string ClientConnection._currentDirectory
        //Содержит путь к текущей рабочей папке
        private string _currentDirectory;
        //string ClientConnection._transferType
        //Содержит строковое значение типа передачи
        private string _transferType;
        //TransferType ClientConnection._connectionType
        //Содержит значение типа передачи
        private TransferType _connectionType = TransferType.Image;
        //FormatControlType ClientConnection._formatControlType
        //Содержит значение формата управления передачи
        private FormatControlType _formatControlType = FormatControlType.NonPrint;
        //DataConnectionType ClientConnection._dataConnectionType
        //Содержит значение типа соединения
        private DataConnectionType _dataConnectionType = DataConnectionType.Passive;
        //IPEndPoint ClientConnection._dataEndpoint
        //Содержит конечную точку с IP-адресом и портом
        private IPEndPoint _dataEndpoint;


        //Конструктор ClientConnection(TcpClient client)
        //Создает соединения, определяет потоки чтени и записи
        public ClientConnection(TcpClient client)
        {
            _controlClient = client;
            _controlStream = _controlClient.GetStream();
            _controlReader = new StreamReader(_controlStream);
            _controlWriter = new StreamWriter(_controlStream);
        }

        //void ClientConnection.HandleClient(object obj)
        //Обрабатывает команды пришедшие по управляющему соединению
        public void HandleClient(object obj)
        {
            _controlWriter.WriteLine("220 Service Ready.");
            _controlWriter.Flush();

            //Файл с настройками
            string fileset = "settings.txt";
            if (File.Exists(fileset))
            {
                StreamReader sr = new StreamReader(fileset);
                string str;
                //Чтение строки настроек
                str = sr.ReadLine();
                sr.Close();
                //Деление строки пробелами
                //Формат: "имя пароль директория"
                string[] lines = str.Split(' ');
                try
                {
                    _username = lines[0];
                    _password = lines[1];
                    _root = lines[2];
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }

            string line;//принятая команда от клиента
            try
            {
                while (!string.IsNullOrEmpty(line = _controlReader.ReadLine()))
                {
                    try
                    {
                        string[] command = line.Split(' ');
                        //Команда
                        string cmd = command[0].ToUpperInvariant();
                        //Аргумент, если есть
                        string arguments = command.Length > 1 ? line.Substring(command[0].Length + 1) : null;

                        if (string.IsNullOrWhiteSpace(arguments))
                            arguments = null;
                        //Ответ сервера
                        string response = null;

                        string[] splitArgs;
                        Console.WriteLine(cmd + "  " + arguments);
                        if (_userid == 0)//Пока нет авторизованного пользователя
                        {
                            switch (cmd)
                            {
                                case "USER":
                                    response = User(arguments);
                                    break;
                                case "PASS":
                                    response = Password(arguments);
                                    break;
                                default:
                                    response = "530 Not logged in";
                                    break;
                            }
                        }
                        else
                        {
                            switch (cmd)
                            {
                                case "USER":
                                    response = User(arguments);
                                    break;
                                case "PASS":
                                    response = Password(arguments);
                                    break;
                                case "CWD":
                                    response = ChangeWorkingDirectory(arguments);
                                    break;
                                case "CDUP":
                                    response = ChangeWorkingDirectory("..");
                                    break;
                                case "PWD":
                                    response = PrintWorkingDirectory();
                                    break;
                                case "QUIT":
                                    response = Quit();
                                    break;
                                case "TYPE":
                                    splitArgs = arguments.Split(' ');
                                    response = Type(splitArgs[0], splitArgs.Length > 1 ? splitArgs[1] : null);
                                    break;
                                case "PORT":
                                    splitArgs = arguments.Split(' ');
                                    response = Port(splitArgs[1]);
                                    break;
                                case "PASV":
                                    response = Passive();
                                    break;
                                case "LIST":
                                    response = List(arguments);
                                    break;
                                case "RETR":
                                    response = Retrieve(arguments);
                                    break;
                                case "STOR":
                                    response = Store(arguments);
                                    break;
                                case "DELE":
                                    response = Delete(arguments);
                                    break;
                                case "RMD":
                                    response = RemoveDir(arguments);
                                    break;
                                case "MKD":
                                    response = CreateDir(arguments);
                                    break;
                                default:
                                    response = "502 Command not implemented";
                                    break;
                            }
                        }

                        //Вывод команды на консоль
                        Console.WriteLine(response);
                        if (_controlClient == null || !_controlClient.Connected)
                        {
                            //Прекращение цикла, если отсутствует соединение
                            break;
                        }
                        else
                        {
                            //Отправка ответа клиенту
                            _controlWriter.WriteLine(response);
                            _controlWriter.Flush();

                            if (response.StartsWith("221"))
                            {
                                //Если ответ означает конец сессии
                                //закончить цикл
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
               
            }
        }
#region FTP Commands
        //string ClientConnection.Type(string typeCode, string formatControl)
        //Выбор типа передачи данных
        //1ый параметр - тип передачи
        //2ой - формат управления передачи
        private string Type(string typeCode, string formatControl)
        {
            string response = "500 Unknown error";

            switch (typeCode)
            {
                case "A":
                    _transferType = typeCode;
                    response = "200 Type set to A";
                    break;
                case "I":
                    _transferType = typeCode;
                    response = "200 Type set to I";
                    break;
                case "E":
                case "L":
                default:
                    response = "504 Command not implemented for that parameter";
                    break;
            }

            if (formatControl != null)
            {
                switch (formatControl)
                {
                    case "N":
                        response = "200 Format control set to N";
                        break;
                    case "T":
                    case "C":
                    default:
                        response = "504 Command not implemented for that parameter";
                        break;
                }
            }

            return response;
        }

        //string ClientConnection.Port(string hostPort)
        //Включения активного режима передачи
        //1ый параметр - порт клиента
        private string Port(string hostPort)
        {
            string response = "200 Command complete";
            string[] ipAndPort = hostPort.Split(',');
            _dataConnectionType = DataConnectionType.Active;
            byte[] ipAddress = ipAndPort.Take(4).Select(s => Convert.ToByte(s)).ToArray();
            byte[] port = ipAndPort.Skip(4).Select(s => Convert.ToByte(s)).ToArray();

            if (BitConverter.IsLittleEndian)
                Array.Reverse(port);

            _dataEndpoint = new IPEndPoint(new IPAddress(ipAddress), BitConverter.ToInt16(port, 0));

            return response;
        }

        //string ClientConnection.Passive()
        //Включения пассивного режима передачи
        private string Passive()
        {
            _dataConnectionType = DataConnectionType.Passive;
            IPAddress localAddress = ((IPEndPoint)_controlClient.Client.LocalEndPoint).Address;//LocalEndPoint

            string myIp = new WebClient().DownloadString(@"http://icanhazip.com").Trim();
            string[] IP = myIp.Split('.');
            byte[] address = IP.Take(4).Select(s => Convert.ToByte(s)).ToArray();
            IPAddress remoteAddress = new IPAddress(address);
            //byte[] address = localEndpoint.Address.GetAddressBytes();

            if (_passiveListener != null)
            {
                _passiveListener.Server.Close();
                _passiveListener.Stop();
                //_passiveListener.Server.Shutdown(SocketShutdown.Both);
                
                if (_dataClient != null)
                   // _dataClient.Client.Shutdown(SocketShutdown.Both);
                    _dataClient.Close();
            }
            _passiveListener = new TcpListener(localAddress, PortNum());//local
            Console.WriteLine(_passiveListener.LocalEndpoint.ToString());
            _passiveListener.Start();
            
            IPEndPoint localEndpoint = ((IPEndPoint)_passiveListener.LocalEndpoint);
            
            short port = (short)localEndpoint.Port;

            byte[] portArray = BitConverter.GetBytes(port);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(portArray);

            return string.Format("227 Entering Passive Mode ({0},{1},{2},{3},{4},{5})",
                          address[0], address[1], address[2], address[3], portArray[0], portArray[1]);
        }

        //string ClientConnection.List(string pathname)
        //Соединение для передачи списка файлов и папок
        //1ый параметр - путь к директории
        private string List(string pathname)
        {
            if (pathname == null)
            {
                pathname = string.Empty;
            }

            pathname = new DirectoryInfo(Path.Combine(_currentDirectory, pathname)).FullName;
            
            if (IsPathValid(pathname))
            {
                if (_dataConnectionType == DataConnectionType.Active)
                {
                    _dataClient = new TcpClient();
                    _dataClient.BeginConnect(_dataEndpoint.Address, _dataEndpoint.Port, DoList, pathname);
                }
                else
                {
                    _passiveListener.BeginAcceptTcpClient(DoList, pathname);
                }

                return string.Format("150 Open {0} mode data connection ", _dataConnectionType);
            }

            return "450 Requested file action not taken";
        }

        //string ClientConnection.DoList(IAsyncResult result)
        //Передача списка файлов и папок
        //1ый параметр - путь к директории в формате состояния асинхронной передачи
        private void DoList(IAsyncResult result)
        {
            if (_dataConnectionType == DataConnectionType.Active)
            {
                _dataClient.EndConnect(result);
            }
            else
            {
                _dataClient = _passiveListener.EndAcceptTcpClient(result);
                
            }

            string pathname = (string)result.AsyncState;
            StreamReader _dataReader;
            StreamWriter _dataWriter;

            using (NetworkStream dataStream = _dataClient.GetStream())
            {
                _dataReader = new StreamReader(dataStream, Encoding.ASCII);
                _dataWriter = new StreamWriter(dataStream, Encoding.ASCII);

                IEnumerable<string> directories = Directory.EnumerateDirectories(pathname);

                foreach (string dir in directories)
                {
                    DirectoryInfo d = new DirectoryInfo(dir);
                    
                    //если время изменнеия было ранее 180 дней от текущего, то использовать формат даты с годом
                    string date = d.LastWriteTime < DateTime.Now - TimeSpan.FromDays(180) ?
                        d.LastWriteTime.ToString("MM dd yyyy") :
                        d.LastWriteTime.ToString("MM dd HH:mm");

                    string line = string.Format("drwxr-xr-x 2 2003 2003 {0,8} {1} {2}", "4096", date, d.Name);

                    _dataWriter.WriteLine(line);
                    _dataWriter.Flush();
                }

                IEnumerable<string> files = Directory.EnumerateFiles(pathname);

                foreach (string file in files)
                {
                    FileInfo f = new FileInfo(file);

                    string date = f.LastWriteTime < DateTime.Now - TimeSpan.FromDays(180) ?
                        f.LastWriteTime.ToString("MM dd yyyy") :
                        f.LastWriteTime.ToString("MM dd HH:mm");

                    string line = string.Format("-rw-r--r-- 2 2003 2003 {0,8} {1} {2}", f.Length, date, f.Name);

                    _dataWriter.WriteLine(line);
                    _dataWriter.Flush();
                }

                _dataClient.Close();
                _dataClient = null;

                Console.WriteLine("226 Closing data connection. Requested action successful");
                _controlWriter.WriteLine("226 Closing data connection. Requested action successful");
                _controlWriter.Flush();
            }
        }

        //string ClientConnection.User(string username)
        //Опредление имени пользователя
        //1ый параметр - имя пользователя
        private string User(string username)
        {
            if (username == _username)
            {
                _usertrue = true;
                return "331 User name okay, need password";
            }
            else
            {
                _usertrue = false;
                return "530 Not logged in";
            }

            
        }

        //string ClientConnection.Password(string password)
        //Опредление пароля и верной аутентификации
        //1ый параметр - пароль пользователя
        private string Password(string password)
        {
            if ((_usertrue)&&(password == _password))
            {
                _currentDirectory = _root;
                _userid = 1;
                return "230 User logged in, proceed";
            }
            else
            {
                _userid = 0;
                return "530 Not logged in";
            }
        }

        //string ClientConnection.ChangeWorkingDirectory(string pathname)
        //Смена текущей директории
        //1ый параметр - название необходимой директории
        private string ChangeWorkingDirectory(string pathname)
        {
            if (pathname == "/")
            {
                _currentDirectory = _root;
            }
            else
            {
                string newDir;

                if (pathname.StartsWith("/"))
                {
                    pathname = pathname.Replace('/', '\\');
                    newDir = Path.Combine(_root, pathname);
                }
                else
                {
                    pathname = pathname.Replace('/', '\\');
                    newDir = Path.Combine(_currentDirectory, pathname);
                }

                if (Directory.Exists(newDir))
                {
                    _currentDirectory = new DirectoryInfo(newDir).FullName;

                    if (!IsPathValid(_currentDirectory))
                    {
                        _currentDirectory = _root;
                    }
                }
                else
                {
                    _currentDirectory = _root;
                }
            }

            return "250 Requested file action okay, completed";
        }

        //string ClientConnection.PrintWorkingDirectory()
        //Вывод имени текущей рабочей директории
        private string PrintWorkingDirectory()
        {
            string current = _currentDirectory.Replace(_root, "/").Replace('\\', '/');

            if (current.Length == 0)
            {
                current = "/";
            }

            return string.Format("257 \"{0}\" open", current); ;
        }

        //string ClientConnection.Retrieve(string pathname)
        //Соединение для передачи файла
        //1ый параметр - имя файла
        private string Retrieve(string pathname)
        {
            pathname = NormalizeFilename(pathname);
            if (IsPathValid(pathname))
            {
                if (File.Exists(pathname))
                {
                    if (_dataConnectionType == DataConnectionType.Active)
                    {
                        _dataClient = new TcpClient();
                        _dataClient.BeginConnect(_dataEndpoint.Address, _dataEndpoint.Port, DoRetrieve, pathname);
                    }
                    else
                    {
                        _passiveListener.BeginAcceptTcpClient(DoRetrieve, pathname);
                    }

                    return string.Format("150 Open {0} mode data connection", _dataConnectionType);
                }
            }

            return "550 Requested action not taken. File unavailable";
        }

        //string ClientConnection.DoRetrieve(IAsyncResult result)
        //Передача файла
        //1ый параметр - путь к файлу в формате состояния асинхронной передачи
        private void DoRetrieve(IAsyncResult result)
        {
            if (_dataConnectionType == DataConnectionType.Active)
            {
                _dataClient.EndConnect(result);
            }
            else
            {
                _dataClient = _passiveListener.EndAcceptTcpClient(result);
            }

            string pathname = (string)result.AsyncState;

            using (NetworkStream dataStream = _dataClient.GetStream())
            {
                using (FileStream fs = new FileStream(pathname, FileMode.Open, FileAccess.Read))
                {
                    CopyStream(fs, dataStream);
                    _dataClient.Close();
                    _dataClient = null;
                    Console.WriteLine("226 Closing data connection. Requested action successful");
                    _controlWriter.WriteLine("226 Closing data connection. Requested action successful");
                    _controlWriter.Flush();
                }
            }
        }

        //string ClientConnection.Store(string pathname)
        //Принятие файла
        //1ый параметр - имя файла
        private string Store(string pathname)
        {
            pathname = NormalizeFilename(pathname);
            if (IsPathValid(pathname))
            {
                    if (_dataConnectionType == DataConnectionType.Active)
                    {
                        _dataClient = new TcpClient();
                        _dataClient.BeginConnect(_dataEndpoint.Address, _dataEndpoint.Port, DoStore, pathname);
                    }
                    else
                    {
                        _passiveListener.BeginAcceptTcpClient(DoStore, pathname);
                    }

                    return string.Format("150 Open {0} mode data connection", _dataConnectionType);
             }
            return "450 Requested file action not taken.";
        }

        //string ClientConnection.DoStore(IAsyncResult result)
        //Принятие файла
        //1ый параметр - путь к файлу в формате состояния асинхронной передачи
        private void DoStore(IAsyncResult result)
        {
            if (_dataConnectionType == DataConnectionType.Active)
            {
                _dataClient.EndConnect(result);
            }
            else
            {
                _dataClient = _passiveListener.EndAcceptTcpClient(result);
            }

            string pathname = (string)result.AsyncState;
            using (NetworkStream dataStream = _dataClient.GetStream())
            {
                using (FileStream fs = new FileStream(pathname, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 4096, FileOptions.SequentialScan))
                {
                    CopyStream(dataStream, fs);
                    _dataClient.Close();
                    _dataClient = null;
                    Console.WriteLine("226 Closing data connection. Requested action successful");
                    _controlWriter.WriteLine("226 Closing data connection. Requested action successful");
                    _controlWriter.Flush();
                }
            } 
        }

        //string ClientConnection.Delete(string pathname)
        //Удаление файла
        //1ый параметр - имя файла
        private string Delete(string pathname)
        {
            pathname = NormalizeFilename(pathname);

            if (pathname != null)
            {
                if (File.Exists(pathname))
                {
                    File.Delete(pathname);
                }
                else
                {
                    return "550 Requested action not taken. File unavailable";
                }

                return "250 Requested file action okay, completed";
            }

            return "550 Requested action not taken. File unavailable";
        }

        //string ClientConnection.RemoveDir(string pathname)
        //Удаление директории
        //1ый параметр - имя директории
        private string RemoveDir(string pathname)
        {
            pathname = NormalizeFilename(pathname);

            if (pathname != null)
            {
                if (Directory.Exists(pathname))
                {
                    Directory.Delete(pathname);
                }
                else
                {
                    return "550 Requested action not taken. File unavailable";
                }

                return "250 Requested file action okay, completed";
            }

            return "550 Requested action not taken. File unavailable";
        }

        //string ClientConnection.CreateDir(string pathname)
        //Создание директории
        //1ый параметр - имя директории
        private string CreateDir(string pathname)
        {
            pathname = NormalizeFilename(pathname);

            if (pathname != null)
            {
                if (!Directory.Exists(pathname))
                {
                    Directory.CreateDirectory(pathname);
                }
                else
                {
                    return "550 Requested action not taken. File already exist";
                }

                return "250 Requested file action okay, completed";
            }

            return "550 Requested action not taken. File not found";
        }

        //string ClientConnection.Quit()
        //Завершение сессии
        private string Quit()
        {
            _userid = 0;
            if (_dataClient!=null)
                _dataClient.Close();
            return "221 Service closing control connection";
        }

#endregion

        //string ClientConnection.IsPathValid(string path)
        //Проверка правильности пути
        //1ый параметр - путь
        private bool IsPathValid(string path)
        {
            return path.StartsWith(_root, StringComparison.OrdinalIgnoreCase);
        }

        //string ClientConnection.NormalizeFilename(string path)
        //Нормализация пути - исправления символов, дополнение до полного пути
        //1ый параметр - путь
        private string NormalizeFilename(string path)
        {
            if (path == null)
            {
                path = string.Empty;
            }

            if (path == "/")
            {
                return _root;
            }
            else if (path.StartsWith("/"))
            {
                path = new FileInfo(Path.Combine(_root, path.Substring(1))).FullName;
            }
            else
            {
                path = new FileInfo(Path.Combine(_currentDirectory, path)).FullName;
            }

            return IsPathValid(path) ? path : null;
        }

        //string ClientConnection.CopyStream(Stream input, Stream output, int bufferSize)
        //Копирование потоков бинарного типа передачи
        //1ый параметр - входной поток
        //2ый параметр - выходной поток
        //3ый параметр - размер байтового массива для буфера
        private static long CopyStream(Stream input, Stream output, int bufferSize)
        {
            byte[] buffer = new byte[bufferSize];
            int count = 0;
            long total = 0;

            while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, count);
                total += count;
            }

            return total;
        }

        //string ClientConnection.CopyStreamAscii(Stream input, Stream output, int bufferSize)
        //Копирование потоков символьного типа передачи
        //1ый параметр - входной поток
        //2ый параметр - выходной поток
        //3ый параметр - размер байтового массива для буфера
        private static long CopyStreamAscii(Stream input, Stream output, int bufferSize)
        {
            char[] buffer = new char[bufferSize];
            int count = 0;
            long total = 0;

            using (StreamReader rdr = new StreamReader(input))
            {
                using (StreamWriter wtr = new StreamWriter(output, Encoding.ASCII))
                {
                    while ((count = rdr.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        wtr.Write(buffer, 0, count);
                        total += count;
                    }
                }
            }

            return total;
        }

        //string ClientConnection.CopyStream(Stream input, Stream output)
        //Выбор метода копирования потока
        //1ый параметр - входной поток
        //2ый параметр - выходной поток
        private long CopyStream(Stream input, Stream output)
        {
            if (_transferType == "I")
            {
                return CopyStream(input, output, 4096);
            }
            else
            {
                return CopyStreamAscii(input, output, 4096);
            }
        }

        //string ClientConnection.PortNum()
        //Выбор случайного порта для принятия входных данных
        private int PortNum()
        {
            int[] port = {1024,1025,1026,1027,1028};
            Random rnd = new Random();
            int index = rnd.Next(0,4);
            return port[index];
        }


    
    }
}
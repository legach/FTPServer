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
    public class ClientConnection
    {


        private enum TransferType
        {
            Ascii,
            Ebcdic,
            Image,
            Local,
        }

        private enum FormatControlType
        {
            NonPrint,
            Telnet,
            CarriageControl,
        }

        private enum DataConnectionType
        {
            Passive,
            Active,
        }

        private enum FileStructureType
        {
            File,
            Record,
            Page,
        }
        
        
        private TcpClient _controlClient;
        private TcpClient _dataClient;
        private NetworkStream _controlStream;
        private StreamReader _controlReader;
        private StreamWriter _controlWriter;
        private TcpListener _passiveListener;
        private string _username;
        private string _password;
        private string _root = "D:\\";
        private string _currentDirectory;// = string.Empty;
        private string _transferType;
        private TransferType _connectionType = TransferType.Ascii;
        private FormatControlType _formatControlType = FormatControlType.NonPrint;
        private DataConnectionType _dataConnectionType = DataConnectionType.Active;
        private FileStructureType _fileStructureType = FileStructureType.File;
        private IPEndPoint _dataEndpoint;
        private X509Certificate _cert = null;
        private SslStream _sslStream;

        public ClientConnection(TcpClient client)
        {
            _controlClient = client;

            _controlStream = _controlClient.GetStream();

            _controlReader = new StreamReader(_controlStream);
            _controlWriter = new StreamWriter(_controlStream);
        }

        public void HandleClient(object obj)
        {
            _controlWriter.WriteLine("220 Service Ready.");
            _controlWriter.Flush();

            string line;

            try
            {
                while (!string.IsNullOrEmpty(line = _controlReader.ReadLine()))
                {
                    string response = null;

                    string[] command = line.Split(' ');

                    string cmd = command[0].ToUpperInvariant();
                    string arguments = command.Length > 1 ? line.Substring(command[0].Length + 1) : null;

                    if (string.IsNullOrWhiteSpace(arguments))
                        arguments = null;

                    if (response == null)
                    {
                        string[] splitArgs;
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
                                response = "221 Service closing control connection";
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

                    if (_controlClient == null || !_controlClient.Connected)
                    {
                        break;
                    }
                    else
                    {
                        _controlWriter.WriteLine(response);
                        _controlWriter.Flush();

                        if (response.StartsWith("221"))
                        {
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
               
            }
        }
#region FTP Commands
        private string Type(string typeCode, string formatControl) //Больше почитать про типы
        {
            string response = "500 Unknown error";

            switch (typeCode)
            {
                case "A":
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

        private string Passive()
        {
            _dataConnectionType = DataConnectionType.Passive;
            IPAddress localAddress = ((IPEndPoint)_controlClient.Client.LocalEndPoint).Address;

            _passiveListener = new TcpListener(localAddress, 0);
            _passiveListener.Start();

            IPEndPoint localEndpoint = ((IPEndPoint)_passiveListener.LocalEndpoint);

            byte[] address = localEndpoint.Address.GetAddressBytes();
            short port = (short)localEndpoint.Port;

            byte[] portArray = BitConverter.GetBytes(port);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(portArray);

            return string.Format("227 Entering Passive Mode ({0},{1},{2},{3},{4},{5})",
                          address[0], address[1], address[2], address[3], portArray[0], portArray[1]);
        }

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

                return string.Format("150 Open {0} mode data connection", _dataConnectionType);
            }

            return "450 Requested file action not taken";
        }

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

                    string line = string.Format("drwxr-xr-x 2 2003 2003 {0,8}       {1}       {2}", "4096", date, d.Name);

                    _dataWriter.WriteLine(line);
                    _dataWriter.Flush();
                }

                IEnumerable<string> files = Directory.EnumerateFiles(pathname);

                foreach (string file in files)
                {
                    FileInfo f = new FileInfo(file);

                    string date = f.LastWriteTime < DateTime.Now - TimeSpan.FromDays(180) ?
                        f.LastWriteTime.ToString("MM dd  yyyy") :
                        f.LastWriteTime.ToString("MM dd HH:mm");

                    string line = string.Format("-rw-r--r-- 2 2003 2003 {0,8}   {1} {2}", f.Length, date, f.Name);

                    _dataWriter.WriteLine(line);
                    _dataWriter.Flush();
                }

                _dataClient.Close();
                _dataClient = null;

                _controlWriter.WriteLine("226 Closing data connection. Requested action successful");
                _controlWriter.Flush();
            }
        }

        private string User(string username)
        {
            _username = username;

            return "331 User name okay, need password";
        }

        private string Password(string password)
        {
            if (true)
            {
                _currentDirectory = _root;
                return "230 User logged in, proceed";
                
            }
            else
            {
                return "530 Not logged in";
            }
        }

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

        private string PrintWorkingDirectory()
        {
            string current = _currentDirectory.Replace(_root, "/").Replace('\\', '/');

            if (current.Length == 0)
            {
                current = "/";
            }

            return string.Format("257 \"{0}\" open", current); ;
        }

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
                    _controlWriter.WriteLine("226 Closing data connection. Requested action successful");
                    _controlWriter.Flush();
                }
            }
        }

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
                    _controlWriter.WriteLine("226 Closing data connection. Requested action successful");
                    _controlWriter.Flush();
                }
            } 
        }

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
#endregion

        private bool IsPathValid(string path)
        {
            return path.StartsWith(_root, StringComparison.OrdinalIgnoreCase);
        }

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
    }
}
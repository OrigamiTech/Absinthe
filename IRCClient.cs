using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace Absinthe
{
    public partial class IRCClient
    {
        #region Private Properties
        private string _Server;
        private ushort _Port;
        private string _Username;
        private string _RealName;
        private bool _AutoPong;
        private bool _Invisible;
        #endregion
        #region Public Properties
        public string Server
        {
            get { return _Server; }
            set
            {
                if(irc.Connected)
                    throw new Exception("Cannot change server while connected.");
                _Server = value;
            }
        }
        public ushort Port
        {
            get { return _Port; }
            set
            {
                if(irc.Connected)
                    throw new Exception("Cannot change port while connected.");
                _Port = value;
            }
        }
        public string Username
        {
            get { return _Username; }
            set
            {
                if(irc.Connected)
                    throw new Exception("Cannot change username while connected.");
                _Username = value;
            }
        }
        public string RealName
        {
            get { return _RealName; }
            set
            {
                if(irc.Connected)
                    throw new Exception("Cannot change real name while connected.");
                _RealName = value;
            }
        }
        public bool AutoPong
        {
            get { return _Invisible; }
            set { _AutoPong = value; }
        }
        public bool Invisible
        {
            get { return _Invisible; }
            set
            {
                if(irc.Connected)
                    throw new Exception("Cannot change invisible status while connected.");
                _Invisible = value;
            }
        }
        #endregion
        #region Private Variables
        private static NetworkStream stream;
        private static TcpClient irc;
        private static StreamReader reader;
        private static StreamWriter writer;
        private Thread pingThread;
        #endregion
        #region Constructors
        public IRCClient(string server, ushort port, string username)
        { Init(server, port, username, "", true, false); }
        public IRCClient(string server, ushort port, string username, string realname)
        { Init(server, port, username, realname, true, false); }
        public IRCClient(string server, ushort port, string username, string realname, bool autopong)
        { Init(server, port, username, realname, autopong, false); }
        public IRCClient(string server, ushort port, string username, string realname, bool autopong, bool invisible)
        { Init(server, port, username, realname, autopong, invisible); }
        #endregion
        #region Public Functions
        public void Connect()
        {
            irc = new TcpClient(_Server, _Port);
            stream = irc.GetStream();
            reader = new StreamReader(stream);
            writer = new StreamWriter(stream) { AutoFlush = true };
            pingThread = new Thread(new ThreadStart(TryPing));
            pingThread.Start();
        }
        public void Handshake()
        {
            USER(_Username, _Invisible, _RealName);
            NICK(_Username);
            Run();
        }
        public void Disconnect()
        {
            pingThread.Abort();
            irc.Close();
        }
        #endregion
        #region Private Functions
        private void Init(string server, ushort port, string username, string realname, bool autopong, bool invisible)
        {
            _Server = server;
            _Port = port;
            _Username = username;
            _RealName = realname;
            _AutoPong = autopong;
            _Invisible = invisible;
        }
        private void TryPing()
        {
            while(true)
            {
                Thread.Sleep(60000);
                if(irc.Connected)
                    PING(_Server);
            }
        }
        private void ParseCommand(string inputLine, out string prefix, out string command, out string[] parameters)
        {
            prefix = "";
            command = "";
            List<string> param = new List<string>();
            bool isOnPrefix = true;
            bool isOnCommand = false;
            int currentParam = -1;
            bool currentParamIsTail = false;
            for(int c = 0; c < inputLine.Length; c++)
            {
                if(c == 0)
                {
                    isOnPrefix = (inputLine[c] == ':');
                    isOnCommand = !isOnPrefix;
                }
                if(isOnPrefix && c != 0)
                {
                    if(inputLine[c] == ' ')
                    {
                        isOnPrefix = false;
                        isOnCommand = true;
                    }
                    else
                        prefix += inputLine[c];
                }
                else if(isOnCommand)
                {
                    if(inputLine[c] == ' ')
                        isOnCommand = false;
                    else
                        command += inputLine[c];
                }
                else if(currentParam != -1)
                {
                    if(inputLine[c] == ' ' && !currentParamIsTail)
                    {
                        currentParam++;
                        param.Add("");
                        if(currentParam == 14)
                            currentParamIsTail = true;
                    }
                    else if(inputLine[c] == ':' && inputLine[c - 1] == ' ' && !currentParamIsTail)
                        currentParamIsTail = true;
                    else
                        param[currentParam] += inputLine[c];
                }
                if(!isOnPrefix && !isOnCommand && currentParam == -1)
                {
                    currentParam = 0;
                    param.Add("");
                }
            }
            parameters = param.ToArray();
        }
        private void Run()
        {
            try
            {
                string inputLine;
                while((inputLine = reader.ReadLine()) != null)
                {
                    /*Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine(inputLine);*/

                    string PREFIX;
                    string COMMAND;
                    string[] PARAMETERS;
                    ParseCommand(inputLine, out PREFIX, out COMMAND, out PARAMETERS);

                    // this is just debug code so I know what's happening
                    Console.ForegroundColor = ConsoleColor.Red;
                    if(PREFIX != "")
                        Console.Write(PREFIX + ' ');
                    Console.ForegroundColor = ConsoleColor.Green;
                    try { Console.Write(((CommandCode.Number)int.Parse(COMMAND)).ToString() + ' '); }
                    catch { Console.Write(COMMAND + ' '); }
                    Console.ForegroundColor = ConsoleColor.Blue;
                    for(int i = 0; i < PARAMETERS.Length; i++)
                        Console.Write(PARAMETERS[i] + ' ');
                    Console.WriteLine();


                    switch(COMMAND)
                    {
                        case CommandCode.PING:
                            if(_AutoPong)
                                PONG(PARAMETERS[0]);
                            break;
                        case CommandCode.PRIVMSG:
                            OnGET_PRIVMSG(EventArgs.Empty); // need to actually do proper EventArgs (PrivmsgEventArgs, etc.)
                            break;
                    }
                    if(Regex.Match(COMMAND, "^\\d+?$").Success)
                    {
                        switch((CommandCode.Number)int.Parse(COMMAND))
                        {
                            case CommandCode.Number.RPL_MOTDSTART:
                                OnRPL_MOTDSTART(EventArgs.Empty);
                                break;
                            case CommandCode.Number.RPL_MOTD:
                                OnRPL_MOTD(EventArgs.Empty);
                                break;
                            case CommandCode.Number.RPL_ENDOFMOTD:
                                OnRPL_ENDOFMOTD(EventArgs.Empty);
                                break;
                        }
                    }
                }
            }
            catch(Exception ex) { Console.WriteLine(ex.ToString()); Console.ReadLine(); }
        }
        #endregion
    }
}
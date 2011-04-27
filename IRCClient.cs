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
    public class IRCClient
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
        #region Public Command Functions
        public void JOIN(string channel)
        {
            JOIN(new Dictionary<string, string>() { { channel, "" } }, false);
        }
        public void JOIN(string channel, string key)
        {
            JOIN(new Dictionary<string, string>() { { channel, key } }, false);
        }
        public void JOIN(Dictionary<string, string> channels, bool zero)
        {
            if(zero)
            {
                writer.WriteLine(CommandCode.JOIN + " 0");
                return;
            }
            string channel = "";
            string key = "";
            bool first = true;
            foreach(KeyValuePair<string, string> pair in channels)
            {
                channel += (first ? "" : ",") + pair.Key;
                key += (first ? "" : ",") + pair.Value;
                first = false;
            }
            writer.WriteLine(CommandCode.JOIN + " " + channel + " " + key);
        }
        public void NICK(string nickname)
        {
            writer.WriteLine(CommandCode.NICK + " " + nickname);
        }
        public void PING(string param)
        {
            writer.WriteLine(CommandCode.PING + (param != "" ? " :" + param : ""));
        }
        public void PONG(string param)
        {
            writer.WriteLine(CommandCode.PONG + (param != "" ? " :" + param : ""));
        }
        public void PRIVMSG(string[] receivers, string message)
        {
            if(receivers.Length == 0)
                return;
            string receiver = receivers[0];
            for(int i = 1; i < receivers.Length; i++)
                receiver += "," + receivers[i];
            writer.WriteLine(CommandCode.PRIVMSG + " " + receiver + " :" + message);
        }
        public void USER(string username, bool invisible, string realname)
        {
            writer.WriteLine(CommandCode.USER + " " + username + " " + (invisible ? "8" : "0") + " * :" + realname);
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
        #region Event Handlers
        public delegate void EventHandler_RPL_MOTDSTART(object sender, EventArgs e);
        public delegate void EventHandler_RPL_MOTD(object sender, EventArgs e);
        public delegate void EventHandler_RPL_ENDOFMOTD(object sender, EventArgs e);
        public event EventHandler_RPL_ENDOFMOTD RPL_MOTDSTART;
        public event EventHandler_RPL_ENDOFMOTD RPL_MOTD;
        public event EventHandler_RPL_ENDOFMOTD RPL_ENDOFMOTD;
        protected virtual void OnRPL_MOTDSTART(EventArgs e) { if(RPL_MOTDSTART != null) RPL_MOTDSTART(this, e); }
        protected virtual void OnRPL_MOTD(EventArgs e) { if(RPL_MOTD != null) RPL_MOTD(this, e); }
        protected virtual void OnRPL_ENDOFMOTD(EventArgs e) { if(RPL_ENDOFMOTD != null) RPL_ENDOFMOTD(this, e); }
        #endregion
        internal static class CommandCode
        {
            public const string
                JOIN = "JOIN",
                NICK = "NICK",
                PING = "PING",
                PONG = "PONG",
                PRIVMSG = "PRIVMSG",
                QUIT = "QUIT",
                USER = "USER";
            public enum Number
            {
                RPL_WELCOME = 001,
                RPL_YOURHOST = 002,
                RPL_CREATED = 003,
                RPL_MYINFO = 004,
                RPL_BOUNCE = 005,
                RPL_USERHOST = 302,
                RPL_ISON = 303,
                RPL_AWAY = 301,
                RPL_UNAWAY = 305,
                RPL_NOWAWAY = 306,
                RPL_WHOISUSER = 311,
                RPL_WHOISSERVER = 312,
                RPL_WHOISOPERATOR = 313,
                RPL_WHOISIDLE = 317,
                RPL_ENDOFWHOIS = 318,
                RPL_WHOISCHANNELS = 319,
                RPL_WHOWASUSER = 314,
                RPL_ENDOFWHOWAS = 369,
                RPL_LISTSTART = 321,
                RPL_LIST = 322,
                RPL_LISTEND = 323,
                RPL_UNIQOPIS = 325,
                RPL_CHANNELMODEIS = 324,
                RPL_NOTOPIC = 331,
                RPL_TOPIC = 332,
                RPL_INVITING = 341,
                RPL_SUMMONING = 342,
                RPL_INVITELIST = 346,
                RPL_ENDOFINVITELIST = 347,
                RPL_EXCEPTLIST = 348,
                RPL_ENDOFEXCEPTLIST = 349,
                RPL_VERSION = 351,
                RPL_WHOREPLY = 352,
                RPL_ENDOFWHO = 315,
                RPL_NAMREPLY = 353,
                RPL_ENDOFNAMES = 366,
                RPL_LINKS = 364,
                RPL_ENDOFLINKS = 365,
                RPL_BANLIST = 367,
                RPL_ENDOFBANLIST = 368,
                RPL_INFO = 371,
                RPL_ENDOFINFO = 374,
                RPL_MOTDSTART = 375,
                RPL_MOTD = 372,
                RPL_ENDOFMOTD = 376,
                RPL_YOUREOPER = 381,
                RPL_REHASHING = 382,
                RPL_YOURESERVICE = 383,
                RPL_TIME = 391,
                RPL_USERSSTART = 392,
                RPL_USERS = 393,
                RPL_ENDOFUSERS = 394,
                RPL_NOUSERS = 395,
                RPL_TRACELINK = 200,
                RPL_TRACECONNECTING = 201,
                RPL_TRACEHANDSHAKE = 202,
                RPL_TRACEUNKNOWN = 203,
                RPL_TRACEOPERATOR = 204,
                RPL_TRACEUSER = 205,
                RPL_TRACESERVER = 206,
                RPL_TRACESERVICE = 207,
                RPL_TRACENEWTYPE = 208,
                RPL_TRACECLASS = 209,
                RPL_TRACERECONNECT = 210,
                RPL_TRACELOG = 261,
                RPL_TRACEEND = 262,
                RPL_STATSLINKINFO = 211,
                RPL_STATSCOMMANDS = 212,
                RPL_ENDOFSTATS = 219,
                RPL_STATSUPTIME = 242,
                RPL_STATSOLINE = 243,
                RPL_UMODEIS = 221,
                RPL_SERVLIST = 234,
                RPL_SERVLISTEND = 235,
                RPL_LUSERCLIENT = 251,
                RPL_LUSEROP = 252,
                RPL_LUSERUNKNOWN = 253,
                RPL_LUSERCHANNELS = 254,
                RPL_LUSERME = 255,
                RPL_ADMINME = 256,
                RPL_ADMINLOC1 = 257,
                RPL_ADMINLOC2 = 258,
                RPL_ADMINEMAIL = 259,
                RPL_TRYAGAIN = 263,
                ERR_NOSUCHNICK = 401,
                ERR_NOSUCHSERVER = 402,
                ERR_NOSUCHCHANNEL = 403,
                ERR_CANNOTSENDTOCHAN = 404,
                ERR_TOOMANYCHANNELS = 405,
                ERR_WASNOSUCHNICK = 406,
                ERR_TOOMANYTARGETS = 407,
                ERR_NOSUCHSERVICE = 408,
                ERR_NOORIGIN = 409,
                ERR_NORECIPIENT = 411,
                ERR_NOTEXTTOSEND = 412,
                ERR_NOTOPLEVEL = 413,
                ERR_WILDTOPLEVEL = 414,
                ERR_BADMASK = 415,
                ERR_UNKNOWNCOMMAND = 421,
                ERR_NOMOTD = 422,
                ERR_NOADMININFO = 423,
                ERR_FILEERROR = 424,
                ERR_NONICKNAMEGIVEN = 431,
                ERR_ERRONEUSNICKNAME = 432,
                ERR_NICKNAMEINUSE = 433,
                ERR_NICKCOLLISION = 436,
                ERR_UNAVAILRESOURCE = 437,
                ERR_USERNOTINCHANNEL = 441,
                ERR_NOTONCHANNEL = 442,
                ERR_USERONCHANNEL = 443,
                ERR_NOLOGIN = 444,
                ERR_SUMMONDISABLED = 445,
                ERR_USERSDISABLED = 446,
                ERR_NOTREGISTERED = 451,
                ERR_NEEDMOREPARAMS = 461,
                ERR_ALREADYREGISTRED = 462,
                ERR_NOPERMFORHOST = 463,
                ERR_PASSWDMISMATCH = 464,
                ERR_YOUREBANNEDCREEP = 465,
                ERR_YOUWILLBEBANNED = 466,
                ERR_KEYSET = 467,
                ERR_CHANNELISFULL = 471,
                ERR_UNKNOWNMODE = 472,
                ERR_INVITEONLYCHAN = 473,
                ERR_BANNEDFROMCHAN = 474,
                ERR_BADCHANNELKEY = 475,
                ERR_BADCHANMASK = 476,
                ERR_NOCHANMODES = 477,
                ERR_BANLISTFULL = 478,
                ERR_NOPRIVILEGES = 481,
                ERR_CHANOPRIVSNEEDED = 482,
                ERR_CANTKILLSERVER = 483,
                ERR_RESTRICTED = 484,
                ERR_UNIQOPPRIVSNEEDED = 485,
                ERR_NOOPERHOST = 491,
                ERR_UMODEUNKNOWNFLAG = 501,
                ERR_USERSDONTMATCH = 502,
                RPL_SERVICEINFO = 231,
                RPL_ENDOFSERVICES = 232,
                RPL_SERVICE = 233,
                RPL_NONE = 300,
                RPL_WHOISCHANOP = 316,
                RPL_KILLDONE = 361,
                RPL_CLOSING = 362,
                RPL_CLOSEEND = 363,
                RPL_INFOSTART = 373,
                RPL_MYPORTIS = 384,
                RPL_STATSCLINE = 213,
                RPL_STATSNLINE = 214,
                RPL_STATSILINE = 215,
                RPL_STATSKLINE = 216,
                RPL_STATSQLINE = 217,
                RPL_STATSYLINE = 218,
                RPL_STATSVLINE = 240,
                RPL_STATSLLINE = 241,
                RPL_STATSHLINE = 244,
                RPL_STATSSLINE = 244,
                RPL_STATSPING = 246,
                RPL_STATSBLINE = 247,
                RPL_STATSDLINE = 250,
                ERR_NOSERVICEHOST = 492
            }
        }
    }
}
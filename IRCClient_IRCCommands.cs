using System;
using System.Collections.Generic;

namespace Absinthe
{
    public partial class IRCClient
    {
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
    }
}
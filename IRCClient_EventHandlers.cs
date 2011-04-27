using System;

namespace Absinthe
{
    public partial class IRCClient
    {
        public event EventHandler_GET_PRIVMSG GET_PRIVMSG;
        public event EventHandler_RPL_ENDOFMOTD RPL_MOTDSTART;
        public event EventHandler_RPL_ENDOFMOTD RPL_MOTD;
        public event EventHandler_RPL_ENDOFMOTD RPL_ENDOFMOTD;
        public delegate void EventHandler_GET_PRIVMSG(object sender, EventArgs e);
        public delegate void EventHandler_RPL_MOTDSTART(object sender, EventArgs e);
        public delegate void EventHandler_RPL_MOTD(object sender, EventArgs e);
        public delegate void EventHandler_RPL_ENDOFMOTD(object sender, EventArgs e);
        protected virtual void OnGET_PRIVMSG(EventArgs e) { if(GET_PRIVMSG != null)GET_PRIVMSG(this, e); }
        protected virtual void OnRPL_MOTDSTART(EventArgs e) { if(RPL_MOTDSTART != null) RPL_MOTDSTART(this, e); }
        protected virtual void OnRPL_MOTD(EventArgs e) { if(RPL_MOTD != null) RPL_MOTD(this, e); }
        protected virtual void OnRPL_ENDOFMOTD(EventArgs e) { if(RPL_ENDOFMOTD != null) RPL_ENDOFMOTD(this, e); }
    }
}
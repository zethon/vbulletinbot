using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace vbotserver
{
    public delegate void OnConnectHandler(Connection conn);

    public abstract class Connection
    {
        private string _strNewLine = "<br>";
        public string NewLine
        {
            get { return _strNewLine; }
            set { _strNewLine = value; }
        }   

        private string _strAlias = string.Empty;
        public string Alias
        {
            get { return _strAlias; }
            set { _strAlias = value; }
        }
       
        public event OnConnectHandler OnConnect;
        
        abstract public void Connect();
        protected void doOnConnectEvent()
        {
            if (OnConnect != null)
            {
                OnConnect(this);
            }
        }

        public delegate void OnSendMessageHandler(Connection conn, InstantMessage im);
        public event OnSendMessageHandler OnSendMessage;

        abstract public void SendMessage(InstantMessage im);
        protected void doSendMessageEvent(InstantMessage im)
        {
            if (OnSendMessage != null)
            {
                OnSendMessage(this, im);
            }
        }

        public delegate void OnMessageHandler(Connection conn, InstantMessage im);
        public event OnMessageHandler OnMessage;

        protected void doOnMessageEvent(InstantMessage im)
        {
            if (OnMessage != null)
            {
                OnMessage(this, im);
            }
        }

        public delegate void OnDisconnectHandler(Connection conn);
        public event OnDisconnectHandler OnDisconnect;

        abstract public void Disconnect();
        protected void doOnDisconnectEvent()
        {
            if (OnDisconnect != null)
            {
                OnDisconnect(this);
            }
        }

        
    }
}

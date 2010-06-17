using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;

namespace vbotserver
{
    public class ResponseChannel
    {
        static ILog log = LogManager.GetLogger(typeof(ResponseChannel));

        private string _strServiceAlias = string.Empty;
        public string ServiceAlias
        {
            get { return _strServiceAlias; }
        }

        private string _strScreenName = string.Empty;
        public string ScreenName
        {
            get { return _strScreenName; }
        }

        //private Connection _connection = null;
        //public Connection IMConnection
        //{
        //    get { return _connection; }
        //}

        public Connection Connection = null;
        public string ToName = string.Empty;

        public ResponseChannel(string strName, string strService, Connection conn)
        {
            _strScreenName = strName;
            _strServiceAlias = strService;
            Connection = conn;
        }

        public void SendMessage(string Message)
        {
            if (Connection != null && ToName != string.Empty)
            {
                Connection.SendMessage(new InstantMessage(ToName, Message));
            }
            else if (Connection == null)
            {
                log.Warn("SendMessage() failed because Connection object is null");
            }
            else if (ToName == string.Empty)
            {
                log.Warn("SendMessage() failed because ToName is empty");
            }
        }
    }
}

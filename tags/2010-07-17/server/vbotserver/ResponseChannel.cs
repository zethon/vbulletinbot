using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;

namespace VBulletinBot
{
    public class ResponseChannel
    {
        static ILog log = LogManager.GetLogger(typeof(ResponseChannel));

        public Connection Connection = null;
        public string ToName = string.Empty;

        public string NewLine
        {
            get { return Connection.NewLine; }
        }

        public ResponseChannel()
        {
        }

        public ResponseChannel(string strName, Connection conn)
        {
            ToName = strName;
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
                log.Info("SendMessage() failed because ToName is empty");
            }
        }
    }
}

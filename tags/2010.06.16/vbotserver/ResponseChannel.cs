using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace vbotserver
{
    class ResponseChannel
    {
        public Connection Connection;
        public string ToName;

        public ResponseChannel()
        {
        }

        public void SendMessage(string strMessage)
        {
            Connection.SendMessage(new InstantMessage(ToName, strMessage));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Reflection;
using log4net;

namespace VBulletinBot
{
    class ConnectionComposite
    {
        static ILog log = LogManager.GetLogger(typeof(ConnectionComposite));



        private List<Connection> _connections = null;
        public List<Connection> Connections
        {
            get { return _connections; }
        }

        public ConnectionComposite()
        {
            _connections = new List<Connection>();
        }

        public void Connect()
        {
            foreach (Connection conn in _connections)
            {
                conn.Connect();
            }
        }

        public void Disconnect()
        {
            foreach (Connection conn in _connections)
            {
                conn.Disconnect();
            }
        }

        public int ConnectedCount()
        {
            int iConnected = 0;

            foreach (Connection conn in _connections)
            {

            }

            return iConnected;
        }

        public Connection GetConnection(string strAlias)
        {
            foreach (Connection conn in _connections)
            {
                if (conn.Alias == strAlias)
                {
                    return conn;
                }
            }

            return null;
        }

        public static ConnectionComposite MakeConnectionComposite(BotConfigSection botconfig)
        {
            string strNameSpace = Assembly.GetExecutingAssembly().GetName().Name;
            ConnectionComposite retval = new ConnectionComposite();

            foreach (IMServiceElement conn in botconfig.IMServices)
            {
                try
                {
                    string strType = string.Format("{0}.{1}", strNameSpace, conn.Type);

                    Type conType = Type.GetType(strType, false, true);
                    object[] args = new object[] { conn.ScreenName, conn.Password };

                    if (conType != null)
                    {
                        Connection newCon = (Connection)System.Activator.CreateInstance(conType, args);
                        newCon.Alias = conn.Name;
                        switch (conn.NewLine)
                        {
                            case "HTML":
                                newCon.NewLine = "<br>";
                                break;

                            case "CRLF":
                                newCon.NewLine = "\r\n";
                                break;

                            default:
                                break;
                        }

                        retval.Connections.Add(newCon);
                        log.InfoFormat("Connection added ({0})", strType);
                    }
                    else
                    {
                        log.ErrorFormat("Could not create connection `{0}`", strType);
                    }
                }
                catch (Exception e)
                {
                    log.Warn("Unable to create connection", e);
                }
            }

            return retval;
        }
    }
}

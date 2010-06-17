using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace vbotserver
{
    public class IMUserInfo
    {
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

        private Connection _connection = null;
        public Connection IMConnection
        {
            get { return _connection; }
        }

        public IMUserInfo(string strName, string strService, Connection conn)
        {
            _strScreenName = strName;
            _strServiceAlias = strService;
            _connection = conn;
        }
    }
}

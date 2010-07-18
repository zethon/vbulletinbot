using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.Data;

namespace VBulletinBot
{
    public partial class VBotDB
    {
        private static VBotDB _instance;

        public static VBotDB Instance
        {
            get
            {
                if (_instance == null)
                {
                    // TODO: use a regular connection string in the app.config and here
                    BotConfigSection botconfig = (BotConfigSection)ConfigurationManager.GetSection("botconfig");
                    IDbConnection conn = new System.Data.SqlServerCe.SqlCeConnection(string.Format("Data Source={0}", botconfig.LocalDatabase));
                    _instance = new VBotDB(conn);
                }

                return _instance;
            }
        }
    }
}

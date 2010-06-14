using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace vbotserver
{
    /// <summary>
    /// Class to wrap the DataContext into a Singleton object
    /// </summary>
    class Database
    {
        private static VBotDB _instance;
        public static VBotDB Instance
        {
            get 
            {
                if (_instance == null)
                {
                    BotConfigSection botconfig = (BotConfigSection)ConfigurationManager.GetSection("botconfig");
                    _instance = new VBotDB(string.Format("Data Source={0}", botconfig.LocalDatabase));
                }
                
                return _instance;
            }
        }

        private Database()
        {
        }
    }
}

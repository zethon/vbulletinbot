using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Configuration;
using log4net;

namespace VBulletinBot
{
    class Templater
    {
        static ILog log = LogManager.GetLogger(typeof(ResponseChannel));

        private static XDocument _templates = null;

        public static XDocument Templates
        {
            get
            {
                if (_templates == null)
                {
                    BotConfigSection botconfig = (BotConfigSection)ConfigurationManager.GetSection("botconfig");
                    _templates = XDocument.Load(botconfig.TemplateFile);
                    log.DebugFormat("Loading template file: {0}", botconfig.TemplateFile);
                }

                return _templates;
            }
        }

        public static void Reload()
        {
            _templates = null;
        }
    }
}

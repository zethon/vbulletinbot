using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.IO;
using log4net;
using log4net.Config;

namespace VBulletinBot
{
    class Program
    {
        static ILog log = LogManager.GetLogger(typeof(Program));

        static void Main(string[] args)
        {
            try
            {
                log4net.Config.XmlConfigurator.Configure();
                log.Info("vbotserver started");

                Controller app = new Controller();
                if (app.Init())
                {
                    app.MainLoop();
                }
                else
                {
                    log.Info("App object failed to initialize.");
                }
            }
            catch (Exception ex)
            {
                log.Fatal("Exception", ex);
            }
        }
    }
}

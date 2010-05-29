using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.IO;
using log4net;
using log4net.Config;

namespace vbotserver
{
    class Program
    {
        static string logConfigXml = @"
<log4net>
<appender name='full_log' type='log4net.Appender.RollingFileAppender'>
    <file value='vbotserver.txt' />
    <appendToFile value='true' />
    <rollingStyle value='Once' />
    <maxSizeRollBackups value='2' />
    <layout type='log4net.Layout.PatternLayout'>
        <conversionPattern value='%9timestamp %date{yyyy/MM/dd HH:mm:ss.fff} [%thread] %-5level %logger - %message%newline' />
    </layout>
</appender>

<appender name='A1' type='log4net.Appender.ConsoleAppender'>
    <layout type='log4net.Layout.PatternLayout'>
        <conversionPattern value='%date{yyyy-MM-dd HH:mm:ss} %message%newline' />
    </layout>
</appender>
<root>
        <level value='DEBUG' />
        <appender-ref ref='full_log' />
        <appender-ref ref='A1' />
</root>
</log4net>
           ";
        static ILog log = LogManager.GetLogger(typeof(Program));

        static void Main(string[] args)
        {
            try
            {
                XmlDocument logConfigDocument = new XmlDocument();
                logConfigDocument.LoadXml(logConfigXml);
                XmlConfigurator.Configure(logConfigDocument.DocumentElement);

                log.Info("vbotserver started");
                XDocument doc = XDocument.Load(@"config.xml");

                Controller app = new Controller();

                if (app.Init(doc))
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
                log.Debug("Exception", ex);
            }
        }
    }
}

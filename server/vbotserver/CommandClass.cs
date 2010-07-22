using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Data;
using System.Net;
using log4net;
using VBulletinBot.VBotService;

using BotService = VBulletinBot.VBotService.VBotService;


namespace VBulletinBot
{
    class CommandClass
    {
        static ILog log = LogManager.GetLogger(typeof(CommandClass));

        private class CommandMethodAttribute : System.Attribute
        {
            private string _name = string.Empty;
            public string Name
            {
                get { return _name; }
            }

            private string _summary = string.Empty;
            public string Summary
            {
                get { return _summary; }
            }

            private string _details = string.Empty;
            public string Details
            {
                get { return _details; }
            }

            public CommandMethodAttribute(string strName, string strSummary, string strDetails)
            {
                _name = strName;
                _summary = strSummary;
                _details = strDetails;
            }

            public CommandMethodAttribute(string strName, string strSummary)
            {
                _name = strName;
                _summary = strSummary;
            }
        }

        protected class MethodAliasAttribute : System.Attribute
        {
            public string[] Aliases;

            public MethodAliasAttribute(string[] aliases)
            {
                Aliases = aliases;
            }
        }

        private Controller _controller = null;

        private string _strUsername = string.Empty;
        private AIMConnection _connection = null;

        public CommandClass(Controller app)
        {
            _controller = app;
        }

        [CommandMethod("break", "break into the debugger")]
        public void Break()
        {
            System.Diagnostics.Debugger.Break();
        }

        [CommandMethod("cls", "clear the console")]
        public void Cls()
        {
            Console.Clear();
        }

        [CommandMethod("connect", "connect all services")]
        [MethodAlias(new string[] { "/connect", "/c" })]
        public void Connect()
        {
            _controller.Connections.Connect();
        }

        [CommandMethod("resetdb", "reset the local database")]
        public void ResetDatabase(CommandParser parser)
        {
            VBotDB.Instance.Connection.Close();
            VBotDB.Instance.DeleteDatabase();
            VBotDB.Instance.CreateDatabase();
            log.Info("Database reset");
        }

        public void Em(CommandParser parser)
        {
            if (parser.Parameters.Length > 0)
            {
                _controller.OnMessageCallback(_connection, new InstantMessage(_strUsername, parser.WorkingString));
            }
        }

        [CommandMethod("help", "this is it")]
        [MethodAlias(new string[] { "/?", "help", "?" })]
        public void Help()
        {
            Type t = this.GetType();

            foreach (MethodInfo info in t.GetMethods())
            {
                object[] obs = info.GetCustomAttributes(false);

                foreach (object o in obs)
                {
                    CommandMethodAttribute rcma = o as CommandMethodAttribute;

                    if (rcma != null)
                    {
                        log.InfoFormat("{0} - {1}", rcma.Name, rcma.Summary);
                        //Log.Instance.WriteConsoleLine("{0} - {1}", rcma.Name, rcma.Summary);
                        //_output.WriteLine("{0} - {1}", rcma.Name, rcma.Summary);
                    }
                }

            }
        }

        //[MethodAlias(new string[] { "/t", "/test", "test" })]
        public void NotTimer()
        {
            _controller.postNotificationElapsed(null, null);
        }


        public void SetUser(CommandParser parser)
        {
            if (parser.Parameters.Length > 0)
            {
                _strUsername = parser.Parameters[0];
               _connection = new AIMConnection(null, null);
            }
        }

        [CommandMethod("rtf", "reload template file")]
        [MethodAlias(new string[] { "/rtf" })]
        public void ReloadTemplateFile()
        {
            Templater.Reload();
            log.Info("Template file reloaded");
        }


        [MethodAlias(new string[] { "/t", "/test", "test" })]
        public void Test(CommandParser parser)
        {
            Connection c = new GTalkConnection("testuser", "testpass");
            ResponseChannel rc = new ResponseChannel("aimname", c);

            Dictionary<string, object> d = new Dictionary<string, object>()
                {
                    {"PageText","this is the pagettext"},
                    {"Index",8},
                    {"DateLineText","Today at 5pm"},
                    {"Username","Manchy"},
                };

            string str = rc.FetchTemplate(@"postbit", new object[] { "text",3,"Yesterday @ 3pm","Frank Power"} );
            log.Debug(str);


            //VBotService.PostNotificationsResult result = BotService.Instance.GetPostNotifications(true);

            //UserCredentials uc = new UserCredentials();
            //uc.ServiceName = @"gtalk";
            //uc.Username = @"aclaure@gmail.com";

            //VBotService.PostReplyResult res = BotService.Instance.PostNewThread(uc, 2, @"title", "page text");//

            //UserCredentials uc1 = new UserCredentials();
            //uc1.ServiceName = @"aim";
            //uc1.Username = @"zethon";

            //VBotService.RequestResult res = BotService.Instance.WhoAmI(uc);

        }

        [CommandMethod("whoami", "[username] [service alias]")]
        [MethodAlias(new string[] { "/w","/whoami" })]
        public void WhoAmI(CommandParser parser)
        {
            try
            {
                UserCredentials uc = new UserCredentials();

                uc.Username = parser.Parameters[0];
                uc.ServiceName = parser.Parameters[1];

                
                RequestResult result = BotService.Instance.WhoAmI(uc);

                if (result.Code == 0 && result.RemoteUser.UserID > 0)
                {
                    log.InfoFormat("UserID: {0}", result.RemoteUser.UserID);
                    log.InfoFormat("UserName: {0}", result.RemoteUser.Username);
                }
                else if (result.Code == 0)
                {
                    log.Info("Unknown User");
                }
                else
                {
                    log.InfoFormat("Web Service Error: {0}", result.Text);
                }
            }
            catch (Exception ex)
            {
                log.Error("Could not execute WhoAMI()", ex);
            }
        }

        public void ExecuteCommand(string strCommand, bool bUseMethodName)
        {
            CommandParser parser = new CommandParser(strCommand, this);
            parser.Parse();

            MethodInfo mi = null;

            if (bUseMethodName)
            {
                mi = this.GetType().GetMethod(parser.ApplicationName, BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public);
            }

            if (mi == null)
            {
                foreach (MethodInfo mit in this.GetType().GetMethods())
                {
                    foreach (object obj in mit.GetCustomAttributes(false))
                    {
                        MethodAliasAttribute maa = obj as MethodAliasAttribute;

                        if (maa != null)
                        {
                            if (maa.Aliases != null && maa.Aliases.Contains(parser.ApplicationName))
                            {
                                mi = mit;
                                break;
                            }
                        }
                    }

                    if (mi != null)
                    {
                        break;
                    }
                }
            }

            if (mi != null)
            {
                try
                {
                    if (mi.GetParameters().Length == 0)
                    {
                        mi.Invoke(this, null);
                    }
                    else
                    {
                        object[] oparams = new object[mi.GetParameters().Length];

                        foreach (ParameterInfo m in mi.GetParameters())
                        {
                            if (m.ParameterType == typeof(string))
                            {
                                oparams[oparams.Length - 1] = parser.WorkingString;
                            }
                            else if (m.ParameterType == typeof(CommandParser))
                                oparams[oparams.Length - 1] = parser;
                        }

                        if (oparams != null)
                            mi.Invoke(this, oparams);

                    }
                }
                catch (Exception e)
                {
                    log.Error("Commands.ExecuteCommand exception: " + e.Message);
                }
            }
            else
            {
                log.Info(@"Unknown command");
            }

        }

    }
}

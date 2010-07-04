using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Data;
using System.Net;
using log4net;
using Microsoft.Web.Services2;
using Microsoft.Web.Services2.Security;
using Microsoft.Web.Services2.Security.Tokens;


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
        public void Connect()
        {
            _controller.Connections.Connect();
        }

        [CommandMethod("resetdb", "reset the local database")]
        public void ResetDatabase(CommandParser parser)
        {
            Database.Instance.Connection.Close();
            Database.Instance.DeleteDatabase();
            Database.Instance.CreateDatabase();
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

        [CommandMethod("nottimer", "elapse the im notifcation timer")]
        public void NotTimer()
        {
            _controller._notTimer_Elapsed(null, null);
        }


        public void SetUser(CommandParser parser)
        {
            if (parser.Parameters.Length > 0)
            {
                _strUsername = parser.Parameters[0];
               _connection = new AIMConnection(null, null);
            }
        }

        [CommandMethod("whoami", "[username] [service alias]")]
        public void WhoAmI(CommandParser parser)
        {
            try
            {
                UserCredentials uc = new UserCredentials();

                uc.Username = parser.Parameters[0];
                uc.ServiceName = parser.Parameters[1];

                RequestResult result = VBotService.Instance.WhoAmI(uc);

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

        public void ExecuteCommand(string strCommand)
        {
            CommandParser parser = new CommandParser(strCommand, this);
            parser.Parse();

            MethodInfo mi = this.GetType().GetMethod(parser.ApplicationName, BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public);

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
                                oparams[oparams.Length - 1] = parser.Parameters[oparams.Length - 1];
                            else if (m.ParameterType == typeof(CommandParser))
                                oparams[oparams.Length - 1] = parser;
                        }

                        if (oparams != null)
                            mi.Invoke(this, oparams);

                    }
                }
                catch (Exception e)
                {
                    log.Debug("Execute Command Exception", e);
                }
            }
            else
            {
                log.Info(@"Unknown command");
            }

        }

    }
}

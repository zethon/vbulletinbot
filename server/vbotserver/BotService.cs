using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Configuration;

namespace vbotserver
{
    /// <summary>
    /// Class to wrap the WebService into a Singleton object
    /// </summary>
    public class BotService
    {
        private static VBotService.VBotService _instance = null;

        public static VBotService.VBotService Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new VBotService.VBotService();

                    BotConfigSection botconfig = (BotConfigSection)ConfigurationManager.GetSection("botconfig");

                    System.Net.CredentialCache cc = new CredentialCache();
                    cc.Add(new Uri(_instance.Url), "Basic", new NetworkCredential(string.Empty, botconfig.WebServicePassword, string.Empty));

                    _instance.Credentials = cc;
                    _instance.PreAuthenticate = true;
                }

                return _instance;
            }
        }

        public static VBotService.UserCredentials Credentialize(string strUser, string strServ)
        {
            VBotService.UserCredentials creds = new VBotService.UserCredentials();

            creds.Username = strUser;
            creds.ServiceName = strServ;

            return creds;
        }

        public static VBotService.UserCredentials Credentialize(ResponseChannel channel)
        {
            VBotService.UserCredentials creds = new VBotService.UserCredentials();

            creds.Username = channel.ToName;
            creds.ServiceName = channel.Connection.Alias;

            return creds;
        }
    }
}

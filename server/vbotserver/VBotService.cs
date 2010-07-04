using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.Net;

namespace VBulletinBot
{
    /// <summary>
    /// Extension of the VBotService class to add Credentials to the SoapContext
    /// </summary>
    public partial class VBotService
    {
        private static VBotService _instance = null;
        public static VBotService Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new VBotService();

                    BotConfigSection botconfig = (BotConfigSection)ConfigurationManager.GetSection("botconfig");

                    System.Net.CredentialCache cc = new CredentialCache();
                    cc.Add(new Uri(_instance.Url), "Basic", new NetworkCredential(string.Empty, botconfig.WebServicePassword, string.Empty));

                    _instance.Credentials = cc;
                    _instance.PreAuthenticate = true;
                }

                return _instance;
            }
        }

        public static UserCredentials Credentialize(string strUser, string strServ)
        {
            UserCredentials creds = new UserCredentials();

            creds.Username = strUser;
            creds.ServiceName = strServ;

            return creds;
        }

        public static UserCredentials Credentialize(ResponseChannel channel)
        {
            UserCredentials creds = new UserCredentials();

            creds.Username = channel.ToName;
            creds.ServiceName = channel.Connection.Alias;

            return creds;
        }

        protected override System.Net.WebRequest GetWebRequest(Uri uri)
        {
            System.Net.WebRequest r = base.GetWebRequest(uri);

            if (PreAuthenticate)
            {
                System.Net.NetworkCredential creds = Credentials.GetCredential(uri, "Basic");

                if (creds != null)
                {
                    byte[] buffer = new UTF8Encoding().GetBytes(creds.UserName + ":" + creds.Password);
                    r.Headers["Authorization"] = "Basic " + Convert.ToBase64String(buffer);
                }
            }

            return r;
        }
    }
}

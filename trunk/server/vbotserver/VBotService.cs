using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.Net;

namespace vbotserver.VBotService
{
    /// <summary>
    /// Extension of the VBotService class to add Credentials to the SoapContext
    /// </summary>
    public partial class VBotService
    {
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

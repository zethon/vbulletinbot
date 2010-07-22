using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using log4net;

namespace VBulletinBot
{
    public class ResponseChannel
    {
        static ILog log = LogManager.GetLogger(typeof(ResponseChannel));

        public Connection Connection = null;
        public string ToName = string.Empty;

        public string NewLine
        {
            get { return Connection.NewLine; }
        }

        public ResponseChannel()
        {
        }

        public ResponseChannel(string strName, Connection conn)
        {
            ToName = strName;
            Connection = conn;
        }

        public void SendMessage(string Message)
        {
            if (Connection != null && ToName != string.Empty)
            {
                Connection.SendMessage(new InstantMessage(ToName, Message));
            }
            else if (Connection == null)
            {
                log.Warn("SendMessage() failed because Connection object is null");
            }
            else if (ToName == string.Empty)
            {
                log.Info("SendMessage() failed because ToName is empty");
            }
        }

        private XElement GetTemplateElement(string strTemplate)
        {
            // try to load a connection specific template
            var q = (from c in Templater.Templates.Descendants(@"template")
                     where
                         (string)c.Attribute(@"name") == strTemplate
                         && (string)c.Attribute(@"style") == this.Connection.Alias
                     select c).FirstOrDefault();

            if (q == null)
            {
                // load the default template
                q = (from c in Templater.Templates.Descendants(@"template")
                     where
                         (string)c.Attribute(@"name") == strTemplate
                         && (string)c.Attribute(@"style") == @"default"
                     select c).FirstOrDefault();
            }

            if (q == null)
            {
                log.ErrorFormat("Unknown template: {0}", strTemplate);
            }
            else if (q.Element(@"text") == null)
            {
                log.ErrorFormat("No 'text' element in template: {0}", strTemplate);
                q = null;
            }
            else if (q.Element(@"order") == null)
            {
                log.ErrorFormat("No 'order' element in template: {0}", strTemplate);
                q = null;
            }

            return q;
        }

        public string FetchTemplate(string strTemplateName, object[] parameters)
        {
            string strRet = string.Empty;
            XElement q = GetTemplateElement("postbit");

            if (q != null)
            {
                strRet = q.Element(@"text").Value;

                string[] strVars = Regex.Split(q.Element(@"order").Value, @"\,");
                string strTemp = string.Empty;
                int iCount = 0;

                foreach (string strVar in strVars)
                {
                    if (iCount >= parameters.Count())
                    {
                        break;
                    }

                    strTemp = "${" + strVar + "}";
                    strRet = strRet.Replace(strTemp, parameters[iCount].ToString());

                    iCount++;
                }
            }

            return strRet;   
        }

        public string FetchTemplate(string strTemplateName, Dictionary<string, object> d)
        {
            string strRet = string.Empty;
            XElement q = GetTemplateElement("postbit");

            if (q != null)
            {
                strRet = q.Element(@"text").Value;
                string[] strVars = Regex.Split(q.Element(@"order").Value, @"\,");

                string strTemp = string.Empty;
                foreach (string strVar in strVars)
                {
                    if (d.ContainsKey(strVar))
                    {
                        strTemp = "${" + strVar + "}";
                        strRet = strRet.Replace(strTemp, d[strVar].ToString());
                    }
                    else
                    {
                        log.WarnFormat("Template '{0}' contains '{1}' variable but dictionary does not", strTemplateName, strVar);
                    }
                }
            }

            return strRet;
        }

    }
}

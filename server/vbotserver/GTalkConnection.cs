using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;
using jabber.client;
using jabber.connection.sasl;


namespace VBulletinBot
{
    class GTalkConnection : Connection
    {
        static ILog log = LogManager.GetLogger(typeof(GTalkConnection));

        private jabber.client.JabberClient _jc;

        public GTalkConnection(string strUsername, string strPassword)
        {
            _jc = new JabberClient();
            _jc.AutoReconnect = 3F;
            _jc.AutoStartCompression = true;
            _jc.AutoStartTLS = true;
            _jc.KeepAlive = 30F;
            _jc.LocalCertificate = null;
            _jc.SSL = false;
            _jc.Proxy = jabber.connection.ProxyType.None;
            _jc.Server = "gmail.com";
            _jc.Password = strPassword;
            _jc.User = strUsername;

            _jc.OnError += new bedrock.ExceptionHandler(_jc_OnError);
            _jc.OnConnect += new jabber.connection.StanzaStreamHandler(this._jc_OnConnect);
            _jc.OnInvalidCertificate += new System.Net.Security.RemoteCertificateValidationCallback(_jc_OnInvalidCertificate);
            _jc.OnAuthError += new jabber.protocol.ProtocolHandler(_jc_OnAuthError);
            _jc.OnMessage += new MessageHandler(_jc_OnMessage);
            _jc.OnDisconnect += new bedrock.ObjectHandler(_jc_OnDisconnect);
        }

        void _jc_OnDisconnect(object sender)
        {
            log.Warn("GTalkConnection::_jc_OnDisconnect");
        }

        void _jc_OnMessage(object sender, jabber.protocol.client.Message msg)
        {
            try
            {
                if (msg.Body != null && msg.From != null)
                {
                    doOnMessageEvent(new InstantMessage(msg.From.Bare, msg.Body));
                }
            }
            catch (Exception ex)
            {
                log.Warn("GTalkConnection::_jc_OnMessage()",ex);
            }
        }

        void _jc_OnError(object sender, Exception ex)
        {
            log.Error("GTalkConnection::_jc_OnError();", ex);
        }

        void _jc_OnAuthError(object sender, System.Xml.XmlElement rp)
        {
            throw new NotImplementedException();
        }

        bool _jc_OnInvalidCertificate(object sender, System.Security.Cryptography.X509Certificates.X509Certificate certificate, System.Security.Cryptography.X509Certificates.X509Chain chain, System.Net.Security.SslPolicyErrors sslPolicyErrors)
        {
            log.Debug("GTalkConnection invalid certificate OK");
            return false;
        }

        public override void Connect()
        {
            _jc.Connect();
            
        }

        void _jc_OnConnect(object sender, jabber.connection.StanzaStream stream)
        {
            doOnConnectEvent();
        }

        public override void Disconnect()
        {
            throw new NotImplementedException();
        }

        public override void SendMessage(InstantMessage im)
        {
            _jc.Message(im.User, im.Text);
            doSendMessageEvent(new InstantMessage(im.User, im.Text));
        }

        public jabber.protocol.ProtocolHandler jc_OnAuthError { get; set; }
    }
}

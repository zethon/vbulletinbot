using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dotTOC;
using log4net;

namespace vbotserver
{
    class AIMConnection : Connection
    {
        static ILog log = LogManager.GetLogger(typeof(AIMConnection));

        private TOC _toc = new TOC();
        private string _strUsername = string.Empty;
        private string _strPassword = string.Empty;

        public AIMConnection(string strUsername, string strPassword)
        {
            _strUsername = strUsername;
            _strPassword = strPassword;

            _toc.OnSignedOn += new IncomingHandlers.OnSignedOnHandler(_toc_OnSignedOn);
            _toc.OnIMIn += new IncomingHandlers.OnIMInHandler(_toc_OnIMIn);
            _toc.OnDisconnect += new TOC.OnDisconnectHandler(_toc_OnDisconnect);
            _toc.OnTOCError += new TOC.OnTOCErrorHandler(_toc_OnTOCError);
        }

        void _toc_OnTOCError(TOCError error)
        {
            log.ErrorFormat("Could not log into AIM: {0} - {1}", error.Code, error.Argument);
        }

        void _toc_OnIMIn(dotTOC.InstantMessage im)
        {
            doOnMessageEvent(new InstantMessage(im.From.NormalizedName, im.Message, im.Auto));
        }

        void _toc_OnDisconnect(Exception ex)
        {
            doOnDisconnectEvent();
        }

        void  _toc_OnSignedOn()
        {
            doOnConnectEvent();
        }

        override public void Connect()
        {
            _toc.Connect(_strUsername,_strPassword);
        }

        public override void Disconnect()
        {
            _toc.Disconnect();
        }

        public override void SendMessage(InstantMessage im)
        {
            dotTOC.InstantMessage newim = new dotTOC.InstantMessage();
            newim.To = new Buddy();
            newim.To.Name = im.User;
            newim.RawMessage = im.Text;

            _toc.SendIM(newim);
            doSendMessageEvent(im);
        }
    }
}

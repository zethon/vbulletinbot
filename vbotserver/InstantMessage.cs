using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace vbotserver
{
    public class InstantMessage
    {
        private string _text;
        public string Text
        {
            get { return _text; }
        }

        private string _user;
        public string User
        {
            get { return _user; }
        }

        private bool _bIsAuto = false;
        public bool IsAuto
        {
            get { return _bIsAuto; }
        }

        public InstantMessage(string User, string Text)
        {
            _text = Text;
            _user = User.Trim();
        }

        public InstantMessage(string strUser, string strText, bool bIsAuto)
        {
            _user = strUser.Trim();
            _text = strText;
            _bIsAuto = bIsAuto;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace vbotserver
{
    public class VBPost
    {
        private int _iPostID = 0;
        public int PostID
        {
            get { return _iPostID; }
        }

        private int _iNextPostID = 0;
        public int NextPostID
        {
            get { return _iNextPostID; }
        }

        private int _iPrevPostID = 0;
        public int PrevPostID
        {
            get { return _iPrevPostID; }
        }

        private string _strUsername = string.Empty;
        public string Username
        {
            get { return _strUsername; }
        }

        private string _strTitle = string.Empty;
        public string Title
        {
            get { return _strTitle; }
        }

        private string _strPageText = string.Empty;
        public string PageText
        {
            get { return _strPageText; }
        }

        private DateTime _dtDateLine = new System.DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).ToLocalTime();
        public DateTime DateLine
        {
            get { return _dtDateLine; }
        }

        private bool _bIsNew = false;
        public bool IsNew
        {
            get { return _bIsNew; }
        }

        public VBPost(Dictionary<string, string> info)
        {
            if (info.ContainsKey(@"postid"))
            {
                _iPostID = int.Parse(info[@"postid"].ToString());
            }
            else
            {
                throw new Exception(@"Key `postid` not found in VBPost constructor hash");
            }

            if (info.ContainsKey(@"nextpostid") && info[@"nextpostid"].Length > 0)
            {
                _iNextPostID = int.Parse(info[@"nextpostid"].ToString());
            }

            if (info.ContainsKey(@"prevpostid") && info[@"prevpostid"].Length > 0)
            {
                _iPrevPostID = int.Parse(info[@"prevpostid"].ToString());
            }

            if (info.ContainsKey(@"title"))
            {
                _strTitle = info[@"title"].ToString();
            }
            else
            {
                throw new Exception(@"Key `title` not found in VBPost constructor hash");
            }

            if (info.ContainsKey(@"username"))
            {
                _strUsername = info[@"username"].ToString();
            }
            else
            {
                throw new Exception(@"Key `username` not found in VBPost constructor hash");
            }

            if (info.ContainsKey(@"pagetext"))
            {
                _strPageText = info[@"pagetext"].ToString();
            }
            else
            {
                throw new Exception(@"Key `pagetext` not found in VBPost constructor hash");
            }

            if (info.ContainsKey(@"dateline"))
            {
                long iEpoch = long.Parse(info[@"dateline"].ToString());
                _dtDateLine = _dtDateLine.AddSeconds(iEpoch);
            }
            else
            {
                throw new Exception(@"Key `dateline` not found in VBPost constructor hash");
            }

            if (info.ContainsKey(@"isnew"))
            {
                if (info[@"isnew"].ToString().ToLower() == "true")
                {
                    _bIsNew = true;
                }
                else
                {
                    _bIsNew = false;
                }
            }
            else
            {
                _bIsNew = false;
            }
        }

        public string GetFriendlyDate()
        {
            string strRet = string.Empty;

            if (DateLine != null)
            {
                string strDate = DateLine.ToShortDateString();
                string strTime = DateLine.ToShortTimeString();

                if (DateLine.Date == DateTime.Now.Date)
                {
                    strDate = "Today";
                }
                else if (DateLine.Date == DateTime.Now.Date.AddDays(-1))
                {
                    strDate = "Yesterday";
                }

                strRet = strDate + " " + strTime;
            }

            return strRet;
        }

        public string GetShortPostText()
        {
            string strPostText = PageText;

            if (strPostText.Length > 25)
            {
                strPostText = strPostText.Substring(0, 22);
                strPostText += @"...";
                strPostText = Regex.Replace(strPostText,@"[\r\n]",string.Empty);
            }

            return strPostText;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace vbotserver
{
    public class VBThread
    {
        private int _iThreadID = 0;
        public int ThreadID
        {
            get { return _iThreadID; }
        }

        private string _strThreadTitle = string.Empty;
        public string ThreadTitle
        {
            get { return _strThreadTitle; }
        }

        private int _iForumID = 0;
        public int ForumID
        {
            get { return _iForumID; }
        }

        private int _iTotalThreads = 0;
        public int TotalThreads
        {
            get { return _iTotalThreads; }
        }

        private DateTime _dtLastPost = new System.DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).ToLocalTime();
        public DateTime LastPost
        {
            get { return _dtLastPost; }
        }

        private string _strLastPoster = string.Empty;
        public string LastPoster
        {
            get { return _strLastPoster; }
        }

        private int _iLastPostID = 0;
        public int LastPostID
        {
            get { return _iLastPostID; }
        }

        private int _iReplyCount = 0;
        public int ReplyCount
        {
            get { return _iReplyCount; }
        }

        private int _iSubscribeThreadID = 0;
        public int SubscribeThreadID
        {
            get { return _iSubscribeThreadID; }
        }

        private bool _bIsNew = false;
        public bool IsNew
        {
            get { return _bIsNew; }
        }

        private string _strPostUsername = string.Empty;
        public string PostUsername
        {
            get { return _strPostUsername; }
        }

        private DateTime _dtDateLine = new System.DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).ToLocalTime();
        public DateTime DateLine
        {
            get { return _dtDateLine; }
        }

        public VBThread(Dictionary<string, string> info)
        {
            if (info.ContainsKey(@"threadid"))
            {
                _iThreadID = int.Parse(info[@"threadid"].ToString());
            }
            else
            {
                throw new Exception(@"Key `threadid` not found in Thread constructor hash");
            }

            if (info.ContainsKey(@"threadtitle"))
            {
                _strThreadTitle = info[@"threadtitle"].ToString();
            }
            else if (info.ContainsKey(@"title"))
            {
                _strThreadTitle = info[@"title"].ToString();
            }
            else
            {
                throw new Exception(@"Key `threadtitle` or `title` not found in Thread constructor hash");
            }

            if (info.ContainsKey(@"forumid"))
            {
                _iForumID = int.Parse(info[@"forumid"].ToString());
            }
            else
            {
                throw new Exception(@"Key `forumid` not found in Thread constructor hash");
            }

            if (info.ContainsKey(@"lastpostid"))
            {
                _iLastPostID = int.Parse(info[@"lastpostid"].ToString());
            }
            else
            {
                throw new Exception(@"Key `lastpostid` not found in Thread constructor hash");
            }

            if (info.ContainsKey(@"lastpost"))
            {
                long iEpoch = long.Parse(info[@"lastpost"].ToString());
                _dtLastPost = _dtLastPost.AddSeconds(iEpoch);
            }

            if (info.ContainsKey(@"lastposter"))
            {
                _strLastPoster = info[@"lastposter"].ToString();
            }
            else
            {
                throw new Exception(@"Key `lastposter` not found in Thread constructor hash");
            }

            if (info.ContainsKey(@"postusername"))
            {
                _strPostUsername = info[@"postusername"].ToString();
            }

            if (info.ContainsKey(@"replycount"))
            {
                _iReplyCount = int.Parse(info[@"replycount"].ToString());
            }
            else
            {
                throw new Exception(@"Key `replycount` not found in Thread constructor hash");
            }

            if (info.ContainsKey(@"subscribethreadid") && info[@"subscribethreadid"].Length > 0)
            {
                _iSubscribeThreadID = int.Parse(info[@"subscribethreadid"].ToString());
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

            if (info.ContainsKey(@"dateline"))
            {
                long iEpoch = long.Parse(info[@"dateline"].ToString());
                _dtDateLine = _dtDateLine.AddSeconds(iEpoch);
            }

            if (info.ContainsKey(@"totalthreads"))
            {
                _iTotalThreads = int.Parse(info[@"totalthreads"].ToString());
            }
        }

        public string GetFriendlyDate()
        {
            return GetFriendlyDate(LastPost);
        }

        public string GetFriendlyDate(DateTime theTime)
        {
            string strRet = string.Empty;

            if (theTime != null)
            {
                string strDate = theTime.ToShortDateString();
                string strTime = theTime.ToShortTimeString();

                if (theTime.Date == DateTime.Now.Date)
                {
                    strDate = "Today";
                }
                else if (theTime.Date == DateTime.Now.Date.AddDays(-1))
                {
                    strDate = "Yesterday";
                }

                strRet = strDate + " " + strTime;
            }

            return strRet;
        }

        public string GetTitle()
        {
           return string.Format("{0} - {1} created by {2}",
                            Regex.Replace(ThreadTitle, @"[\']", string.Empty),
                            GetFriendlyDate(DateLine),
                            PostUsername);
        }

        
    }
}

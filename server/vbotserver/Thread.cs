using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace VBulletinBot.VBotService
{
    public partial class Thread
    {
        public string GetFriendlyDate()
        {
            return GetFriendlyDate(LastPost);
        }

        public string GetFriendlyDate(int iSeconds)
        {
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).ToLocalTime();
            return GetFriendlyDate(epoch.AddSeconds(iSeconds));
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

                strTime = strTime.Replace(" ", string.Empty);
                strRet = string.Format("{0} {1}", strDate, strTime,theTime.Date.ToString(),DateTime.Now.Date.ToString());
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

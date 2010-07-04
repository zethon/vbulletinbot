using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace vbotserver.VBotService
{
    public partial class Post
    {
        public string GetFriendlyDate()
        {
            string strRet = string.Empty;
            DateTime dt = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).ToLocalTime().AddSeconds(DateLine);
            string strDate = dt.ToShortDateString();
            string strTime = dt.ToShortTimeString();

            if (dt.Date == DateTime.Now.Date)
            {
                strDate = "Today";
            }
            else if (dt.Date == DateTime.Now.Date.AddDays(-1))
            {
                strDate = "Yesterday";
            }

            strRet = strDate + " " + strTime;
            

            return strRet;
        }

        public string GetShortPostText()
        {
            string strPostText = PageText;

            if (strPostText.Length > 25)
            {
                strPostText = strPostText.Substring(0, 22);
                strPostText += @"...";
                strPostText = Regex.Replace(strPostText, @"[\r\n]", string.Empty);
            }

            return strPostText;
        }
    }
}

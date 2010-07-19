using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace VBulletinBot.VBotService
{
    public partial class Post : IVBEntity
    {
        public int DatabaseID
        {
            get
            {
                return PostID;
            }

            set
            {
                PostID = value;
            }
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

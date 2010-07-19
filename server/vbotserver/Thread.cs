using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace VBulletinBot.VBotService
{
    public partial class Thread : IVBEntity
    {
        public int DatabaseID
        {
            get
            {
                return ThreadID;
            }

            set
            {
                ThreadID = value;
            }
        }

        public string GetTitle()
        {
            return string.Format("{0} - {1} created by {2}",
                             Regex.Replace(ThreadTitle, @"[\']", string.Empty),
                             DateLineText,
                             PostUsername);
        }
    }
}

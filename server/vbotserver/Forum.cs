using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VBulletinBot.VBotService
{
    public partial class Forum : VBEntity
    {
        public int DatabaseID
        {
            get
            {
                return ForumID;
            }

            set
            {
                ForumID = value;
            }
        }
    }
}

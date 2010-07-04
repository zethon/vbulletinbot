using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;

namespace VBulletinBot
{
    public partial class LocalUser
    {
        static ILog log = LogManager.GetLogger(typeof(LocalUser));
        
        public ResponseChannel ResponseChannel;

        public void SaveLastList(string strLastList)
        {
            UserLastList ll = VBotDB.Instance.UserLastLists.FirstOrDefault(l => l.LocalUserID == LocalUserID);

            if (ll != null)
            {
                ll.Name = strLastList.ToLower();
            }
            else
            {
                VBotDB.Instance.UserLastLists.InsertOnSubmit(
                    new UserLastList { LocalUserID = LocalUserID, Name = strLastList.ToLower() });
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Saved last list {0} for LocalUser {1}", strLastList, _LocalUserID);
            }

            VBotDB.Instance.SubmitChanges();
        }

        public void SaveLastPostIndex(int iPostIndex)
        {
            UserPostIndex upi = VBotDB.Instance.UserPostIndexes.FirstOrDefault(u => u.LocalUserID == LocalUserID);

            if (upi != null)
            {
                upi.PostIndex = iPostIndex;
            }
            else
            {
                VBotDB.Instance.UserPostIndexes.InsertOnSubmit(
                    new UserPostIndex { LocalUserID = LocalUserID, PostIndex = iPostIndex });
            }

            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Saved post index {0} for LocalUser {1}", iPostIndex, _LocalUserID);
            }

            VBotDB.Instance.SubmitChanges();
        }
    }
}

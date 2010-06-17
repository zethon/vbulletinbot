using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;

namespace vbotserver
{
    public class LocalUserAdapter
    {
        static ILog log = LogManager.GetLogger(typeof(LocalUserAdapter));

        public LocalUser LocalUser;
        public ResponseChannel ResponseChannel;

        public void SaveLastList(string strLastList)
        {
            UserLastList ll = Database.Instance.UserLastLists.FirstOrDefault(l => l.LocalUserID == LocalUser.LocalUserID);

            if (ll != null)
            {
                ll.Name = strLastList.ToLower();
            }
            else
            {
                Database.Instance.UserLastLists.InsertOnSubmit(
                    new UserLastList { LocalUserID = this.LocalUser.LocalUserID, Name = strLastList.ToLower() });
            }

            Database.Instance.SubmitChanges();
        }

        public void SaveLastPostIndex(int iPostIndex)
        {
            UserPostIndex upi = Database.Instance.UserPostIndexes.FirstOrDefault(u => u.LocalUserID == LocalUser.LocalUserID);

            if (upi != null)
            {
                upi.PostIndex = iPostIndex;
            }
            else
            {
                Database.Instance.UserPostIndexes.InsertOnSubmit(
                    new UserPostIndex { LocalUserID = this.LocalUser.LocalUserID, PostIndex = iPostIndex });
            }

            Database.Instance.SubmitChanges();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;

namespace vbotserver
{
    public class User
    {
        static ILog log = LogManager.GetLogger(typeof(User));

        private string _strConnUsername = string.Empty;
        public string UserConnectionName
        {
            get { return _strConnUsername; }
            set { _strConnUsername = value; }
        }

        private Connection _conn = null;
        public Connection UserConnection
        {
            get { return _conn; }
            set { _conn = value; }
        }

        private int _iLocalUserID = 0;
        public int LocalUserID
        {
            get { return _iLocalUserID; }
        }

        private int _iVBUserID = 0;
        public int VBUserID
        {
            get { return _iVBUserID; }
        }

        private Dictionary<string, string> _dbUser;
        public Dictionary<string, string> DBUser
        {
            get { return _dbUser; }
            set 
            { 
                Dictionary<string,string> temp = value as Dictionary<string,string>;

                if (!temp.ContainsKey(@"localuserid") || !int.TryParse(temp[@"localuserid"].ToString(),out _iLocalUserID))
                    throw new Exception("Cannot set DBUser, dictionary does not contain `localuserid` column.");

                _dbUser = value; 
            }
        }

        private Dictionary<string, string> _vbUser;
        public Dictionary<string, string> VBUser
        {
            get { return _vbUser; }
            set
            {
                Dictionary<string, string> temp = value as Dictionary<string, string>;

                if (!temp.ContainsKey(@"userid") || !int.TryParse(temp[@"userid"].ToString(), out _iVBUserID))
                    throw new Exception("Cannot set VBUser, dictionary does not contain `userid`.");

                _vbUser = value;
            }
        }

        public void SaveLastList(string strLastList)
        {
            UserLastList ll = Database.Instance.UserLastLists.FirstOrDefault(l => l.LocalUserID == LocalUserID);

            if (ll != null)
            {
                ll.Name = strLastList.ToLower();
            }
            else
            {
                Database.Instance.UserLastLists.InsertOnSubmit(
                    new UserLastList { LocalUserID = this.LocalUserID, Name = strLastList.ToLower() });
            }

            Database.Instance.SubmitChanges();
        }

        public void SaveLastPostIndex(int iPostIndex)
        {
            UserPostIndex upi = Database.Instance.UserPostIndexes.FirstOrDefault(u => u.LocalUserID == LocalUserID);

            if (upi != null)
            {
                upi.PostIndex = iPostIndex;
            }
            else
            {
                Database.Instance.UserPostIndexes.InsertOnSubmit(
                    new UserPostIndex { LocalUserID = this.LocalUserID, PostIndex = iPostIndex });
            }

            Database.Instance.SubmitChanges();
        }
    }
}

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
            strLastList = strLastList.ToLower();

            string strQuery = string.Format(@"
                                UPDATE userlastlist 
                                SET name = '{0}'
                                WHERE (localuserid = {1})
                            ", strLastList, LocalUserID);

            int iRows = DB.Instance.QueryWrite(strQuery,true);
            if (iRows <= 0)
            {
                strQuery = string.Format(@"
                                INSERT INTO userlastlist 
                                (name,localuserid)
                                VALUES
                                ('{0}',{1});
                            ", strLastList, LocalUserID);

                iRows = DB.Instance.QueryWrite(strQuery);
            }
        }

        public void SaveLastPostIndex(int iPostIndex)
        {
            string strQuery = string.Format(@"
                        UPDATE userpostindex
                        SET postindex = {0}
                        WHERE (localuserid = {1})
                    ",iPostIndex, LocalUserID);

            int iRows = DB.Instance.QueryWrite(strQuery, true);
            if (iRows <= 0)
            {
                strQuery = string.Format(@"
                                INSERT INTO userpostindex
                                (postindex,localuserid)
                                VALUES
                                ({0},{1});
                            ", iPostIndex, LocalUserID);

                iRows = DB.Instance.QueryWrite(strQuery);
            }
        }
    }
}

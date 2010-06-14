using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using log4net;

namespace vbotserver
{
    public enum UserLocationType
    {
        FORUM,
        THREAD,
        POST
    }

    public class UserLocationT
    {
        static ILog log = LogManager.GetLogger(typeof(UserLocationT));

        private int _iUserLocationID = 0;
        public int UserLocationID
        {
            get { return _iUserLocationID; }
            set { _iUserLocationID = value; }
        }

        private User _owner = null;
        public User Owner
        {
            get { return _owner; }
        }

        private string _strTitle = string.Empty;
        public string Title
        {
            get { return _strTitle; }
            set { _strTitle = value; }
        }

        private List<string> _IDList = new List<string>();
        public List<string> IDList
        {
            get { return _IDList; }
        }

        private int _iLocationRemoteID = 0;
        public int LocationRemoteID
        {
            get { return _iLocationRemoteID; }
            set { _iLocationRemoteID = value; }
        }

        private UserLocationType _locType;
        public UserLocationType LocationType
        {
            get { return _locType; }
            set { _locType = value; }
        }

        private int _iPageNumber = 0;
        public int PageNumber
        {
            get { return _iPageNumber; }
            set { _iPageNumber = value; }
        }

        private int _iPerPage = 0;
        public int PerPage
        {
            get { return _iPerPage; }
            set { _iPerPage = value; }
        }

        public UserLocationT(Dictionary<string, string> info,User owner)
        {
            if (owner == null)
            {
                throw new Exception("Userlocation owner is empty");
            }
            
            _owner = owner;

            if (info.ContainsKey(@"userlocationtype"))
            {
                _locType = (UserLocationType) Enum.Parse(typeof(UserLocationType), info[@"userlocationtype"].ToString(), true);
            }
            else
            {
                throw new Exception("Key `userlocationtype` not found in constructor for UserLocation");
            }

            if (info.ContainsKey(@"userlocationid"))
            {
                if (!int.TryParse(info[@"userlocationid"].ToString(), out _iUserLocationID))
                {
                    throw new Exception("Could not parse `userlocationid` for UserLocation constructor");
                }
            }

            if (info.ContainsKey(@"locationremoteid"))
            {
                if (!int.TryParse(info[@"locationremoteid"].ToString(), out _iLocationRemoteID))
                {
                    throw new Exception("Could not parse `locationremoteid` for UserLocation constructor");
                }
            }
            else
            {
                throw new Exception("Key `locationremoteid` not found in hash for UserLocation constructor");
            }

            if (info.ContainsKey(@"pagenumber"))
            {
                if (!int.TryParse(info[@"pagenumber"].ToString(), out _iPageNumber))
                {
                    throw new Exception("Could not parse `pagenumber` for UserLocation constructor");
                }
            }
            else
            {
                _iPageNumber = 1;
            }

            if (info.ContainsKey(@"perpage"))
            {
                if (!int.TryParse(info[@"perpage"].ToString(), out _iPerPage))
                {
                    throw new Exception("Could not parse `perpage` for UserLocation constructor");
                }
            }
            else
            {
                _iPerPage = 5; // HARDCODED DEFAULT! ACK!
            }

            if (info.ContainsKey(@"title"))
            {
                _strTitle = info[@"title"].ToString();
            }
            else
            {
                throw new Exception("Key `title` not found in hash for UserLocation constructor");
            }


            if (info.ContainsKey(@"list"))
            {
                ParseList(info[@"list"].ToString());
            }
            else
            {
                throw new Exception("Key `list` not found in hash for UserLocation constructor");
            }

        }

        public void ParseList(string strList)
        {
            _IDList.Clear();
            string[] IDs = Regex.Split(strList, @"[\s+]");

            foreach (string id in IDs)
            {
                if (id.Length > 0)
                {
                    _IDList.Add(id);
                }
            }
        }

        public void ParsePostList(List<VBPost> list)
        {
            IDList.Clear();

            foreach (VBPost post in list)
            {
                IDList.Add(post.PostID.ToString());
            }
        }

        public void ParseThreadList(List<VBThread> list)
        {
            IDList.Clear();

            foreach (VBThread thread in list)
            {
                _IDList.Add(thread.ThreadID.ToString());
            }
        }

        public void ParseForumsList(List<Dictionary<string, string>> list)
        {
            IDList.Clear();

            foreach (Dictionary<string, string> forumInfo in list)
            {
                if (!forumInfo.ContainsKey(@"forumid"))
                {
                    throw new Exception(@"Dictionary does not contain `forumid`");
                }

                if (!forumInfo.ContainsKey(@"title"))
                {
                    throw new Exception(@"Dictionary does not contain `title`");
                }
                        
                if (forumInfo.ContainsKey(@"iscurrent") && forumInfo[@"iscurrent"] == "1")
                { 
                    if (!int.TryParse(forumInfo[@"forumid"],out _iLocationRemoteID))
                    {
                        throw new Exception(@"Could not parse parent `forumid`");
                    }

                    _strTitle = forumInfo[@"title"];
                }
                else
                {
                    int iTemp = 0;
                    if (!int.TryParse(forumInfo[@"forumid"],out iTemp) || iTemp <= 0)
                    {
                        throw new Exception(@"Could not parse child `forumid` ");
                    }

                    _IDList.Add(forumInfo[@"forumid"]);
                }
            }
        }

        public void SaveLocation()
        {
            string strTempTitle = Title.Replace(@"'", @"''");

            if (UserLocationID > 0)
            {
                string strQuery = string.Format(@"
                                    UPDATE userlocation
                                    SET
                                        title = '{0}',
                                        locationremoteid = {1},
                                        list = '{2}',
                                        userlocationtype = '{3}',
                                        pagenumber = {4},
                                        perpage = {5}
                                    WHERE
                                        (userlocationid = {6});
                                ", strTempTitle,
                                 LocationRemoteID,
                                 string.Join(" ", IDList.ToArray()),
                                 LocationType.ToString().ToLower(),
                                 PageNumber,
                                 PerPage,
                                 UserLocationID
                                 );

                int iRows = DB.Instance.QueryWrite(strQuery);
            }
            else
            {
                string strQuery = string.Format(@"
                                    INSERT INTO userlocation
                                    (title,locationremoteid,list,userlocationtype,localuserid,pagenumber,perpage)
                                    VALUES
                                    ('{0}',{1},'{2}','{3}',{4},{5},{6})
                                ", strTempTitle,
                                 LocationRemoteID,
                                 string.Join(" ", IDList.ToArray()).Trim(),
                                 LocationType.ToString().ToLower(),
                                 Owner.LocalUserID,
                                 PageNumber,
                                 PerPage
                                 );

                int iRows = DB.Instance.QueryWrite(strQuery);

            }            

        }

        static public UserLocationT GetDefaultLocation(UserLocationType locType,User owner)
        {
            UserLocationT loc = null;

            if (locType == UserLocationType.FORUM)
            {
                Dictionary<string, string> info = new Dictionary<string, string>();

                info.Add(@"totalpages", "0");
                info.Add(@"perpage", "5");
                info.Add(@"pagenumber", "1");
                info.Add(@"title", "INDEX");                
                info.Add(@"locationremoteid", "-1");
                info.Add(@"list", string.Empty);
                info.Add(@"userlocationtype", @"forum");
                info.Add(@"localuserid", owner.LocalUserID.ToString());

                loc = new UserLocationT(info, owner);
            }
            else if (locType == UserLocationType.THREAD)
            {
                Dictionary<string, string> info = new Dictionary<string, string>();

                info.Add(@"totalpages", "0");
                info.Add(@"perpage", "5");
                info.Add(@"pagenumber", "1");
                info.Add(@"title", string.Empty);
                info.Add(@"locationremoteid", "0");
                info.Add(@"list", string.Empty);
                info.Add(@"userlocationtype", @"thread");
                info.Add(@"localuserid", owner.LocalUserID.ToString());

                loc = new UserLocationT(info, owner);
            }
            else if (locType == UserLocationType.POST)
            {
                Dictionary<string, string> info = new Dictionary<string, string>();

                info.Add(@"totalpages", "0");
                info.Add(@"perpage", "5");
                info.Add(@"pagenumber", "1");
                info.Add(@"title", string.Empty);
                info.Add(@"locationremoteid", "0");
                info.Add(@"list", string.Empty);
                info.Add(@"userlocationtype", @"post");
                info.Add(@"localuserid", owner.LocalUserID.ToString());

                loc = new UserLocationT(info, owner);
            }

            return loc;
        }



        static public UserLocationT LoadLocation(UserLocationType locType, User user)
        {
            UserLocationT retVal = null;
            string strLocType = locType.ToString().ToLower();

            try
            {
                Dictionary<string, string> locInfo = DB.Instance.QueryFirst(string.Format(@"
                                                    SELECT *
                                                    FROM userlocation
                                                    WHERE (localuserid = {0})
                                                    AND (userlocationtype = '{1}');
                                                    ", user.LocalUserID, strLocType));

                if (locInfo != null)
                {
                    retVal = new UserLocationT(locInfo, user);
                }
            }
            catch (System.Data.SQLite.SQLiteException ex)
            {
                log.Debug("Could not load userlocation", ex);
            }

            return retVal;
        }

        public void BuildParentThreadInfo(Dictionary<string, string> threadInfo)
        {
            long lEpoch = long.Parse(threadInfo[@"dateline"].ToString());

            DateTime dateline = new System.DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).ToLocalTime();
            dateline.AddSeconds(lEpoch);

            string strDate = dateline.ToShortDateString();
            string strTime = dateline.ToShortTimeString();

            if (dateline.Date == DateTime.Now.Date)
            {
                strDate = "Today";
            }
            else if (dateline.Date == DateTime.Now.Date.AddDays(-1))
            {
                strDate = "Yesterday";
            }

            _strTitle = string.Format("{0} - {1} created by {2}",
                    threadInfo[@"title"].ToString(),
                    strDate + " " + strTime,
                    threadInfo[@"postusername"].ToString()
                );

        }


    }
}

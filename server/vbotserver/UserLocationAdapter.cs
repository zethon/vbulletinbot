using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using log4net;

namespace vbotserver
{
    public enum UserLocationTypeEnum
    {
        FORUM,
        THREAD,
        POST
    }

    public class UserLocationAdapter
    {
        static ILog log = LogManager.GetLogger(typeof(UserLocationAdapter));

        public UserLocation UserLocation;
        public UserAdapter User;
        public UserLocationTypeEnum UserLocationType;

        public int UserLocationID
        {
            get { return (int)UserLocation.UserLocationID; }
        }

        public string Title
        {
            get { return UserLocation.Title; }
            set { UserLocation.Title = value; }
        }

        private List<string> _IDList = new List<string>();
        public List<string> IDList
        {
            get 
            { 
                return _IDList; 
            }
        }

        public int LocationRemoteID
        {
            get { return (int)UserLocation.LocationRemoteID; }
            set { UserLocation.LocationRemoteID = value; }
        }

        public int PageNumber
        {
            get { return (int)UserLocation.PageNumber; }
            set { UserLocation.PageNumber = value; }
        }

        public int PerPage
        {
            get { return (int)UserLocation.PerPage; }
            set { UserLocation.PerPage = value; }
        }

        public UserLocationAdapter()
        {
        }

        public UserLocationAdapter(UserLocation ul)
        {
            UserLocation = ul;

            UserLocationType = (UserLocationTypeEnum)Enum.Parse(typeof(UserLocationTypeEnum), ul.UserLocationType, true);
            ParseList(ul.List);
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

        public void ParseThreadList(VBotService.Thread[] list)
        {
            IDList.Clear();

            foreach (VBotService.Thread thread in list)
            {
                _IDList.Add(thread.ThreadID.ToString());
            }
        }

        public void ParseForumsList(VBotService.Forum[] forums)
        {
            IDList.Clear();

            foreach (VBotService.Forum forum in forums)
            {
                IDList.Add(forum.ForumID.ToString());
            }
        }

        public void SetCurrentForum(VBotService.Forum forum)
        {
            LocationRemoteID = forum.ForumID;
            Title = forum.Title;
        }

        public void SaveLocation()
        {
            if (UserLocation.UserLocationID > 0)
            {
                UserLocation.List = string.Join(" ", IDList.ToArray());
                Database.Instance.SubmitChanges();
            }
            else
            {
                UserLocation.List = string.Join(" ", IDList.ToArray());
                Database.Instance.UserLocations.InsertOnSubmit(UserLocation);
            }

            Database.Instance.SubmitChanges();
            log.DebugFormat("Saved Location LocalUserID '{0}', UserLocationType '{1}', LocationRemoteID '{2}'",
                    UserLocation.LocalUserID, UserLocation.UserLocationType, UserLocation.LocationRemoteID);
        }

        static public UserLocationAdapter GetDefaultLocation(UserLocationTypeEnum locType,UserAdapter owner)
        {
            UserLocationAdapter loc = null;

            if (locType == UserLocationTypeEnum.FORUM)
            {
                UserLocation ul = new UserLocation();
                ul.PerPage = 5;
                ul.PageNumber = 1;
                ul.Title = @"INDEX";
                ul.LocationRemoteID = -1;
                ul.List = string.Empty;
                ul.UserLocationType = @"forum";
                ul.LocalUserID = owner.LocalUser.LocalUserID;
                loc = new UserLocationAdapter(ul);
            }
            else if (locType == UserLocationTypeEnum.THREAD)
            {
                UserLocation ul = new UserLocation();
                ul.PerPage = 5;
                ul.PageNumber = 1;
                ul.Title = string.Empty;
                ul.LocationRemoteID = 0;
                ul.List = string.Empty;
                ul.UserLocationType = @"thread";
                ul.LocalUserID = owner.LocalUser.LocalUserID;
                loc = new UserLocationAdapter(ul);
            }
            else if (locType == UserLocationTypeEnum.POST)
            {
                UserLocation ul = new UserLocation();
                ul.PerPage = 5;
                ul.PageNumber = 1;
                ul.Title = string.Empty;
                ul.LocationRemoteID = 0;
                ul.List = string.Empty;
                ul.UserLocationType = @"post";
                ul.LocalUserID = owner.LocalUser.LocalUserID;
                loc = new UserLocationAdapter(ul);
            }

            return loc;
        }

        static public UserLocationAdapter LoadLocation(UserLocationTypeEnum locType, UserAdapter user)
        {
            UserLocationAdapter retval = null;

            UserLocation ul = Database.Instance.UserLocations.FirstOrDefault(
                l => l.LocalUserID == user.LocalUser.LocalUserID
                    && l.UserLocationType == locType.ToString().ToLower());

            if (ul != null)
            {
                retval = new UserLocationAdapter(ul);
            }

            return retval;
        }
    }
}

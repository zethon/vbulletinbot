using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using log4net;
using VBulletinBot.VBotService;

namespace VBulletinBot
{
	public partial class UserLocation
	{
		static ILog log = LogManager.GetLogger(typeof(UserLocation));

		public LocalUser User;
		public List<string> IDList = new List<string>();

		public UserLocationType GetUserLocationType()
		{
			return (UserLocationType)Enum.Parse(typeof(UserLocationType), UserLocationType, true);
		}

		public void SetUserLocationType(UserLocation ut)
		{
			UserLocationType = ut.ToString();
		}

		public void SetCurrentForum(Forum forum)
		{
			LocationRemoteID = forum.ForumID;
			Title = forum.Title;
		}

		public void ParseList(string strList)
		{
			IDList.Clear();
			string[] IDs = Regex.Split(strList, @"[\s+]");

			foreach (string id in IDs)
			{
				if (id.Length > 0)
				{
					IDList.Add(id);
				}
			}
		}

        public void ParseIDList(VBEntity[] list)
        {
            IDList.Clear();

            foreach (VBEntity item in list)
            {
                IDList.Add(item.DatabaseID.ToString());
            }
        }

		public void SaveLocation()
		{
			if (UserLocationID > 0)
			{
				List = string.Join(" ", IDList.ToArray());
				VBotDB.Instance.SubmitChanges();
			}
			else
			{
				List = string.Join(" ", IDList.ToArray());
				VBotDB.Instance.UserLocations.InsertOnSubmit(this);
			}

			VBotDB.Instance.SubmitChanges();
			log.DebugFormat("Saved Location LocalUserID '{0}', UserLocationType '{1}', LocationRemoteID '{2}'",
					LocalUserID, UserLocationType, LocationRemoteID);
		}

		static public UserLocation LoadLocation(UserLocationType locType, LocalUser user)
		{
			UserLocation ul = VBotDB.Instance.UserLocations.FirstOrDefault(
				l => l.LocalUserID == user.LocalUserID
					&& l.UserLocationType == locType.ToString().ToLower());

			if (ul != null)
			{
				ul.ParseList(ul.List);
			}

			return ul;
		}

		static public UserLocation GetDefaultLocation(UserLocationType locType, LocalUser owner)
		{
			UserLocation ul = new UserLocation();

            
			if (locType == VBulletinBot.UserLocationType.FORUM)
			{
				ul.PerPage = 5;
				ul.PageNumber = 1;
				ul.Title = @"INDEX";
				ul.LocationRemoteID = -1;
				ul.List = string.Empty;
				ul.UserLocationType = @"forum";
				ul.LocalUserID = owner.LocalUserID;
			}
            else if (locType == VBulletinBot.UserLocationType.THREAD)
			{
				ul.PerPage = 5;
				ul.PageNumber = 1;
				ul.Title = string.Empty;
				ul.LocationRemoteID = 0;
				ul.List = string.Empty;
				ul.UserLocationType = @"thread";
				ul.LocalUserID = owner.LocalUserID;
			}
            else if (locType == VBulletinBot.UserLocationType.POST)
			{
				ul.PerPage = 5;
				ul.PageNumber = 1;
				ul.Title = string.Empty;
				ul.LocationRemoteID = 0;
				ul.List = string.Empty;
				ul.UserLocationType = @"post";
				ul.LocalUserID = owner.LocalUserID;
			}

			return ul;
		}
	}
}

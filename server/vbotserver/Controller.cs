using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.IO;
using System.Data.SQLite;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;
using log4net;

namespace vbotserver
{
    enum InputStateEnum
    {
        None,
        Waiting,
        Responded,
        TimeOut
    }

    class InputState
    {
        public InputStateEnum State = InputStateEnum.None;

        public string _strPageText = string.Empty;
        public string PageText
        {
            get { return _strPageText; }
            set { _strPageText = value; }
        }


        public InputState(InputStateEnum en)
        {
            State = en;
        }
    }

    class Controller
    {
        Dictionary<int, InputState> _inputs = new Dictionary<int, InputState>();
        
        static ILog log = LogManager.GetLogger(typeof(Controller));

        string _strStatePath = string.Empty;
        CommandClass _commands = null;

        ConnectionComposite _conComp = null;
        public ConnectionComposite Connections
        {
            get { return _conComp; }
        }

        private bool _bAppQuit = false;
        public bool AppQuit
        {
            get { return _bAppQuit; }
            set { _bAppQuit = value; }
        }

        System.Timers.Timer _notTimer = null;
        System.Timers.Timer NotificationTimer
        {
            get { return _notTimer; }
        }


        public bool Init(XDocument config)
        {
            // initialize the path we store user states in
            var xPath = (from path in config.Descendants(@"userstatepath")
                         select path).Single();
            _strStatePath = xPath.Value;

            if (!Directory.Exists(_strStatePath))
            {
                Directory.CreateDirectory(_strStatePath);
                log.InfoFormat("User State Path created: {0}", _strStatePath);
            }

            _conComp = ConnectionComposite.MakeConnectionComposite(config);
            foreach (Connection conn in _conComp.Connections)
            {
                conn.OnConnect += new OnConnectHandler(OnConnectCallback);
                conn.OnMessage += new Connection.OnMessageHandler(OnMessageCallback);
                conn.OnSendMessage += new Connection.OnSendMessageHandler(OnSendMessageCallback);
                conn.OnDisconnect += new Connection.OnDisconnectHandler(OnDisconnectCallback);
            }

            _commands = new CommandClass(this);

            var wsvc = (from xElem in config.Descendants(@"webserviceurl")
                        select xElem).Single();

            var wsvcpw = (from xElem in config.Descendants(@"webservicepw")
                        select xElem).Single();

            VB.Instance.ServiceURL = wsvc.Value;
            VB.Instance.ServicePassword = wsvcpw.Value;

             // start the notification timer
            _notTimer = new System.Timers.Timer(1000 * 60); // every 5 minutes
            _notTimer.Elapsed += new ElapsedEventHandler(_notTimer_Elapsed);
            _notTimer.Enabled = true;

            return true;
        }

        public void _notTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            log.Info("Notification Timer Elapsed()");
            if (_conComp.Connections != null && _conComp.Connections.Count() > 0)
            {
                VBRequestResult r = VB.Instance.GetPostNotifications(true);

                if (r.ResultCode == VBRequestResultCode.Success)
                {
                    List<Dictionary<string, string>> nots = r.Data as List<Dictionary<string, string>>;
                    if (nots != null)
                    {
                        log.Info(string.Format("{0} post notifications recieved", nots.Count()));
                        foreach (Dictionary<string, string> infoDict in nots)
                        {
                            Connection c = _conComp.GetConnection(infoDict[@"instantimservice"]);
                            string strScreenName = infoDict[@"instantimscreenname"];

                            VBPost post = new VBPost(infoDict);
                            string strResponse = c.NewLine + "Forum: '" + infoDict["title"] + "'" + c.NewLine + "Thread: '" + infoDict["threadtitle"] + "'" + c.NewLine;
                            strResponse += FetchPostBit(post, c.NewLine) + c.NewLine;
                            strResponse += "(Type 'gt " + infoDict["threadid"] + "' to go to the thread. Type 'im off' to turn off IM Notification)";
                            
                            c.SendMessage(new InstantMessage(strScreenName, strResponse));
                            Thread.Sleep(2000);
                        }
                    }
                    else
                    {// TODO: error checking

                    }
                }
            }
            else
            {
                log.Info("No open connections.");
            }
        }

        void OnConnectCallback(Connection conn)
        {
            log.InfoFormat("Connection {0} connected", conn.GetType().Name);
        }

        void OnDisconnectCallback(Connection conn)
        {
            log.InfoFormat("Connection {0} disconnected", conn.GetType().Name);
        }

        void OnSendMessageCallback(Connection conn, InstantMessage im)
        {
            log.InfoFormat("OUTMSG ({0}) {1}: {2}", conn.GetType().Name, im.User, im.Text);
        }

        public void OnMessageCallback(Connection conn, InstantMessage im)
        {
            log.InfoFormat("INMSG ({0}) << {1}: {2}", conn.GetType().Name, im.User, im.Text);

            lock (this)
            {
                try
                {
                    IMUserInfo creds = new IMUserInfo(im.User, conn.Alias,conn);
                    User user = LoadUser(creds);

                    if (user != null && user.LocalUserID > 0)
                    {

                        if (_inputs.ContainsKey(user.LocalUserID) && _inputs[user.LocalUserID].State == InputStateEnum.Waiting)
                        { // waiting for input?

                            InputState ist = new InputState(InputStateEnum.Responded);
                            ist.PageText = im.Text;
                            _inputs[user.LocalUserID] = ist;
                        }
                        else
                        { // the user is at the 'main menu'
                            
                            Result lastRes = new Result();
                            string[] strCommands = Regex.Split(im.Text, @"\;");

                            foreach (string strCommand in strCommands)
                            {
                                lastRes = DoCommand(strCommand, user);

                                if (lastRes.Code != ResultCode.Success)
                                {
                                    break;
                                }
                            }

                            if (lastRes != null && (lastRes.Code == ResultCode.Success || lastRes.Code == ResultCode.Error))
                            {
                                conn.SendMessage(new InstantMessage(im.User, lastRes.Message));
                            }
                        }
                    }
                    else
                    {
                        string strResponse = @"Unknown screen name. Please add this screen name to your user profile.";
                        conn.SendMessage(new InstantMessage(im.User,strResponse));
                        log.Error(@"Could not load user.");
                    }
                }
                catch (Exception ex)
                {
                    log.Debug("Something bad",ex);
                }
            }
        }

        public Result DoCommand(string strCommand,User user)
        {
            bool @bool = false;
            Result retval = new Result();
            CommandParser parser = new CommandParser(strCommand);

            if (parser.Parse())
            {
                int iListChoice = 0;

                if (int.TryParse(parser.ApplicationName, out iListChoice) && iListChoice > 0)
                { // user entered a number, let's deal with the lastlists

                    Dictionary<string, string> lastList = DB.Instance.QueryFirst("SELECT * FROM userlastlist WHERE (localuserid = " + user.LocalUserID.ToString() + ")");
                    if (lastList.ContainsKey(@"name"))
                    {
                        switch (lastList[@"name"])
                        {
                            case @"forum":
                                retval = GotoForumIndex(iListChoice, user);
                                break;

                            case @"thread":
                                retval = GotoThreadIndex(iListChoice, user);
                                break;

                            case @"post":
                                retval = GotoPostIndex(iListChoice, user);
                                break;
                        }
                    }
                    else
                    {
                        retval = new Result(ResultCode.Error, @"Use `lf`,`lt` or `lp` to browse forums, threads and posts.");
                    }
                }
                else
                { // assume a command was entered
                    switch (parser.ApplicationName.ToLower())
                    {
                        case "/":
                            retval = GotoForumIndex(-1, user, true);
                            break;

                        case @".":
                            retval = GotoParentForum(user);
                            break;

                        case @"gt":
                            retval = GotoThread(user, parser.Parameters);
                            break;

                        case @"im":
                            retval = TurnOnOffAutoIMS(user, parser.Parameters);
                            break;

                        case "lf":
                            retval = ListForum(user);
                            break;

                        case @"lp":
                            retval = ListPosts(user, parser.Parameters);
                            break;

                        case "lt":
                            retval = ListThreads(user, parser.Parameters);
                            break;

                        case "mfr":
                            retval = MarkRead(user, @"forum");
                        break;

                        case "mtr":
                            retval = MarkRead(user, @"thread");
                        break; 

                        case @"n":
                            retval = GotoNextPost(user, true);
                            break;

                        case @"p":
                            retval = GotoNextPost(user, false);
                            break;

                        case @"r":
                            retval = ThreadReply(user);
                        break;

                        case "sub":
                            retval = SubscribeThread(user, parser.Parameters);
                        break;

                        case "unsub":
                            retval = UnsubscribeThread(user, parser.Parameters);
                        break;

                        case @"whereami":
                            retval = WhereAmI(user);
                        break;

                        case "whoami":
                            retval = WhoAmI(user.UserConnection, user.UserConnectionName, user);
                            break;

                        default:
                            retval = new Result(ResultCode.Error, @"Unknown command. Please see http://code.google.com/p/vbulletinbot/ for help.");
                        break;
                    }
                }
            }

            return retval;
        }

        public User LoadUser(IMUserInfo imuserinfo)
        {
            User user = new User();

            // load the userinfo from the local db
            Dictionary<string, string> localuser = DB.Instance.QueryFirst(string.Format(@" SELECT * FROM localuser WHERE (service = '{0}') AND (screenname = '{1}')", imuserinfo.ServiceAlias, imuserinfo.ScreenName));

            if (localuser == null || !localuser.ContainsKey(@"localuserid"))
            { // user does not exist in the local db

                Dictionary<string, string> vbuserInfo = new Dictionary<string, string>();
                vbuserInfo = VB.Instance.WhoAMI(imuserinfo.ScreenName, imuserinfo.ServiceAlias);

                if (vbuserInfo.ContainsKey(@"userid"))
                { // user exists on vbulletin, create localdb row

                    int iRows = DB.Instance.QueryWrite(string.Format(@"
                                    INSERT INTO localuser
                                    (lastupdate,service,screenname,boarduserid)
                                    VALUES
                                    ({0},'{1}','{2}',{3})
                                    ", 0,
                                     imuserinfo.ServiceAlias,
                                     imuserinfo.ScreenName,
                                     vbuserInfo[@"userid"].ToString()));

                    if (iRows > 0)
                    {
                        int iLocalUserID = DB.Instance.LastInsertID();
                        user.DBUser = DB.Instance.QueryFirst(string.Format("SELECT * FROM localuser WHERE (localuserid = {0})", iLocalUserID));
                        user.VBUser = vbuserInfo;
                    }
                    else
                    {
                        log.Error("Could not insert new user into `localuser` table.");
                    }
                }
                else
                {
                    log.Error("No `userid` defined from VB::WHoAMI()");
                }
            }
            else
            { // user exists in the local db
                user.DBUser = localuser;                
            }

            user.UserConnection = imuserinfo.IMConnection;
            user.UserConnectionName = imuserinfo.ScreenName;
            return user;
        }

        public string FetchPostBit(VBPost post, string strNewLine)
        {
            string strResponse = string.Empty;

            string strPageText = post.PageText;
            strResponse += strNewLine;
            strResponse += string.Format("{0}", strPageText) + strNewLine;
            strResponse += string.Format("{0} by {1}", post.GetFriendlyDate(), post.Username);

            return strResponse;
        }

        public Result GotoForumIndex(int iIndex, User user)
        {
            return GotoForumIndex(iIndex, user, false);
        }

        public Result GotoForumIndex(int iIndex, User user, bool bGotoRoot)
        {
            Result retval = null;
            UserLocation curLoc = UserLocation.LoadLocation(UserLocationType.FORUM, user);

            if (curLoc == null)
            { // this location does not exist

                curLoc = UserLocation.GetDefaultLocation(UserLocationType.FORUM, user);
                IMUserInfo iminfo = new IMUserInfo(user.UserConnectionName, user.UserConnection.Alias, user.UserConnection);
                VBRequestResult res = VB.Instance.ListForums(iminfo, curLoc.LocationRemoteID);

                if (res.ResultCode == VBRequestResultCode.Success)
                {
                    List<Dictionary<string, string>> forums = res.Data as List<Dictionary<string, string>>;

                    curLoc.ParseForumsList(forums);
                    curLoc.SaveLocation();
                    curLoc.UserLocationID = DB.Instance.LastInsertID();
                }
            }

            if (iIndex > curLoc.IDList.Count || iIndex < 0 && !bGotoRoot)
            {
                retval = new Result(ResultCode.Error, @"Invalid Forums Index");
            }
            else
            {
                int iNewForumID = 0;
                string strNewForumID = "-1";
                if (!bGotoRoot)
                {
                    strNewForumID = curLoc.IDList[--iIndex];
                }

                if (!int.TryParse(strNewForumID, out iNewForumID))
                {
                    throw new Exception(@"Corrupt ID in IDList");
                }

                // set the FORUMS location
                IMUserInfo iminfo = new IMUserInfo(user.UserConnectionName, user.UserConnection.Alias, user.UserConnection);
                VBRequestResult res = VB.Instance.ListForums(iminfo, iNewForumID);

                // TODO: error checking of the above call
                List<Dictionary<string, string>> forums = res.Data as List<Dictionary<string, string>>;
                curLoc.ParseForumsList(forums);
                curLoc.SaveLocation();

                // reset the THREAD location
                UserLocation threadLoc = UserLocation.LoadLocation(UserLocationType.THREAD, user);

                if (threadLoc == null)
                {
                    threadLoc = UserLocation.GetDefaultLocation(UserLocationType.THREAD, user);
                }

                threadLoc.Title = curLoc.Title;
                threadLoc.LocationRemoteID = iNewForumID;
                threadLoc.SaveLocation();

                retval = ListForum(user, forums);
            }

            return retval;
        }

        public Result GotoNextPost(User user, bool bGotoNext)
        {
            Result rs = null;

            UserLocation curThreadLocation = UserLocation.LoadLocation(UserLocationType.POST, user);

            if (curThreadLocation != null)
            {
                Dictionary<string, string> postIndex = DB.Instance.QueryFirst("SELECT * FROM userpostindex WHERE (localuserid = " + user.LocalUserID.ToString() + ")");

                if (postIndex.ContainsKey(@"postindex"))
                {
                    int iPostIndex = int.Parse(postIndex[@"postindex"]);
                    if (bGotoNext)
                    {
                        iPostIndex++;
                    }
                    else
                    {
                        iPostIndex--;
                    }
                    rs = GotoPostIndex(iPostIndex, user);
                }
                else
                {// userpostindex table is fucked

                }
            }
            else
            {
                rs = new Result(ResultCode.Error, @"No active thread set. Use `lt` and browse to a thread");
            }

            return rs;
        }

        public Result GotoParentForum(User user)
        {
            Result ret = null;
            IMUserInfo imuserinfo = new IMUserInfo(user.UserConnectionName, user.UserConnection.Alias, user.UserConnection);
            UserLocation forumLoc = UserLocation.LoadLocation(UserLocationType.FORUM, user);

            if (forumLoc != null)
            {
                VBRequestResult res = VB.Instance.ListParentForums(imuserinfo, forumLoc.LocationRemoteID);

                if (res.ResultCode == VBRequestResultCode.Success && res.Data != null)
                {
                    List<Dictionary<string, string>> forums = res.Data as List<Dictionary<string, string>>;
                    forumLoc.ParseForumsList(forums);
                    forumLoc.SaveLocation();

                    // reset the THREAD location
                    UserLocation threadLoc = UserLocation.LoadLocation(UserLocationType.THREAD, user);

                    if (threadLoc != null)
                    {
                        threadLoc.Title = forumLoc.Title;
                        threadLoc.LocationRemoteID = forumLoc.LocationRemoteID;
                        threadLoc.SaveLocation();
                    }

                    ret = ListForum(user, forums);
                }
                else
                {
                    ret = new Result(ResultCode.Error, @"Error navigating to parent forum. Please try again.");
                    // TODO: log error?
                }
            }
            else
            {
                ret = new Result(ResultCode.Error, @"No current forum set. Use `lf` to select a forum.");
            }

            return ret;
        }

        public Result GotoPostIndex(int iChoice, User user)
        {
            Result rs = null;
            UserLocation curPostLoc = UserLocation.LoadLocation(UserLocationType.POST, user);

            if (curPostLoc != null)
            {
                IMUserInfo iminfo = new IMUserInfo(user.UserConnectionName, user.UserConnection.Alias, user.UserConnection);
                VBRequestResult r = VB.Instance.GetPostByIndex(iminfo, curPostLoc.LocationRemoteID, iChoice);
                if (r.ResultCode == VBRequestResultCode.Success)
                {
                    VBPost post = r.Data as VBPost;
                    if (post != null)
                    {
                        string strText = FetchPostBit(post, user.UserConnection.NewLine);
                        user.SaveLastPostIndex(iChoice);
                        rs = new Result(ResultCode.Success, strText);
                    }
                    else
                    {
                        rs = new Result(ResultCode.Error, @"Invalid post index.");
                    }
                }
                else
                {
                    rs = new Result(ResultCode.Error, @"Invalid request.");
                }
            }
            else
            {// no location for a thread exists in the local db

                rs = new Result(ResultCode.Error, @"Invalid thread id. Use `lt` and browse to a thread");
            }
            return rs;
        }

        public Result GotoThread(User user, string[] options)
        {
            Result rs = null;

            if (options != null && options.Count() > 0)
            {
                int iNewThreadID = 0;

                if (int.TryParse(options[0], out iNewThreadID))
                {
                    UserLocation postLoc = UserLocation.LoadLocation(UserLocationType.POST, user);
                    if (postLoc == null)
                    {
                        postLoc = UserLocation.GetDefaultLocation(UserLocationType.POST, user);
                    }

                    VBThread thread = null;
                    IMUserInfo imuserinfo = new IMUserInfo(user.UserConnectionName, user.UserConnection.Alias, user.UserConnection);
                    VBRequestResult r = VB.Instance.ListPosts(imuserinfo, iNewThreadID, postLoc.PageNumber, postLoc.PerPage, out thread);

                    if (r.ResultCode == VBRequestResultCode.Success)
                    {
                        List<VBPost> posts = r.Data as List<VBPost>;

                        // TODO: build the postLoc location title here
                        postLoc.Title = string.Format("{0} - {1} created by {2}",
                                        Regex.Replace(thread.ThreadTitle, @"[\']", string.Empty),
                                        thread.GetFriendlyDate(thread.DateLine),
                                        thread.PostUsername);

                        postLoc.LocationRemoteID = iNewThreadID;
                        postLoc.ParsePostList(posts);
                        postLoc.SaveLocation();

                        rs = ListPosts(user, new string[] { postLoc.PageNumber.ToString(), postLoc.PerPage.ToString() }, posts, thread);
                    }
                    else
                    {
                        return new Result(ResultCode.Error, "Invalid thread.");
                    }

                }
                else
                {
                    return new Result(ResultCode.Error, "Inavlid threadid.");
                }

            }
            else
            {
                return new Result(ResultCode.Error, "Please specify which thread you wish to navigate to.");
            }

            return rs;
        }

        public Result GotoThreadIndex(int iChoice, User user)
        {
            Result rs = null;
            UserLocation curLoc = UserLocation.LoadLocation(UserLocationType.THREAD, user);

            if (curLoc != null)
            {
                if (iChoice > curLoc.IDList.Count)
                { // invalid choice

                    rs = new Result(ResultCode.Error, @"Invalid Thread Index");
                }
                else
                {
                    int iNewThreadID = 0;
                    string strNewThreadID = curLoc.IDList[iChoice - 1];

                    UserLocation postLoc = UserLocation.LoadLocation(UserLocationType.POST, user);
                    if (postLoc == null)
                    {
                        postLoc = UserLocation.GetDefaultLocation(UserLocationType.POST, user);
                    }

                    if (int.TryParse(strNewThreadID, out iNewThreadID))
                    {
                        VBThread thread = null;
                        IMUserInfo imuserinfo = new IMUserInfo(user.UserConnectionName, user.UserConnection.Alias, user.UserConnection);
                        VBRequestResult r = VB.Instance.ListPosts(imuserinfo, iNewThreadID, postLoc.PageNumber, postLoc.PerPage, out thread);

                        if (r.ResultCode == VBRequestResultCode.Success)
                        {
                            List<VBPost> posts = r.Data as List<VBPost>;

                            // TODO: build the postLoc location title here
                            postLoc.Title = string.Format("{0} - {1} created by {2}",
                                            Regex.Replace(thread.ThreadTitle, @"[\']", string.Empty),
                                            thread.GetFriendlyDate(thread.DateLine),
                                            thread.PostUsername);

                            postLoc.LocationRemoteID = iNewThreadID;
                            postLoc.ParsePostList(posts);
                            postLoc.SaveLocation();

                            rs = ListPosts(user, new string[] { postLoc.PageNumber.ToString(), postLoc.PerPage.ToString() }, posts, thread);
                        }
                    }
                    else
                    { // there's a non-integer in the IDList, something is seriously fucked
                        rs = new Result(ResultCode.Error, @"This should be an excetion");
                    }
                }
            }
            else
            {// no location for a thread exists in the local db
                rs = new Result(ResultCode.Error, @"Invalid forum id. Use `lf` and browse to a forum");
            }

            return rs;
        }

        public Result ListForum(User user)
        {
            return ListForum(user, null);
        }

        public Result ListForum(User user, List<Dictionary<string, string>> forums)
        {
            lock (this)
            {
                Result resval = null;
                IMUserInfo iminfo = new IMUserInfo(user.UserConnectionName, user.UserConnection.Alias, user.UserConnection);
                UserLocation loc = UserLocation.LoadLocation(UserLocationType.FORUM, user);

                if (loc == null)
                { // this location does not exist

                    loc = UserLocation.GetDefaultLocation(UserLocationType.FORUM, user);
                    VBRequestResult res = VB.Instance.ListForums(iminfo, loc.LocationRemoteID);
                    forums = res.Data as List<Dictionary<string, string>>;

                    loc.ParseForumsList(forums);
                    loc.SaveLocation();
                }

                if (forums == null)
                {
                    VBRequestResult res = VB.Instance.ListForums(iminfo, loc.LocationRemoteID);
                    forums = res.Data as List<Dictionary<string, string>>;
                }

                Connection conn = user.UserConnection;
                string strResponse = conn.NewLine + "Subforums in `" + loc.Title + "`" + conn.NewLine;
                bool bForumsExist = false;
                string strIsNew = string.Empty;

                if (forums.Count > 0)
                {
                    int iCount = 1;
                    foreach (Dictionary<string, string> foruminfo in forums)
                    {
                        if (foruminfo.ContainsKey(@"iscurrent") && foruminfo[@"iscurrent"] == "1")
                        {
                            continue;
                        }


                        strIsNew = string.Empty;
                        if (foruminfo.ContainsKey(@"isnew") && foruminfo[@"isnew"].ToLower() == "new")
                        {
                            strIsNew = "*";

                        }

                        bForumsExist = true;
                        strResponse += iCount.ToString() + ". " + strIsNew + foruminfo[@"title"] + conn.NewLine;
                        iCount++;
                    }

                    user.SaveLastList(@"forum");
                }

                if (!bForumsExist)
                {
                    strResponse += "No subforums";
                    resval = new Result(ResultCode.Error, strResponse);
                }
                else
                {
                    resval = new Result(ResultCode.Success, strResponse);
                }

                return resval;
            }
        }

        public Result ListPosts(User user, string[] options)
        {
            return ListPosts(user, options, null, null);
        }

        public Result ListPosts(User user, string[] options, List<VBPost> posts, VBThread thread)
        {
            lock (this)
            {
                ResultCode rc = ResultCode.Unknown;
                UserLocation loc = UserLocation.LoadLocation(UserLocationType.POST, user);

                if (loc == null)
                {
                    Result ret = new Result(ResultCode.Error, @"No active thread. Use `lt` to browse to a thread.");
                    return ret;
                }

                int iPageNumber = 0;
                int iPerPage = 0;

                if (options.Length < 1 || !int.TryParse(options[0], out iPageNumber))
                {
                    iPageNumber = 1;
                }

                if (options.Length < 2 || !int.TryParse(options[1], out iPerPage))
                {
                    iPerPage = 5;
                }

                if (iPerPage > 30)
                {
                    iPerPage = 30;
                }


                if (posts == null || thread == null)
                {
                    IMUserInfo iminfo = new IMUserInfo(user.UserConnectionName, user.UserConnection.Alias, user.UserConnection);
                    VBRequestResult r = VB.Instance.ListPosts(iminfo, loc.LocationRemoteID, iPageNumber, iPerPage, out thread);

                    if (r.ResultCode == VBRequestResultCode.Success)
                    {
                        posts = r.Data as List<VBPost>;
                    }
                }

                Connection conn = user.UserConnection;
                string strResponse = conn.NewLine + "Thread: " + loc.Title + conn.NewLine;

                double dTotalPosts = (double)(thread.ReplyCount + 1);
                int iTotalPages = (int)Math.Ceiling(dTotalPosts / (double)iPerPage);

                if (iPageNumber <= iTotalPages)
                {
                    strResponse += string.Format("Posts: Page {0} of {1} ({2} per page)", iPageNumber, iTotalPages, iPerPage) + conn.NewLine;
                }

                string strIsNew = string.Empty;
                if (posts.Count > 0)
                {
                    int iCount = ((iPageNumber - 1) * iPerPage) + 1;
                    foreach (VBPost postInfo in posts)
                    {
                        strIsNew = string.Empty;
                        if (postInfo.IsNew)
                        {
                            strIsNew = "*";
                        }

                        strResponse += string.Format("{0}. {1}\"{2}\" - {3} by {4}" + conn.NewLine,
                                            iCount,
                                            strIsNew,
                                            postInfo.GetShortPostText(),
                                            postInfo.GetFriendlyDate(),
                                            postInfo.Username
                                        );

                        iCount++;
                    }

                    rc = ResultCode.Success;
                }
                else
                {
                    strResponse += "Invalid page number";
                    rc = ResultCode.Error;
                }

                user.SaveLastList(@"post");
                user.SaveLastPostIndex(1);
                return new Result(rc, strResponse);
            }
        }

        public Result ListThreads(User user, string[] options)
        {
            return ListThreads(user, options, null);
        }

        public Result ListThreads(User user, string[] options, List<VBThread> threads)
        {
            lock (this)
            {
                ResultCode rc = ResultCode.Unknown;

                Connection conn = user.UserConnection;
                string strResponse = string.Empty;
                UserLocation loc = UserLocation.LoadLocation(UserLocationType.THREAD, user);

                if (loc == null)
                {
                    Result ret = new Result(ResultCode.Error, @"No active forum. Use `lf` to browse to a forum.");
                    return ret;
                }

                if (loc != null)
                {
                    int iPageNumber = 0;
                    int iPerPage = 0;

                    if (options.Length < 1 || !int.TryParse(options[0], out iPageNumber))
                    {
                        iPageNumber = 1;
                    }

                    if (options.Length < 2 || !int.TryParse(options[1], out iPerPage))
                    {
                        iPerPage = 5;
                    }

                    if (iPerPage > 30)
                    {
                        iPerPage = 30;
                    }


                    if (threads == null)
                    {
                        IMUserInfo imuserinfo = new IMUserInfo(user.UserConnectionName, user.UserConnection.Alias, user.UserConnection);
                        VBRequestResult r = VB.Instance.ListThreads(imuserinfo, loc.LocationRemoteID, iPageNumber, iPerPage);

                        if (r.ResultCode != VBRequestResultCode.Success)
                        {
                            return new Result(ResultCode.Error, "Could not view threads. Please try again.");
                        }

                        threads = r.Data as List<VBThread>;
                    }

                    bool bShowIDs = false;
                    foreach (string strOption in options)
                    {
                        if (strOption.ToLower() == "ids")
                        {
                            bShowIDs = true;
                            break;
                        }
                    }

                    strResponse += conn.NewLine + "Threads in `" + loc.Title + "`" + conn.NewLine;

                    bool bForumsExist = false;
                    string strIsNew = string.Empty;
                    string strIsSubscribed = string.Empty;

                    if (threads.Count > 0)
                    {
                        VBThread firstThread = threads[0];
                        int iTotalPages = (int)Math.Ceiling((double)firstThread.TotalThreads / (double)iPerPage);
                        iTotalPages += 1;

                        if (iPageNumber <= iTotalPages)
                        {
                            strResponse += string.Format("Page {0} of {1} ({2} per page)", iPageNumber, iTotalPages, iPerPage) + conn.NewLine;
                        }

                        int iCount = 1;
                        foreach (VBThread thread in threads)
                        {
                            string strID = string.Empty;
                            if (bShowIDs)
                            {
                                strID = " [" + thread.ThreadID.ToString() + "]";
                            }

                            strIsNew = string.Empty;
                            if (thread.IsNew)
                            {
                                strIsNew = "*";
                            }

                            strIsSubscribed = thread.SubscribeThreadID > 0 ? "#" : string.Empty;

                            bForumsExist = true;

                            strResponse += string.Format("{0}. {1}{2}{3}{4} ({5}) - {6} by {7}" + conn.NewLine,
                                                iCount,
                                                strIsSubscribed,
                                                strIsNew,
                                                thread.ThreadTitle,
                                                strID,
                                                thread.ReplyCount + 1,
                                                thread.GetFriendlyDate(),
                                                thread.LastPoster
                                            );
                            iCount++;
                        }
                    }

                    if (!bForumsExist)
                    {
                        strResponse += "There are no threads in this forum.";
                        rc = ResultCode.Error;
                    }
                    else
                    {
                        loc.PageNumber = iPageNumber;
                        loc.PerPage = iPerPage;
                        loc.ParseThreadList(threads);
                        loc.SaveLocation();
                        rc = ResultCode.Success;
                    }
                }
                else
                {
                    strResponse = "No forum selected. (Try typing `lf` and selecting a forum.)";
                    rc = ResultCode.Error;
                }

                user.SaveLastList(@"thread");
                return new Result(rc, strResponse);
            }
        }

        public Result WhereAmI(User user)
        {
            string strNewLine = user.UserConnection.NewLine;
            string strResponse = strNewLine;

            UserLocation forumLoc = UserLocation.LoadLocation(UserLocationType.FORUM, user);
            strResponse += "Current Forum: ";
            if (forumLoc != null)
            {
                strResponse += string.Format("{0} ({1}){2}", forumLoc.Title, forumLoc.LocationRemoteID, strNewLine);
            }
            else
            {
                strResponse += "None" + strNewLine;
            }


            UserLocation threadLoc = UserLocation.LoadLocation(UserLocationType.POST, user);
            strResponse += "Current Thread: ";
            if (threadLoc != null)
            {
                strResponse += string.Format("{0} ({1}){2}", threadLoc.Title, threadLoc.LocationRemoteID, strNewLine);
            }
            else
            {
                strResponse += "None" + strNewLine;
            }

            return new Result(ResultCode.Success, strResponse);
        }

        public Result WhoAmI(Connection conn, string strUsername, User user)
        {
            Result retval;
            string strResponse = string.Empty;

            try
            {
                user.VBUser = VB.Instance.WhoAMI(strUsername, conn.Alias);
                if (user.VBUser.ContainsKey(@"username"))
                {
                    strResponse = conn.NewLine
                                      + "VBUserID: " + user.VBUserID.ToString() + conn.NewLine
                                      + "VBUsername: " + user.VBUser[@"username"].ToString();
                }
                else
                {
                    strResponse = @"Uknown user. Please update your profile.";
                }

                retval = new Result(ResultCode.Success, strResponse);
            }
            catch (Exception e)
            {
                retval = new Result(ResultCode.Error, e.Message);
                log.Debug(@"WhoAMI failed", e);
                return retval;
            }

            return retval;
        }

        public Result UnsubscribeThread(User user, string[] options)
        {
            Result ret = null;
            object[] objs = { user, options };

            if (options.Count() > 0 && options[0].ToLower() == @"all")
            {
                object[] param = { user, true, null };
                Thread t = new Thread(new ParameterizedThreadStart(DoUnsubscribeThread));
                t.Start(param);                
                ret = new Result(ResultCode.Halt, string.Empty);
            }
            else if (options.Count() == 0)
            {
                UserLocation threadLoc = UserLocation.LoadLocation(UserLocationType.POST, user);
                if (threadLoc != null)
                {
                    object[] param = { user, false, threadLoc.LocationRemoteID };
                    Thread t = new Thread(new ParameterizedThreadStart(DoUnsubscribeThread));
                    t.Start(param);

                    ret = new Result(ResultCode.Halt, string.Empty);
                }
                else
                {
                    ret = new Result(ResultCode.Error, @"No current thread. User `lt` to browse to a thread.");
                }
            }
            else
            {
                ret = new Result(ResultCode.Error,@"Invalid parameter to `usub` command");
            }

            return ret;
        }

        public void DoUnsubscribeThread(object o)
        {
            object[] objs = o as object[];

            if (objs == null)
            {
                throw new Exception(@"Could not cast object to object[] in DoUnsubscribeThread");
            }

            if (objs.Count() != 3)
            {
                throw new Exception(@"Something weird passed into DoUnsubscribeThread");
            }

            User user = objs[0] as User;
            bool bAll = (bool)objs[1];

            Connection c = user.UserConnection;
            IMUserInfo i = new IMUserInfo(user.UserConnectionName, user.UserConnection.Alias, user.UserConnection);

            int iThreadID = -1;
            string strConfMsg = @"Are you sure you want to unsubscribe from all threads?";

            if (!bAll)
            {
                iThreadID = (int)objs[2];
                strConfMsg = @"Are you sure you want to unsubscribe from thread " + iThreadID.ToString() + "?";
            }

            if (GetConfirmation(user,strConfMsg))
            {
                VBRequestResult r = VB.Instance.UnSubscribeThread(i, iThreadID);
                if (r.ResultCode == VBRequestResultCode.Success)
                {
                    c.SendMessage(new InstantMessage(user.UserConnectionName, @"Subscription(s) removed."));
                }
                else
                {
                    c.SendMessage(new InstantMessage(user.UserConnectionName, @"Could not remove subscription(s)."));
                    log.Error(r.Message);
                }
            }
            else
            {
                c.SendMessage(new InstantMessage(user.UserConnectionName, @"Action cancelled."));
            }
        }

        public Result TurnOnOffAutoIMS(User user, string[] options)
        {
            Result ret = null;

            if (options.Count() != 1)
            {
                ret = new Result(ResultCode.Error, @"Use `im on` or `im off` to turn on/off IM Notification");
            }
            else if (options[0].ToLower() == @"on")
            {
                object[] objs = { user, true };
                Thread t = new Thread(new ParameterizedThreadStart(DoTurnOnOffAutoIMS));
                t.Start(objs);   

                ret = new Result(ResultCode.Halt, string.Empty);
            }
            else if (options[0].ToLower() == @"off")
            {
                object[] objs = { user, false };
                Thread t = new Thread(new ParameterizedThreadStart(DoTurnOnOffAutoIMS));
                t.Start(objs);   

                ret = new Result(ResultCode.Halt, string.Empty);
            }
            else
            {
                ret = new Result(ResultCode.Error, @"Use `im on` or `im off` to turn on/off IM Notification");
            }
            
            

            
            return ret;
        }

        public void DoTurnOnOffAutoIMS(object o)
        {
            object[] objs = o as object[];

            if (objs == null)
            {
                throw new Exception(@"Could not cast object to object[] in DoTurnOnOffAutoIMS");
            }

            if (objs.Count() != 2)
            {
                throw new Exception(@"Something weird passed into DoUnsubscribeThread");
            }

            User user = objs[0] as User;
            bool bOn = (bool)objs[1];
            Connection c = user.UserConnection;
            IMUserInfo i = new IMUserInfo(user.UserConnectionName, user.UserConnection.Alias, user.UserConnection);

            string strOnOff = "off";
            if (bOn)
            {
                strOnOff = "on";
            }

            if (GetConfirmation(user, "Are you sure you want to turn IM Notification " + strOnOff + "?"))
            {
                VBRequestResult r = VB.Instance.TurnOnOffIMNotification(i, bOn);

                if (r.ResultCode == VBRequestResultCode.Success)
                {
                    c.SendMessage(new InstantMessage(user.UserConnectionName, @"IM Notification turned " + strOnOff + "."));
                }
                else
                {
                    c.SendMessage(new InstantMessage(user.UserConnectionName, @"Could not set IM Notification"));
                    log.Error(r.Message);
                }
            }
            else
            {
                c.SendMessage(new InstantMessage(user.UserConnectionName, @"Action cancelled."));
            }
        }

        #region Threaded Functions
        public string GetString(User user)
        {
            string strRet = string.Empty;
            DateTime start = DateTime.Now;

            _inputs[user.LocalUserID] = new InputState(InputStateEnum.Waiting);

            while (_inputs[user.LocalUserID].State == InputStateEnum.Waiting)
            {
                TimeSpan span = DateTime.Now - start;
                if (span.TotalMinutes > 9) // ten minute wait for input
                {
                    break;
                }
                else
                {
                    Thread.Sleep(500);
                }
            };

            // state is set to 'Responded' in the callback....
            if (_inputs[user.LocalUserID].State == InputStateEnum.Responded)
            {
                strRet = _inputs[user.LocalUserID].PageText;
            }

            _inputs.Remove(user.LocalUserID);
            return strRet;
        }

        public bool GetConfirmation(User user)
        {
            return GetConfirmation(user, @"Are you sure? (y or n)");
        }

        public bool GetConfirmation(User user, string strMessage)
        {
            bool bRetval = false;
            Connection c = user.UserConnection;
            c.SendMessage(new InstantMessage(user.UserConnectionName, strMessage));

            string strResponse = GetString(user);

            if (strResponse.ToLower() == "yes" || strResponse.ToLower() == "y")
            {
                bRetval = true;
            }

            return bRetval;
        }

        public Result MarkRead(User user, string strField)
        {
            object[] objs = { user, strField };
            Thread t = new Thread(new ParameterizedThreadStart(DoMarkRead));
            t.Start(objs);

            return new Result(ResultCode.Halt, string.Empty);
        }

        public void DoMarkRead(object o)
        {
            object[] objs = o as object[];

            if (objs != null && objs.Count() == 2)
            {
                User user = objs[0] as User;
                string strField = objs[1] as string;
                Connection c = user.UserConnection;
                string strUpper = char.ToUpper(strField[0]) + strField.Substring(1);

                UserLocation loc = null;
                if (strField == @"thread")
                {
                    loc = UserLocation.LoadLocation(UserLocationType.POST, user);
                }
                else if (strField == @"forum")
                {
                    loc = UserLocation.LoadLocation(UserLocationType.FORUM, user);
                }
                else
                {
                    // TODO: this should throw an exception
                    c.SendMessage(new InstantMessage(user.UserConnectionName, @"Could not make " + strField + " as read."));
                    log.Error("Unknown `strField` in MarkRead()");
                }

                if (loc != null)
                {
                    if (GetConfirmation(user, @"Mark this " + strField + "as read?"))
                    {
                        IMUserInfo i = new IMUserInfo(user.UserConnectionName, user.UserConnection.Alias, user.UserConnection);
                        VBRequestResult r = VB.Instance.MarkRead(i, loc.LocationRemoteID, strField);

                        if (r.ResultCode == VBRequestResultCode.Success)
                        {
                            c.SendMessage(new InstantMessage(user.UserConnectionName, strUpper + @" marked as read."));
                        }
                        else
                        {
                            c.SendMessage(new InstantMessage(user.UserConnectionName, strUpper + @" could not be marked as read."));
                            log.Error(r.Message);
                        }
                    }
                    else
                    {
                        c.SendMessage(new InstantMessage(user.UserConnectionName, "Mark " + strField + " read cancelled."));
                    }
                }
                else
                {
                    c.SendMessage(new InstantMessage(user.UserConnectionName, string.Format("No current {0}.", strField)));
                }
            }
            else
            {
                throw new Exception("DoMarkRead got passed some bad shit");
            }
        }

        public Result SubscribeThread(User user, string[] options)
        {
            int iThreadID = 0;

            if (options.Count() > 0)
            {
                if (!int.TryParse(options[0],out iThreadID))
                {
                    return new Result(ResultCode.Error, @"Invalid thread id");
                }
            }

            object[] parameters = { user, iThreadID };
            Thread replyThread = new Thread(new ParameterizedThreadStart(DoSubscribeThread));
            replyThread.Start(parameters);

            return new Result(ResultCode.Halt, string.Empty);
        }

        public void DoSubscribeThread(object o)
        {
            object[] objs = o as object[];

            if (objs != null && objs.Count() == 2)
            {
                User user = objs[0] as User;
                int iThreadId = (int)objs[1];

                Connection c = user.UserConnection;
                string strMessage = string.Empty;

                if (iThreadId == 0)
                {
                    UserLocation loc = UserLocation.LoadLocation(UserLocationType.POST, user);

                    if (loc != null)
                    {
                        iThreadId = loc.LocationRemoteID;
                    }
                }

                // check to see if iThreadId was set above
                if (iThreadId > 0)
                {
                    IMUserInfo iminfo = new IMUserInfo(user.UserConnectionName, user.UserConnection.Alias, user.UserConnection);
                    VBRequestResult r = VB.Instance.GetThread(iminfo, iThreadId);

                    if (r != null && r.ResultCode == VBRequestResultCode.Success)
                    {
                        Dictionary<string, string> threadInfo = r.Data as Dictionary<string, string>;
                        VBThread thread = new VBThread(threadInfo);

                        string strConf = string.Format("{1}Thread: {0}{1}Are you sure you wish to subscribe to this thread?",
                                                thread.GetTitle(), user.UserConnection.NewLine);

                        if (GetConfirmation(user, strConf))
                        {
                            r = VB.Instance.SubscribeThread(iminfo, iThreadId);

                            // TODO: test what happens when r is null
                            if (r.ResultCode == VBRequestResultCode.Success)
                            {
                                strMessage = string.Format("Subscribed to thread {0}", iThreadId);
                            }
                            else
                            {
                                strMessage = string.Format("Could not subscribe user to thread id {2}.", user.VBUser, user.VBUserID, iThreadId);
                            }
                        }
                        else
                        {
                            strMessage = "Action cancelled";
                        }

                    }
                    else
                    {
                        strMessage = @"Invalid threadid.";
                    }
                }
                else
                {
                    strMessage = @"No thread to subscribe to. Use `lt` to browse threads or use `sub XYZ` where XYZ is the thread id.";
                }

                c.SendMessage(new InstantMessage(user.UserConnectionName, strMessage));
            }

            
            


            //else
            //{
            //    string strConf = string.Format("{1}Current Thread: {0}{1}Are you sure you wish to subscribe to this thread?", loc.Title, user.UserConnection.NewLine);
            //    if (GetConfirmation(user, strConf))
            //    {

            //        IMUserInfo iminfo = new IMUserInfo(user.UserConnectionName, user.UserConnection.Alias, user.UserConnection);
            //        VBRequestResult r = VB.Instance.SubscribeThread(iminfo, loc.LocationRemoteID);

            //        // TODO: test what happens when r is null
            //        if (r.ResultCode == VBRequestResultCode.Success)
            //        {
            //            strMessage = string.Format("Subscribed to thread {0}", loc.LocationRemoteID);
            //        }
            //        else
            //        {
            //            strMessage = string.Format("Could not subscribe user to thread id {2}.", user.VBUser, user.VBUserID, loc.LocationRemoteID);
            //        }
            //    }

            //}

            //c.SendMessage(new InstantMessage(user.UserConnectionName, strMessage));
        }

        public Result ThreadReply(User user)
        {
            Thread replyThread = new Thread(new ParameterizedThreadStart(DoThreadReply));
            replyThread.Start(user);

            return new Result(ResultCode.Halt, string.Empty);
        }

        public void DoThreadReply(object userObj)
        {
            User user = userObj as User;
            Connection c = user.UserConnection;
            UserLocation postLoc = UserLocation.LoadLocation(UserLocationType.POST, user);

            if (postLoc != null)
            {
                string strResponse = string.Format("New Thread Reply:{0}Current Thread: {1}{0}", user.UserConnection.NewLine, postLoc.Title);
                strResponse += @"Enter your post text:";

                c.SendMessage(new InstantMessage(user.UserConnectionName, strResponse));

                string strPostText = GetString(user);

                if (strPostText != string.Empty)
                {
                    if (GetConfirmation(user))
                    {
                        IMUserInfo iminfo = new IMUserInfo(user.UserConnectionName, user.UserConnection.Alias, user.UserConnection);
                        VBRequestResult r = VB.Instance.PostReply(iminfo, postLoc.LocationRemoteID, strPostText);

                        if (r.ResultCode == VBRequestResultCode.Success && r.Data != null && (int)r.Data > 0)
                        {
                            c.SendMessage(new InstantMessage(user.UserConnectionName, @"Post submitted successfully."));
                        }
                        else
                        {
                            c.SendMessage(new InstantMessage(user.UserConnectionName, @"There was an error submitting the post."));
                        }
                    }
                    else
                    {
                        c.SendMessage(new InstantMessage(user.UserConnectionName, @"Post cancelled"));
                    }
                }
                else
                {
                    c.SendMessage(new InstantMessage(user.UserConnectionName, @"No post entered"));
                }
            }
            else
            {
                c.SendMessage(new InstantMessage(user.UserConnectionName, @"No current thread. Use `lt` to browse to a thread."));
            }
        }

        #endregion

        public void MainLoop()
        {
            string strInput = string.Empty;

            do
            {
                strInput = System.Console.ReadLine();

                if (strInput.Length == 0)
                {
                    continue;
                }

                if (strInput.ToLower() == @"quit" || strInput.ToLower() == @"exit")
                {
                    AppQuit = true;
                }
                else
                {
                    _commands.ExecuteCommand(strInput);
                }

            } while (!AppQuit);
        }
    }
}

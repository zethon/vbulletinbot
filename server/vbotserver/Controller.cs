using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;
using System.Configuration;
using log4net;
using BotService = VBulletinBot.VBotService.VBotService;
using VBulletinBot.VBotService;

namespace VBulletinBot
{
    class Controller
    {
        Dictionary<int, InputState> _inputs = new Dictionary<int, InputState>();
        
        static ILog log = LogManager.GetLogger(typeof(Controller));

        public ResponseChannel ResponseChannel = null;

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


        public bool Init()
        {
            BotConfigSection botconfig = (BotConfigSection)ConfigurationManager.GetSection("botconfig");

            if (!VBotDB.Instance.DatabaseExists())
            {
                log.InfoFormat("Creating local database from datacontext...");
                VBotDB.Instance.CreateDatabase();
            }

            log.InfoFormat("Total IM Services Loaded: {0}", botconfig.IMServices.Count);

            _conComp = ConnectionComposite.MakeConnectionComposite(botconfig);
            foreach (Connection conn in _conComp.Connections)
            {
                conn.OnConnect += new OnConnectHandler(OnConnectCallback);
                conn.OnMessage += new Connection.OnMessageHandler(OnMessageCallback);
                conn.OnSendMessage += new Connection.OnSendMessageHandler(OnSendMessageCallback);
                conn.OnDisconnect += new Connection.OnDisconnectHandler(OnDisconnectCallback);
            }

            _commands = new CommandClass(this);

            // start the notification timer
            int iTimer = 0;
            if (int.TryParse(botconfig.AutoInterval, out iTimer))
            {
                _notTimer = new System.Timers.Timer(1000 * 60 * iTimer);
                _notTimer.Elapsed += new ElapsedEventHandler(_notTimer_Elapsed);
                _notTimer.Enabled = true;
                _notTimer.Start();
            }

            if (botconfig.AutoConnect)
            {
                log.Info("Auto-connecting all connections");
                _conComp.Connect();
            }

            return true;
        }

        public void _notTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            log.Info("Notification Timer Elapsed()");
            if (_conComp.Connections != null && _conComp.Connections.Count() > 0)
            {
                VBotService.IMNotificationsResult result = BotService.Instance.GetIMNotifications(true);

                if (result.Result.Code == 0)
                {
                    if (result.IMNotificationList != null && result.IMNotificationList.Count() > 0)
                    {
                        log.Info(string.Format("{0} post notifications recieved", result.IMNotificationList.Count()));

                        foreach(VBotService.IMNotification not in result.IMNotificationList)
                        {
                            Connection c = _conComp.GetConnection(not.IMNotificationInfo.InstantIMService);
                            string strScreenName = not.IMNotificationInfo.InstantIMScreenname;

                            string strResponse = c.NewLine + "Forum: '" + not.Forum.Title + "'" + c.NewLine + "Thread: '" + not.Thread.ThreadTitle + "'" + c.NewLine;
                            strResponse += FetchPostBit(not.Post, c.NewLine) + c.NewLine;
                            strResponse += "(Type 'gt " + not.Thread.ThreadID.ToString() + "' to go to the thread. Type 'im off' to turn off IM Notification)";

                            ResponseChannel rc = new VBulletinBot.ResponseChannel(strScreenName, c);
                            rc.SendMessage(strResponse);
                            System.Threading.Thread.Sleep(2000);
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
            lock (this)
            {
                log.InfoFormat("INMSG ({0}) << {1}: {2}", conn.GetType().Name, im.User, im.Text);

                DateTime dtStart = DateTime.Now;

                try
                {
                    ResponseChannel = new ResponseChannel(im.User, conn);
                    LocalUser user = LocalUser.GetUser(ResponseChannel);

                    if (user != null && user.LocalUserID > 0)
                    {
                        #region if

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
                        #endregion
                    }
                    else
                    {
                        string strResponse = @"Unknown screen name. Please add this screen name to your user profile.";
                        conn.SendMessage(new InstantMessage(im.User, strResponse));
                        log.DebugFormat(@"Unknown user: '{0}' ({1})",im.User,conn.Alias);
                    }
                }
                catch (Exception ex)
                {
                    log.Error("Something bad",ex);
                }

                if (log.IsDebugEnabled)
                {
                    TimeSpan elapsed = DateTime.Now - dtStart;

                    log.InfoFormat("Response Time: {0}.{1} seconds",elapsed.Seconds, elapsed.Milliseconds);
                }
            }
        }

        public Result DoCommand(string strCommand,LocalUser user)
        {
            Result retval = new Result();
            CommandParser parser = new CommandParser(strCommand);

            if (parser.Parse())
            {
                int iListChoice = 0;

                if (int.TryParse(parser.ApplicationName, out iListChoice) && iListChoice > 0)
                { 
                    // user entered a number, let's deal with the lastlists
                    UserLastList ll = VBotDB.Instance.UserLastLists.FirstOrDefault(l => l.LocalUserID == user.LocalUserID);

                    if (ll != null)
                    {
                        switch (ll.Name)
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

                            default:
                                log.ErrorFormat("Unknown lastlist {0}", ll.Name);
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
                        case @"\":
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

                        case @"nt":
                            retval = NewThread(user);
                            break;

                        case @"p":
                            retval = GotoNextPost(user, false);
                            break;

                        case @"r":
                            retval = ThreadReply(user,false);
                            break;

                        case @"rq":
                            retval = ThreadReply(user, true);
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
                            // TODO: the string in UserConnectionName should come from somewhere else?
                            retval = WhoAmI(ResponseChannel.ToName, ResponseChannel.Connection.Alias);
                        break;

                        default:
                            retval = new Result(ResultCode.Error, @"Unknown command. Please see http://code.google.com/p/vbulletinbot/ for help.");
                        break;
                    }
                }
            }

            return retval;
        }

        public string FetchPostBit(VBotService.Post post, string strNewLine)
        {
            string strResponse = string.Empty;

            string strPageText = post.PageText;
            strResponse += strNewLine;
            strResponse += string.Format("{0}", strPageText) + strNewLine;
            strResponse += string.Format("{0} by {1}", post.GetFriendlyDate(), post.Username);

            return strResponse;
        }

        public Result GotoForumIndex(int iIndex, LocalUser user)
        {
            return GotoForumIndex(iIndex, user, false);
        }

        public Result GotoForumIndex(int iIndex, LocalUser user, bool bGotoRoot)
        {
            Result retval = null;
            UserLocation curLoc = UserLocation.LoadLocation(UserLocationType.FORUM, user);

            if (curLoc == null)
            { // this location does not exist

                curLoc = UserLocation.GetDefaultLocation(UserLocationType.FORUM, user);
                VBotService.ForumListResult result = BotService.Instance.ListForums(BotService.Credentialize(ResponseChannel), (int)curLoc.LocationRemoteID);

                if (result.Result.Code == 0)
                {
                    curLoc.SetCurrentForum(result.CurrentForum);
                    //curLoc.ParseForumsList(result.ForumList);
                    curLoc.ParseIDList(result.ForumList);
                    curLoc.SaveLocation();
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
                VBotService.ForumListResult res = BotService.Instance.ListForums(BotService.Credentialize(ResponseChannel), iNewForumID);

                // TODO: error checking of the above call
                curLoc.SetCurrentForum(res.CurrentForum);
                curLoc.ParseIDList(res.ForumList);
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

                retval = ListForum(user,res.ForumList);
            }

            return retval;
        }

        public Result GotoNextPost(LocalUser user, bool bGotoNext)
        {
            Result rs = null;

            UserLocation curThreadLocation = UserLocation.LoadLocation(UserLocationType.POST, user);

            if (curThreadLocation != null)
            {
                UserPostIndex upi = VBotDB.Instance.UserPostIndexes.FirstOrDefault(u => u.LocalUserID == user.LocalUserID);

                if (upi != null)
                {
                    int iPostIndex = upi.PostIndex.Value;

                    if (bGotoNext)
                        iPostIndex++;
                    else
                        iPostIndex--;

                    rs = GotoPostIndex(iPostIndex, user);
                }
                else
                {
                    rs = new Result(ResultCode.Error, @"Invalid post index. Use `lp` to browse a thread.");
                    log.WarnFormat("Invalid `UserPostIndex` for LocalUserID {0}", user.LocalUserID);
                }
            }
            else
            {
                rs = new Result(ResultCode.Error, @"No active thread set. Use `lt` and browse to a thread");
            }

            return rs;
        }

        public Result GotoParentForum(LocalUser user)
        {
            Result ret = null;
            UserLocation forumLoc = UserLocation.LoadLocation(UserLocationType.FORUM, user);

            if (forumLoc != null)
            {
                VBotService.ForumListResult result = BotService.Instance.ListParentForums(BotService.Credentialize(ResponseChannel), (int)forumLoc.LocationRemoteID);

                if (result.Result.Code == 0)
                {
                    forumLoc.SetCurrentForum(result.CurrentForum);
                    forumLoc.ParseIDList(result.ForumList);
                    forumLoc.SaveLocation();

                    // reset the THREAD location
                    UserLocation threadLoc = UserLocation.LoadLocation(UserLocationType.THREAD, user);

                    if (threadLoc != null)
                    {
                        threadLoc.Title = forumLoc.Title;
                        threadLoc.LocationRemoteID = forumLoc.LocationRemoteID;
                        threadLoc.SaveLocation();
                    }

                    ret = ListForum(user, result.ForumList);
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

        public Result GotoPostIndex(int iChoice, LocalUser user)
        {
            Result rs = null;
            UserLocation curPostLoc = UserLocation.LoadLocation(UserLocationType.POST, user);

            if (curPostLoc != null)
            {
                VBotService.UserCredentials uc = BotService.Credentialize(ResponseChannel);
                VBotService.GetPostResult r = BotService.Instance.GetPostByIndex(uc, (int)curPostLoc.LocationRemoteID, iChoice);

                if (r.Result.Code == 0)
                {
                    if (r.Post != null && r.Post.PostID > 0)
                    {
                        string strText = FetchPostBit(r.Post, ResponseChannel.Connection.NewLine);
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

        public Result GotoThread(LocalUser user, string[] options)
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

                    VBotService.UserCredentials uc = BotService.Credentialize(ResponseChannel);
                    VBotService.PostListResult r = BotService.Instance.ListPosts(uc, iNewThreadID, (int)postLoc.PageNumber, (int)postLoc.PerPage);

                    if (r.Result.Code == 0)
                    {
                        // TODO: build the postLoc location title here
                        postLoc.Title = string.Format("{0} - {1} created by {2}",
                                        Regex.Replace(r.Thread.ThreadTitle, @"[\']", string.Empty),
                                        r.Thread.GetFriendlyDate(r.Thread.DateLine),
                                        r.Thread.PostUsername);

                        postLoc.LocationRemoteID = iNewThreadID;

                        postLoc.ParseIDList(r.PostList);
                        postLoc.SaveLocation();

                        rs = ListPosts(user, new string[] { postLoc.PageNumber.ToString(), postLoc.PerPage.ToString() }, r);
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

        public Result GotoThreadIndex(int iChoice, LocalUser user)
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
                        VBotService.UserCredentials uc = BotService.Credentialize(ResponseChannel);
                        VBotService.PostListResult r = BotService.Instance.ListPosts(uc, iNewThreadID, (int)postLoc.PageNumber, (int)postLoc.PerPage);

                        if (r.Result.Code == 0)
                        {
                            // TODO: build the postLoc location title here
                            postLoc.Title = string.Format("{0} - {1} created by {2}",
                                            Regex.Replace(r.Thread.ThreadTitle, @"[\']", string.Empty),
                                            r.Thread.GetFriendlyDate(r.Thread.DateLine),
                                            r.Thread.PostUsername);

                            postLoc.LocationRemoteID = iNewThreadID;
                            postLoc.ParseIDList(r.PostList);
                            postLoc.SaveLocation();

                            rs = ListPosts(user, new string[] { postLoc.PageNumber.ToString(), postLoc.PerPage.ToString() }, r);
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

        public Result ListForum(LocalUser user)
        {
            return ListForum(user, null);
        }

        public Result ListForum(LocalUser user, VBotService.Forum[] forums)
        {
            lock (this)
            {
                Result resval = null;
                UserLocation loc = UserLocation.LoadLocation(UserLocationType.FORUM, user);

                if (loc == null)
                { // this location does not exist

                    loc = UserLocation.GetDefaultLocation(UserLocationType.FORUM, user);
                    VBotService.ForumListResult res = BotService.Instance.ListForums(BotService.Credentialize(ResponseChannel), (int)loc.LocationRemoteID);

                    // TODO: error checking of the above call
                    loc.SetCurrentForum(res.CurrentForum);
                    loc.ParseIDList(res.ForumList);
                    loc.SaveLocation();
                }

                if (forums == null)
                {
                    VBotService.ForumListResult res = BotService.Instance.ListForums(BotService.Credentialize(ResponseChannel), (int)loc.LocationRemoteID);
                    forums = res.ForumList;
                }

                string strResponse = ResponseChannel.Connection.NewLine + "Subforums in `" + loc.Title + "`" + ResponseChannel.Connection.NewLine;
                bool bForumsExist = false;
                string strIsNew = string.Empty;

                if (forums.Count() > 0)
                {
                    int iCount = 1;
                    foreach (VBotService.Forum foruminfo in forums)
                    {
                        strIsNew = string.Empty;
                        if (foruminfo.IsNew)
                        {
                            strIsNew = "*";
                        }

                        bForumsExist = true;
                        strResponse += iCount.ToString() + ". " + strIsNew + foruminfo.Title+ ResponseChannel.NewLine;
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

        public Result ListPosts(LocalUser user, string[] options)
        {
            return ListPosts(user, options, null);
        }

        public Result ListPosts(LocalUser user, string[] options, VBotService.PostListResult result)
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


                if (result == null)
                {
                    VBotService.UserCredentials uc = BotService.Credentialize(ResponseChannel);
                    result = BotService.Instance.ListPosts(uc, (int)loc.LocationRemoteID, iPageNumber, iPerPage);
                }

                string strResponse = ResponseChannel.NewLine + "Thread: " + loc.Title + ResponseChannel.NewLine;

                double dTotalPosts = (double)(result.Thread.ReplyCount + 1);
                int iTotalPages = (int)Math.Ceiling(dTotalPosts / (double)iPerPage);

                if (iPageNumber <= iTotalPages)
                {
                    strResponse += string.Format("Posts: Page {0} of {1} ({2} per page)", iPageNumber, iTotalPages, iPerPage) + ResponseChannel.NewLine;
                }

                string strIsNew = string.Empty;
                if (result.PostList.Count() > 0)
                {
                    int iCount = ((iPageNumber - 1) * iPerPage) + 1;
                    foreach (VBotService.Post postInfo in result.PostList)
                    {
                        strIsNew = string.Empty;
                        if (postInfo.IsNew)
                        {
                            strIsNew = "*";
                        }

                        strResponse += string.Format("{0}. {1}\"{2}\" - {3} by {4}" + ResponseChannel.NewLine,
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

        public Result ListThreads(LocalUser user, string[] options)
        {
            lock (this)
            {
                ResultCode rc = ResultCode.Unknown;

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


                    VBotService.UserCredentials uc = BotService.Credentialize(ResponseChannel);
                    VBotService.ThreadListResult r = BotService.Instance.ListThreads(uc, (int)loc.LocationRemoteID, iPageNumber, iPerPage);

                    if (r.Result.Code != 0)
                    {
                        log.ErrorFormat("Could not list threads: {0}", r.Result.Text);
                        return new Result(ResultCode.Error, "Could not view threads. Please try again.");
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

                    strResponse += ResponseChannel.Connection.NewLine + "Threads in `" + loc.Title + "`" + ResponseChannel.Connection.NewLine;

                    bool bForumsExist = false;
                    string strIsNew = string.Empty;
                    string strIsSubscribed = string.Empty;

                    if (r.ThreadList.Count() > 0)
                    {
                        int iTotalPages = (int)Math.Ceiling((double)r.ThreadCount / (double)iPerPage);
                        iTotalPages += 1;

                        if (iPageNumber <= iTotalPages)
                        {
                            strResponse += string.Format("Page {0} of {1} ({2} per page)", iPageNumber, iTotalPages, iPerPage) + ResponseChannel.Connection.NewLine;
                        }

                        int iCount = 1;
                        foreach (VBotService.Thread thread in r.ThreadList)
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

                            strIsSubscribed = thread.IsSubscribed ? "#" : string.Empty;

                            bForumsExist = true;

                            strResponse += string.Format("{0}. {1}{2}{3}{4} ({5}) - {6} by {7}" + ResponseChannel.Connection.NewLine,
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
                        loc.ParseIDList(r.ThreadList);                        
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
 
        /// <summary>
        /// Returns the current Forum and Thread of the user
        /// </summary>
        /// <param name="user">The user</param>
        /// <returns></returns>
        public Result WhereAmI(LocalUser user)
        {
            string strNewLine = ResponseChannel.Connection.NewLine;
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

        /// <summary>
        /// Returns the vBulletin userid and username of the associated User
        /// </summary>
        /// <param name="strUsername">The connection screen name</param>
        /// <param name="user"></param>
        /// <returns></returns>
        public Result WhoAmI(string strUsername, string strConnectionName)
        {
            Result retval;
            string strResponse = string.Empty;

            try
            {
                VBotService.RequestResult result = BotService.Instance.WhoAmI(BotService.Credentialize(strUsername,strConnectionName));

                if (result.Code == 0)
                {
                    if (result.RemoteUser.UserID > 0)
                    {
                        strResponse = ResponseChannel.Connection.NewLine
                                          + "VBUserID: " + result.RemoteUser.UserID.ToString() + ResponseChannel.Connection.NewLine
                                          + "VBUsername: " + result.RemoteUser.Username;
                    }
                    else
                    {
                        strResponse = @"Unknown user. Please update your profile.";
                    }
                }
                else
                {
                    strResponse = @"There was an error. Please try again later.";
                    log.WarnFormat(@"Could not process WhoAmI(), result.Text: '{0}'", result.Text);
                }

                retval = new Result(ResultCode.Success, strResponse);
            }
            catch (Exception e)
            {
                retval = new Result(ResultCode.Error, e.Message);
                log.Error(@"WhoAMI failed", e);
                return retval;
            }

            return retval;
        }

        #region Threaded Functions
        public string GetString(LocalUser user)
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
                    System.Threading.Thread.Sleep(500);
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

        public bool GetConfirmation(LocalUser user)
        {
            return GetConfirmation(user, @"Are you sure? (y or n)");
        }

        public bool GetConfirmation(LocalUser user, string strMessage)
        {
            bool bRetval = false;
            //Connection c = user.Connection;
            //c.SendMessage(new InstantMessage(user.UserConnectionName, strMessage));
            user.ResponseChannel.SendMessage(strMessage);

            string strResponse = GetString(user);

            if (strResponse.ToLower() == "yes" || strResponse.ToLower() == "y")
            {
                bRetval = true;
            }

            return bRetval;
        }

        public Result MarkRead(LocalUser user, string strField)
        {
            object[] objs = { user, strField };
            System.Threading.Thread t = new System.Threading.Thread(new ParameterizedThreadStart(DoMarkRead));
            t.Start(objs);

            return new Result(ResultCode.Halt, string.Empty);
        }

        public void DoMarkRead(object o)
        {
            object[] objs = o as object[];

            if (objs != null && objs.Count() == 2)
            {
                LocalUser user = objs[0] as LocalUser;

                string strField = objs[1] as string;
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
                    user.ResponseChannel.SendMessage(@"Could not make " + strField + " as read.");
                    log.Error("Unknown `strField` in MarkRead()");
                }

                if (loc != null)
                {
                    if (GetConfirmation(user, @"Mark this " + strField + " read?"))
                    {
                        VBotService.UserCredentials uc = BotService.Credentialize(user.ResponseChannel);
                        
                        VBotService.RequestResult r = null; 
                        
                        // TODO: this is a hack, redesign how MarkThreadRead and MarkForumRead are called
                        if (strField == @"thread")
                        {
                            r = BotService.Instance.MarkThreadRead(uc, (int)loc.LocationRemoteID);
                        }
                        else if (strField == @"forum")
                        {
                            r = BotService.Instance.MarkForumRead(uc, (int)loc.LocationRemoteID);
                        }
                        
                        // TODO: can r be null? if so, this could be bad
                        if (r.Code == 0)
                        {
                            user.ResponseChannel.SendMessage(strUpper + @" marked as read.");
                        }
                        else
                        {
                            user.ResponseChannel.SendMessage(strUpper + @" could not be marked as read.");
                            log.Error(r.Text);
                        }
                    }
                    else
                    {
                        user.ResponseChannel.SendMessage("Mark " + strField + " read cancelled.");
                    }
                }
                else
                {
                    user.ResponseChannel.SendMessage(string.Format("No current {0}.", strField));
                }
            }
            else
            {
                throw new Exception("DoMarkRead got passed some bad shit");
            }
        }

        public Result SubscribeThread(LocalUser user, string[] options)
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
            System.Threading.Thread replyThread = new System.Threading.Thread(new ParameterizedThreadStart(DoSubscribeThread));
            replyThread.Start(parameters);

            return new Result(ResultCode.Halt, string.Empty);
        }

        public void DoSubscribeThread(object o)
        {
            object[] objs = o as object[];

            if (objs != null && objs.Count() == 2)
            {
                LocalUser user = objs[0] as LocalUser;

                int iThreadId = (int)objs[1];
                string strMessage = string.Empty;

                if (iThreadId == 0)
                {
                    UserLocation loc = UserLocation.LoadLocation(UserLocationType.POST, user);

                    if (loc != null)
                    {
                        iThreadId = (int)loc.LocationRemoteID;
                    }
                }

                // check to see if iThreadId was set above
                if (iThreadId > 0)
                {
                    VBotService.UserCredentials uc = BotService.Credentialize(user.ResponseChannel);
                    VBotService.GetThreadResult r = BotService.Instance.GetThread(uc, iThreadId);

                    if (r.Result.Code == 0)
                    {
                        string strConf = string.Format("{1}Thread: {0}{1}Are you sure you wish to subscribe to this thread?",
                                                r.Thread.GetTitle(), user.ResponseChannel.NewLine);

                        if (GetConfirmation(user, strConf))
                        {
                            r = BotService.Instance.SubscribeThread(uc, iThreadId);

                            // TODO: test what happens when r is null
                            if (r.Result.Code == 0)
                            {
                                strMessage = string.Format("Subscribed to thread {0}", iThreadId);
                            }
                            else
                            {
                                strMessage = string.Format("Could not subscribe user to thread id {0}.", iThreadId);
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

                user.ResponseChannel.SendMessage(strMessage);
            }
        }

        public Result NewThread(LocalUser LocalUser)
        {
            System.Threading.Thread replyThread = new System.Threading.Thread(new ParameterizedThreadStart(DoNewThread));
            replyThread.Start(LocalUser);

            return new Result(ResultCode.Halt, string.Empty);
        }

        public void DoNewThread(object userObj)
        {
            LocalUser user = userObj as LocalUser;
            UserLocation forumLoc = UserLocation.LoadLocation(UserLocationType.FORUM, user);

            if (forumLoc != null)
            {
                string strResponse = string.Format("New Thread:{0}Current Forum: {1}{0}", user.ResponseChannel.NewLine, forumLoc.Title);
                strResponse += @"Enter new thread title:";

                user.ResponseChannel.SendMessage(strResponse);

                string strThreadTitle = GetString(user);

                if (strThreadTitle == string.Empty)
                {
                    user.ResponseChannel.SendMessage(@"No post entered");
                    return;
                }

                strResponse = @"Enter post text:";
                user.ResponseChannel.SendMessage(strResponse);
                string strPost = GetString(user);

                if (GetConfirmation(user))
                {
                    VBotService.UserCredentials uc = BotService.Credentialize(user.ResponseChannel);
                    VBotService.PostReplyResult r = BotService.Instance.PostNewThread(uc, (int)forumLoc.LocationRemoteID, strThreadTitle, strPost);

                    if (r.Result.Code != 0 && r.PostID > 0)
                    {
                        user.ResponseChannel.SendMessage(@"Post submitted successfully.");
                    }
                    else
                    {
                        user.ResponseChannel.SendMessage(@"There was an error submitting the post.");
                    }
                }
            }
            else
            {
                user.ResponseChannel.SendMessage(@"No current forum. Use `lf` to browse to a forum.");
            }
        }

        public Result ThreadReply(LocalUser user, bool bDoQuote)
        {
            System.Threading.Thread replyThread = new System.Threading.Thread(new ParameterizedThreadStart(DoThreadReply));
            
            replyThread.Start(new object[] { user, bDoQuote} );

            return new Result(ResultCode.Halt, string.Empty);
        }

        public void DoThreadReply(object obj)
        {
            object[] objs = obj as object[];

            if (obj == null)
            {
                throw new Exception(@"Could not cast DoThreadTreply parameters correctly");
            }

            LocalUser user = objs[0] as LocalUser;
            bool? bDoQuote = objs[1] as bool?;

            if (user == null || bDoQuote == null)
            {
                throw new Exception(@"Invalid cast of 'user' or 'bDoQuote' in DoThreadReply");
            }

            UserLocation postLoc = UserLocation.LoadLocation(UserLocationType.POST, user);

            if (postLoc != null)
            {
                string strResponse = string.Format("New Thread Reply:{0}Current Thread: {1}{0}", user.ResponseChannel.NewLine, postLoc.Title);
                strResponse += @"Enter your post text:";

                user.ResponseChannel.SendMessage(strResponse);

                string strPostText = GetString(user);

                if (strPostText != string.Empty)
                {
                    if (GetConfirmation(user))
                    {
                        int iOriginalPostId = 0;
                        if (bDoQuote != null && (bool)bDoQuote)
                        {
                            UserLocation postLost = UserLocation.LoadLocation(UserLocationType.POST, user);
                            int iPostIndex = user.PostIndex;

                            if (postLoc != null && iPostIndex > 0)
                            {
                                // user.PostIndex is 1 based array, IDList is zero based
                                iOriginalPostId = postLoc.GetIDListIndexOf(iPostIndex-1);
                            }
                        }

                        VBotService.UserCredentials uc = BotService.Credentialize(user.ResponseChannel);

                        log.DebugFormat("PostReply Parameters: LocationRemoteID: {0}, iOriginalPostId: {1}", postLoc.LocationRemoteID, iOriginalPostId);
                        VBotService.PostReplyResult r = BotService.Instance.PostReply(uc, (int)postLoc.LocationRemoteID, strPostText, iOriginalPostId);
                        
                        if (r.Result.Code != 0 && r.PostID > 0)
                        {
                            user.ResponseChannel.SendMessage(@"Post submitted successfully.");
                        }
                        else
                        {
                            user.ResponseChannel.SendMessage(@"There was an error submitting the post.");
                        }
                    }
                    else
                    {
                        user.ResponseChannel.SendMessage(@"Post cancelled");
                    }
                }
                else
                {
                    user.ResponseChannel.SendMessage(@"No post entered");
                }
            }
            else
            {
                user.ResponseChannel.SendMessage(@"No current thread. Use `lt` to browse to a thread.");
            }
        }

        public Result UnsubscribeThread(LocalUser user, string[] options)
        {
            Result ret = null;
            int iThread = 0;

            if (options.Count() > 0 && options[0].ToLower() == @"all")
            {
                iThread = -1;
            }
            else if (options.Count() > 0)
            {
                if (!int.TryParse(options[0], out iThread))
                {
                    // TODO: what happens to iThread if TryParse fails?
                    iThread = 0;
                }
            }
            else
            {
                // no parameters, get the current thread
                UserLocation threadLoc = UserLocation.LoadLocation(UserLocationType.POST, user);
                if (threadLoc != null)
                {
                    iThread = (int)threadLoc.LocationRemoteID;
                }
                else
                {
                    return new Result(ResultCode.Error, @"No current thread. User `lt` to browse to a thread.");
                }
            }

            if (iThread != 0)
            {
                object[] param = { user, iThread };
                System.Threading.Thread t = new System.Threading.Thread(new ParameterizedThreadStart(DoUnsubscribeThread));
                t.Start(param);
                ret = new Result(ResultCode.Halt, string.Empty);
            }
            else
            {
                ret = new Result(ResultCode.Error, @"Invalid parameter to `unsub` command");
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

            if (objs.Count() != 2)
            {
                throw new Exception(@"Something weird passed into DoUnsubscribeThread");
            }

            LocalUser user = objs[0] as LocalUser;
            int iThreadID = (int)objs[1];
            string strConfMsg = @"Are you sure you want to unsubscribe from all threads?";

            if (iThreadID > 0)
            {
                strConfMsg = @"Are you sure you want to unsubscribe from thread " + iThreadID.ToString() + "?";
            }

            if (GetConfirmation(user, strConfMsg))
            {
                VBotService.UserCredentials uc = BotService.Credentialize(user.ResponseChannel);
                VBotService.RequestResult r = BotService.Instance.UnSubscribeThread(uc, iThreadID);

                if (r.Code == 0)
                {
                    user.ResponseChannel.SendMessage(@"Subscription(s) removed.");

                }
                else
                {
                    user.ResponseChannel.SendMessage(@"Could not remove subscription(s).");
                    log.Error(r.Text);
                }
            }
            else
            {
                user.ResponseChannel.SendMessage(@"Action cancelled.");
                log.DebugFormat("User '{0}' cancelled action DoUnsubscribeThread()", user.ResponseChannel.ToName);
            }
        }

        public Result TurnOnOffAutoIMS(LocalUser user, string[] options)
        {
            Result ret = null;

            if (options.Count() != 1)
            {
                ret = new Result(ResultCode.Error, @"Use `im on` or `im off` to turn on/off IM Notification");
            }
            else if (options[0].ToLower() == @"on")
            {
                object[] objs = { user, true };
                System.Threading.Thread t = new System.Threading.Thread(new ParameterizedThreadStart(DoTurnOnOffAutoIMS));
                t.Start(objs);

                ret = new Result(ResultCode.Halt, string.Empty);
            }
            else if (options[0].ToLower() == @"off")
            {
                object[] objs = { user, false };
                System.Threading.Thread t = new System.Threading.Thread(new ParameterizedThreadStart(DoTurnOnOffAutoIMS));
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

            LocalUser user = objs[0] as LocalUser;
            bool bOn = (bool)objs[1];

            string strOnOff = "off";
            if (bOn)
            {
                strOnOff = "on";
            }

            if (GetConfirmation(user, "Are you sure you want to turn IM Notification " + strOnOff + "?"))
            {
                VBotService.UserCredentials uc = BotService.Credentialize(user.ResponseChannel);
                VBotService.RequestResult r = BotService.Instance.SetIMNotification(uc, bOn);

                if (r.Code == 0)
                {
                    user.ResponseChannel.SendMessage(@"IM Notification turned " + strOnOff + ".");
                }
                else
                {
                    user.ResponseChannel.SendMessage(@"Could not set IM Notification");
                    log.Error(r.Text);
                }
            }
            else
            {
                user.ResponseChannel.SendMessage(@"Action cancelled.");
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

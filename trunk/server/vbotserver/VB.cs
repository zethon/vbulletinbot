using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Xml.Linq;
using System.Xml;
using System.IO;
using log4net;

namespace vbotserver
{
    public enum VBRequestResultCode
    {
        Unknown,
        Success,
        Error
    }

    public class VBRequestResult
    {
        private VBRequestResultCode _resultCode = VBRequestResultCode.Unknown;
        public VBRequestResultCode ResultCode
        {
            get { return _resultCode; }
        }

        private object _data = null;
        public object Data
        {
            get { return _data; }
        }

        private string _strMessage = string.Empty;
        public string Message
        {
            get { return _strMessage; }
        }

        private Dictionary<string, string> _userInfo = new Dictionary<string, string>();
        public Dictionary<string, string> UserInfo
        {
            get { return _userInfo; }
            set { _userInfo = value; }
        }

        public VBRequestResult(VBRequestResultCode rc, string strMessage, object data)
        {
            _resultCode = rc;
            _strMessage = strMessage;
            _data = data;
        }

        public VBRequestResult()
        {
        }
    }

    public sealed class VB
    {
        static ILog log = LogManager.GetLogger(typeof(VB));

        private string _strLastRequest = string.Empty;
        public string LastRequest
        {
            get { return _strLastRequest; }
        }

        private string _strLastResponse = string.Empty;
        public string LastResponse
        {
            get { return _strLastResponse; }
        } 

        private int _iRequestCount = 0;
        public int RequestCount
        {
            get { return _iRequestCount; }
        }

        static readonly VB _instance = new VB();
        public static VB Instance
        {
            get { return _instance; }
        }

        string _strServiceUrl = string.Empty;
        public string ServiceURL
        {
            get { return _strServiceUrl; }
            set { _strServiceUrl = value; }
        }

        string _strServicePassword = string.Empty;
        public string ServicePassword
        {
            get { return _strServicePassword; }
            set { _strServicePassword = value; }
        }

        private VB()
        {
            //_strServiceUrl = strUrl;
        }

        public void ResetCount()
        {
            _iRequestCount = 0;
        }

        public VBRequestResult GetPostByIndex(ResponseChannel imuserinfo, int iThreadID, int iIndex)
        {
            VBRequestResult vbrr = new VBRequestResult();

            string strXML = @"
<request>
<command>gpi</command>
    <threadid>" + iThreadID.ToString() + @"</threadid>
    <index>" + iIndex.ToString() + @"</index>
<usercredentials>
    <type>service</type>
    <screenname>" + imuserinfo.ScreenName + @"</screenname>
    <service>" + imuserinfo.ServiceAlias + @"</service>
</usercredentials>
</request>
";
            XDocument doc = SendRawRequest(strXML);

            if (doc.Element(@"response") != null)
            {
                XElement successEl = doc.Element(@"response").Element(@"success");
                if (successEl != null)
                {
                    if (successEl.Value.ToLower() == @"true")
                    {
                        VBPost post = null;
                        if (doc.Element(@"response").Element(@"post") != null)
                        {
                            Dictionary<string, string> postInfo = new Dictionary<string, string>();
                            var vals = (from xElem in doc.Descendants(@"response").Descendants(@"post")
                                        select xElem).Single();

                            XElement el = vals as XElement;
                            var subVals = from subElem in el.Descendants()
                                          select subElem;

                            foreach (XElement subel in subVals)
                            {
                                postInfo.Add(subel.Name.ToString(), subel.Value.ToString());
                            }

                            post = new VBPost(postInfo);
                        }

                        vbrr = new VBRequestResult(VBRequestResultCode.Success, string.Empty, post);
                    }
                    else
                    {
                        vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Service Error", null);
                        log.Error("GetPostByIndex() service error");
                        log.Error(string.Format("Last Request: {0}", LastRequest));
                        log.Error(string.Format("Last Response: {0}", LastResponse));
                    }
                }
                else
                {
                    vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Service Error", null);
                    log.Error("GetPostByIndex() corrupt response: no <success> element in XML");
                    log.Error(string.Format("Last Request: {0}", LastRequest));
                    log.Error(string.Format("Last Response: {0}", LastResponse));
                }
            }
            else
            {
                vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Service Error", null);
                log.Error("GetPostByIndex() corrupt response: no <response> element in XML");
                log.Error(string.Format("Last Request: {0}", LastRequest));
                log.Error(string.Format("Last Response: {0}", LastResponse));
            }


            return vbrr;
        }

        public VBRequestResult GetPostNotifications(bool bDelete)
        {
            VBRequestResult vbrr = null;

            StringWriter stringWriter = new StringWriter();
            XmlTextWriter xmlWriter = new XmlTextWriter(stringWriter);
            xmlWriter.Formatting = Formatting.Indented;

            xmlWriter.WriteStartDocument();
            xmlWriter.WriteStartElement(@"request");
            xmlWriter.WriteElementString(@"command", @"getpostnotifications");
            xmlWriter.WriteElementString(@"delete", bDelete.ToString().ToLower());

            // <request> tag
            xmlWriter.WriteEndElement();
            xmlWriter.Flush();
            xmlWriter.Close();
            stringWriter.Flush();

            XDocument doc = SendRawRequest(stringWriter.ToString());

            if (doc.Element(@"response") != null)
            {
                XElement successEl = doc.Element(@"response").Element(@"success");
                if (successEl != null)
                {
                    if (successEl.Value.ToLower() == @"true")
                    {
                        List<Dictionary<string, string>> listDict = new List<Dictionary<string, string>>();

                        if (doc.Element(@"response").Elements(@"notification") != null)
                        {
                            foreach (XElement notElement in doc.Element(@"response").Elements(@"notification"))
                            {
                                if (notElement.Element(@"instantimnotification") != null && notElement.Element(@"instantimnotification").Value.ToLower() == "1")
                                {
                                    Dictionary<string, string> dict = new Dictionary<string, string>();

                                    // TODO: iterate through the elements,a dding them all to the dictionary
                                    foreach (XElement detailElement in notElement.Elements())
                                    {
                                        string strFieldName = detailElement.Name.ToString();
                                        dict.Add(strFieldName, notElement.Element(strFieldName).Value);
                                    }
                                    dict["dateline"] = dict["postdateline"];

                                    //dict.Add(@"postid", notElement.Element(@"postid").Value);
                                    //dict.Add(@"title", notElement.Element(@"title").Value);
                                    //dict.Add(@"username", notElement.Element(@"username").Value);
                                    //dict.Add(@"pagetext", notElement.Element(@"pagetext").Value);
                                    //dict.Add(@"dateline", notElement.Element(@"dateline").Value);
                                    //dict.Add(@"isnew", true.ToString().ToLower());

                                    //dict.Add(@"threadid", notElement.Element(@"threadid").Value);
                                    //dict.Add(@"instantimservice",notElement.Element(@"instantimservice").Value);
                                    //dict.Add(@"instantimscreenname", notElement.Element(@"instantimscreenname").Value);

                                    listDict.Add(dict);
                                }
                            }
                        }

                        vbrr = new VBRequestResult(VBRequestResultCode.Success, string.Empty, listDict);

                    }
                    else
                    {
                        vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Service Error", null);
                        log.Error("TurnOnOffIMNotification() service error");
                        log.Error(string.Format("Last Request: {0}", LastRequest));
                        log.Error(string.Format("Last Response: {0}", LastResponse));
                    }

                }
                else
                {
                    vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Service Error", null);
                    log.Error("TurnOnOffIMNotification() corrupt response: no <success> element in XML");
                    log.Error(string.Format("Last Request: {0}", LastRequest));
                    log.Error(string.Format("Last Response: {0}", LastResponse));
                }
            }
            else
            {
                vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Service Error", null);
                log.Error("TurnOnOffIMNotification() corrupt response: no <response> element in XML");
                log.Error(string.Format("Last Request: {0}", LastRequest));
                log.Error(string.Format("Last Response: {0}", LastResponse));
            }

            return vbrr;
        }

        public VBRequestResult GetThread(ResponseChannel iminfo, int iThreadID)
        {
            VBRequestResult vbrr = null;

            StringWriter stringWriter = new StringWriter();
            XmlTextWriter xmlWriter = new XmlTextWriter(stringWriter);
            xmlWriter.Formatting = Formatting.Indented;

            xmlWriter.WriteStartDocument();
            xmlWriter.WriteStartElement(@"request");

            xmlWriter.WriteElementString(@"command", @"ft");
            xmlWriter.WriteElementString(@"threadid", iThreadID.ToString());

            xmlWriter.WriteStartElement(@"usercredentials");
            xmlWriter.WriteElementString(@"type", @"service");
            xmlWriter.WriteElementString(@"service", iminfo.ServiceAlias);
            xmlWriter.WriteElementString(@"screenname", iminfo.ScreenName);
            xmlWriter.WriteEndElement();

            // <request> tag
            xmlWriter.WriteEndElement();
            xmlWriter.Flush();
            xmlWriter.Close();
            stringWriter.Flush();

            XDocument doc = SendRawRequest(stringWriter.ToString());

            if (doc.Element(@"response") != null)
            {
                XElement successEl = doc.Element(@"response").Element(@"success");
                if (successEl != null)
                {
                    if (successEl.Value.ToLower() == @"true")
                    {
                        Dictionary<string, string> info = new Dictionary<string, string>();

                        foreach (XNode el in doc.Element(@"response").Elements("thread").Nodes())
                        {
                            if (el.NodeType == XmlNodeType.Element)
                            {
                                XElement xel = el as XElement;
                                info.Add(xel.Name.ToString(), xel.Value.ToString());
                            }
                        }

                        vbrr = new VBRequestResult(VBRequestResultCode.Success, string.Empty, info);
                    }
                    else
                    {
                        vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Service Error", null);
                        log.Error("GetThread() service error");
                        log.Error(string.Format("Last Request: {0}", LastRequest));
                        log.Error(string.Format("Last Response: {0}", LastResponse));
                    }
                }
                else
                {
                    vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Service Error", null);
                    log.Error("GetThread() corrupt response: no <success> element in XML");
                    log.Error(string.Format("Last Request: {0}", LastRequest));
                    log.Error(string.Format("Last Response: {0}", LastResponse));
                }
            }
            else
            {
                vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Service Error", null);
                log.Error("GetThread() corrupt response: no <response> element in XML");
                log.Error(string.Format("Last Request: {0}", LastRequest));
                log.Error(string.Format("Last Response: {0}", LastResponse));
            }

            return vbrr;
        }

        public VBRequestResult ListForums(ResponseChannel imuserinfo, int iForumID)
        {
            VBRequestResult vbrr = new VBRequestResult();
            Dictionary<string, string> userdict = new Dictionary<string, string>();

            string strXML = @"
            <request>
            	<command>lf</command>
                <forumid>" + iForumID.ToString() + @"</forumid>
            	<usercredentials>
            		<type>service</type>
            		<screenname>" + imuserinfo.ScreenName + @"</screenname>
                    <service>" + imuserinfo.ServiceAlias + @"</service>
            	</usercredentials>
            </request>
            ";

            XDocument doc = SendRawRequest(strXML);

            if (doc.Descendants() != null && doc.Descendants(@"response") != null)
            {
                XElement successEl = doc.Descendants(@"success").Single();
                if (successEl != null && successEl.Value.ToLower() == @"true")
                {
                    // create the forums list object
                    List<Dictionary<string, string>> retdict = new List<Dictionary<string, string>>();


                    XElement userEl = doc.Descendants(@"userinfo").Single();
                    var userVals = from xuserEl in userEl.Descendants()
                                   select xuserEl;

                    foreach (XElement el in userVals)
                    {
                        userdict.Add(el.Name.ToString(), el.Value.ToString());
                    }

                    XElement forumsEl = doc.Descendants(@"forums").Single();
                    var vals = from xElem in forumsEl.Descendants(@"forum")
                               select xElem;

                    var xRootElement = (from xElem in doc.Descendants(@"response") select xElem).Single();

                    if (xRootElement.Attribute(@"title") != null && xRootElement.Attribute(@"forumid") != null)
                    {
                        Dictionary<string, string> tempHash = new Dictionary<string, string>();
                        tempHash.Add(@"title", xRootElement.Attribute(@"title").Value);
                        tempHash.Add(@"forumid", xRootElement.Attribute(@"forumid").Value);
                        tempHash.Add(@"iscurrent", "1");

                        if (xRootElement.Attribute(@"isnew") != null)
                        {
                            tempHash.Add(@"isnew", xRootElement.Attribute(@"isnew").Value.ToLower());
                        }

                        retdict.Add(tempHash);
                    }

                    foreach (XElement el in vals)
                    {
                        Dictionary<string, string> newForum = new Dictionary<string, string>();
                        var subVals = from subElem in el.Descendants()
                                      select subElem;

                        foreach (XElement subel in subVals)
                        {
                            newForum.Add(subel.Name.ToString(), subel.Value.ToString());
                        }

                        retdict.Add(newForum);
                    }

                    vbrr = new VBRequestResult(VBRequestResultCode.Success, string.Empty, retdict);
                }
                else if (successEl != null && successEl.Value.ToLower() != @"true")
                {
                    vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Service Error", null);
                    log.Error("ListForums() service error");
                    log.Error(string.Format("Last Request: {0}", LastRequest));
                    log.Error(string.Format("Last Response: {0}", LastResponse));
                }
                else
                {
                    vbrr = new VBRequestResult(VBRequestResultCode.Error, @"ListForums() Corrupt Reponse", null);
                    log.Error("ListForums() corrupt response: no <success> element in XML");
                    log.Error(string.Format("Last Request: {0}", LastRequest));
                    log.Error(string.Format("Last Response: {0}", LastResponse));
                }

            }
            else
            {
                vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Invalid return XML", null);
            }

            vbrr.UserInfo = userdict;
            return vbrr;
        }

        public VBRequestResult ListParentForums(ResponseChannel imuserinfo, int iForumID)
        {
            VBRequestResult vbrr = new VBRequestResult();
            List<Dictionary<string, string>> retdict = new List<Dictionary<string, string>>();

            string strXML = @"
<request>
<command>lpf</command>
<forumid>" + iForumID.ToString() + @"</forumid>
<usercredentials>
	<type>service</type>
	<screenname>" + imuserinfo.ScreenName + @"</screenname>
    <service>" + imuserinfo.ServiceAlias + @"</service>
</usercredentials>
</request>
";
            XDocument doc = SendRawRequest(strXML);

            if (doc.Descendants() != null && doc.Descendants(@"response") != null)
            {
                XElement successEl = doc.Descendants(@"success").Single();
                if (successEl != null && successEl.Value.ToLower() == @"true")
                {
                    Dictionary<string, string> userdict = new Dictionary<string, string>();
                    XElement userEl = doc.Descendants(@"userinfo").Single();

                    var userVals = from xuserEl in userEl.Descendants()
                                   select xuserEl;

                    foreach (XElement el in userVals)
                    {
                        userdict.Add(el.Name.ToString(), el.Value.ToString());
                    }

                    var vals = from xElem in doc.Descendants(@"response").Descendants(@"forum")
                               select xElem;

                    var xRootElement = (from xElem in doc.Descendants(@"response") select xElem).Single();

                    if (xRootElement.Attribute(@"title") != null && xRootElement.Attribute(@"forumid") != null)
                    {
                        Dictionary<string, string> tempHash = new Dictionary<string, string>();
                        tempHash.Add(@"title", xRootElement.Attribute(@"title").Value);
                        tempHash.Add(@"forumid", xRootElement.Attribute(@"forumid").Value);
                        tempHash.Add(@"iscurrent", "1");

                        if (xRootElement.Attribute(@"isnew") != null)
                        {
                            tempHash.Add(@"isnew", xRootElement.Attribute(@"isnew").Value.ToLower());
                        }

                        retdict.Add(tempHash);
                    }

                    foreach (XElement el in vals)
                    {
                        Dictionary<string, string> newForum = new Dictionary<string, string>();
                        var subVals = from subElem in el.Descendants()
                                      select subElem;

                        foreach (XElement subel in subVals)
                        {
                            newForum.Add(subel.Name.ToString(), subel.Value.ToString());
                        }

                        retdict.Add(newForum);
                    }

                    vbrr = new VBRequestResult(VBRequestResultCode.Success, string.Empty, retdict);
                    vbrr.UserInfo = userdict;
                }
                else if (successEl != null && successEl.Value.ToLower() != @"true")
                {
                    vbrr = new VBRequestResult(VBRequestResultCode.Error, @"ervice Error", null);
                    log.Error("ListParentForums() service error");
                    log.Error(string.Format("Last Request: {0}", LastRequest));
                    log.Error(string.Format("Last Response: {0}", LastResponse));
                }
                else
                {
                    vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Corrupt Reponse", null);
                    log.Error("ListParentForums() corrupt response: no <success> element in XML");
                    log.Error(string.Format("Last Request: {0}", LastRequest));
                    log.Error(string.Format("Last Response: {0}", LastResponse));
                }
            }

            return vbrr;
        }

        public VBRequestResult ListPosts(ResponseChannel imuserinfo, int iThreadId, int iPageNumber, int iPerPage)
        {
            VBThread t = null;
            return ListPosts(imuserinfo, iThreadId, iPageNumber, iPerPage, out t);
        }

        public VBRequestResult ListPosts(ResponseChannel imuserinfo, int iThreadId, int iPageNumber, int iPerPage, out VBThread thread)
        {
            VBRequestResult vbrr = new VBRequestResult();
            List<VBPost> retdict = new List<VBPost>();
            VBThread retThread = null;

            string strXML = @"
<request>
<command>lp</command>
    <threadid>" + iThreadId.ToString() + @"</threadid>
    <pagenumber>" + iPageNumber.ToString() + @"</pagenumber>
    <perpage>" + iPerPage.ToString() + @"</perpage>
<usercredentials>
    <type>service</type>
    <screenname>" + imuserinfo.ScreenName + @"</screenname>
    <service>" + imuserinfo.ServiceAlias + @"</service>
</usercredentials>
</request>
";
            XDocument doc = SendRawRequest(strXML);

            if (doc.Element(@"response") != null)
            {
                XElement successEl = doc.Element(@"response").Element(@"success");
                if (successEl != null)
                {
                    if (successEl.Value.ToLower() == @"true")
                    {
                        if (doc.Descendants(@"response").Descendants(@"thread") != null)
                        {
                            Dictionary<string, string> threadDict = new Dictionary<string, string>();

                            var threadVals = (from xElem in doc.Descendants(@"response").Descendants(@"thread")
                                              select xElem).Single();

                            var threadSubVals = from subElem in threadVals.Descendants()
                                                select subElem;

                            foreach (XElement subel in threadSubVals)
                            {
                                threadDict.Add(subel.Name.ToString(), subel.Value.ToString());
                            }

                            retThread = new VBThread(threadDict);
                        }

                        var vals = from xElem in doc.Descendants(@"response").Descendants(@"post")
                                   select xElem;

                        foreach (XElement el in vals)
                        {
                            Dictionary<string, string> postInfo = new Dictionary<string, string>();
                            var subVals = from subElem in el.Descendants()
                                          select subElem;

                            foreach (XElement subel in subVals)
                            {
                                postInfo.Add(subel.Name.ToString(), subel.Value.ToString());
                            }

                            VBPost post = new VBPost(postInfo);
                            retdict.Add(post);
                        }

                        vbrr = new VBRequestResult(VBRequestResultCode.Success, string.Empty, retdict);
                    }
                    else
                    {
                        vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Service Error", null);
                        log.Error("ListPosts() service error");
                        log.Error(string.Format("Last Request: {0}", LastRequest));
                        log.Error(string.Format("Last Response: {0}", LastResponse));
                    }
                }
                else
                {
                    vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Service Error", null);
                    log.Error("ListPosts() corrupt response: no <success> element in XML");
                    log.Error(string.Format("Last Request: {0}", LastRequest));
                    log.Error(string.Format("Last Response: {0}", LastResponse));
                }
            }
            else
            {
                vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Service Error", null);
                log.Error("ListPosts() corrupt response: no <response> element in XML");
                log.Error(string.Format("Last Request: {0}", LastRequest));
                log.Error(string.Format("Last Response: {0}", LastResponse));
            }

            thread = retThread;
            return vbrr;
        }

        public VBRequestResult ListThreads(ResponseChannel imuserinfo, int iForumID, int iPageNumber, int iPerPage)
        {
            VBRequestResult vbrr = new VBRequestResult();
            List<VBThread> retdict = new List<VBThread>();


            string strXML = @"
<request>
<command>lt</command>
    <forumid>" + iForumID.ToString() + @"</forumid>
    <pagenumber>" + iPageNumber.ToString() + @"</pagenumber>
    <perpage>" + iPerPage.ToString() + @"</perpage>
<usercredentials>
        		<type>service</type>
        		<screenname>" + imuserinfo.ScreenName + @"</screenname>
                <service>" + imuserinfo.ServiceAlias + @"</service>
</usercredentials>
</request>
";
            XDocument doc = SendRawRequest(strXML);

            if (doc.Element(@"response") != null)
            {
                XElement successEl = doc.Element(@"response").Element(@"success");
                if (successEl != null)
                {
                    if (successEl.Value.ToLower() == @"true")
                    {
                        var vals = from xElem in doc.Descendants(@"response").Descendants(@"thread")
                                   select xElem;

                        foreach (XElement el in vals)
                        {
                            Dictionary<string, string> threadInfo = new Dictionary<string, string>();
                            var subVals = from subElem in el.Descendants()
                                          select subElem;

                            foreach (XElement subel in subVals)
                            {
                                threadInfo.Add(subel.Name.ToString(), subel.Value.ToString());
                            }

                            VBThread thread = new VBThread(threadInfo);
                            retdict.Add(thread);
                        }

                        vbrr = new VBRequestResult(VBRequestResultCode.Success, string.Empty, retdict);
                    }
                    else
                    {
                        vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Service Error", null);
                        log.Error("ListThreads() service error");
                        log.Error(string.Format("Last Request: {0}", LastRequest));
                        log.Error(string.Format("Last Response: {0}", LastResponse));
                    }

                }
                else
                {
                    vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Service Error", null);
                    log.Error("ListThreads() corrupt response: no <success> element in XML");
                    log.Error(string.Format("Last Request: {0}", LastRequest));
                    log.Error(string.Format("Last Response: {0}", LastResponse));
                }
            }
            else
            {
                vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Service Error", null);
                log.Error("ListThreads() corrupt response: no <response> element in XML");
                log.Error(string.Format("Last Request: {0}", LastRequest));
                log.Error(string.Format("Last Response: {0}", LastResponse));
            }

            return vbrr;
        }

        public VBRequestResult MarkRead(ResponseChannel iminfo, int iID, string strField)
        {
            VBRequestResult vbrr = null;
            StringWriter stringWriter = new StringWriter();
            XmlTextWriter xmlWriter = new XmlTextWriter(stringWriter);

            xmlWriter.Formatting = Formatting.Indented;

            xmlWriter.WriteStartDocument();
            xmlWriter.WriteStartElement(@"request");

            if (strField.ToLower() == "thread")
            {
                xmlWriter.WriteElementString(@"command", @"mtr");
            }
            else if (strField.ToLower() == "forum")
            {
                xmlWriter.WriteElementString(@"command", @"mfr");
            }
            xmlWriter.WriteElementString(strField + @"id", iID.ToString());

            // write user credentials
            xmlWriter.WriteStartElement(@"usercredentials");
            xmlWriter.WriteElementString(@"type", @"service");
            xmlWriter.WriteElementString(@"service", iminfo.ServiceAlias);
            xmlWriter.WriteElementString(@"screenname", iminfo.ScreenName);
            xmlWriter.WriteEndElement();

            // <request> tag
            xmlWriter.WriteEndElement();
            xmlWriter.Flush();
            xmlWriter.Close();
            stringWriter.Flush();

            XDocument doc = SendRawRequest(stringWriter.ToString());

            if (doc.Element(@"response") != null)
            {
                XElement successEl = doc.Element(@"response").Element(@"success");
                if (successEl != null)
                {
                    if (successEl.Value.ToLower() == @"true")
                    {
                        XElement xe = doc.Descendants(@"response").Descendants(strField).Single();
                        if (xe.Element(strField + @"id") != null)
                        {
                            vbrr = new VBRequestResult(VBRequestResultCode.Success, string.Empty, null);
                        }
                    }
                    else
                    {
                        vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Service Error", null);
                        log.Error("MarkRead() service error");
                        log.Error(string.Format("Last Request: {0}", LastRequest));
                        log.Error(string.Format("Last Response: {0}", LastResponse));
                    }

                }
                else
                {
                    vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Service Error", null);
                    log.Error("MarkRead() service error");
                    log.Error(string.Format("Last Request: {0}", LastRequest));
                    log.Error(string.Format("Last Response: {0}", LastResponse));
                }
            }
            else
            {
                vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Service Error", null);
                log.Error("MarkRead() corrupt response: no <response> element in XML");
                log.Error(string.Format("Last Request: {0}", LastRequest));
                log.Error(string.Format("Last Response: {0}", LastResponse));
            }

            return vbrr;
        }

        public VBRequestResult PostReply(ResponseChannel iminfo, int iThreadID, string strPageText)
        {
            VBRequestResult vbrr = new VBRequestResult();
            int iPostID = 0;
            StringWriter stringWriter = new StringWriter();
            XmlTextWriter xmlWriter = new XmlTextWriter(stringWriter);
            xmlWriter.Formatting = Formatting.Indented;

            xmlWriter.WriteStartDocument();
            xmlWriter.WriteStartElement(@"request");


            xmlWriter.WriteElementString(@"command", @"reply");
            xmlWriter.WriteElementString(@"threadid", iThreadID.ToString());

            xmlWriter.WriteStartElement(@"pagetext");
            xmlWriter.WriteCData(strPageText);
            xmlWriter.WriteEndElement();

            xmlWriter.WriteStartElement(@"usercredentials");
            xmlWriter.WriteElementString(@"type", @"service");
            xmlWriter.WriteElementString(@"service", iminfo.ServiceAlias);
            xmlWriter.WriteElementString(@"screenname", iminfo.ScreenName);
            xmlWriter.WriteEndElement();

            // <request> tag
            xmlWriter.WriteEndElement();
            xmlWriter.Flush();
            xmlWriter.Close();
            stringWriter.Flush();

            XDocument doc = SendRawRequest(stringWriter.ToString());

            if (doc.Element(@"response") != null)
            {
                XElement successEl = doc.Element(@"response").Element(@"success");
                if (successEl != null)
                {
                    if (successEl.Value.ToLower() == @"true")
                    {
                        if (doc.Element(@"response").Element(@"postid") != null)
                        {
                            string strTemp = doc.Element(@"response").Element(@"postid").Value;
                            iPostID = int.Parse(strTemp);
                            vbrr = new VBRequestResult(VBRequestResultCode.Success, string.Empty, iPostID);
                        }
                        else
                        {
                            vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Response Error", null);
                            log.Error("PostReply() response error, no `postid` element");
                            log.Error(string.Format("Last Request: {0}", LastRequest));
                            log.Error(string.Format("Last Response: {0}", LastResponse));
                        }
                    }
                    else
                    {
                        vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Service Error", null);
                        log.Error("PostReply() service error");
                        log.Error(string.Format("Last Request: {0}", LastRequest));
                        log.Error(string.Format("Last Response: {0}", LastResponse));
                    }
                }
                else
                {
                    vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Service Error", null);
                    log.Error("PostReply() corrupt response: no <success> element in XML");
                    log.Error(string.Format("Last Request: {0}", LastRequest));
                    log.Error(string.Format("Last Response: {0}", LastResponse));
                }
            }
            else
            {
                vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Service Error", null);
                log.Error("PostReply() corrupt response: no <response> element in XML");
                log.Error(string.Format("Last Request: {0}", LastRequest));
                log.Error(string.Format("Last Response: {0}", LastResponse));
            }

            return vbrr;
        }

        public VBRequestResult SubscribeThread(ResponseChannel iminfo, int iThreadID)
        {
            VBRequestResult vbrr = null;

            StringWriter stringWriter = new StringWriter();
            XmlTextWriter xmlWriter = new XmlTextWriter(stringWriter);
            xmlWriter.Formatting = Formatting.Indented;

            xmlWriter.WriteStartDocument();
            xmlWriter.WriteStartElement(@"request");

            xmlWriter.WriteElementString(@"command", @"sub");
            xmlWriter.WriteElementString(@"threadid", iThreadID.ToString());

            xmlWriter.WriteStartElement(@"usercredentials");
            xmlWriter.WriteElementString(@"type", @"service");
            xmlWriter.WriteElementString(@"service", iminfo.ServiceAlias);
            xmlWriter.WriteElementString(@"screenname", iminfo.ScreenName);
            xmlWriter.WriteEndElement();

            // <request> tag
            xmlWriter.WriteEndElement();
            xmlWriter.Flush();
            xmlWriter.Close();
            stringWriter.Flush();

            XDocument doc = SendRawRequest(stringWriter.ToString());

            if (doc.Element(@"response") != null)
            {
                XElement successEl = doc.Element(@"response").Element(@"success");
                if (successEl != null)
                {
                    if (successEl.Value.ToLower() == @"true")
                    {
                        if (doc.Element(@"response").Element(@"thread") != null)
                        {
                            if (doc.Element(@"response").Element(@"thread").Element(@"threadid") != null)
                            {
                                XElement threadid = doc.Element(@"response").Element(@"thread").Element(@"threadid");
                                int iConfThreadID = int.Parse(threadid.Value);

                                vbrr = new VBRequestResult(VBRequestResultCode.Success, string.Empty, iConfThreadID);
                            }
                            else
                            {
                                vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Service Error", null);
                                log.Error("SubscribeThread() to <threadid> element");
                                log.Error(string.Format("Last Request: {0}", LastRequest));
                                log.Error(string.Format("Last Response: {0}", LastResponse));
                            }
                        }
                        else
                        {
                            vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Service Error", null);
                            log.Error("SubscribeThread() to <thread> element");
                            log.Error(string.Format("Last Request: {0}", LastRequest));
                            log.Error(string.Format("Last Response: {0}", LastResponse));
                        }
                    }
                    else
                    {
                        vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Service Error", null);
                        log.Error("SubscribeThread() service error");
                        log.Error(string.Format("Last Request: {0}", LastRequest));
                        log.Error(string.Format("Last Response: {0}", LastResponse));
                    }

                }
                else
                {
                    vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Service Error", null);
                    log.Error("SubscribeThread() corrupt response: no <success> element in XML");
                    log.Error(string.Format("Last Request: {0}", LastRequest));
                    log.Error(string.Format("Last Response: {0}", LastResponse));
                }
            }
            else
            {
                vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Service Error", null);
                log.Error("SubscribeThread() corrupt response: no <response> element in XML");
                log.Error(string.Format("Last Request: {0}", LastRequest));
                log.Error(string.Format("Last Response: {0}", LastResponse));
            }

            return vbrr;
        }

        public VBRequestResult TurnOnOffIMNotification(ResponseChannel iminfo, bool bOn)
        {
            VBRequestResult vbrr = null;

            StringWriter stringWriter = new StringWriter();
            XmlTextWriter xmlWriter = new XmlTextWriter(stringWriter);
            xmlWriter.Formatting = Formatting.Indented;

            xmlWriter.WriteStartDocument();
            xmlWriter.WriteStartElement(@"request");

            string strCommand = "im" + (bOn ? "on" : "off");
            xmlWriter.WriteElementString(@"command", strCommand);

            xmlWriter.WriteStartElement(@"usercredentials");
            xmlWriter.WriteElementString(@"type", @"service");
            xmlWriter.WriteElementString(@"service", iminfo.ServiceAlias);
            xmlWriter.WriteElementString(@"screenname", iminfo.ScreenName);
            xmlWriter.WriteEndElement();

            // <request> tag
            xmlWriter.WriteEndElement();
            xmlWriter.Flush();
            xmlWriter.Close();
            stringWriter.Flush();

            XDocument doc = SendRawRequest(stringWriter.ToString());

            if (doc.Element(@"response") != null)
            {
                XElement successEl = doc.Element(@"response").Element(@"success");
                if (successEl != null)
                {
                    if (successEl.Value.ToLower() == @"true")
                    {
                        vbrr = new VBRequestResult(VBRequestResultCode.Success, string.Empty, null);
                    }
                    else
                    {
                        vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Service Error", null);
                        log.Error("TurnOnOffIMNotification() service error");
                        log.Error(string.Format("Last Request: {0}", LastRequest));
                        log.Error(string.Format("Last Response: {0}", LastResponse));
                    }

                }
                else
                {
                    vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Service Error", null);
                    log.Error("TurnOnOffIMNotification() corrupt response: no <success> element in XML");
                    log.Error(string.Format("Last Request: {0}", LastRequest));
                    log.Error(string.Format("Last Response: {0}", LastResponse));
                }
            }
            else
            {
                vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Service Error", null);
                log.Error("TurnOnOffIMNotification() corrupt response: no <response> element in XML");
                log.Error(string.Format("Last Request: {0}", LastRequest));
                log.Error(string.Format("Last Response: {0}", LastResponse));
            }

            return vbrr;
        }

        public VBRequestResult UnSubscribeThread(ResponseChannel iminfo, int iThreadID)
        {
            VBRequestResult vbrr = null;

            StringWriter stringWriter = new StringWriter();
            XmlTextWriter xmlWriter = new XmlTextWriter(stringWriter);
            xmlWriter.Formatting = Formatting.Indented;

            xmlWriter.WriteStartDocument();
            xmlWriter.WriteStartElement(@"request");

            xmlWriter.WriteElementString(@"command", @"unsub");

            string strThreadID = iThreadID.ToString();
            if (iThreadID == -1)
            {
                strThreadID = "all";
            }

            xmlWriter.WriteElementString(@"threadid", strThreadID);

            xmlWriter.WriteStartElement(@"usercredentials");
            xmlWriter.WriteElementString(@"type", @"service");
            xmlWriter.WriteElementString(@"service", iminfo.ServiceAlias);
            xmlWriter.WriteElementString(@"screenname", iminfo.ScreenName);
            xmlWriter.WriteEndElement();

            // <request> tag
            xmlWriter.WriteEndElement();
            xmlWriter.Flush();
            xmlWriter.Close();
            stringWriter.Flush();

            XDocument doc = SendRawRequest(stringWriter.ToString());

            if (doc.Element(@"response") != null)
            {
                XElement successEl = doc.Element(@"response").Element(@"success");
                if (successEl != null)
                {
                    if (successEl.Value.ToLower() == @"true")
                    {
                        vbrr = new VBRequestResult(VBRequestResultCode.Success, string.Empty, null);
                    }
                    else
                    {
                        vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Service Error", null);
                        log.Error("SubscribeThread() service error");
                        log.Error(string.Format("Last Request: {0}", LastRequest));
                        log.Error(string.Format("Last Response: {0}", LastResponse));
                    }

                }
                else
                {
                    vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Service Error", null);
                    log.Error("SubscribeThread() corrupt response: no <success> element in XML");
                    log.Error(string.Format("Last Request: {0}", LastRequest));
                    log.Error(string.Format("Last Response: {0}", LastResponse));
                }
            }
            else
            {
                vbrr = new VBRequestResult(VBRequestResultCode.Error, @"Service Error", null);
                log.Error("SubscribeThread() corrupt response: no <response> element in XML");
                log.Error(string.Format("Last Request: {0}", LastRequest));
                log.Error(string.Format("Last Response: {0}", LastResponse));
            }

            return vbrr;
        }



        public XDocument SendRawRequest(string strXml)
        {
            lock (this)
            {
                // add the bot credentials to the xml
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(strXml);
                XmlElement botCreds = doc.CreateElement(@"botcredentials");
                XmlElement pwElement = doc.CreateElement(@"servicepw");
                pwElement.InnerText = ServicePassword;
                botCreds.AppendChild(pwElement);
                doc.DocumentElement.AppendChild(botCreds);

                WebClient client = new WebClient();

                               _strLastRequest = doc.InnerXml;
                _strLastResponse = string.Empty;
                byte[] postArray = Encoding.ASCII.GetBytes(doc.InnerXml);

                client.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                byte[] responseArray = client.UploadData(_strServiceUrl, postArray);
                _iRequestCount++;

                _strLastResponse = Encoding.ASCII.GetString(responseArray);
                XDocument retdoc = XDocument.Parse(LastResponse);
                return retdoc;
            }
        }
        public VBPost GetPost(int iVBUserId, int iPostID)
        {
            VBPost post = null;

            if (iVBUserId > 0)
            {
                string strXML = @"
<request>
	<command>gp</command>
        <postid>" + iPostID.ToString() + @"</postid>
	<usercredentials>
		<type>userid</type>
		<userid>" + iVBUserId.ToString() + @"</userid>
	</usercredentials>
</request>
";
                XDocument doc = SendRawRequest(strXML);

                if (doc.Descendants(@"response") != null && doc.Descendants(@"response").Descendants(@"post") != null)
                {
                    var vals = (from xElem in doc.Descendants(@"response").Descendants(@"post")
                               select xElem).Single();

                    //foreach (XElement el in vals)
                    XElement el = vals as XElement;

                    Dictionary<string, string> postInfo = new Dictionary<string, string>();
                    var subVals = from subElem in el.Descendants()
                                  select subElem;

                    foreach (XElement subel in subVals)
                    {
                        postInfo.Add(subel.Name.ToString(), subel.Value.ToString());
                    }

                    post = new VBPost(postInfo);
                }
            }

            return post;
        }

        public Dictionary<string, string> WhoAMI(string strUsername, string strServiceName)
        {
            Dictionary<string, string> retval = new Dictionary<string, string>();
            string strXML = @"
<request>
	<command>whoami</command>
	<usercredentials>
		<type>service</type>
		<screenname>"+strUsername+@"</screenname>
        <service>"+strServiceName+@"</service>
	</usercredentials>
</request>
";
            try
            {
                XDocument doc = SendRawRequest(strXML);

                if (doc.Descendants(@"response") != null && doc.Descendants(@"response").Descendants(@"user") != null)
                {
                    var vals = from xElem in doc.Descendants(@"response").Descendants(@"user")
                               select xElem;

                    foreach (XElement el in vals.Descendants())
                    {
                        if (el.Value.Length > 0)
                        {
                            retval.Add(el.Name.ToString(), el.Value);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Debug(@"Service error", ex);
                return retval;
            }

            return retval;
        }
    }
}

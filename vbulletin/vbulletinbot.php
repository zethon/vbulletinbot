<?php
//-----------------------------------------------------------------------------
// $RCSFile: vbotservice.php $ $Revision: 1.19 $
// $Date: 2010/01/05 06:14:58 $
//-----------------------------------------------------------------------------

// hack for vbulletin 4.0 and CSRF Protection
$_POST["vb4"] = "";

// ######################## SET PHP ENVIRONMENT ###########################
error_reporting(E_ALL & ~E_NOTICE);

// ##################### DEFINE IMPORTANT CONSTANTS #######################
define('THIS_SCRIPT', 'vbulletinbot.php');
define('CSRF_PROTECTION', false); 
define('DIE_QUIETLY', 1);

// ########################## REQUIRE BACK-END ############################
require_once("nusoap/nusoap.php");

require_once('./global.php');
require_once(DIR . '/includes/class_dm.php');
require_once(DIR . '/includes/class_dm_threadpost.php');
require_once(DIR . '/includes/class_xml.php');
require_once(DIR . '/includes/functions_bigthree.php');
require_once(DIR . '/includes/functions_forumlist.php');
require_once(DIR . '/includes/functions_vbot.php');

// #######################################################################
// ######################## START MAIN SCRIPT ############################
// #######################################################################


 /**
 * ProcessSimpleType method
 * @param string $who name of the person we'll say hello to
 * @return string $helloText the hello  string
 */
function ProcessSimpleType($who) 
{
	$test = print_r($_SERVER,true);
	$who = "[$test]:$who";
	return "Hello $who";
}

function RegisterService($who)
{
	global $db,$vbulletin,$server;
	$result = array();
	
	if (!$vbulletin->options['vbb_serviceonoff'])
	{
		$result['Code'] = 1;
		$result['Text'] = 'vbb_service_turned_off';
	}	
	else if ($vbulletin->options['vbb_servicepw'] != $_SERVER['PHP_AUTH_PW'])
	{
		$result['Code'] = 1;
		$result['Text'] = 'vbb_invalid_servicepw';
	}
	else
	{
        $userid = fetch_userid_by_service($who['ServiceName'],$who['Username']);

        if (empty($userid) || $userid <= 0)
        {
            $result['Code'] = 1;
            $result['Text'] = 'invalid_user';
        }
        else
        {
            unset($vbulletin->userinfo);
            $vbulletin->userinfo = fetch_userinfo($userid);
            $permissions = cache_permissions($vbulletin->userinfo);            
            
		    // everything is ok
		    $result['Code'] = 0;
        }
	}
	
	return $result;
}

function ListForums($who,$forumid)
{
    global $db,$vbulletin,$server,$structtypes,$lastpostarray;
    
    $result = RegisterService($who);
    if ($result['Code'] != 0)
    {
        return $result;
    }

    $userid = $vbulletin->userinfo['userid'];
    //$xml = new XMLexporter($vbulletin);
    
    // ### GET FORUMS & MODERATOR iCACHES ########################
    cache_ordered_forums(1,1);
    if (empty($vbulletin->iforumcache))
    {
        $forums = $vbulletin->db->query_read_slave("
            SELECT forumid, title, link, parentid, displayorder, title_clean, description, description_clean,
            (options & " . $vbulletin->bf_misc_forumoptions['cancontainthreads'] . ") AS cancontainthreads
            FROM " . TABLE_PREFIX . "forum AS forum
            WHERE displayorder <> 0 AND
            password = '' AND
            (options & " . $vbulletin->bf_misc_forumoptions['active'] . ")
            ORDER BY displayorder
        ");
        
        $vbulletin->iforumcache = array();
        while ($forum = $vbulletin->db->fetch_array($forums))
        {
            $vbulletin->iforumcache["$forum[parentid]"]["$forum[displayorder]"]["$forum[forumid]"] = $forum;
        }
        unset($forum);
        $vbulletin->db->free_result($forums);
    }    

    // define max depth for forums display based on $vbulletin->options[forumhomedepth]
    define('MAXFORUMDEPTH', 1);
    
    if (is_array($vbulletin->iforumcache["$forumid"]))
    {
        $childarray = $vbulletin->iforumcache["$forumid"];
    }
    else
    {
        $childarray = array($vbulletin->iforumcache["$forumid"]);
    }
    
    if (!is_array($lastpostarray))
    {
        fetch_last_post_array();
    }    
    
    // add the current forum info
    // get the current location title
    $current = $db->query_first("SELECT title FROM " . TABLE_PREFIX . "forum AS forum WHERE (forumid = $forumid)");
    if (strlen($current['title']) == 0)
    {
        $current['title'] = 'INDEX';
    }

    $forum = fetch_foruminfo($forumid);
    $lastpostinfo = $vbulletin->forumcache["$lastpostarray[$forumid]"];    
    $isnew = fetch_forum_lightbulb($forumid, $lastpostinfo, $forum);
    
    $curforum['ForumID'] = $forumid;
    $curforum['Title'] = $current['title'];
    $curforum['IsNew'] = $isnew == "new";
    $curforum['IsCurrent'] = true;
    
    $forumlist = array();
    
    foreach ($childarray as $subforumid)
    {
        // hack out the forum id
        $forum = fetch_foruminfo($subforumid);
        if (!$forum['displayorder'] OR !($forum['options'] & $vbulletin->bf_misc_forumoptions['active']))
        {
            continue;
        }    

        $forumperms = $vbulletin->userinfo['forumpermissions']["$subforumid"];
        if (!($forumperms & $vbulletin->bf_ugp_forumpermissions['canview']) AND ($vbulletin->forumcache["$subforumid"]['showprivate'] == 1 OR (!$vbulletin->forumcache["$subforumid"]['showprivate'] AND !$vbulletin->options['showprivateforums'])))
        { // no permission to view current forum
            continue;
        }    
        
        $lastpostinfo = $vbulletin->forumcache["$lastpostarray[$subforumid]"];    
        $isnew = fetch_forum_lightbulb($forumid, $lastpostinfo, $forum);            
        
        $tempforum['ForumID'] = $forum['forumid'];
        $tempforum['Title'] = $forum['title'];
        $tempforum['IsNew'] = $isnew == "new";
        $tempforum['IsCurrent'] = false;
        array_push($forumlist,$tempforum);
        unset($tempforum);
    }        
    
    $result['RemoteUser'] = ConsumeArray($vbulletin->userinfo,$structtypes['RemoteUser']); 
    
    $retval['Result'] = $result;
    $retval['CurrentForum'] = $curforum;
    $retval['ForumList'] = $forumlist;
    
    return $retval;
}

function ListParentForums($who,$forumid)
{
    global $db,$vbulletin,$server,$structtypes,$lastpostarray;
    
    $tempinfo = fetch_foruminfo($forumid);
    
    if ($forumid == -1)    
    {
        return ListForums($who,-1);
    }
    
    if ($tempinfo['parentid'] != -1)
    {
        $info = fetch_foruminfo($tempinfo['parentid']);
        return ListForums($who,$info['forumid']);        
    }
    else
    {
        return ListForums($who,-1);        
    }      
}

function ListThreads($who,$forumid,$pagenumber,$perpage)
{
    global $db,$vbulletin,$server,$structtypes,$lastpostarray;
    
    $result = RegisterService($who);
    if ($result['Code'] != 0)
    {
        return $result;
    }

    // get the total threads count    
    $threadcount = $db->query_first("SELECT threadcount FROM " . TABLE_PREFIX . "forum WHERE (forumid = $forumid);");
    
    if ($threadcount > 0)
    {
        $forumperms = fetch_permissions($forumid);
        if (!($forumperms & $vbulletin->bf_ugp_forumpermissions['canview']))
        {
            // TODO: handle this properly
            //print_error_xml('no_permission_fetch_threadsxml');
        }            
        
        $userid = $vbulletin->userinfo['userid'];
        $limitlower = ($pagenumber - 1) * $perpage;
        
        $getthreadidssql = ("
            SELECT 
                thread.threadid, 
                thread.lastpost, 
                thread.lastposter, 
                thread.lastpostid, 
                thread.replycount, 
                IF(thread.views<=thread.replycount, thread.replycount+1, thread.views) AS views
            FROM " . TABLE_PREFIX . "thread AS thread
            WHERE forumid = $forumid
                AND sticky = 0
                AND visible = 1
            ORDER BY 
                lastpost DESC         
            LIMIT $limitlower, $perpage
        ");    
    
        $getthreadids = $db->query_read_slave($getthreadidssql);
        
        $ids = '';
        while ($thread = $db->fetch_array($getthreadids))
        {
            $ids .= ',' . $thread['threadid'];
        }
    
            $threadssql = "
                SELECT 
                    thread.threadid, 
                    thread.title AS threadtitle, 
                    thread.forumid, 
                    thread.lastpost, 
                    thread.lastposter, 
                    thread.lastpostid, 
                    thread.replycount,
                    threadread.readtime AS threadread,
                    forumread.readtime as forumread,
                    subscribethread.subscribethreadid AS subscribethreadid
                FROM " . TABLE_PREFIX . "thread AS thread    
                LEFT JOIN " . TABLE_PREFIX . "threadread AS threadread ON (threadread.threadid = thread.threadid AND threadread.userid = $userid)         
                LEFT JOIN " . TABLE_PREFIX . "forumread AS forumread ON (thread.forumid = forumread.forumid AND forumread.userid = $userid)         
                LEFT JOIN " . TABLE_PREFIX . "subscribethread AS subscribethread ON (thread.threadid = subscribethread.threadid AND subscribethread.userid = $userid)        
                WHERE thread.threadid IN (0$ids)
                ORDER BY lastpost DESC
            ";
            
        $threads = $db->query_read_slave($threadssql);        
        $threadlist = array();
    
        while ($thread = $db->fetch_array($threads))
        {   
            $thread['issubscribed'] = $thread['subscribethreadid'] > 0;
            
            $thread['isnew'] = true;        
            if ($thread['forumread'] >= $thread['lastpost'] || $thread['threadread'] >= $thread['lastpost'] || (TIMENOW - ($vbulletin->options['markinglimit'] * 86400)) > $thread['lastpost'] )
            {
                $thread['isnew'] = false;
            }

            $thread = ConsumeArray($thread,$structtypes['Thread']);            
            array_push($threadlist,$thread);
        }        
    }      
    
    $result['RemoteUser'] = ConsumeArray($vbulletin->userinfo,$structtypes['RemoteUser']);   
    $retval['Result'] = $result;
    $retval['ThreadList'] = $threadlist;
    $retval['ThreadCount'] = $threadcount['threadcount'];
    
    return $retval;
}

function WhoAmI($who)
{
	global $db,$vbulletin,$server,$structtypes;
	
	$result = RegisterService($who);
	if ($result['Code'] != 0)
	{
		return $result;
	}
	
	$retuser = ConsumeArray($vbulletin->userinfo,$structtypes['RemoteUser']);

	$result['Code'] = 0;
	$result['Text'] = '';
	$result['RemoteUser'] = ConsumeArray($vbulletin->userinfo,$structtypes['RemoteUser']); 
    
	return $result;
}

$namespace = $vbulletin->options['bburl'];

// create a new soap server
$server = new soap_server();

// configure our WSDL
$server->configureWSDL("VBotService","urn:VBotService");

// set our namespace
$server->wsdl->schemaTargetNamespace = $namespace;

// include service types and functions           
include (DIR . '/includes/types_vbot.php');
include (DIR . '/includes/services_vbot.php');

// Get our posted data if the service is being consumed
// otherwise leave this data blank.                
$HTTP_RAW_POST_DATA = isset($GLOBALS['HTTP_RAW_POST_DATA']) 
                ? $GLOBALS['HTTP_RAW_POST_DATA'] : '';

// pass our posted data (or nothing) to the soap service                    
$server->service($HTTP_RAW_POST_DATA);                
exit();
?>
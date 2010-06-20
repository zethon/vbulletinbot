<?php
//-----------------------------------------------------------------------------
// $RCSFile: functions_vbot.php $ $Revision: 1.21 $
// $Date: 2010/01/05 06:15:26 $
//-----------------------------------------------------------------------------

define('CVS_REVISION', '$RCSfile: functions_vbot.php,v $ - $Revision: 1.21 $');
error_reporting(E_ALL & ~E_NOTICE);

define('PAGE_NUMBER_DEFAULT',1);
define('PERPAGE_DEFAULT',5);

function attach_userinfo($xml)
{
	global $vbulletin; 
	
	if (is_array($vbulletin->userinfo))
	{
		$xml->add_group('userinfo');
		foreach($vbulletin->userinfo as $key => $val)
		{	
			if (!is_array($val))
			{
				$xml->add_tag($key,$val);
			}
		}
		$xml->close_group();
	}
	
	return $xml;
}

function print_error_xml($errortxt)
{
	global $vbulletin; 
	
	$xml = new XMLexporter($vbulletin);
	
	$xml->add_group('response');
	$xml->add_tag('success','false');
	$xml->add_tag('errortext',$errortxt);
	
	$xml->close_group();
	
	// send the output to the requester
	header('Content-Type: text/xml;');
	echo $xml->output();	
	exit;
}

function fetch_userid_by_service($service,$username)
{
	global $db,$vbulletin;
	
	$userinfo = $db->query_first(sprintf("SELECT * 
											FROM " . TABLE_PREFIX . "user 
											WHERE 
												(instantimservice = '%s')
												AND 
												(instantimscreenname = '%s');
											",
											mysql_real_escape_string($service),
											mysql_real_escape_string($username)
											));
	
	return $userinfo['userid'];
}

function fetch_userinfoxml()
{
	global $db,$vbulletin;
	
	$xml = new XMLexporter($vbulletin);
	$xml->add_group('response');
	
	$xml->add_group('user');
	foreach($vbulletin->userinfo as $key => $val)
	{
		if (!is_array($val))
		{
			$xml->add_tag($key,$val);
		}
	}
	$xml->close_group();
	
	$xml->close_group();
	return $xml->output();		
}

function fetch_parent_subsxml($forumid)
{
	global $db,$vbulletin;
	
	$tempinfo = fetch_foruminfo($forumid);
	
	if ($tempinfo['parentid'] != -1)
	{
		$info = fetch_foruminfo($tempinfo['parentid']);
		return fetch_subsxml($info['forumid']);		
	}
	else
	{
		return fetch_subsxml(-1);		
	}
}

function fetch_subsxml($forumid)
{
	global $db,$vbulletin, $lastpostarray;
	$userid = $vbulletin->userinfo['userid'];
	
	$xml = new XMLexporter($vbulletin);
	
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
	$xml->add_group('response',array('forumid' => $forumid, 'title' => $current['title'], 'isnew' => $isnew));
	
	$xml->add_group('forums');
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
		
		$xml->add_group('forum');		
		$xml->add_tag('forumid',$forum['forumid']);
		$xml->add_tag('title',$forum['title']);
		$xml->add_tag('isnew',$isnew);
		$xml->close_group();
	}	
	$xml->close_group();
	
	$xml = attach_userinfo($xml);
	$xml->add_tag('success','true');
	$xml->close_group();
	return $xml->output();
}

function fetch_threadxml($threadid)
{
	global $db,$vbulletin;
	
	$threadinfo = $thread = fetch_threadinfo($threadid);	
	$forum = fetch_foruminfo($thread['forumid']);
	$foruminfo =& $forum;	
	
	// check forum permissions
	$forumperms = fetch_permissions($thread['forumid']);
	if (!($forumperms & $vbulletin->bf_ugp_forumpermissions['canview']) OR !($forumperms & $vbulletin->bf_ugp_forumpermissions['canviewthreads']))
	{
		print_error_xml('no_permission_fetch_threadxml');
	}
	if (!($forumperms & $vbulletin->bf_ugp_forumpermissions['canviewothers']) AND ($thread['postuserid'] != $vbulletin->userinfo['userid'] OR $vbulletin->userinfo['userid'] == 0))
	{
		print_error_xml('no_permission_fetch_threadxml');
	}	
	
	// *********************************************************************************
	// check if there is a forum password and if so, ensure the user has it set
	verify_forum_password($foruminfo['forumid'], $foruminfo['password']);		
		
	$xml = new XMLexporter($vbulletin);
	$xml->add_group('response');	
	
	$userid = $vbulletin->userinfo['userid'];
	$threadssql = "
		SELECT 
			thread.*,
			threadread.readtime AS threadread,
			forumread.readtime as forumread,
			subscribethread.subscribethreadid AS subscribethreadid
		FROM " . TABLE_PREFIX . "thread AS thread	
		LEFT JOIN " . TABLE_PREFIX . "threadread AS threadread ON (threadread.threadid = thread.threadid AND threadread.userid = $userid) 		
		LEFT JOIN " . TABLE_PREFIX . "forumread AS forumread ON (thread.forumid = forumread.forumid AND forumread.userid = $userid) 		
		LEFT JOIN " . TABLE_PREFIX . "subscribethread AS subscribethread ON (thread.threadid = subscribethread.threadid AND subscribethread.userid = $userid)		
		WHERE thread.threadid IN (0$threadid)
		ORDER BY lastpost DESC
	";
		
	$thread = $db->query_first($threadssql);		

	$xml->add_group('thread');
	foreach($thread as $key => $val)
	{
		$xml->add_tag($key,$val);
	}
	$xml->close_group();	
	
	$xml->add_tag('success','true');
	$xml->close_group();
	return $xml->output();				
}

function fetch_threadsxml($forumid,$pagenumber = PAGE_NUMBER_DEFAULT,$perpage = PERPAGE_DEFAULT)
{
	global $db,$vbulletin;
	
	$xml = new XMLexporter($vbulletin);
	$xml->add_group('response');	
	
	// get the total threads count	
	$threadcount = $db->query_first("SELECT threadcount FROM " . TABLE_PREFIX . "forum WHERE (forumid = $forumid);");
	
	if ($threadcount > 0)
	{
		$forumperms = fetch_permissions($forumid);
		if (!($forumperms & $vbulletin->bf_ugp_forumpermissions['canview']))
		{
			print_error_xml('no_permission_fetch_threadsxml');
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
	
		while ($thread = $db->fetch_array($threads))
		{	
			$thread['isnew'] = 'true';		
			if ($thread['forumread'] >= $thread['lastpost'] || $thread['threadread'] >= $thread['lastpost'] || (TIMENOW - ($vbulletin->options['markinglimit'] * 86400)) > $thread['lastpost'] )
			{
				$thread['isnew'] = 'false';
			}
	
			$xml->add_group('thread');
			foreach($thread as $key => $val)
			{
				$xml->add_tag($key,$val);
			}
			// add the thread count to each element
			$xml->add_tag('totalthreads',$threadcount['threadcount']);
			
			$xml->close_group();
		}		
	}
	
	$xml->add_tag('success','true');
	$xml->close_group();
	return $xml->output();	
}

function fetch_postsxml($threadid,$pagenumber = PAGE_NUMBER_DEFAULT, $perpage = PERPAGE_DEFAULT)
{
	global $db,$vbulletin;
	
	// *********************************************************************************
	// get thread info
	$threadinfo = $thread = fetch_threadinfo($threadid);
	
	if (!($thread['threadid'] > 0))
	{
		print_error_xml('invalid_threadid_fetch_postsxml');			
	}
	
	// *********************************************************************************
	// get forum info
	$forum = fetch_foruminfo($thread['forumid']);
	$foruminfo =& $forum;

	// *********************************************************************************
	// check forum permissions
	$forumperms = fetch_permissions($thread['forumid']);
	if (!($forumperms & $vbulletin->bf_ugp_forumpermissions['canview']) OR !($forumperms & $vbulletin->bf_ugp_forumpermissions['canviewthreads']))
	{
		print_error_xml('no_permission_fetch_postsxml');
	}
	if (!($forumperms & $vbulletin->bf_ugp_forumpermissions['canviewothers']) AND ($thread['postuserid'] != $vbulletin->userinfo['userid'] OR $vbulletin->userinfo['userid'] == 0))
	{
		print_error_xml('no_permission_fetch_postsxml');
	}
	
	// *********************************************************************************
	// check if there is a forum password and if so, ensure the user has it set
	verify_forum_password($foruminfo['forumid'], $foruminfo['password']);	
	
	
	$xml = new XMLexporter($vbulletin);
	$xml->add_group('response');
	
	//$threadinfo = $db->query_first("SELECT * FROM " . TABLE_PREFIX . "thread as thread WHERE (threadid = $threadid);");
	$xml->add_group('thread');
	foreach($threadinfo as $key => $val)
	{
		$xml->add_tag($key,$val);
	}	
	$xml->add_tag('pagenumber',$pagenumber);
	$xml->close_group();
	
	$limitlower = ($pagenumber - 1) * $perpage;
	$userid = $vbulletin->userinfo['userid'];
	
	$postssql = "
		SELECT 
			*,
			post.dateline as dateline,
			threadread.readtime as threadread,
			forumread.readtime as forumread
		FROM " . TABLE_PREFIX . "post as post 
		LEFT JOIN " . TABLE_PREFIX . "thread AS thread ON (thread.threadid = post.threadid)
		LEFT JOIN " . TABLE_PREFIX . "threadread AS threadread ON (threadread.threadid = post.threadid AND threadread.userid = $userid) 
		LEFT JOIN " . TABLE_PREFIX . "forumread AS forumread ON (thread.forumid = forumread.forumid AND forumread.userid = $userid) 
		WHERE 
			post.threadid = $threadid 
			AND post.visible = 1 
		ORDER By post.dateline ASC 
		LIMIT $limitlower, $perpage		
	";
	
	$posts = $db->query_read_slave($postssql);		
	while ($post = $db->fetch_array($posts))
	{	
			$post['isnew'] = 'false';
	
		$post['isnew'] = 'true';		
		if ($post['threadread'] >= $post['dateline'] || (TIMENOW - ($vbulletin->options['markinglimit'] * 86400)) >= $post['dateline'] )
		{
			$post['isnew'] = 'false';
		}		
		
		$xml->add_group('post');
		foreach($post as $key => $val)
		{
			if ($key == "pagetext")
			{
				$val = strip_bbcode($val,true,false,false);
			}
			
			$xml->add_tag($key,$val);
		}
		
		$xml->close_group();		
	}
	
	$xml->add_tag('success','true');
	$xml->close_group();
	return $xml->output();	
}

function fetch_postxml($postid)
{
	global $db,$vbulletin;
	
	$xml = new XMLexporter($vbulletin);
	$xml->add_group('response');

	$userid = $vbulletin->userinfo['userid'];	

	$postsql = "
		SELECT 
			*,
			post.dateline as dateline,
			threadread.readtime as threadread,
			forumread.readtime as forumread
		FROM " . TABLE_PREFIX . "post as post 
		LEFT JOIN " . TABLE_PREFIX . "thread AS thread ON (thread.threadid = post.threadid)
		LEFT JOIN " . TABLE_PREFIX . "threadread AS threadread ON (threadread.threadid = post.threadid AND threadread.userid = $userid) 
		LEFT JOIN " . TABLE_PREFIX . "forumread AS forumread ON (thread.forumid = forumread.forumid AND forumread.userid = $userid) 
		WHERE 
			post.postid = $postid 
			AND post.visible = 1 
	";

	$post = $db->query_first($postsql);
	
	$post['isnew'] = 'true';
	if ($post['threadread'] >= $post['dateline'] || (TIMENOW - ($vbulletin->options['markinglimit'] * 86400)) >= $post['dateline'] )
	{
		$post['isnew'] = 'false';
	}		
	
	if ($post['threadid'] > 0)
	{
		$nextpost = $db->query_first("SELECT * FROM ". TABLE_PREFIX ."post WHERE (post.threadid = $post[threadid]) AND (dateline > $post[dateline]) ORDER BY dateline ASC LIMIT 1");
		$post['nextpostid'] = $nextpost['postid'];

		$prevpost = $db->query_first("SELECT * FROM ". TABLE_PREFIX ."post WHERE (post.threadid = $post[threadid]) AND (dateline < $post[dateline]) ORDER BY dateline DESC LIMIT 1");
		$post['prevpostid'] = $prevpost['postid'];
	}
	
	$xml->add_group('post');
	foreach($post as $key => $val)
	{
			if ($key == "pagetext")
			{
				$val = strip_bbcode($val,true,false,false);
			}
			
			$xml->add_tag($key,$val);
	}
	$xml->close_group();	

	$threadinfo = fetch_threadinfo($post['threadid']);
	$foruminfo = fetch_foruminfo($threadinfo['forumid'],false);
	mark_thread_read($threadinfo, $foruminfo, $vbulletin->userinfo['userid'], $post['dateline']);

	$xml->close_group();
	return $xml->output();		
}

function fetch_postxmlbyindex($threadid,$index)
{
	global $db,$vbulletin;
	
	$xml = new XMLexporter($vbulletin);
	$xml->add_group('response');	
	
	if ($index > 0)
	{
		$index -= 1;
		$postinfo = $db->query_first("SELECT * FROM ". TABLE_PREFIX ."post as post WHERE (threadid = $threadid) ORDER BY dateline ASC LIMIT $index,1");
		
		if (is_array($postinfo))
		{
			$xml->add_group('post');
			foreach($postinfo as $key => $val)
			{
				if ($key == "pagetext")
				{
					$val = strip_bbcode($val,true,false,false);
				}
				
				$xml->add_tag($key,$val);
			}
			$xml->close_group();
		}
	
		if ($postinfo['postid'] > 0)
		{
			$threadinfo = fetch_threadinfo($postinfo['threadid']);
			$foruminfo = fetch_foruminfo($threadinfo['forumid'],false);
			mark_thread_read($threadinfo, $foruminfo, $vbulletin->userinfo['userid'], $postinfo['dateline']);
		}
	}
	
	$xml->add_tag('success','true');	
	$xml->close_group();
	return $xml->output();			
}

function fetch_post_notifications($dodelete = false)
{
	global $db,$vbulletin;
	
	$xml = new XMLexporter($vbulletin);
	$xml->add_group('response');		
	
	$query = "
		SELECT 
			imnotification.*, 
			post.*, 
			thread.*, 
			forum.*, 
			post.dateline as postdateline,
			thread.title as threadtitle,
			user.username AS newpostusername,
			user.instantimnotification AS instantimnotification,  
			user.instantimscreenname AS instantimscreenname,  
			user.instantimservice AS instantimservice
		FROM " . TABLE_PREFIX . "imnotification AS imnotification 
		LEFT JOIN " . TABLE_PREFIX . "user AS user ON (imnotification.userid = user.userid)
		LEFT JOIN " . TABLE_PREFIX . "post AS post ON (imnotification.postid = post.postid)
		LEFT JOIN " . TABLE_PREFIX . "thread AS thread ON (post.threadid = thread.threadid)
		LEFT JOIN " . TABLE_PREFIX . "forum AS forum ON (thread.forumid = forum.forumid)

		ORDER By imnotification.dateline ASC 
	";
	
	$postnotifications = $db->query_read_slave($query);		
	while ($notification = $db->fetch_array($postnotifications))
	{	
		$xml->add_group('notification');
		foreach($notification as $key => $val)
		{
			if ($key == "pagetext")
			{
				$val = strip_bbcode($val,true,false,false);
			}
						
			$xml->add_tag($key,$val);
		}		
		$xml->close_group();
		
		if ($dodelete)
		{
			$db->query_write("DELETE FROM " . TABLE_PREFIX . "imnotification WHERE (imnotificationid = $notification[imnotificationid]);");
		}
	}	
	
	$xml->add_tag('success','true');		
	$xml->close_group();
	return $xml->output();		
}

function correct_forum_counters($threadid, $forumid) 
{
	
    // select lastpostid from thread where threadid =  $threadid
    // select dateline from post where postid = $postid
    // update thread set lastpost =  $time where threadid = $threadid

    global $db;
    $lastpostid = $db->query_first("SELECT lastpostid FROM " . TABLE_PREFIX . "thread WHERE threadid = '".$threadid."'");
    $dateline = $db->query_first("SELECT dateline FROM " . TABLE_PREFIX . "post WHERE postid = '".$lastpostid['lastpostid']."'");

    // Update thread table and threadread table to reflect new post
    $db->query_write("UPDATE " . TABLE_PREFIX . "thread SET lastpost = '".$dateline['dateline']."' WHERE threadid = '".$threadid."'");
    $db->query_write("UPDATE " . TABLE_PREFIX . "threadread SET readtime = '".($dateline['dateline']-1)."' WHERE threadid = '".$threadid."' AND readtime >= '".($dateline['dateline']-1)."'");

    // Update forum table and forumread to reflect new post
    $db->query_write("UPDATE " . TABLE_PREFIX . "forum SET lastpost = '".$dateline['dateline']."' WHERE forumid = '".$forumid."'");
    $db->query_write("UPDATE " . TABLE_PREFIX . "forumread SET readtime = '".($dateline['dateline']-1)."' WHERE forumid = '".$forumid."' AND readtime >= '".($dateline['dateline']-1)."'");
} 

function subscribe_thread($threadid)
{
	global $db,$vbulletin;
	
	$xml = new XMLexporter($vbulletin);
	$xml->add_group('response');			
	
	$threadinfo = fetch_threadinfo($threadid);
	$foruminfo = fetch_foruminfo($threadinfo['forumid'],false);
	
	if (!$foruminfo['forumid'])
	{
		print_error_xml("invalid_forumid_subscribe_thread");
	}
	
	$forumperms = fetch_permissions($foruminfo['forumid']);
	if (!($forumperms & $vbulletin->bf_ugp_forumpermissions['canview']))
	{
		print_error_xml("no_forum_permission_subscribe_thread");
	}	
	
	if (!$foruminfo['allowposting'] OR $foruminfo['link'] OR !$foruminfo['cancontainthreads'])
	{
		print_error_xml("forum_closed_subscribe_thread");
	}	
	
	if (!verify_forum_password($foruminfo['forumid'], $foruminfo['password'], false))
	{
		print_error_xml("invalid_forum_password_subscribe_thread");
	}
	
	if ($threadinfo['threadid'] > 0)
	{
		if ((!$threadinfo['visible'] AND !can_moderate($threadinfo['forumid'], 'canmoderateposts')) OR ($threadinfo['isdeleted'] AND !can_moderate($threadinfo['forumid'], 'candeleteposts')))
		{
			print_error_xml('cannot_view_thread_subscribe_thread');	
		}		
		
		if (!($forumperms & $vbulletin->bf_ugp_forumpermissions['canviewthreads']) OR (($vbulletin->userinfo['userid'] != $threadinfo['postuserid'] OR !$vbulletin->userinfo['userid']) AND !($forumperms & $vbulletin->bf_ugp_forumpermissions['canviewothers'])))
		{
			print_error_xml("no_thread_permission_subscribe_thread");
		}		
		
		$emailupdate = 1; // Instant notification by email
		$folderid = 0; // Delfault folder
		
		/*insert query*/
		$db->query_write("
			REPLACE INTO " . TABLE_PREFIX . "subscribethread (userid, threadid, emailupdate, folderid, canview)
			VALUES (" . $vbulletin->userinfo['userid'] . ", $threadinfo[threadid], $emailupdate, $folderid, 1)
		");		

		$xml->add_group('thread');
		foreach($threadinfo as $key => $val)
		{
			$xml->add_tag($key,$val);
		}		
		$xml->close_group();
	}
	else
	{
		print_error_xml("invalid_threadid_subscribe_thread");		
	}
	
	$xml->add_tag('success','true');
	$xml->close_group();
	return $xml->output();			
}

function unsubscribe_thread($threadid)
{
	global $db,$vbulletin;
	
	$xml = new XMLexporter($vbulletin);
	$xml->add_group('response');		
	
	if (is_numeric($threadid) && $threadid > 0)
	{ // delete this specific thread subscription
		
		$db->query_write("
			DELETE FROM " . TABLE_PREFIX . "subscribethread 
			WHERE (threadid = $threadid);
		");			
		
		$xml->add_tag('success','true');
	}
	else if ($threadid == 'all')
	{ // delete all of these users thread subscriptions

		$userid = $vbulletin->userinfo['userid'];

		$db->query_write("
			DELETE FROM " . TABLE_PREFIX . "subscribethread 
			WHERE (userid = $userid);
		");			

		$xml->add_tag('success','true');
	}	
	else
	{
		$xml->add_tag('success','false');
		$xml->add_tag('errortext','invalid_threadid_unsubscribe_thread');		
	}	
	
	$xml->close_group();
	return $xml->output();			
}	

function thread_reply($threadid,$pagetext)
{
	global $db,$vbulletin;
	
	$xml = new XMLexporter($vbulletin);
	$xml->add_group('response');	
	
	$threadinfo = fetch_threadinfo($threadid);
	$foruminfo = fetch_foruminfo($threadinfo['forumid'],false);
	
	//$threadinfo = $db->query_first("SELECT * FROM " . TABLE_PREFIX . "thread as thread WHERE (threadid = $threadid);");
	//$foruminfo = $db->query_first("SELECT * FROM " . TABLE_PREFIX . "forum as forum WHERE (forumid = $threadinfo[forumid]);");

	$postdm = new vB_DataManager_Post($vbulletin, ERRTYPE_STANDARD);
	
	$postdm->set_info('skip_maximagescheck', true);
	$postdm->set_info('forum', $foruminfo);
	$postdm->set_info('thread', $threadinfo);  
	$postdm->set('threadid', $threadid);	
	$postdm->set('userid', $vbulletin->userinfo['userid']);	
	$postdm->set('pagetext', $pagetext);
	$postdm->set('allowsmilie', 1);
	$postdm->set('visible', 1);
	$postdm->set('dateline', TIMENOW);	
	
	$postdm->pre_save();
	if (count($postdm->errors) > 0)
	{ // pre_save failed
		print_error_xml('pre_save_failed_thread_reply');
	}	
	else
	{
		$postid = $postdm->save();
		$xml->add_tag('postid',$postid);
		
		require_once('./includes/functions_databuild.php'); 
		build_thread_counters($threadinfo['threadid']); 
		build_forum_counters($foruminfo['forumid']);					
		correct_forum_counters($threadinfo['threadid'], $foruminfo['forumid']);		
		
		mark_thread_read($threadinfo, $foruminfo, $vbulletin->userinfo['userid'], TIMENOW);
	}
	
	$xml->add_tag('success','true');
	$xml->close_group();
	return $xml->output();		
}

function set_imnotification($on)
{
	global $vbulletin,$db;
	
	$xml = new XMLexporter($vbulletin);
	$xml->add_group('response');		
	
	$userid = $vbulletin->userinfo['userid'];
	$onoff = 0;
	if ($on)
	{
		$onoff = 1;
	}
	
	$db->query_write("
		UPDATE " . TABLE_PREFIX . "user 
		SET instantimnotification=$onoff
		WHERE (userid = $userid);
	");				
	
	
	$xml->add_tag('success','true');
	$xml->close_group();
	return $xml->output();		
}

?>

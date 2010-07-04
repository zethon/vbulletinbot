<?

$structtypes = array();

$structtypes['RequestResult'] = array(
        'Code' => array('name'=>'Code','type'=>'xsd:int'),
        'Text' => array('name'=>'Text','type'=>'xsd:string'),
        'RemoteUser' => array('name'=>'RemoteUser','type'=>'tns:RemoteUser')
    );
    
$structtypes['UserCredentials'] = array(
        'Username' => array('name'=>'Username','type'=>'xsd:string'),
        'ServiceName' => array('name'=>'ServiceName','type'=>'xsd:string')
    );
    
$structtypes['RemoteUser'] = array(
		'UserID' => array('UserID','type'=>'xsd:int'),
		'Username' => array('Username','type'=>'xsd:string')
    );     
    
$structtypes['Forum'] = array(
        'ForumID' => array('ForumID','type'=>'xsd:int'),
        'Title' => array('Title','type'=>'xsd:string'),
        'IsNew' => array('IsNew', 'type'=>'xsd:boolean'),
        'IsCurrent' => array('IsCurrent', 'type'=>'xsd:boolean')
    ); 
    
$structtypes['Thread'] = array(
        'ThreadID' => array('ThreadID','type'=>'xsd:int'),
        'ThreadTitle' => array('ThreadTitle','type'=>'xsd:string'),
        'Title' => array('Title','type'=>'xsd:string'),
        'ForumID' => array('ForumID','type'=>'xsd:int'),
        'PostID' => array('PostID','type'=>'xsd:int'), 
        'LastPost' => array('LastPost','type'=>'xsd:int'), 
        'LastPoster' => array('LastPoster','type'=>'xsd:string'), 
        'PostUsername' => array('PostUsername','type'=>'xsd:string'), 
        'ReplyCount' => array('ReplyCount','type'=>'xsd:int'), 
        'IsSubscribed' => array('IsSubscribed', 'type'=>'xsd:boolean'),
        'IsNew' => array('IsNew', 'type'=>'xsd:boolean'),
        'DateLine' => array('DateLine','type'=>'xsd:int'), 
    ); 
    
$structtypes['Post'] = array(
        'PostID' => array('PostID','type'=>'xsd:int'),
        'Username' => array('Username','type'=>'xsd:string'),
        'PageText' => array('PageText','type'=>'xsd:string'),
        'Title' => array('Title','type'=>'xsd:string'),
        'DateLine' => array('DateLine','type'=>'xsd:int'),
        'IpAddress' => array('IpAddress','type'=>'xsd:string'),
        'IsNew' => array('IsNew', 'type'=>'xsd:boolean'),
    );
    
$structtypes['IMNotificationInfo'] = array(
        'IMNotificationID' => array('IMNotificationID','type'=>'xsd:int'),
        'InstantIMService' => array('InstantIMService','type'=>'xsd:string'),
        'InstantIMScreenname' => array('InstantIMScreenname','type'=>'xsd:string'),
        'PostDateLine' => array('PostDateLine','type'=>'xsd:int'),
        'ThreadTitle' => array('ThreadTitle','type'=>'xsd:string'),
        'NewPostUsername' => array('NewPostUsername','type'=>'xsd:string'),        
    );    
            
$structtypes['IMNotification'] = array(
        'IMNotificationInfo' => array('IMNotificationInfo','type'=>'tns:IMNotificationInfo'),
        'Post' => array('Post','type'=>'tns:Post'),
        'Thread' => array('Thread','type'=>'tns:Thread'),
        'Forum' => array('Forum','type'=>'tns:Forum'),
    );
                
$structtypes['ForumListResult'] = array(
        'Result' => array('name'=>'Result','type'=>'tns:RequestResult'),
        'CurrentForum' => array('name'=>'CurrentForum','type'=>'tns:Forum'),
        'ForumList' => array('name'=>'ForumList','type'=>'tns:ForumArray'),
    );
    
$structtypes['ThreadListResult'] = array(
        'Result' => array('name'=>'Result','type'=>'tns:RequestResult'),
        'ThreadList' => array('name'=>'ThreadList','type'=>'tns:ThreadArray'),
        'ThreadCount' => array('name'=>'ThreadCount','type'=>'xsd:int')
    );    
    
$structtypes['PostListResult'] = array(
        'Result' => array('name'=>'Result','type'=>'tns:RequestResult'),
        'Thread' => array('name'=>'Thread','type'=>'tns:Thread'),
        'PostList' => array('name'=>'PostList','type'=>'tns:PostArray')
    ); 
    
$structtypes['GetPostResult']  = array(
        'Result' => array('name'=>'Result','type'=>'tns:RequestResult'),
        'Post' => array('name'=>'Post','type'=>'tns:Post'),
    );          
    
$structtypes['GetThreadResult']  = array(
        'Result' => array('name'=>'Result','type'=>'tns:RequestResult'),
        'Thread' => array('name'=>'Thread','type'=>'tns:Thread'),
    );   
    
$structtypes['PostReplyResult']  = array(
        'Result' => array('name'=>'Result','type'=>'tns:RequestResult'),
        'PostID' => array('name'=>'PostID','type'=>'xsd:int'),
    );     
    
$structtypes['IMNotificationsResult']  = array(
        'Result' => array('name'=>'Result','type'=>'tns:RequestResult'),
        'IMNotificationList' => array('name'=>'PostID','type'=>'tns:IMNotificationArray'),
    );  
        
foreach ($structtypes as $key => $val)
{
	$server->wsdl->addComplexType(	
		$key,
    'complexType',
    'struct',
    'all',
    '',
    $val
   );
}    

// non-struct types										
$server->wsdl->addComplexType(
		'RemoteUserArray', 
		'complexType',
		'array',
		'',
		'SOAP-ENC:Array',
		array(),
		array(array('ref'=>'SOAP-ENC:arrayType','wsdl:arrayType'=>'tns:RemoteUser[]')),
		'tns:RemoteUser'
);	

$server->wsdl->addComplexType(
        'ForumArray', 
        'complexType',
        'array',
        '',
        'SOAP-ENC:Array',
        array(),
        array(array('ref'=>'SOAP-ENC:arrayType','wsdl:arrayType'=>'tns:Forum[]')),
        'tns:Forum'
);    

$server->wsdl->addComplexType(
        'ThreadArray', 
        'complexType',
        'array',
        '',
        'SOAP-ENC:Array',
        array(),
        array(array('ref'=>'SOAP-ENC:arrayType','wsdl:arrayType'=>'tns:Thread[]')),
        'tns:Thread'
); 

$server->wsdl->addComplexType(
        'PostArray', 
        'complexType',
        'array',
        '',
        'SOAP-ENC:Array',
        array(),
        array(array('ref'=>'SOAP-ENC:arrayType','wsdl:arrayType'=>'tns:Post[]')),
        'tns:Post'
); 

$server->wsdl->addComplexType(
        'IMNotificationArray', 
        'complexType',
        'array',
        '',
        'SOAP-ENC:Array',
        array(),
        array(array('ref'=>'SOAP-ENC:arrayType','wsdl:arrayType'=>'tns:IMNotification[]')),
        'tns:IMNotification'
);

function ConsumeArray($datarray,$destarray)
{
    $retval = array();
    
    if (is_array($datarray) && is_array($destarray))
    {
        foreach ($destarray as $key => $val)                
        {
            $tempkey = strtolower($key);
            
            if (array_key_exists($tempkey,$datarray))
            {
                $retval[$key] = $datarray[$tempkey];
            }
        }
    }

    return $retval;
}

?>
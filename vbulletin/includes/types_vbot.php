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
        'ForumID' => array('UserID','type'=>'xsd:int'),
        'Title' => array('Username','type'=>'xsd:string'),
        'IsNew' => array('IsNew', 'type'=>'xsd:boolean'),
        'IsCurrent' => array('IsNew', 'type'=>'xsd:boolean')
    ); 
    
$structtypes['ForumListResult'] = array(
        'Result' => array('name'=>'Result','type'=>'tns:RequestResult'),
        'CurrentForum' => array('name'=>'CurrentForum','type'=>'tns:Forum'),
        'ForumList' => array('name'=>'ForumList','type'=>'tns:ForumArray'),
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
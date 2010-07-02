<?

$server->register(
                // method name:
                'WhoAmI', 		 
                // parameter list:
                array('UserCredentials'=>'tns:UserCredentials'), 
                // return value(s):
                array('return'=>'tns:RequestResult'),
                // namespace:
                $namespace,
                // soapaction: (use default)
                false,
                // style: rpc or document
                'rpc',
                // use: encoded or literal
                'encoded',
                // description: documentation for the method
                'Returns the user information of the associated user credentials'); 
                
$server->register(
    'ListForums',
    array('UserCredentials'=>'tns:UserCredentials','ForumID'=>'xsd:int'),
    array('return'=>'tns:ForumListResult'),
    $namespace,
    false,
    'rpc',
    'encoded',
    'Returns a list of forums of the associated forumid'
);
          
$server->register(
    'ListParentForums',
    array('UserCredentials'=>'tns:UserCredentials','ForumID'=>'xsd:int'),
    array('return'=>'tns:ForumListResult'),
    $namespace,
    false,
    'rpc',
    'encoded',
    'Returns a list of forums of the associated forumid'
); 
               
?>
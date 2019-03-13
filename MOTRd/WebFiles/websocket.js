function MyWebsocket() {
    this.timeout = 2000;
    this.clearTimer = -1;
    this.data = {};
    this.socket = null;
	this.myparent = null;
	this.server = ""
	this.path = ""
	this.port = 0
    this.setOnMessage = "";
    this.action = "";
	this.retryconnect = false;
	this.maxretrycount = 5;
	this.retrycount = 0;
};

//Helper function to get cookies
MyWebsocket.prototype.getCookie = function(cname) {
    var name = cname + "=";
    var decodedCookie = decodeURIComponent(document.cookie);
    var ca = decodedCookie.split(';');
    for(var i = 0; i <ca.length; i++) {
        var c = ca[i];
        while (c.charAt(0) == ' ') {
            c = c.substring(1);
        }
        if (c.indexOf(name) == 0) {
            return c.substring(name.length, c.length);
        }
    }
    return "";
}

//Returns the sessionid (a demand on JSON queries for sending commands)
MyWebsocket.prototype.getSessionID = function ()
{
	if (typeof(Storage) == "undefined")
	{
		console.log("Cannot store data, browser sucks...");
		return "";
	}
	
	//Get the SessionID from the session register, if not try the local permanent storage
	var SessionID = "";
	if (sessionStorage.SessionID)
		SessionID = sessionStorage.SessionID;
	else
		SessionID = localStorage.getItem("SessionID");
	
	if(SessionID == null)
		SessionID = "";
	
	return SessionID;
	//return this.getCookie("SessionID");
}

//Returns the sessionid (a demand on JSON queries for sending commands)
MyWebsocket.prototype.getAuthID = function ()
{
	if (typeof(Storage) == "undefined")
	{
		console.log("Cannot store data, browser sucks...");
		return "";
	}
	
	//Get the AuthID from the session register, if not try the local permanent storage
	var AuthID = "";
	if (sessionStorage.AuthID)
		AuthID = sessionStorage.AuthID;
	else
		AuthID = localStorage.getItem("AuthID");
	
	if(AuthID == null)
		AuthID = "";
	
	return AuthID;
}

//Returns the userid (a demand on JSON queries for sending commands)
MyWebsocket.prototype.getUserID = function ()
{
	if (typeof(Storage) == "undefined")
	{
		console.log("Cannot store data, browser sucks...");
		return "";
	}
	
	//Get the AuthID from the session register, if not try the local permanent storage
	var UserID = "";
	if (sessionStorage.UserID)
		UserID = sessionStorage.UserID;
	else
		UserID = localStorage.getItem("UserID");
	
	if(UserID == null)
		UserID = "";	
	
	return UserID;
}

MyWebsocket.prototype.SendCommand = function (command, parameter)
{
	var message = {
//		'$types': {	'MOTRd.WebSocketCommandClass, MOTRd, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null': "1" },
//		'$type': "1",
//		'sessionid': this.getSessionID(),
		'command': command.toString(),
		'parameter': parameter.toString(),
	};
	this.send(JSON.stringify(message));
}

MyWebsocket.prototype.getSocketState = function () {
    console.log("get state; port: " + this.port);
    return (this.socket != null) ? this.socket.readyState : 0;
};

MyWebsocket.prototype.send = function (jsonstring) {
    this.socket.send(jsonstring);
};

//EVENTS
MyWebsocket.prototype.onOpen = function () {
    console.log("onOpen, port: "  + this.port);
	this.retrycount = 0;
	this.myparent.OnOpen();
};

MyWebsocket.prototype.onError = function (event) {
	if(event.code == null)
		return;

    console.log("onError; port: " + this.port);
 	console.log(this.errorTranslate(event));
	this.myparent.OnError(this.errorTranslate(event));
};

MyWebsocket.prototype.onClose = function (event) {
    console.log("onClose; port: " + this.port);
	this.myparent.OnClose(this.errorTranslate(event));
	
	//Retry connection
	if(this.retryconnect)
	{
		console.log(this.retrycount + " - Retry connection...");
		clearInterval(this.clearTimer);
		var t = this;
		this.clearTimer = setInterval(function(){t.internalconnect();}, this.timeout);
	}
};

MyWebsocket.prototype.onMessage = function (e) {
	console.log("Received data: " + e.data.length/8 + " bytes");
	this.myparent.OnMessage(e);
};

MyWebsocket.prototype.reconnect = function () {
	this.connect(this.server, this.port, this.path, this.myparent);
}

//Used for retry connect
MyWebsocket.prototype.internalconnect = function () {
	this.connect(this.server, this.port, this.path, this.myparent);

	//Update the retry count
	this.retrycount++;
	if(this.retrycount == this.maxretrycount)
	{
		clearInterval(this.clearTimer);
		return;
	}	
}

//Functions called by external: connect / disconnect...
MyWebsocket.prototype.connect = function (server, port, path, myparent) {

	//Set the variables
	this.server = server;
	this.port = port;
	this.path = path;
	this.myparent = myparent; 
	this.bconnected = false;

	/*
    this.STATE_DISCONNECTED = 0;
	this.STATE_CONNECTED = 1;
	this.STATE_CONNECTING = 2;
	this.STATE_CLOSED = 3;
	*/

    if ("WebSocket" in window) {
        if (this.getSocketState() == 1) {
            this.socket.onopen = this.onOpen;
            clearInterval(this.clearTimer);
        }
        else {
            try {
				var sConnectionString = "ws://";
				if (location.protocol == 'https:')
					sConnectionString = "wss://";
                var host = sConnectionString + this.server + ":" + this.port + "/" + path;
                this.socket = new WebSocket(host);
                this.socket.onopen = this.onOpen.bind(this);
                this.socket.onmessage = this.onMessage.bind(this);
                this.socket.onerror = this.onError.bind(this);
                this.socket.onclose = this.onClose.bind(this);
                console.log(this.socket);
            }
            catch (exeption) {
                console.log(exeption);
            }
        }
    }
};

MyWebsocket.prototype.disconnect = function () {
    console.log("disconnect; port: " + this.port);   
	this.socket.onclose = function (event) {};
	this.socket.close(1000);
};

//Returns true if connected
MyWebsocket.prototype.isConnected = function () {
	//CONNECTING OPEN CLOSING or CLOSED	
	console.log("Readystate is: " + this.getSocketState() + " - state: " + this.OPEN );
	if(this.getSocketState() == 1) //1 = connected, 0 = disconnected, 2 = connecting, 3 = closed
		return true;
	else
		return false;
};

//Returns a string with the error code
MyWebsocket.prototype.errorTranslate = function(event)
{
	var reason;

	// See http://tools.ietf.org/html/rfc6455#section-7.4.1
	if (event.code == 1000)
		reason = "Normal state";
	else if(event.code == 1001)
		reason = "An endpoint is \"going away\", such as a server going down or a browser having navigated away from a page.";
	else if(event.code == 1002)
		reason = "An endpoint is terminating the connection due to a protocol error";
	else if(event.code == 1003)
		reason = "An endpoint is terminating the connection because it has received a type of data it cannot accept (e.g., an endpoint that understands only text data MAY send this if it receives a binary message).";
	else if(event.code == 1004)
		reason = "Reserved. The specific meaning might be defined in the future.";
	else if(event.code == 1005)
		reason = "No status code was actually present.";
	else if(event.code == 1006)
	   reason = "The connection was closed abnormally, e.g., without sending or receiving a Close control frame";
	else if(event.code == 1007)
		reason = "An endpoint is terminating the connection because it has received data within a message that was not consistent with the type of the message (e.g., non-UTF-8 [http://tools.ietf.org/html/rfc3629] data within a text message).";
	else if(event.code == 1008)
		reason = "An endpoint is terminating the connection because it has received a message that \"violates its policy\". This reason is given either if there is no other sutible reason, or if there is a need to hide specific details about the policy.";
	else if(event.code == 1009)
	   reason = "An endpoint is terminating the connection because it has received a message that is too big for it to process.";
	else if(event.code == 1010) // Note that this status code is not used by the server, because it can fail the WebSocket handshake instead.
		reason = "An endpoint (client) is terminating the connection because it has expected the server to negotiate one or more extension, but the server didn't return them in the response message of the WebSocket handshake. <br /> Specifically, the extensions that are needed are: " + event.reason;
	else if(event.code == 1011)
		reason = "A server is terminating the connection because it encountered an unexpected condition that prevented it from fulfilling the request.";
	else if(event.code == 1015)
		reason = "The connection was closed due to a failure to perform a TLS handshake (e.g., the server certificate can't be verified).";
	else
		reason = "Unknown reason";
	
	//Write the eventcode first and message after...
	reason = "[" + event.code + "] " + reason;
	return reason;
}
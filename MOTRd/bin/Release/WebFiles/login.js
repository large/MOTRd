/**
	Login handler for MOTR
*/

var thesocket = null;
var websockethandler = null;
var dologin = false;

//*****************************************
//* WEBSOCKET STUFF
function WSHandler()
{
}

//Event from websocket (after connection is done)
WSHandler.prototype.OnOpen = function()
{
	//Set the button connected
	//SetWebsocketButton("Connected");
	//SetStatus("Websocket connected, ready to login");
	
	//Check if we have a session, send it if so
	if(thesocket.getSessionID().length > 0)
	{
		ShowInformation('Session found, trying to login', 1000);
		//SetStatus("Session found, sending query to restore...");
		thesocket.SendCommand("SESSIONRESTORE", thesocket.getSessionID()+";"+thesocket.getAuthID()+";"+thesocket.getUserID());
	}
	else if(dologin) //If we was disconnected in the "DoSubmit()" we try again
	{
		dologin = false;
		OnSubmit();
	}
}

WSHandler.prototype.OnError = function(strError)
{
	ShowInformation("Error on Websocket connection, please reload the page to try to reconnect!" + strError, 8000);
	//SetStatus("SocketShowPopup error: " + strError);
}

WSHandler.prototype.OnClose = function(strError)
{
	ShowInformation("Websocket connection is closed, try to reload the page. If the error is presistent the server is not running. See error: " + strError, 8000);
	//SetStatus("Websocket is closed, reconnect at submit");
}

WSHandler.prototype.OnMessage = function(e)
{
	//Receive JSON data from server
	var received = JSON.parse(e.data);
	console.log("OnMessage: " + e.data);

	//Handle a valid login
	if(received.command == "APPLOGIN")
	{
		var sSession = received["aArray"][0];
		var sTempID = received["aArray"][1];
		var sAuthID = received["aArray"][2];
		//console.log("Session is: " + sSession);
		if($("#storesession:checked").length > 0)
		{
			console.log("Store permanent");
			localStorage.setItem("SessionID", sSession);
			localStorage.setItem("AuthID", sAuthID);
			sessionStorage.removeItem("SessionID");
			sessionStorage.removeItem("AuthID");
		}
		else
		{
			console.log("Session storage");
			sessionStorage.SessionID = sSession;
			sessionStorage.AuthID = sAuthID
			localStorage.removeItem("SessionID");
			localStorage.removeItem("AuthID");
		}
		//document.cookie = "SessionID=;";
		document.cookie = "TempID=" + sTempID + "; SameSite=strict;";
		SetStatus("Login OK, going to directory");
		window.location.replace("/directory.motr");
	}
	
	//Handle a pong message, it is the same as redirecting to main page
	if(received.command == "PONG")
		window.location.replace("/directory.motr");

	//Handle the errors by adding the all to one line
	if(received.command == "ERRORNOTLOGGEDIN")
	{	
		ShowPopup("Session is no longer valid", "Please login again to get new credentials");
		RemoveAllStorage();

	}
	
	//Handle the errors by adding the all to one line
	if(received.command == "ERROR")
	{	
		var text = "";
		for(i=0;i<received["count"];i++)
			text += received["aArray"][i];		
		
		ShowPopup("Error in credentials", text);
		SetStatus(text);
		RemoveAllStorage();
	}
	
	if(received.command == "SESSIONRESTORE")
	{
		SetStatus("Login OK, going to directory");
		var sTempID = received["aArray"][0];
		document.cookie = "TempID=" + sTempID + "; SameSite=strict;";
		window.location.replace("/directory.motr");
	}
}

//Removes all the storage we have
function RemoveAllStorage()
{
	localStorage.removeItem("SessionID");
	localStorage.removeItem("AuthID");
	localStorage.removeItem("UserID");
	sessionStorage.removeItem("SessionID");
	sessionStorage.removeItem("AuthID");
	sessionStorage.removeItem("UserID");
}

//Shows a popup with own text
function ShowPopup(sHeader, sText)
{
	$("#dlg-header").text(sHeader);
	$("#dlg-maintext").text(sText);
	$("#dlg-message" ).popup( "open" );
}

//Shows a information with toast
function ShowInformation(sText, nDelay)
{
	$.toast(sText, {'duration': nDelay});
}

function SetStatus(sText)
{
	$("#status").text(sText);
}

//Show message
function showMessage(message, delay)
{
	$.toast(message, {'duration': delay});
}

//Try to connect with the credentials given
function OnSubmit()
{
	console.log("OnSubmit()");
	//Create a new connection if nothing is set
	if (typeof thesocket == 'undefined' || thesocket == null)
	{
		dologin = true;
		CreateAndConnect();
		return;
	}
	if(thesocket.isConnected() == false)
	{
		dologin = true;
		CreateAndConnect();
		return;
	}
	
	//Now send our username and password
	var sUser = $("#username").val();
	var sPass = $("#password").val();
	if(sUser.length == 0 || sPass.length == 0)
	{
		ShowPopup("Invalid login credentials", "Please provide a username and a password");
		return;
	}
	
	//Store the username in session/localstorage
	if($("#storesession:checked").length > 0)
		localStorage.setItem("UserID", sUser);
	else
		sessionStorage.UserID = sUser
	
	//Now send command to the socket
	SetStatus("Sending login credentials to websocket...");
	thesocket.SendCommand("APPLOGIN", sUser+";"+sPass);
}

//Connects to the server
function CreateAndConnect()
{
	if(thesocket == null)
		thesocket = new MyWebsocket();
	else if(thesocket.isConnected() == true)
		return;
	
	//Probably the websockethandler is zero when thesocket is null
	if(websockethandler == null)
		websockethandler = new WSHandler();
	
	//Set status
	SetStatus("Trying to connect to " + window.location.hostname);
	
	//Now connect :)
	thesocket.connect(window.location.hostname, window.location.port, "directory", window.websockethandler);
}

//First init when reloaded
$(document).on("pageinit", "#loginpage", function () {

	//Set the checkbox on
	$("#storesession").prop('checked', true);
	$("#storesession").checkboxradio('refresh');

	//Create and connect to websocket
	CreateAndConnect();
});
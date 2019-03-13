/**
	This script connects to the Websocket of MOTR
*/

var thesocket = null;
var websockethandler = null;


//*****************************************
//* WEBSOCKET STUFF
function WSHandler()
{
}

//Event from websocket (after connection is done)
WSHandler.prototype.OnOpen = function()
{
	thesocket.SendCommand("ISLOGGEDIN",thesocket.getSessionID()); //Check if socket / session is logged in as admin
}

WSHandler.prototype.OnError = function(event)
{
}

WSHandler.prototype.OnClose = function(event)
{
}

WSHandler.prototype.OnMessage = function(e)
{
	//Receive JSON data from server
	var received = JSON.parse(e.data);

	console.log(received.command);

	//Check if we are logged in, show popup if not...
	if(received.command == "ADMINERRORMESSAGE")
	{
		showErrorMessage(received["aArray"][0], 3000);
	}
	
	if(received.command == "UNKNOWN")
	{
		showErrorMessage("Unknown command " + received["aArray"][0] + ", parameter: " + received["aArray"][1] , 3000);
	}
	
	if(received.command == "ADMINCHECKFORTOOLUPDATE")
	{
		var i;
		for(i=0;i<received["count"];i++)
			showErrorMessage(received["aArray"][i] , 5000);
	}
	
	//Error message from server that you have not logged in first
	if(received.command == "ISLOGGEDIN")
	{
		thesocket.SendCommand("ISLOGGEDIN",thesocket.getSessionID()); //Check if socket / session is logged in as admin
	}
	
	//Check if we are logged in, show popup if not...
	if(received.command == "LOGGEDIN")
	{
		if(received["aArray"][0] == false)
		{
			//$.mobile.pageContainer.pagecontainer("change", "#login");
			$.mobile.changePage("#login");
			console.log("Showing login page...");
		}
		else
		{
			if($(".ui-page-active").attr("id") == "paths")
			{
				$("#select-paths").addClass("ui-btn-active");
				$("#select-users").removeClass("ui-btn-active");
				$("#select-settings").removeClass("ui-btn-active");
				console.log("paths selected...");
				thesocket.SendCommand("DIRLIST","");
			}
			if($(".ui-page-active").attr("id") == "users")
			{
				$("#select-users").addClass("ui-btn-active");
				$("#select-paths").removeClass("ui-btn-active");
				$("#select-settings").removeClass("ui-btn-active");
				console.log("users selected...");
				thesocket.SendCommand("USERLIST","");
			}
			if($(".ui-page-active").attr("id") == "settings")
			{
				$("#select-settings").addClass("ui-btn-active");
				$("#select-paths").removeClass("ui-btn-active");
				$("#select-users").removeClass("ui-btn-active");
				console.log("Settings selected...");
				
				//Setting default functions for the tool clicking
				$("#tools").off();
				$("#tools").on("click", ".toolclick", function (e)
				{
					e.preventDefault();
					var tool = $(this).attr('id');
					$("#checkforupdates").data("tool", tool);
					console.log('Clicked on tool: ' + tool);
					$("#checkforupdates").popup("open");
				});	
			
			}
			if($(".ui-page-active").attr("id") == "dirbrowse")
			{
				console.log("Admin directory browse...");
				
				//Remove the items and add waiting message
				$("#admindirs").off();
				$("#admindirs").empty();
				$("#adminfilebrowser").off();
				$("#adminfilebrowser").empty();
				$.mobile.silentScroll(0);
				 
				//Show wait dialog while we get the list
				$("#waitfordrives").popup('open');
				
				thesocket.SendCommand("ADMINDRIVES","");
			}
		}
	} //LOGGEDIN
	
	//Gets a userlist from the server
	if(received.command == "USERLIST")
	{
		//Clear the listview
		$("#userlist").off(); //Remove all events set by .on()!
		//Clear the listview
		$("#userlist").empty();
		$.mobile.silentScroll(0);
		
		console.log("User count: " + received["count"]);
		
		var i;
		var li="";
		for(i=0;i<received["count"];i=i+2)
		{
				li += '<li><a href="#edituser" id="' + received["aArray"][i] + '" username="' + received["aArray"][i+1] + '" class="user-edit" data-rel="popup" data-position-to="window"><img src="/.images/user.png" alt="Folder" class="ui-li-icon">' + received["aArray"][i+1] + '</a>';
				li += '<a href="#deleteuser" id="' + received["aArray"][i] + '" username="' + received["aArray"][i+1] + '" class="user-delete" data-rel="popup" data-position-to="window">Delete user</a></li>';
		}
				
		//append list to ul
		$("#userlist").append(li).promise().done(function ()
		{
			//wait for append to finish - thats why you use a promise()
			//done() will run after append is done
			//add the click event for the redirection to happen to #details-page
			$(this).on("click", ".user-edit", function (e)
			{
				e.preventDefault();
				var userid = $(this).attr('id');
				var username = $(this).attr('username');
				$("#edituser").data("userid", userid);
				$("#edituser").data("username", username);
				$("#edituser").popup("open");
				$("#edituser").find('.ui-title').html('Edit user<br>' + username);
				$("#pwchange").val('');
				console.log('Clicked on: ' + userid + username);
			});		

			$(this).on("click", ".user-delete", function (e)
			{
				e.preventDefault();
				var userid = $(this).attr('id');
				$("#deleteuser").data("userid", userid);
				var username = $(this).attr('username');
				$("#deleteuser").data("userid", userid);
				$("#deleteuser").data("username", username);
				$("#deleteuser").popup("open");
				$("#deleteuser").find('.ui-title').html('Delete user<br>' + username);				
				console.log('Clicked on: ' + userid);
			});	
			
		//Set data to drives list
		$(this).listview("refresh").trigger('create');
		});
	} //USERLIST
	
	//Gets a directory list from the server
	if(received.command == "DIRLIST")
	{
		//Clear the listview
		$("#dirlist").off(); //Remove all events set by .on()!
		//Clear the listview
		$("#dirlist").empty();
		$.mobile.silentScroll(0);
		
		console.log("Dir count: " + received["count"]);
		
		var i;
		var li="";
		for(i=0;i<received["count"];i++)
		{
				li += '<li><a href="#editdir" id="' + received["aArray"][i]["Id"] + '" class="dir-edit" data-rel="popup" data-position-to="window"><img src="/.images/folder.png" alt="Folder" class="ui-li-icon">' + received["aArray"][i]["DisplayName"] + '</a>';
				li += '<a href="#removedirectory" id="' + received["aArray"][i]["Id"] + '" dirname="' + received["aArray"][i]["DisplayName"] + '" class="dir-remove" data-rel="popup" data-position-to="window">Remove dir</a></li>';
		}
				
		//append list to ul
		$("#dirlist").append(li).promise().done(function ()
		{
			//wait for append to finish - thats why you use a promise()
			//done() will run after append is done
			//add the click event for the redirection to happen to #details-page
			$(this).on("click", ".dir-remove", function (e)
			{
				e.preventDefault();
				var dirid = $(this).attr('id');
				$("#removedirectory").data("dirid", dirid);
				var dirname = $(this).attr('dirname');
				$("#removedirectory").data("dirname", dirname);
				$("#removedirectory").popup("open");
				$("#removedirectory").find('.ui-title').html('Remove directory<br>' + dirname);				
				console.log('Clicked on: ' + dirid);
			});	
			
		//Set data to drives list
		$(this).listview("refresh").trigger('create');
		});
		
	} //DIRLIST

	//Gets a directory list from the server
	if(received.command == "ADMINDRIVES")
	{
		//Clear the listview
		$("#admindirs").off(); //Remove all events set by .on()!
		$("#admindirs").empty();
		$.mobile.silentScroll(0);
		
		//Hide the wait dialog
		$("#waitfordrives").popup('close');
		
		var i;
		var li="";
		var lastdivider = "";
		for(i=0;i<received["count"];i++)
		{
			if(lastdivider != received["aArray"][i]["sType"])
			{
				//Store the divider
				lastdivider = received["aArray"][i]["sType"];
				li += '<li data-role="list-divider">' + lastdivider + '</li>';
			}
			li += '<li><a href="#" id="' + received["aArray"][i]["sName"] + '" class="drive-select">' + received["aArray"][i]["sName"] + '</a>';
			if(lastdivider == "Network server")
				li += '<a href="#" id="' + received["aArray"][i]["sName"] + '" class="uncinfosettings">No text</a></li>';
			else
				li += '</li>';
			
		}
				
		//append list to ul
		$("#admindirs").append(li).promise().done(function ()
		{
			//wait for append to finish - thats why you use a promise()
			//done() will run after append is done
			//add the click event for the redirection to happen to #details-page
			$(this).on("click", ".uncinfosettings", function (e)
			{
				e.preventDefault();
				var path = $(this).attr('id');
				$("#uncinfo").data("path", path);
				$("#uncinfo").popup('open');
				console.log('Clicked on unc info');
			});	

			$(this).on("click", ".drive-select", function (e)
			{
				e.preventDefault();
				var path = $(this).attr('id');
				$("#adminfilebrowser").off();
				$("#adminfilebrowser").empty();
				$.mobile.silentScroll(0);
				thesocket.SendCommand("ADMINSETDRIVE",path);
				console.log('Set drive: ' + path);
			});
			
		//Set data to drives list
		$(this).listview("refresh").trigger('create');
		});
		
	} //ADMINDRIVES
	
		//Gets a directory list from the server
	if(received.command == "ADMINFILELIST")
	{
		//Clear the listview
		$("#adminfilebrowser").off(); //Remove all events set by .on()!
		$("#adminfilebrowser").empty();
		$.mobile.silentScroll(0);
				
		var i;
		var li="";
		var htmlIcon = "";
		for(i=0;i<received["count"];i++)
		{
			//First is always ".." and does not need an icon
			if( received["aArray"][i]["sPath"] != ".." )
				htmlIcon = '<img src="/.images/folder.png" alt="Folder" class="ui-li-icon">';
			else
				htmlIcon = '<img src="/.images/folder-up.png" alt="Back" class="ui-li-icon">';	
		
			li += '<li><a href="#" class="directory-select">'+ htmlIcon + received["aArray"][i]["sName"] + '</a>';
			
			if(received["aArray"][i]["sPath"] != "..")
				li += '<a href="#" id="' + received["aArray"][i]["sPath"] + '" dispname="' + received["aArray"][i]["sName"] + '" class="directory-selected">No text</a></li>';
			else
				li += '</li>';
		}
				
		//append list to ul
		$("#adminfilebrowser").append(li).promise().done(function ()
		{
			//wait for append to finish - thats why you use a promise()
			//done() will run after append is done
			//add the click event for the redirection to happen to #details-page
			$(this).on("click", ".directory-selected", function (e)
			{
				e.preventDefault();
				var path = $(this).attr('id');
				var dispname = $(this).attr('dispname');
				var uncuser = $("#uncshareuser").val();
				var uncpass = $("#uncsharepass").val();
				console.log("UNCUser: " + uncuser + " - UNCPass: " + uncpass);
				//$("#adddir").data("pathset", path);
				$.mobile.changePage("#paths");
				console.log('Path set to: ' + path);
				setTimeout(function setDirectoryAtPopup() { $("#displayname").val(dispname); $("#path").val(path); $("#uncuser").val(uncuser); $("#uncpass").val(uncpass); $('#adddir').popup('open'); if($("#path").val().substring(0, 2) == "\\\\") $("#unc").show(); else $("#unc").hide(); }, 500);
			});

			$(this).on("click", ".directory-select", function (e)
			{
				e.preventDefault();
				var path = $(this).text();
				thesocket.SendCommand("ADMINSETPATH",path);
				console.log('Set path: ' + path);
			});
			
		//Set data to drives list
		$(this).listview("refresh").trigger('create');
		});
		
	} //ADMINFILELIST
	
	//
	//Check the result of a login attempt
	if(received.command == "ADMINRESTARTSERVER")
	{
		//Retrive the error flag, false = restarting
		if(received["aArray"][0] == false)
		{
			//$.mobile.pageContainer.pagecontainer("change", "#login");
			thesocket.disconnect();
			thesocket = null;
			setInterval(function(){this.CreateAndConnect();}, 3000);
			console.log("Server restarted... Retry connection in 3 seconds");

			//Get the connectstring
			var iPort = received["aArray"][1]; //1 = http
			if (location.protocol == 'https:')
				iPort = received["aArray"][2]; //2 = https
			
			//Reload the page
			console.log("prot: " + location.protocol);
			console.log("hostname: " + location.hostname);
			console.log("iport: " + iPort);
			console.log("Pathname: " + location.pathname);
			setInterval(function(){window.location.href = location.protocol + '//' + location.hostname + ':' + iPort + location.pathname + '#settings';}, 5000);
			showErrorMessage("Please wait 5 seconds for the page to reload to the new port. Portchange will affect all users on the server", 5000);
		}
	}
	
	//Check the result of a login attempt
	if(received.command == "LOGIN")
	{
		if(received["aArray"][0] == false)
		{
			//$.mobile.pageContainer.pagecontainer("change", "#login");
			$("#result").text("Wrong password!");
			console.log("Password fail...");
		}
		else
		{
			$.mobile.changePage("#paths");
		}
	}	
	
	//Just a temp routine, to be deleted
    for (var key in received) {
        //Im using grid layout here.
        //use any kind of layout you want.
        //key is the key of the property in the object 
        //if obj = {name: 'k'}
        //key = name, value = k
        //info_view += '<div class="ui-grid-a"><div class="ui-block-a"><div class="ui-bar field" style="font-weight : bold; text-align: left;">' + key + '</div></div><div class="ui-block-b"><div class="ui-bar value" style="width : 75%">' + info[key] + '</div></div></div>';
		console.log(key + " - " + received[key] );
    }	
}

//If the filelist/network drives takes a while to show, people hammer the cancelbutton
function AbortAndReturn()
{
	thesocket.disconnect();
	thesocket = null;
	$.mobile.changePage("#paths");
}

function SendLoginPassword()
{
	//console.log("Password is: " + $("#passwordoutput").val());
	
	//Send login password to server
	$("#result").text("");
	thesocket.SendCommand("LOGIN",$("#passwordoutput").val());
}

//Showed in a add user popup
function AdminAdduser()
{
	//Get username and password
	var username = $('#un').val();
	var password = $('#pw').val();
	
	//Send command to add user to the program
	thesocket.SendCommand("ADDUSER",username+","+password);
	
	//Close the popup
	$('#adduser').popup('close');
}

function AdminChangeUser()
{
	//Get the userid to delete
	var userid = $("#edituser").data("userid");
	var password = $('#pwchange').val();
	console.log("Changing id: " + userid + " to password: " + password);
	
	//Send command to add user to the program
	thesocket.SendCommand("CHANGEUSER",userid+","+password);
	
	//Close the popup
	$('#edituser').popup('close');
	
}

function AdminDeleteUser()
{
	//Get the userid to delete
	var userid = $("#deleteuser").data("userid");
	console.log("Deleting id: " + userid);
	
	//Send command to add user to the program
	thesocket.SendCommand("DELETEUSER",userid);
	
	//Close the popup
	$('#deleteuser').popup('close');
}

function AdminBrowse()
{
	$.mobile.changePage("#dirbrowse");
	console.log("Toggling to directory browser...");
}

function AdminAddDirectory()
{
	var sdisplayname = $("#displayname").val();
	var spath = $("#path").val();
	var suncuser="", suncpassword="";
	
	//If we have a network path, get the username and password
	if(spath.substring(0, 2) == "\\\\")
	{
		suncuser = $("#uncuser").val();
		suncpassword = $("#uncpass").val();
	}
	
	console.log("Adding: " + sdisplayname + " to path " + spath + " - with user: " + suncuser + " - pass: " + suncpassword);
	
	//Send command for adding drive
	thesocket.SendCommand("ADMINADDIRECTORY",sdisplayname+","+spath+","+suncuser+","+suncpassword);
	
	//Close popup for add dir info
	$("#adddir").popup('close');
}

function AdminRemoveDirectory()
{
	//Get the userid to delete
	var dirid = $("#removedirectory").data("dirid");
	console.log("Removing directory id: " + dirid);
	
	//Send command to add user to the program
	thesocket.SendCommand("ADMINREMOVEDIRECTORY",dirid);
	
	//Close the popup
	$('#removedirectory').popup('close');
}

function AdminConnectUsingCredentials()
{
	//Get the server to connect to
	var path = $("#uncinfo").data("path");
	
	//Get filename and password
	var uncuser = $("#uncshareuser").val();
	var uncpass = $("#uncsharepass").val();

	console.log("Connecting to: " + path + " - using " + uncuser + "/" + uncpass);
	var sendstring = path + "#" + uncuser + "#" + uncpass;
	
	//Zero filelist and try to get directory list
	$("#adminfilebrowser").off();
	$("#adminfilebrowser").empty();
	$.mobile.silentScroll(0);
	thesocket.SendCommand("ADMINSETDRIVE",sendstring);

	//Close the popup
	$('#uncinfo').popup('close');
}

function CheckForUpdateTool()
{
	var tool = $("#checkforupdates").data("tool");
	thesocket.SendCommand("ADMINCHECKFORTOOLUPDATE",tool);
	showErrorMessage("Request update for " + tool, 5000);
	$('#checkforupdates').popup('close');
}

function RestartServerWithPorts()
{
	var http = $("#httpport").val();
	var https = $("#httpsport").val();
	thesocket.SendCommand("ADMINRESTARTSERVER",http+";"+https);
}

//Simple error display
function showErrorMessage(message, delay)
{
	$.toast(message, {'duration': delay});
}

function CreateAndConnect()
{
	if(thesocket == null)
		thesocket = new MyWebsocket();
	else if(thesocket.isConnected() == true)
		return;
		
	//Probably the websockethandler is zero when thesocket is null
	if(websockethandler == null)
		websockethandler = new WSHandler();
	
	//Now connect :)
	thesocket.connect(window.location.hostname, window.location.port, "admin", window.websockethandler);	
}

//use pagebeforeshow
//DONT USE PAGEINIT! 
//the reason is you want this to happen every single time
//pageinit will happen only once
$(document).on("pageshow", "#paths", function () {

	//Create and connect to websocket
	if (typeof thesocket === 'undefined' || thesocket === null)
	{
		//Variable is global, but a refresh kills it!
		CreateAndConnect();
		return;
	}
	thesocket.SendCommand("ISLOGGEDIN",thesocket.getSessionID()); //Check if socket / session is logged in as admin
});

$(document).on("pageshow", "#users", function () {

	//No websocket, then no love! Go back
	if (typeof thesocket === 'undefined' || thesocket === null)
	{
		//Variable is global, but a refresh kills it!
		CreateAndConnect();
		return;
	}
	thesocket.SendCommand("ISLOGGEDIN",thesocket.getSessionID()); //Check if socket / session is logged in as admin
});

$(document).on("pageshow", "#settings", function () {
	//No websocket, then no love! Go back
	if (typeof thesocket === 'undefined' || thesocket === null)
	{
		//Variable is global, but a refresh kills it!
		CreateAndConnect();
		return;
	}
	thesocket.SendCommand("ISLOGGEDIN",thesocket.getSessionID()); //Check if socket / session is logged in as admin
});

$(document).on("pageshow", "#dirbrowse", function () {
	//No websocket, then no love! Go back
	if (typeof thesocket === 'undefined' || thesocket === null)
	{
		//Variable is global, but a refresh kills it!
		CreateAndConnect();
		return;
	}
	
	thesocket.SendCommand("ISLOGGEDIN",thesocket.getSessionID()); //Check if socket / session is logged in as admin
});

$(document).on("pageshow", "#login", function () {
	//No websocket, then no love! Go back
	if (typeof thesocket === 'undefined' || thesocket === null)
	{
		//Variable is global, but a refresh kills it!
		CreateAndConnect();
		return;
	}
});

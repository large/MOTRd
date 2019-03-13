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

WSHandler.prototype.HelperClearAllGUIItems = function()
{
	$("#filelist").off(); 	//Remove all events set by .on()!
	$("#nav-list").off(); 	//Remove all events set by .on()!
	$("#queue-running").off(); //Remove all events set by .on()!
	$("#queue-notrunning").off(); //Remove all events set by .on()!
	$("#queue-finished").off(); //Remove all events set by .on()!
	$("#nav-list").empty();
	$("#filelist").empty();
	
	//Add a helper text if we don't have any files available
	if($("#filelist li").length == 0)
		ClearFileList('Select a directory to browse. Push the home-icon (top-left) to see the available drives.<br>If the list is empty login to admin and add new directories</li>');
	
	$("#queue-running").empty();
	$("#queue-notrunning").empty();
	$("#queue-finished").empty();
	
	$.mobile.silentScroll(0);
}

//Event from websocket (after connection is done)
WSHandler.prototype.OnOpen = function()
{
	//Set the button connected
	SetWebsocketButton("Connected");
		
	//If the available drives list is empty, request items directly
	//if($(".ui-page-active").attr("id") == "thepage")
	//{
		if($("#navigation li").length == 0)
		{
			thesocket.SendCommand("SESSIONRESTORE", thesocket.getSessionID()+";"+thesocket.getAuthID()+";"+thesocket.getUserID());
		}
		else
		{
			console.log("Li is not zero");
		}
	//}
	//else
	//{
		console.log("No the page: " + $(".ui-page-active").attr("id"));
	//}
}

WSHandler.prototype.OnError = function(event)
{
	//alert('Websocket error ffs...');
	this.HelperClearAllGUIItems();
	SetWebsocketButton("Disconnected");
}

WSHandler.prototype.OnClose = function(event)
{
	this.HelperClearAllGUIItems();
	SetWebsocketButton("Disconnected");
}

WSHandler.prototype.OnMessage = function(e)
{
	//Receive JSON data from server
	var received = JSON.parse(e.data);

	console.log(received.command);
	
	//Handle the sessions
	if(received.command == "SESSIONRESTORE")
	{
		thesocket.SendCommand("GETAVAILABLEDIRS",""); //Gets all the drives available in the system		
	}
	
	//Handle adding drives to the list
	if(received.command == "AVAILABLEDIRS")
	{
		//Clear the listview
		this.HelperClearAllGUIItems();

		var i;
		var li="";
		for(i=0;i<received["count"];i++)
			li += '<li><a href="#" id="' + i + '" class="drive-select"><img src="/.images/directory.png" alt="Folder" class="ui-li-icon">' + received["aArray"][i]["sDisplayName"] + '</a></li>';		
		
		//append list to ul
		$("#nav-list").append(li).promise().done(function ()
		{
			//wait for append to finish - thats why you use a promise()
			//done() will run after append is done
			//add the click event for the redirection to happen to #details-page
			$(this).on("click", ".drive-select", function (e)
			{
				e.preventDefault();
				//store the information in the next page's data
				var infostored = received["aArray"][this.id];
				$("#headertext").text(this.text); //Set the name as header when selected
				$("#headertext").prepend('<span class="ui-icon-myimage ui-btn-icon-notext inlineIcon"/></span>');
				$("[data-role=panel]").panel("close"); //Close the sidepanel
				
				ClearFileList("Please wait while filelist is loading...");
				
				DrivesListClick(infostored);
				
				//$("#details-page").data("info", infostored);
				//change the page # to second page. 
				//Now the URL in the address bar will read index.html#details-page
				//where #details-page is the "id" of the second page
				//we're gonna redirect to that now using changePage() method
				//$.mobile.changePage("#details-page");
			});		
		
		//Set data to drives list
		$(this).listview("refresh").trigger('create');
		});
		//$('#nav-list').append(li).listview("refresh").trigger('create');
	
		//Now get the sorting
		thesocket.SendCommand("GETFILESORTING","");

	} //AVAILABLEDIRS
	
	//Gets the sorting, set the graphics for the buttons
	if(received.command == "FILESORTING")
	{
		if(received["aArray"][0] == "MODIFY")
		{
			$("#sort-name").removeClass("ui-btn-active");
			$("#sort-modified").addClass("ui-btn-active");
			$("#sort-size").removeClass("ui-btn-active");
		}
		else if(received["aArray"][0] == "NAME")
		{
			$("#sort-name").addClass("ui-btn-active");
			$("#sort-modified").removeClass("ui-btn-active");
			$("#sort-size").removeClass("ui-btn-active");
		}
		else if(received["aArray"][0] == "SIZE")
		{
			$("#sort-name").removeClass("ui-btn-active");
			$("#sort-modified").removeClass("ui-btn-active");
			$("#sort-size").addClass("ui-btn-active");
		}
		
		//Get the cleanfile-flag
		thesocket.SendCommand("GETCLEANFILENAMES","");
	} //FILESORTING

	if(received.command == "GETCLEANFILENAMES")
	{
		//Set the value to the checkbox after validated as true
		var bToggle = (received["aArray"][0] == 'true');
		$('#cleanfilenames').prop('checked', bToggle).checkboxradio('refresh');

		//Now restore the filelist and refresh queue, if it existed
		thesocket.SendCommand("RESTOREFILELIST","");
	} //GETCLEANFILENAMES
	
	//We did not receive anything, ask for the queue
	if(received.command == "NOFILELIST")
	{
		//Open the navigation window, for fast access
		 setTimeout(function () {$('#navigation').panel('open');}, 100); // delay above zero

		//Ask for the queue
		thesocket.SendCommand("QUEUEREFRESH","");
	} //NOFILELIST

	//Restorefilelist contain the headertext we want to set
	if(received.command == "RESTOREFILELIST")
	{
		var i;
		var li="";
		if(received["count"]>0)
		{
			console.log("Restorename: " + received["aArray"][0]);
			$("#headertext").text(received["aArray"][0]);
			$("#headertext").prepend('<span class="ui-icon-myimage ui-btn-icon-notext inlineIcon"/></span>');
		}
		
		//Ask for the queue
		thesocket.SendCommand("QUEUEREFRESH","");		
	} //RESTOREFILELIST
	
	
	//Gets a filelist from the server
	if(received.command == "FILELIST")
	{
		ClearFileList("");
		
		var i;
		var li="";
		var sImage="";
		var knownExtensions = ['.MP4','.M4V', '.MKV', '.MPG', '.MPEG', '.AVI', '.WMV', '.FLV', '.WEBM', '.TS', '.MTS', '.M2TS', '.MOV'];
		var sExtension="";
		for(i=0;i<received["count"];i++)
			if(received["aArray"][i]["bIsFolder"] == true)
			{
				sImage="folder.png";
				if(received["aArray"][i]["sFileSize"]=="..")
					sImage="folder-up.png";
				li += '<li><a href="#" id="' + i + '" class="file-select"><img src="/.images/' + sImage + '" alt="Folder" class="ui-li-icon">' + received["aArray"][i]["sDisplayName"] + '</a></li>';		
			}
			else
			{
				//Default image
				sImage="file.png";
				
				//Get the extension
				sExtension='.'+received["aArray"][i]["sDisplayName"].split('.').pop().toUpperCase();
				for(o=0;o<knownExtensions.length;o++)
					if( sExtension.toUpperCase().indexOf(knownExtensions[o]) != -1 )
						sImage="movie.png";
				if(sExtension == ".RAR")
					sImage="archive.png";
						
				li += '<li><a href="#" id="' + i + '" class="file-select"><img src="/.images/' + sImage + '" alt="Folder" class="ui-li-icon">' + received["aArray"][i]["sDisplayName"] + '<span class="ui-li-count">' + received["aArray"][i]["sFileSize"] + '</span></a></li>';
			}

		//append list to ul
		$("#filelist").append(li).promise().done(function ()
		{
			//wait for append to finish - thats why you use a promise()
			//done() will run after append is done
			//add the click event for the redirection to happen to #details-page
			$(this).on("click", ".file-select", function (e)
			{
				e.preventDefault();
				var fileinfostored = received["aArray"][this.id];
				FileListClick(fileinfostored);
				console.log(fileinfostored);
			});		
		
		//Set data to drives list
		$(this).listview("refresh").trigger('create');
		});
	} //FILELIST
	
	//Gets fileattributes about the selected file
	if(received.command == "SETFILESELECTED")
	{
		$("#fileinfo").empty();
		
		//Add attributes to list
		var i;
		var li="";
		var sDescriptionFront = ['Clean filename:', 'Filesize:', 'Real filesize:', 'File extension:', 'Date created:', 'Date last changed:'];
		var sDescriptionBack = ['', '', 'bytes','', '', ''];
		for(i=0;i<received["count"]-2;i++)
			li += '<li><b>' + sDescriptionFront[i] + '</b> ' + received["aArray"][i+1] + ' <b>' + sDescriptionBack[i] + '</b></li>';
		
		//Store the fileid and extension in page
		$("#details-page").data("fileid", received["aArray"][7] );
		$("#details-page").data("extension", received["aArray"][4] );
		$("#details-page").data("filename", received["aArray"][0]);
		$("#details-page").data("filenameclean", received["aArray"][1]);

		$.mobile.pageContainer.pagecontainer("change", "#details-page");
		
		//Add the filename to the first 
		var strHeader = $('#filename').find('.ui-collapsible-heading-toggle');
		strHeader.text(received["aArray"][0]);

		//append list to ul
		$("#fileinfo").append(li).promise();
		$("#fileinfo").listview("refresh").trigger('create');
		//$.mobile.changePage("#details-page");
		//$.mobile.pageContainer.pagecontainer("change", "#details-page");
	} //SETFILESELECTED
	
	
	//Handle the queue of converted items
	if(received.command == "QUEUEREFRESH")
	{
		//Clear the listview
		$("#queue-running").off(); //Remove all events set by .on()!
		$("#queue-notrunning").off(); //Remove all events set by .on()!
		$("#queue-finished").off(); //Remove all events set by .on()!
		//Clear the listview
		$("#queue-running").empty();
		$("#queue-notrunning").empty();
		$("#queue-finished").empty();
		
		var i;
		var li_running = '<li data-role="list-divider">Running</li>';
		var li_notrunning = '<li data-role="list-divider">Queue</li>';
		var li_finished = '<li data-role="list-divider">Finished</li>';
		for(i=0;i<received["count"];i++)
		{
			var li="";
			var sStatus = received["aArray"][i]["nStatus"];
			var nQueueID = received["aArray"][i]["nQueueID"];

			//alert(received["aArray"][i]["nQueueID"]);
			
			li += '<li><a href="#" id="' + nQueueID + '" class="queue-select">';
			li += '<h2>' + received["aArray"][i]["sDisplayName"] + '</h2>';		
			li += '<p><strong>Profile:</strong> ' + received["aArray"][i]["sHandbrakeProfile"] + '</p>';
			li += '<p><strong>Drive:</strong> ' + received["aArray"][i]["sDisplayDirectory"] + '</p>';			
//			li += '<p><strong>Temp nQueueID:</strong> ' + nQueueID + '</p>';			
//			li += '<p><strong>Temp Status:</strong> ' +  sStatus + '</p>';			
			if(sStatus == "RUNNING")
			{
				li += '<div id="progressbar' + nQueueID + '"></div>';
				li += '<script>';
				li += '	var progressbar'+ nQueueID + ' = new Nanobar({target: document.getElementById("progressbar' + nQueueID + '")});';
				li += '	progressbar'+ nQueueID + '.go(' + received["aArray"][i]["iProcentage"] + ' );';
				li += '</script>';
				li += '<div id="eta' + nQueueID + '" style="text-align:center;font-size: 10px;">' + received["aArray"][i]["sETA"] + '</div>';
				li += '<a href="#queuemanagement-running" onclick="$(\'#queuemanagement-running\').data(\'queueid\', \''+ received["aArray"][i]["nQueueID"] + '\');" data-rel="popup" data-position-to="window" data-transition="pop">Queue management</a>';
			}
			if(sStatus == "NOT_RUNNING")
				li += '<a href="#queuemanagement" onclick="$(\'#queuemanagement\').data(\'queueid\', \''+ received["aArray"][i]["nQueueID"] + '\');" data-rel="popup" data-position-to="window" data-transition="pop">Queue management</a>';
			li += '</a></li>';

			if(sStatus == "RUNNING")
				li_running += li;
			else if(sStatus == "NOT_RUNNING")
				li_notrunning += li;
			else
				li_finished += li;
		}

		//Fill the queue-running
		$("#queue-running").append(li_running).promise().done(function ()
		{
			$(this).on("click", ".queue-select", function (e)
			{
				e.preventDefault();
				var queueinfostored = this.id;
				QueueListClick(queueinfostored);
			});				
			//Set data to queue-running
			$(this).listview("refresh").trigger('create');
		});		

		//Fill the queue-notrunning
		$("#queue-notrunning").append(li_notrunning).promise().done(function ()
		{
			$(this).on("click", ".queue-select", function (e)
			{
				e.preventDefault();
				var queueinfostored = this.id;
				QueueListClick(queueinfostored);
			});
			//Set data to queue-running
			$(this).listview("refresh").trigger('create');
		});		

		//Fill the queue-finished
		$("#queue-finished").append(li_finished).promise().done(function ()
		{
			$(this).on("click", ".queue-select", function (e)
			{
				e.preventDefault();
				var queueinfostored = this.id;
				QueueListClick(queueinfostored);
			});				
			//Set data to queue-running
			$(this).listview("refresh").trigger('create');
		});		

		
		thesocket.SendCommand("MOBILELIST", "");
	} //QUEUEREFRESH	

	//Gets fileattributes about the selected file (array is little different than others)
	if(received.command == "SETQUEUESELECTED")
	{
		$("#queueinfo").empty();
				
		//Add attributes to list
		var i;
		var li="";
		
		var sDescriptionFront = ['Displayname:', 'Drive:', 'Path:', 'Source:', 'Destination:', 'Convert profile:', 'Currently converting:'];
		for(i=0;i<received["count"]-5;i++)
			li += '<li><b>' + sDescriptionFront[i] + '</b> ' + received["aArray"][i] + '</b></li>';
		
		
		//Only display progress if we are currently converting
		if(received["aArray"][6] == "RUNNING")
		{
			li+='<li>';
			li += '<div id="queueprogressbar"></div><center><p id="eta">' + received["aArray"][8] + '</p></center>';
			li += '<script>';
			li += '	var queueprogressbar = new Nanobar({target: document.getElementById("queueprogressbar")});';
			li += '	queueprogressbar.go(' + received["aArray"][7] + ' );';
			li += '</script>';
			li+='</li>';
		}
		
		//Set the outputtext
		var output = "";
		var outputcount = received["count"]-1
		for(i=0;i<received["aArray"][outputcount].length;i++)
			output += received["aArray"][outputcount][i] + "<br>"

		//Store the QueueID selected (used for progressbar checking)
		$("#queue-page").data("queueid", received["aArray"][9] );
		
		$.mobile.pageContainer.pagecontainer("change", "#queue-page");
		
		//append list to ul
		$("#queueinfo").append(li).promise();
		$("#queueoutput").empty().append(output).promise();
		$("#queueinfo").listview("refresh").trigger('create');
	} //SETQUEUESELECTED

	if(received.command == "UPDATEQUEUEPROCENTAGE")
	{
		//Set the current procentage & eta for the queue
		eval('progressbar' + received["aArray"][0] ).go(received["aArray"][1]);
		$('#eta' + received["aArray"][0] ).text(received["aArray"][2]);
		
		//Update the queueprogressbar if it exists and the queue selected is right :)
		if (typeof queueprogressbar !== 'undefined')
		{
			if($("#queue-page").data("queueid") == received["aArray"][0])
			{
				queueprogressbar.go(received["aArray"][1]);
				$("#eta").text(received["aArray"][2]);
			}
		}
	} //UPDATEQUEUEPROCENTAGE
	
	if(received.command == "DOWNLOAD")
	{		
		//Get the clean file tag
		var isCleanfilename =  $('#cleanfilenames').is(':checked');
		
		//Set the name based on the clean-flag
		var sMovieName = "";
		if(!isCleanfilename)
			sMovieName = $("#details-page").data("filename");
		else
			sMovieName = $("#details-page").data("filenameclean");
		var sMovieNameWOExtension = sMovieName.replace(/\.[^/.]+$/, "")
		
		//Generate a M3U-filelist on the fly, based on the download-generated link
		download("#EXTM3U\n#EXTINF:0," + sMovieNameWOExtension +"\n" + location.protocol + "//" + location.host + "/MOTR-download/" + received["aArray"][0] + "/" + sMovieName, sMovieNameWOExtension + ".m3u", "application/x-winamp-playlist");
	}
	
	//Mobilelist is all the available mobiles the user have
	if(received.command == "MOBILELIST")
	{
		$("#popupItems").off(); 
		$("#popupItems").empty();
		$("#mobileselected").off();
		$("#mobileselected").empty();

		var outputmobiledownload = '<option value="-1">Select mobile here</option>';
		
		var output = '<li data-role="list-divider">Please select destination:</li>';
		output += '<li><a href="#" id="' + -1 + '" class="download-select">Download to this machine</a></li>';
		var outputcount = received["count"];
		for(var i=0;i<outputcount;i=i+2)
		{
			output += '<li><a href="#" id="' + received["aArray"][i] + '" class="download-select">' + received["aArray"][i+1] + '</a></li>';		
			outputmobiledownload += '<option value="' + received["aArray"][i] + '">' + received["aArray"][i+1] + '</option>';
		}

		//Add the mobile to the dropdownload list in the front
		console.log("Adding mobiles");
		console.log(outputmobiledownload);
		$("#mobileselected").append(outputmobiledownload).promise().done(function ()
		{
			$(this).on("change", function(event,ui)
			{
				e.preventDefault();
				//console.log("MobileID: " + event.target.value);				
				$(this).selectmenu("refresh");
				
				//Ask for a filelist
				thesocket.SendCommand("MOBILEDOWNLOADLIST",event.target.value);		
			});	
			
			//Set data to drives list
			$(this).selectmenu("refresh").trigger('create');
		});	
		
		//Check if the popup menu exists, return if not
		if($("#popupItems").hasClass('ui-listview') == false)
			return;
		
		//Add information regarding mobile registration if none exists = 0
		if(outputcount == 0)
		{
			output +='<li type="separator"></li>';
			output +='<li>All your registered mobile devices will show here.<br>You can sync them from here. </li>';
		}
		
		//append list to ul with eventhandler
		$("#popupItems").append(output).promise().done(function ()
		{
			$(this).on("click", ".download-select", function (e)
			{
				e.preventDefault();
				var nMobileID = this.id;
				if(nMobileID!=-1)
				{
					var info = $("#details-page").data("fileid");
					thesocket.SendCommand("MOBILEDOWNLOAD", nMobileID + ";" + info);
				}
				else
				{
					//Get current info from page and use that for downloading
					var info = $("#details-page").data("fileid");
					var sDownloadLink = '/' + info + '/down.load';
					window.open(sDownloadLink);
				}
				$("#popupMenu").popup("close");
			});		
	
			//Set data to drives list
			$(this).listview("refresh").trigger('create');
		});

		//Show popup
		$("#popupMenu").popup("open", { positionTo: '#download-button' }); 
	} //MOBILELIST

	
	//A list of all the downloads the user currently have
	if(received.command == "MOBILEDOWNLOADLIST")
	{
		$("#mobiledownloadlist").off(); 
		$("#mobiledownloadlist").empty();
		
		var outputcount = received["count"];
		var output = "";
		for(var i=0;i<outputcount;i=i+6)
		{
			output += '<li><a href="#" id="' + received["aArray"][i] + '" class="mobiledownload-select"><h3>' + received["aArray"][i+2] + '</h3><p>Status: ' + received["aArray"][i+4] + '</p><p>Status: ' + received["aArray"][i+5] + '</p></a></li>';		
		}
		
		//append list to ul with eventhandler
		$("#mobiledownloadlist").append(output).promise().done(function ()
		{
			$(this).on("click", ".mobiledownload-select", function (e)
			{
				e.preventDefault();
				var nMobileID = $("#mobileselected").val();
				console.log("MobileSelected: " + nMobileID);
				$('#mobiledownloadlistmanagement').data("MobileID", nMobileID);
				$('#mobiledownloadlistmanagement').data("MobileDownloadID", this.id);
				$('#mobiledownloadlistmanagement').popup('open');
			});		
	
			//Set data to drives list
			$(this).listview("refresh").trigger('create');
		});
		
	} //MOBILEDOWNLOADLIST
	
	if(received.command == "ERRORNOTLOGGEDIN")
	{
		this.HelperClearAllGUIItems();
		$.toast('Session not valid, redirect to login page', {'duration': 5000});	
		window.location.replace("/");
	}
	
	
	/*
	//Just a temp routine, to be deleted
    for (var key in received) {
        //Im using grid layout here.
        //use any kind of layout you want.
        //key is the key of the property in the object 
        //if obj = {name: 'k'}
        //key = name, value = k
        //info_view += '<div class="ui-grid-a"><div class="ui-block-a"><div class="ui-bar field" style="font-weight : bold; text-align: left;">' + key + '</div></div><div class="ui-block-b"><div class="ui-bar value" style="width : 75%">' + info[key] + '</div></div></div>';
		console.log("Temp logger: " + key + " - " + received[key] );
    }
	*/	
}

//Clear the filelist and add a message if needed
function ClearFileList(sTemporaryMessage)
{
	//Clear the listview
	$("#filelist").off(); //Remove all events set by .on()!

	//Set filter off
	$('input[data-type="search"]').val('');
	$('input[data-type="search"]').trigger("keyup");

	//Clear the listview
	$("#filelist").empty();
	$.mobile.silentScroll(0);
	
	if(sTemporaryMessage != "")
	{
		sTemporaryMessage = "<li>" + sTemporaryMessage + "</li>";
		$("#filelist").append(sTemporaryMessage).promise().done();
		$("#filelist").listview("refresh").trigger('create');
	}
}

//Function called from the websocketbutton in bottom.
function tryReconnect() {thesocket.reconnect();}

//Function open a new page that forwards to the actual file and starts downloading...
function onDownloadFile()
{
	//Send request for all mobile units available
	thesocket.SendCommand("MOBILELIST", "");


}

function onStreamMedia()
{
	//Close the popup
	$('#dlg-streammessage').popup('close');
	
	//Get the fileid and request an file
	var fileid = $("#details-page").data("fileid");
	thesocket.SendCommand("DOWNLOAD", fileid);
}

function showPopupConvert(sHeader, sText, sElement)
{
	var pos = $(sElement).offset();
	$("#popup-line1").text(sHeader);
	$("#popup-line2").text(sText);
	$("#popupArrow" ).popup( "open", { positionTo: sElement } );
}

//Function to add to the queue
function onQueueAdd(bTop)
{
	var fileid = $("#details-page").data("fileid");
	var filename = $("#output-filename").val();
	var profile = $("#convert-selection option:selected").val();
	if(profile == "Choose...")
	{
		showPopupConvert("Heads up!", "First off, select the profile you want to convert with!", "#convert-selection");
		return;
	}
	if(filename.length == 0)
	{
		showPopupConvert("Note to self!", "Filename cannot be empty!", "#output-filename");
		return;
	}
	
	//Hide the convert div
	onConvertDiv(false);
	
	//Send command
	thesocket.SendCommand("QUEUEADD",fileid+";"+filename+";"+aHandBreakPresets[profile]+";"+bTop);
	
	//Show popup
	showPopupConvert(filename + " added to queue!", "Go back and check the queue to see status!", "#convert-button");
}

//Override for rar archives
function onQueueUnrar()
{
	var fileid = $("#details-page").data("fileid");
	//Send command
	thesocket.SendCommand("QUEUEADD",fileid+";;Extracting file(s);false");
	
	//Show popup
	var strHeader = $('#filename').find('.ui-collapsible-heading-toggle');
	showPopupConvert(strHeader.text() + " added for extraction, to the bottom of the queue!", "Go back and check the queue to see status!", "#extract-button");
}

function onQueueAction(sAction)
{
/*	$("#dialog-header").text("tes");
	$("#dialog-text").text(sAction);
	$("#dialog-button").text("jadda");
	$.mobile.changePage('#popup-message', 'pop', true, true);*/

/*	var r = confirm("Do you want to execute this command on queue?");
	if (r != true)
		return;*/

	
	//Items that are running has a smaller menu
	if(sAction == 'stop-running' || sAction == 'remove-running')
	{
		var nQueueID = $('#queuemanagement-running').data('queueid');
		//alert(nQueueID);
		thesocket.SendCommand("QUEUEMANAGEMENT", nQueueID+";"+sAction);		
		$('#queuemanagement-running').popup('close');
		return;
	}

	var nQueueID = $('#queuemanagement').data('queueid');
	thesocket.SendCommand("QUEUEMANAGEMENT", nQueueID+";"+sAction);
	$('#queuemanagement').popup('close');
}

//******************************************
//** JQUERY / GUI stuff

function SetWebsocketButton(sText)
{
	if(sText.length == 0)
		sText = "Disconnected";

	var button = $("#websocketbtn");
	var innerTextSpan = button.find(".ui-btn-text");

	if(sText != "Disconnected")
		button.addClass('ui-disabled');
	else
		button.removeClass('ui-disabled');
	
	// not initialized - just change label
	if (innerTextSpan.size() == 0) {
		button.text(sText);

	// already initialized - find innerTextSpan and change its label    
	} else {
		innerTextSpan.text(sText);
	}
}

function DrivesListClick(aarray)
{
	//Here we fire away a command to get the filelist
	if(thesocket.getSocketState() != 1)
	{
		thesocket.reconnect();
		alert('You have to try again when connected...');
		return;
	}
	
	thesocket.SendCommand("SETDRIVE",aarray.nID);
}

function QueueListClick(nQueueID)
{
	//Here we fire away a command to get the filelist
	if(thesocket.getSocketState() != 1)
	{
		thesocket.reconnect();
		alert('You have to try again when connected...');
		return;
	}
		
	//Handle folder and files different
	thesocket.SendCommand("SETQUEUESELECTED",nQueueID);
}

function OnQueueRefresh()
{
	if(thesocket.getSocketState() != 1)
	{
		thesocket.reconnect();
		alert('You have to try again when connected...');
		return;
	}
		
	//Handle folder and files different
	thesocket.SendCommand("REFRESHQUEUESELECTED","");
}

function FileListClick(aarray)
{
	//Here we fire away a command to get the filelist
	if(thesocket.getSocketState() != 1)
	{
		thesocket.reconnect();
		alert('You have to try again when connected...');
		return;
	}

	//Handle folder and files different
	if(!aarray["bIsFolder"] )
	{
		//alert('is file, function not ready...');
		thesocket.SendCommand("SETFILESELECTED",aarray.nID);
	}
	else
	{
		thesocket.SendCommand("SETFOLDER",aarray.nID);
	}
}

function toggleCleanFilenames()
{
	//Here we fire away a command to get the filelist
	if(thesocket.getSocketState() != 1)
	{
		thesocket.reconnect();
		alert('You have to try again when connected...');
		return;
	}
	
	var isChecked =  $('#cleanfilenames').is(':checked');
	thesocket.SendCommand("CLEANFILENAMES",isChecked);
}

function setSorting(sSort)
{
	thesocket.SendCommand("SETFILESORTING",sSort);
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
	thesocket.connect(window.location.hostname, window.location.port, "directory", window.websockethandler);	
}

//First init when reloaded
$(document).on("pageinit", "#thepage", function () {

	//Create and connect to websocket
	console.log("Pageinit..");
	CreateAndConnect();
});


$(document).on("pagebeforeshow", "#popup-message", function () {
	if (typeof thesocket === 'undefined' || thesocket === null)
	{
		//Variable is global, but a refresh kills it!
		$.mobile.changePage("#thepage");
		return;
	}
});

//use pagebeforeshow
//DONT USE PAGEINIT! 
//the reason is you want this to happen every single time
//pageinit will happen only once
$(document).on("pagebeforeshow", "#details-page", function () {

	//No websocket, then no love! Go back
	if (typeof thesocket === 'undefined' || thesocket === null)
	{
		//Variable is global, but a refresh kills it!
		$.mobile.changePage("#thepage");
		console.log("Refresh on details page...");
		return;
	}
	
	//Check the state of clean files, and set the toggleswitch
	if($('#cleanfilenames').is(':checked'))
		$("#fileclean").val('on').flipswitch('refresh');
	else
		$("#fileclean").val('off').flipswitch('refresh');
	
	//Hide the convert data and button as default
	$('#convert-options').hide();
	$('#convert-button').hide();
	$('#extract-button').hide();
	$('#stream-button').hide();
	
	//Hide the convert-button if the extension is not known
	var knownExtensions = ['.MP4','.M4V', '.MKV', '.MPG', '.MPEG', '.AVI', '.WMV', '.FLV', '.WEBM', '.TS', '.MTS', '.M2TS', '.MOV'];
	
	//Override of the convertbutton if the extension of the file contains -MOTR[
	var sExtension = $("#details-page").data("extension");
	var sFileName = $("#details-page").data("filename");
	if( sFileName.toUpperCase().indexOf("-MOTR[") != -1)
		knownExtensions = '';
	
	for(i=0;i<knownExtensions.length;i++)
	{
		if( sExtension.toUpperCase().indexOf(knownExtensions[i]) != -1 )
		{
			$('#convert-button').show();
			$('#stream-button').show();
		}
	}
	if( sExtension.toUpperCase() == ".RAR") //Add the extract button
		$('#extract-button').show();
});

$(document).on("pagebeforeshow", "#queue-page", function () {
	//No websocket, then no love! Go back
	if (typeof thesocket === 'undefined' || thesocket === null)
	{
		//Variable is global, but a refresh kills it!
		$.mobile.changePage("#thepage");
		return;
	}
});
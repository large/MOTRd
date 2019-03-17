using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using WebSockets.Common;
using WebSockets.Server.WebSocket;
using System.Runtime.Serialization;
using fastJSON;

namespace MOTRd
{
    class MOTR_DirectoryWebsocket : WebSocketService
    {
        private readonly IWebSocketLogger _logger;
        private readonly MOTR_Sessions m_Sessions;
        private readonly MOTR_Dirs m_Dirs;
        private readonly MOTR_Queue m_Queue;
        private readonly MOTR_Users m_Users;
        private readonly MOTR_Downloads m_Downloads;
        private MOTR_Moviescraper m_Moviescraper;
        private bool bIsLoggedIn;
        private string sSessionID;

        public event EventHandler OnQueueUpdate;

        public MOTR_DirectoryWebsocket(Stream stream, TcpClient tcpClient, string header, IWebSocketLogger logger, MOTR_Sessions _sessions, MOTR_Dirs _dirs, MOTR_Queue _queue, MOTR_Users _users, MOTR_Downloads _downloads)
            : base(stream, tcpClient, header, true, logger)
        {
            _logger = logger;
            m_Sessions = _sessions;
            m_Dirs = _dirs;
            m_Queue = _queue;
            m_Users = _users;
            m_Downloads = _downloads;
            bIsLoggedIn = false;
            sSessionID = "";
            m_Moviescraper = null;
        }

        protected override void OnTextFrame(string text)
        {
            string jsonText = null;

            //             WebsocketSendClass WSTest2 = new WebsocketSendClass();
            //             WSTest2.command = "Do this thing";
            //             WSTest2.aArray = m_Dirs.GetArray();
            //             WSTest2.count = WSTest2.aArray.Count;
            //             jsonText = fastJSON.JSON.ToJSON(WSTest2);

            //Just for debugging purposes
            //Console.WriteLine("Received: " + text);

            //This object is used to send info
            WebsocketSendClass WSSend = new WebsocketSendClass();

            //Every object is linked to this
            WebSocketCommandClass WSCommand = new WebSocketCommandClass();

            //Try & catch on the parsing
            try
            {
                if (text.Length <= 0 || text=="{}")
                {
                    WSCommand.command = "";
                    WSCommand.parameter = "";
                }
                else
                {
                    dynamic WSTemp = fastJSON.JSON.ToDynamic(text);
                    WSCommand.command = WSTemp.command;
                    WSCommand.parameter = WSTemp.parameter;
                }
                //sSessionID = WSTemp.sessionid;
                //WSCommand = (WebSocketCommandClass)fastJSON.JSON.ToObject(text, new JSONParameters() { UseExtensions = false });
            }
            catch(Exception ex)
            {
                _logger.Error(typeof(MOTR_DirectoryWebsocket), "Error parsing JSON: " + text + " - Returnstring: " + ex.ToString());
                return;
            }

            _logger.Information(typeof(MOTR_DirectoryWebsocket), "<-- Command: " + WSCommand.command);


            //if (!m_Sessions.SessionLoggedIn(sSessionID))
            if (!bIsLoggedIn)
            {
                //Check if we are going to try to login 
                if (WSCommand.command == "APPLOGIN")
                {
                    string[] aParameters = WSCommand.parameter.Split(';');
                    if (aParameters.Count() >= 1)
                    {
                        string sUser = aParameters[0];
                        string sPass = aParameters[1];
                        if (m_Users.UsernameAndPasswordMatch(sUser, sPass))
                        {
                            string sSession = m_Sessions.AddSession();
                            m_Sessions.SetLogin(sSession, true, sUser);
                            sSessionID = sSession; //Store the session in the Websocket, only valid as long as the connection is running
                            bIsLoggedIn = true; //Now we are logged in
                            string sEncryptedUsername = m_Sessions.Encrypt(sUser); //Created as an "Auth" function to link in sessionrestore

                            //Get the TempID or create a new one if it does not exist
                            string sTempID = m_Sessions.GetTempIDBySession(sSessionID);
                            if (sTempID.Length == 0)
                                sTempID = m_Sessions.GenerateTempIDInSession(sSessionID);

                            //Store the UserID & Username
                            m_Sessions.SetTemporaryVariable(sSessionID, "UserID", m_Users.GetUserID(sUser).ToString());
                            m_Sessions.SetTemporaryVariable(sSessionID, "Username", sUser);

                            //Send response back
                            WSSend.command = "APPLOGIN";
                            WSSend.aArray = new ArrayList();
                            WSSend.aArray.Add(sSession);
                            WSSend.aArray.Add(sTempID);
                            WSSend.aArray.Add(sEncryptedUsername);
                            WSSend.count = WSSend.aArray.Count;
                            jsonText = fastJSON.JSON.ToJSON(WSSend/*, new JSONParameters() { UseExtensions = false }*/);
                            _logger.Information(typeof(MOTR_DirectoryWebsocket), "--> APPLOGIN (OK)");
                        }
                        else
                        {
                            //Send response back
                            WSSend.command = "ERROR";
                            WSSend.aArray = new ArrayList();
                            WSSend.aArray.Add("Username or password is incorrect");
                            WSSend.count = WSSend.aArray.Count;
                            jsonText = fastJSON.JSON.ToJSON(WSSend);
                            _logger.Information(typeof(MOTR_DirectoryWebsocket), "--> APPLOGIN (ERROR)");
                        }
                        base.Send(jsonText);
                        return;
                    }
                }

                if (WSCommand.command == "SESSIONRESTORE")
                {
                    string[] aParameters = WSCommand.parameter.Split(';');
                    if (aParameters.Count() == 3)
                    {
                        string sSession = aParameters[0];
                        string sEncryptedUser = aParameters[1];
                        string sUsernameFromUser = aParameters[2];
                        string sDecryptedUser = m_Sessions.Decrypt(sEncryptedUser);

                        //If the user isn't telling us the correct username, then drop the session
                        if(sUsernameFromUser.ToUpper() == sDecryptedUser.ToUpper())
                        {
                            //Store the Username before SessionLoggedIn since it uses it
                            m_Sessions.SetTemporaryVariable(sSession, "Username", sDecryptedUser);
                            if (m_Sessions.SessionLoggedIn(sSession, sDecryptedUser))
                            {
                                //Store the session
                                bIsLoggedIn = true;
                                sSessionID = sSession; //Store the session

                                //Get the TempID
                                string sTempID = m_Sessions.GetTempIDBySession(sSession);
                                if (sTempID.Length == 0)
                                    sTempID = m_Sessions.GenerateTempIDInSession(sSession);

                                //Store the UserID
                                m_Sessions.SetTemporaryVariable(sSessionID, "UserID", m_Users.GetUserID(sDecryptedUser).ToString());

                                //Send response back
                                WSSend.command = "SESSIONRESTORE";
                                WSSend.aArray = new ArrayList();
                                WSSend.aArray.Add(sTempID);
                                WSSend.count = WSSend.aArray.Count;
                                jsonText = fastJSON.JSON.ToJSON(WSSend);
                                base.Send(jsonText);
                                _logger.Information(typeof(MOTR_DirectoryWebsocket), "Sessionrestore OK");
                                return;
                            }
                        } //Username decrypted vs users input, usally stored
                    }
                }
    
                //Send response back for everything else
                _logger.Warning(typeof(MOTR_DirectoryWebsocket), "Not logged in response sent");
                WSSend.command = "ERRORNOTLOGGEDIN";
                WSSend.aArray = new ArrayList();
                WSSend.aArray.Add("Please login before sending commands to MOTR");
                WSSend.count = WSSend.aArray.Count;
                jsonText = fastJSON.JSON.ToJSON(WSSend);
                base.Send(jsonText);
                return;
            }

            //Request for available drives, send array
            if (WSCommand.command == "GETAVAILABLEDIRS")
            {
                WSSend.command = "AVAILABLEDIRS";
                WSSend.aArray = m_Dirs.GetDirectoryArray();
                WSSend.count = WSSend.aArray.Count;
                jsonText = fastJSON.JSON.ToJSON(WSSend);
            }
            else if (WSCommand.command == "SETDRIVE")
            {
                //Store which drive selected here, clear earlier path
                string sDrive = m_Dirs.GetDriveById(Convert.ToInt32(WSCommand.parameter));
                m_Sessions.SetTemporaryVariable(sSessionID, "Drive", sDrive); //Static to known loweste path
                m_Sessions.SetTemporaryVariable(sSessionID, "DrivePosition", sDrive); //Dynamic, sets to the path we currently are browsing
                m_Sessions.SetTemporaryVariable(sSessionID, "DriveID", WSCommand.parameter); //Store the DriveID for use later
                m_Sessions.SetTemporaryVariable(sSessionID, "LastFolder", ""); //Change of drive resets the lastfolder
                jsonText = CreateJSONFilelist(sSessionID, sDrive, WSSend);
            }
            else if (WSCommand.command == "SETFOLDER")
            {
                string sPath = m_Sessions.GetPathByID(sSessionID, Convert.ToInt32(WSCommand.parameter));
                string sDrive = m_Sessions.GetTemporaryVariable(sSessionID, "Drive");
                if (sPath.Substring(sPath.Length - 1) != "\\")
                    sPath += '\\';

                //Store the last folder for the "LASTFOLDER" function
                string sLastPath = m_Sessions.GetTemporaryVariable(sSessionID, "DrivePosition");
                if (sLastPath.Length > sPath.Length)
                {
                    sLastPath = sLastPath.Substring(sPath.Length);
                    sLastPath = sLastPath.Substring(0, sLastPath.Length - 1);
                    m_Sessions.SetTemporaryVariable(sSessionID, "LastFolder", sLastPath);
                }
                m_Sessions.SetTemporaryVariable(sSessionID, "DrivePosition", sPath);
                jsonText = CreateJSONFilelist(sSessionID, sPath, WSSend);
            }
            else if (WSCommand.command == "LASTFOLDER")
            {
                string sLastFolder = m_Sessions.GetTemporaryVariable(sSessionID, "LastFolder");

                //No love, no answer
                if (sLastFolder.Length == 0 || sLastFolder.IndexOf('\\') != -1)
                    return;

                //CleanDirectoryAndFilename
                string sClean = m_Sessions.GetTemporaryVariable(sSessionID, "Clean");

                if (sClean == "true")
                    sLastFolder = m_Sessions.CleanDirectoryAndFilename(sLastFolder);

                //Send the name of the directory selected
                WSSend.command = "LASTFOLDER";
                WSSend.aArray = new ArrayList();
                WSSend.aArray.Add(sLastFolder);
                WSSend.count = WSSend.aArray.Count;
                jsonText = fastJSON.JSON.ToJSON(WSSend);
            }
            else if (WSCommand.command == "CLEANFILENAMES")
            {
                //Store the parameter
                m_Sessions.SetTemporaryVariable(sSessionID, "Clean", WSCommand.parameter);

                string sPath = m_Sessions.GetTemporaryVariable(sSessionID, "DrivePosition");
                string sDrive = m_Sessions.GetTemporaryVariable(sSessionID, "Drive");
                jsonText = CreateJSONFilelist(sSessionID, sPath, WSSend);
            }
            else if (WSCommand.command == "GETCLEANFILENAMES")
            {
                //Get clean variable and send in return
                string sClean = m_Sessions.GetTemporaryVariable(sSessionID, "Clean");
                if (sClean.Length == 0)
                    sClean = "false";
                WSSend.command = "GETCLEANFILENAMES";
                WSSend.aArray = new ArrayList();
                WSSend.aArray.Add(sClean);
                WSSend.count = WSSend.aArray.Count;
                jsonText = fastJSON.JSON.ToJSON(WSSend);
                //Console.WriteLine("GetCleanFilenames: " + jsonText);
            }
            else if (WSCommand.command == "GETFILESORTING")
            {
                string sSorting = m_Sessions.GetTemporaryVariable(sSessionID, "Sorting");
                if (sSorting.Length == 0)
                    sSorting = "NAME";
                ArrayList aArray = new ArrayList(1);
                aArray.Add(sSorting);

                WSSend.command = "FILESORTING";
                WSSend.aArray = aArray;
                WSSend.count = WSSend.aArray.Count;
                jsonText = fastJSON.JSON.ToJSON(WSSend);
                //Console.WriteLine("Sorting: " + jsonText);
            }
            else if (WSCommand.command == "SETFILESORTING")
            {
                //Check which sorting we're going to use
                string sSort = "";
                if (WSCommand.parameter == "MODIFY")
                    sSort = "MODIFY";
                if (WSCommand.parameter == "SIZE")
                    sSort = "SIZE";
                if (WSCommand.parameter == "NAME")
                    sSort = "NAME";

                //Update sort and send updated filelist back
                if (sSort.Length > 0)
                {
                    m_Sessions.SetTemporaryVariable(sSessionID, "Sorting", sSort);
                    string sPath = m_Sessions.GetTemporaryVariable(sSessionID, "DrivePosition");
                    string sDrive = m_Sessions.GetTemporaryVariable(sSessionID, "Drive");
                    jsonText = CreateJSONFilelist(sSessionID, sPath, WSSend);
                }
            }
            else if (WSCommand.command == "RESTOREFILELIST")
            {
                string sPath = m_Sessions.GetTemporaryVariable(sSessionID, "DrivePosition");
                string sDrive = m_Sessions.GetTemporaryVariable(sSessionID, "Drive");

                //If no files was selected, we do nothing
                if (sPath.Length == 0 | sDrive.Length == 0)
                {
                    WSSend.command = "NOFILELIST";
                    WSSend.aArray = new ArrayList();
                    WSSend.count = WSSend.aArray.Count;
                    jsonText = fastJSON.JSON.ToJSON(WSSend);
                    base.Send(jsonText);
                    _logger.Information(typeof(MOTR_DirectoryWebsocket), "--> Command: " + WSSend.command);
                    return;
                }

                //Send the name of the directory selected
                WSSend.command = "RESTOREFILELIST";
                WSSend.aArray = new ArrayList();
                WSSend.aArray.Add(m_Dirs.GetNameByPath(sDrive));
                WSSend.count = WSSend.aArray.Count;
                jsonText = fastJSON.JSON.ToJSON(WSSend);
                base.Send(jsonText);
                _logger.Information(typeof(MOTR_DirectoryWebsocket), "--> Command: " + WSSend.command);

                //Here we send the filelist afterwards
                jsonText = CreateJSONFilelist(sSessionID, sPath, WSSend);
            }
            else if (WSCommand.command == "SETFILESELECTED")
            {
                string sFileID = WSCommand.parameter;
                if (sFileID.Length > 0 && sFileID.All(char.IsDigit))
                {
                    m_Sessions.SetTemporaryVariable(sSessionID, "FileSelected", sFileID);
                    string sPath = m_Sessions.GetTemporaryVariable(sSessionID, "DrivePosition");
                    string sFile = m_Sessions.GetPathByID(sSessionID, Convert.ToInt32(sFileID));
                    WSSend.command = "SETFILESELECTED";
                    WSSend.aArray = m_Sessions.GetFileInformation(sPath + sFile);
                    WSSend.aArray.Add(sFileID); //Added fileID at the end of array
                    WSSend.count = WSSend.aArray.Count;
                    jsonText = fastJSON.JSON.ToJSON(WSSend);
                    //Console.WriteLine("SETFILESELECT: " + jsonText);
                }
            }
            else if (WSCommand.command == "QUEUEADD")
            {
                _logger.Debug(typeof(MOTR_DirectoryWebsocket), "Parameters: " + WSCommand.parameter);
                string[] aParameters = WSCommand.parameter.Split(';');
                if (aParameters.Count() >= 3)
                {
                    string sFileID = aParameters[0];
                    if (sFileID.Length > 0 && sFileID.All(char.IsDigit))
                    {
                        string sPath = m_Sessions.GetTemporaryVariable(sSessionID, "DrivePosition");
                        string sFile = m_Sessions.GetPathByID(sSessionID, Convert.ToInt32(sFileID));
                        string sDrive = m_Sessions.GetTemporaryVariable(sSessionID, "Drive");
                        string sPathDisplay = sPath.Substring(sDrive.Length, sPath.Length - sDrive.Length);
                        string sDriveDisplay = m_Dirs.GetNameByPath(sDrive);
                        _logger.Debug(typeof(MOTR_DirectoryWebsocket), "Input: " + sFile);
                        _logger.Debug(typeof(MOTR_DirectoryWebsocket), "Output: " + aParameters[1]);
                        _logger.Debug(typeof(MOTR_DirectoryWebsocket), "Profile: " + aParameters[2]);
                        _logger.Debug(typeof(MOTR_DirectoryWebsocket), "Add top: " + aParameters[3]);
                        _logger.Debug(typeof(MOTR_DirectoryWebsocket), "Drive display: " + sDriveDisplay);
                        m_Queue.Add(sFile, sDriveDisplay, sPathDisplay, sPath, sFile, aParameters[1], aParameters[2], Convert.ToBoolean(aParameters[3]));
                    }
                }

                //Trigger event to force refresh on all clients
                this.QueueUpdate();
                return;
            }
            else if (WSCommand.command == "QUEUEREFRESH")
            {
                //Use the command equal to a refresh from others
                this.SendCommand("QUEUEREFRESHBYEVENT", new ArrayList());
                return;
            }
            else if (WSCommand.command == "SETQUEUESELECTED")
            {
                string sQueueID = WSCommand.parameter;
                if (sQueueID.Length > 0 && sQueueID.All(char.IsDigit))
                {
                    m_Sessions.SetTemporaryVariable(sSessionID, "QueueSelected", sQueueID);
                    WSSend.command = "SETQUEUESELECTED";
                    WSSend.aArray = m_Queue.GetQueueByID(Convert.ToInt64(sQueueID));
                    WSSend.count = WSSend.aArray.Count;
                    jsonText = fastJSON.JSON.ToJSON(WSSend);
                    //Console.WriteLine("SETQUEUESELECTED: " + jsonText);
                }
            }
            else if (WSCommand.command == "REFRESHQUEUESELECTED")
            {
                string sQueueID = m_Sessions.GetTemporaryVariable(sSessionID, "QueueSelected");
                if (sQueueID.Length > 0)
                {
                    WSSend.command = "SETQUEUESELECTED";
                    WSSend.aArray = m_Queue.GetQueueByID(Convert.ToInt64(sQueueID));
                    WSSend.count = WSSend.aArray.Count;
                    jsonText = fastJSON.JSON.ToJSON(WSSend);
                    //Console.WriteLine("REFRESHQUEUESELECTED: " + jsonText);
                }
            }
            else if (WSCommand.command == "QUEUEMANAGEMENT")
            {
                //Console.WriteLine("Parameters: " + WSCommand.parameter);
                string[] aParameters = WSCommand.parameter.Split(';');
                if (aParameters.Count() == 2)
                {
                    string sQueueID = aParameters[0];
                    if ((sQueueID.Length > 0 && sQueueID.All(char.IsDigit)) || sQueueID == "-1")
                    {
                        string sCommand = aParameters[1];
                        long nQueueID = Convert.ToInt64(sQueueID);
                        if (sCommand.ToUpper() == "RUN")
                            m_Queue.Run(nQueueID);
                        else
                            m_Queue.QueueManangement(nQueueID, sCommand);
                    }

                    //Update the queue for everyone
                    this.QueueUpdate();
                    return;
                }
            }
            else if (WSCommand.command == "PING")
            {
                WSSend.command = "PONG";
                WSSend.aArray = new ArrayList();
                WSSend.count = WSSend.aArray.Count;
                jsonText = fastJSON.JSON.ToJSON(WSSend);
                _logger.Debug(typeof(MOTR_DirectoryWebsocket), "Got PING from: " + sSessionID);
            }
            else if (WSCommand.command == "APPLOGOUT")
            {
                bool bRet = m_Sessions.RemoveSession(sSessionID);
                WSSend.aArray = new ArrayList();
                if (bRet)
                    WSSend.command = "APPLOGOUT";
                else
                {
                    WSSend.command = "ERROR";
                    WSSend.aArray.Add("SessionID does not exists!");
                }
                WSSend.count = WSSend.aArray.Count;
                jsonText = fastJSON.JSON.ToJSON(WSSend);
                _logger.Debug(typeof(MOTR_DirectoryWebsocket), "Logout from: " + sSessionID);
            }
            else if (WSCommand.command == "DOWNLOAD")
            {
                string sFileID = WSCommand.parameter;
                if (sFileID.Length > 0 && sFileID.All(char.IsDigit))
                {
                    string sPath = m_Sessions.GetTemporaryVariable(sSessionID, "DrivePosition");
                    string sFile = m_Sessions.GetPathByID(sSessionID, Convert.ToInt32(sFileID));
                    WSSend.command = "DOWNLOAD";
                    string sHours = DateTime.Now.AddHours(3).ToString(); //Three hours if downloadtime is usally enough
                    string sDownloadID = m_Downloads.AddDownload(sSessionID, sPath + sFile, false, sHours);
                    WSSend.aArray = new ArrayList();
                    WSSend.aArray.Add(sDownloadID); //Added fileID at the end of array
                    WSSend.count = WSSend.aArray.Count;
                    jsonText = fastJSON.JSON.ToJSON(WSSend);
                    _logger.Debug(typeof(MOTR_DirectoryWebsocket), "Download: " + sFile);
                }
                else
                {
                    WSSend.command = "ERROR";
                    WSSend.aArray = new ArrayList();
                    WSSend.aArray.Add("Could not find the ID"); //Added fileID at the end of array
                    WSSend.count = WSSend.aArray.Count;
                    jsonText = fastJSON.JSON.ToJSON(WSSend);
                    _logger.Debug(typeof(MOTR_DirectoryWebsocket), "Download not found...");
                }
            }
            else if (WSCommand.command == "GETSTOREDPARAMETER")
            {
                //Parameter is: <FILEID>;<COMMAND>
                string[] aParameters = WSCommand.parameter.Split(';');
                if (aParameters.Count() == 2)
                {
                    string sFileID = aParameters[0];
                    if (sFileID.Length > 0 && sFileID.All(char.IsDigit))
                    {
                        string sCommand = aParameters[1];
                        string sValue = m_Sessions.GetSessionLessVariable(sSessionID, Convert.ToInt32(sFileID), sCommand);
                        WSSend.command = "GETSTOREDPARAMETER";
                        WSSend.aArray = new ArrayList();
                        WSSend.aArray.Add(sCommand);
                        WSSend.aArray.Add(sValue); 
                        WSSend.count = WSSend.aArray.Count;
                        jsonText = fastJSON.JSON.ToJSON(WSSend);
                        //_logger.Information(typeof(MOTR_DirectoryWebsocket), "Value of command " + sCommand + " is " + sValue + "...");
                    }


                }
            }
            else if (WSCommand.command == "SETSTOREDPARAMETER")
            {
                //Parameter is: <FILEID>;<COMMAND>;<VALUE>
                string[] aParameters = WSCommand.parameter.Split(';');
                if (aParameters.Count() == 3)
                {
                    string sFileID = aParameters[0];
                    if (sFileID.Length > 0 && sFileID.All(char.IsDigit))
                    {
                        string sCommand = aParameters[1];
                        string sValue = aParameters[2];
                        if(sCommand.Length>0)
                        {
                            bool bRet = m_Sessions.StoreSessionLessVariable(sSessionID, Convert.ToInt32(sFileID), sCommand, sValue);

                            WSSend.command = "SETSTOREDPARAMETER";
                            WSSend.aArray = new ArrayList();
                            WSSend.aArray.Add(bRet.ToString());
                            WSSend.count = WSSend.aArray.Count;
                            jsonText = fastJSON.JSON.ToJSON(WSSend);
                            //_logger.Information(typeof(MOTR_DirectoryWebsocket), "Set value of command " + sCommand + " to " + sValue);
                        }
                    }
                }
            }
            else if (WSCommand.command == "MOVIEINFOQUERY")
            {
                string[] aParameters = WSCommand.parameter.Split(';');
                if (aParameters.Count() == 2)
                {
                    //Create an instance of our scraper
                    if (m_Moviescraper == null)
                        m_Moviescraper = new MOTR_Moviescraper(_logger);

                    //First check if the info already exists
                    string sFileID = aParameters[0];
                    if (sFileID.Length > 0 && sFileID.All(char.IsDigit))
                    {
                        //Checks if the movie already exists in the database
                        MovieInformation movieInformation = m_Sessions.GetMovieInformation(sSessionID, Convert.ToInt32(sFileID));
                        if(movieInformation!=null)
                        {
                            m_Moviescraper.movieInformation = movieInformation;
                            WSSend.command = "MOVIEINFO";
                            WSSend.aArray = m_Moviescraper.GetMovieArray(); //Also ensure that covers are downloaded
                            WSSend.count = WSSend.aArray.Count;
                            jsonText = fastJSON.JSON.ToJSON(WSSend);
                            aParameters[1] = ""; //Just a little hack todo nothing
                        }
                    }
                    
                    //Now get the movie and query the 
                    string sMovieQuery = aParameters[1];
                    if (sMovieQuery.Length > 0)
                    {
                        //Clean the query, remove any extension first
                        int nPos = sMovieQuery.LastIndexOf('.');
                        if (nPos > 0)
                            sMovieQuery = sMovieQuery.Substring(0, nPos);

                        //Use the cleaner in session to ensure that we only search text that is valid
                        string sCleaned = m_Sessions.CleanDirectoryAndFilename(sMovieQuery);

                        //Remove the MOTR if found
                        nPos = sCleaned.IndexOf("-MOTR[");
                        if (nPos > 0)
                            sCleaned = sCleaned.Substring(0, nPos);

                        //Remove any year at the end of string
                        int numericValue;
                        if (Int32.TryParse(sCleaned.Substring(sCleaned.Length - 1), out numericValue))
                        {
                            nPos = sCleaned.LastIndexOf(' ');
                            if (nPos > 0)
                                sCleaned = sCleaned.Substring(0, nPos);
                        }

                        WSSend.command = "MOVIEINFOQUERY";
                        WSSend.aArray = m_Moviescraper.Query(sCleaned);
                        WSSend.count = WSSend.aArray.Count;
                        jsonText = fastJSON.JSON.ToJSON(WSSend);
                    }
                }
            }
            else if (WSCommand.command == "MOVIEINFOSELECT")
            {
                string[] aParameters = WSCommand.parameter.Split(';');
                if (aParameters.Count() == 2)
                {
                    string sFileID = aParameters[0];
                    if (sFileID.Length > 0 && sFileID.All(char.IsDigit))
                    {
                        string sMovieID = aParameters[1];
                        if (sMovieID.Length > 0 && sMovieID.All(char.IsDigit) && m_Moviescraper != null)
                        {
                            //Select the movie here, we store that into the database
                            bool bRes = m_Moviescraper.Select(Convert.ToInt32(sMovieID));
                            if (bRes)
                                bRes = m_Sessions.StoreMovieInformation(sSessionID, Convert.ToInt32(sFileID), m_Moviescraper.movieInformation);

                            WSSend.command = "MOVIEINFO";
                            WSSend.aArray = m_Moviescraper.GetMovieArray(); //Also ensure that covers are downloaded
                            WSSend.count = WSSend.aArray.Count;
                            jsonText = fastJSON.JSON.ToJSON(WSSend);
                        }
                    }
                }
            }
            else if (WSCommand.command == "CONVERTHEADERS")
            {
                WSSend.command = "CONVERTHEADERS";
                WSSend.aArray = m_Queue.convertprofiles.GetHeadersArray();
                WSSend.count = WSSend.aArray.Count;
                jsonText = fastJSON.JSON.ToJSON(WSSend);
                //_logger.Information(typeof(MOTR_DirectoryWebsocket), "Got PING from: " + sSessionID);
            }
            else if (WSCommand.command == "CONVERTPROFILES")
            {
                WSSend.command = "CONVERTPROFILES";
                WSSend.aArray = m_Queue.convertprofiles.GetProfilesArray();
                WSSend.count = WSSend.aArray.Count;
                jsonText = fastJSON.JSON.ToJSON(WSSend);
                //_logger.Information(typeof(MOTR_DirectoryWebsocket), "Got PING from: " + sSessionID);
            }
            else if (WSCommand.command == "CONVERTDESCRIPTION")
            {
                string sProfileDescriptionID = WSCommand.parameter;
                if (sProfileDescriptionID.Length > 0 && sProfileDescriptionID.All(char.IsDigit))
                {
                    WSSend.command = "CONVERTDESCRIPTION";
                    WSSend.aArray = new ArrayList();
                    WSSend.aArray.Add(m_Queue.convertprofiles.GetDescription(Convert.ToInt32(sProfileDescriptionID)));
                    WSSend.count = WSSend.aArray.Count;
                    jsonText = fastJSON.JSON.ToJSON(WSSend);
                }
                //_logger.Information(typeof(MOTR_DirectoryWebsocket), "Got PING from: " + sSessionID);
            }
            else if (WSCommand.command == "MOBILEISREGISTERED")
            {
                int UserID = Convert.ToInt32(m_Sessions.GetTemporaryVariable(sSessionID, "UserID"));
                string MobileID = WSCommand.parameter;
                WSSend.command = "MOBILEISREGISTERED";
                WSSend.aArray = new ArrayList();
                WSSend.aArray.Add(m_Users.IsMobileRegistered(MobileID, UserID));
                WSSend.aArray.Add(m_Users.MobileDisplayname(MobileID, UserID));
                WSSend.aArray.Add(m_Users.GetMobileID(MobileID, UserID));
                WSSend.count = WSSend.aArray.Count;

                //Check if the mobile was registered, GetID and store it in session
                if((bool)WSSend.aArray[0] == true)
                    m_Sessions.SetTemporaryVariable(sSessionID, "MobileID", m_Users.GetMobileID(MobileID, UserID).ToString());

                jsonText = fastJSON.JSON.ToJSON(WSSend);
                _logger.Debug(typeof(MOTR_DirectoryWebsocket), "Mobile registered: " + WSSend.aArray[0].ToString());
            }
            else if (WSCommand.command == "MOBILEREGISTER")
            {
                string[] aParameters = WSCommand.parameter.Split(';');
                if (aParameters.Count() == 2)
                {
                    int UserID = Convert.ToInt32(m_Sessions.GetTemporaryVariable(sSessionID, "UserID"));
                    string DisplayName = aParameters[0];
                    string MobileID = aParameters[1];
                    WSSend.command = "MOBILEREGISTER";
                    WSSend.aArray = new ArrayList();
                    WSSend.aArray.Add(m_Users.MobileRegister(MobileID, UserID, DisplayName));
                    WSSend.aArray.Add(m_Users.MobileDisplayname(MobileID, UserID));
                    WSSend.aArray.Add(m_Users.GetMobileID(MobileID, UserID));
                    WSSend.count = WSSend.aArray.Count;
                    jsonText = fastJSON.JSON.ToJSON(WSSend);

                    //Store the ID of the mobile in session
                    m_Sessions.SetTemporaryVariable(sSessionID, "MobileID", m_Users.GetMobileID(MobileID, UserID).ToString());

                    _logger.Debug(typeof(MOTR_DirectoryWebsocket), "Was mobile " + DisplayName + " registered: " + WSSend.aArray[0].ToString());
                }
            }
            else if (WSCommand.command == "MOBILELIST")
            {
                int UserID = Convert.ToInt32(m_Sessions.GetTemporaryVariable(sSessionID, "UserID"));
                WSSend.command = "MOBILELIST";
                WSSend.aArray = m_Users.MobileList(UserID);
                WSSend.count = WSSend.aArray.Count;
                jsonText = fastJSON.JSON.ToJSON(WSSend);
            }
            else if (WSCommand.command == "MOBILEDOWNLOADCHECK")
            {
                int UserID = Convert.ToInt32(m_Sessions.GetTemporaryVariable(sSessionID, "UserID"));
                string MobileID = WSCommand.parameter;
                int nMobileID = m_Users.GetMobileID(MobileID, UserID);
                WSSend.command = "MOBILEDOWNLOADCHECK";
                WSSend.aArray = new ArrayList();

                //Check if a download exists for this mobile
                string sPath = m_Downloads.GetMobileDownload(nMobileID);
                if(sPath.Length>0)
                {
                    m_Downloads.AddDownload(sSessionID, sPath, false); //Adds the download, from download is "unknown", we use the DownloadID from mobile insted
                    WSSend.aArray.Add(m_Downloads.GetMobileDownload(nMobileID, true)); //Just add to downloads and return, this is not for down.load (!)
                    string sFileName = sPath.Substring(sPath.LastIndexOf('\\') + 1);
                    WSSend.aArray.Add(sFileName);
                    FileInfo fileInfo = new FileInfo(sPath);
                    WSSend.aArray.Add(fileInfo.Length.ToString());
                }

                WSSend.count = WSSend.aArray.Count;
                jsonText = fastJSON.JSON.ToJSON(WSSend);
            }
            else if (WSCommand.command == "MOBILEPUSH")
            {
                string[] aParameters = WSCommand.parameter.Split(';');
                if (aParameters.Count() == 2)
                {
                    int UserID = Convert.ToInt32(m_Sessions.GetTemporaryVariable(sSessionID, "UserID"));
                    string PushID = aParameters[0];
                    string MobileID = aParameters[1];
                    m_Users.SetPushID(MobileID, UserID, PushID); //Sets the PushID to the mobile registered
                    WSSend.command = "MOBILEPUSH";
                    WSSend.aArray = new ArrayList();
                    WSSend.count = WSSend.aArray.Count;
                    jsonText = fastJSON.JSON.ToJSON(WSSend);
                    _logger.Debug(typeof(MOTR_DirectoryWebsocket), "PushID: " + PushID);
                }
            }
            else if (WSCommand.command == "MOBILEDOWNLOAD") //Trigger download to mobile
            {
                string[] aParameters = WSCommand.parameter.Split(';');
                if (aParameters.Count() == 2)
                {
                    int UserID = Convert.ToInt32(m_Sessions.GetTemporaryVariable(sSessionID, "UserID"));
                    string MobileID = aParameters[0];
                    string FileID = aParameters[1];

                    //Set the mobiledownload header and array
                    WSSend.command = "MOBILEDOWNLOAD";
                    WSSend.aArray = new ArrayList();

                    if (FileID.Length > 0 && FileID.All(char.IsDigit))
                    {
                        string sDrive = m_Sessions.GetTemporaryVariable(sSessionID, "Drive");
                        string sDrivePosition = m_Sessions.GetTemporaryVariable(sSessionID, "DrivePosition");
                        string sFile = m_Sessions.GetPathByID(sSessionID, Convert.ToInt32(FileID));
                        string sDownloadPath = sDrivePosition.Substring(sDrive.Length) + sFile;

                        //If all are chars, then add it
                        if (MobileID.All(char.IsDigit) && sDownloadPath.Length > 0)
                        {
                            int DirID = m_Dirs.GetIDByPath(sDrive);

                            //Get the filesize before adding
                            FileInfo fileInfo = new FileInfo(sDrivePosition + sFile);

                            m_Downloads.AddMobileDownload(Convert.ToInt32(MobileID), DirID, sDownloadPath, fileInfo.Length);
                            WSSend.aArray.Add("True");

                            //Notify the mobile that we have downloads.
                            //(we need a sidecheck to see if the phone has already a session first) - Then do the push!
                            string PushID = m_Users.GetPushID(Convert.ToInt32(MobileID), UserID);

                            //Eventargs for triggering download either on 
                            MobileDownloadEventArgs e = new MobileDownloadEventArgs();
                            e.UserID = UserID;
                            e.MobileID = Convert.ToInt32(MobileID);
                            e.PushID = PushID;

                            //Checks if the user is already connect through websocket, else send push to mobile
                            m_Downloads.DownloadMobileTrigger(e);
                        }
                        else
                        {
                            WSSend.command = "ERROR";
                            WSSend.aArray.Add("Unknown mobile downloadpath, please select a drive first");
                        }
                    }

                    if (WSSend.aArray.Count == 0)
                        WSSend.aArray.Add("False");

                    WSSend.count = WSSend.aArray.Count;
                    jsonText = fastJSON.JSON.ToJSON(WSSend);
                }
            }
            else if (WSCommand.command == "MOBILEDOWNLOADSTATUS")
            {
                string[] aParameters = WSCommand.parameter.Split(';');
                if (aParameters.Count() == 3)
                {
                    //int UserID = Convert.ToInt32(m_Sessions.GetTemporaryVariable(sSessionID, "UserID"));
                    string DownloadID = aParameters[0];
                    string sStatus = aParameters[1];
                    string sLongText = aParameters[2];
                    WSSend.command = "MOBILEDOWNLOADSTATUS";
                    WSSend.aArray = new ArrayList();

                    DownloadStatus newStatus;
                    if (Enum.TryParse(sStatus.ToUpper(), out newStatus))
                        WSSend.aArray.Add(m_Downloads.SetMobileDownloadStatus(DownloadID, newStatus, sLongText));

                    WSSend.count = WSSend.aArray.Count;
                    jsonText = fastJSON.JSON.ToJSON(WSSend);
                    _logger.Debug(typeof(MOTR_DirectoryWebsocket), "Mobildownloadstatus set: " + newStatus.ToString() + " on " + DownloadID);
                }
            }
            else if (WSCommand.command == "MOBILEDOWNLOADLIST")
            {
                int UserID = Convert.ToInt32(m_Sessions.GetTemporaryVariable(sSessionID, "UserID"));
                string MobileID = WSCommand.parameter;
                int nMobileID = Convert.ToInt32(MobileID);
                WSSend.command = "MOBILEDOWNLOADLIST";
                WSSend.aArray = m_Downloads.GetMobileDownloadList(nMobileID);
                WSSend.count = WSSend.aArray.Count;
                jsonText = fastJSON.JSON.ToJSON(WSSend);
                _logger.Debug(typeof(MOTR_DirectoryWebsocket), "Sending mobiledownloadlist");
            }
            else if (WSCommand.command == "MOBILEDOWNLOADREMOVE")
            {
                string DownloadID = WSCommand.parameter;
                WSSend.command = "MOBILEDOWNLOADREMOVE";
                WSSend.aArray = new ArrayList();
                WSSend.aArray.Add(m_Downloads.RemoveMobileDownload(DownloadID));
                WSSend.count = WSSend.aArray.Count;
                jsonText = fastJSON.JSON.ToJSON(WSSend);
            }
            else if (WSCommand.command == "MOBILEDOWNLOADMOVE")
            {
                string[] aParameters = WSCommand.parameter.Split(';');
                if (aParameters.Count() == 2)
                {
                    string DownloadID = aParameters[0];
                    string sMove = aParameters[1];
                    WSSend.command = "MOBILEDOWNLOADMOVE";
                    WSSend.aArray = new ArrayList();

                    MOTR_Downloads.MobileDownloadMover newMovement;
                    if (Enum.TryParse(sMove.ToUpper(), out newMovement))
                        WSSend.aArray.Add(m_Downloads.MoveMobileDownload(DownloadID, newMovement));

                    WSSend.count = WSSend.aArray.Count;
                    jsonText = fastJSON.JSON.ToJSON(WSSend);
                }
            }
            //Send if the jsonText is set
            if (jsonText != null)
            {
                if(jsonText.Length > 0)
                {
                    base.Send(jsonText);
                    _logger.Information(typeof(MOTR_DirectoryWebsocket), "--> Command: " + WSSend.command);
                }
                else
                    _logger.Information(typeof(MOTR_DirectoryWebsocket), "jsonText is empty, not sending anything");
            }
            else
            {
                _logger.Warning(typeof(MOTR_DirectoryWebsocket), "Unknown command from websocket: " + WSCommand.command);
                WSSend.command = "UNKNOWN";
                WSSend.count = 2;
                WSSend.aArray = new ArrayList();
                WSSend.aArray.Add(WSCommand.command);
                WSSend.aArray.Add(WSCommand.parameter);
                jsonText = fastJSON.JSON.ToJSON(WSSend);
                base.Send(jsonText);
            }
        }

        private string CreateJSONFilelist(string sSessionID, string sPath, WebsocketSendClass WSSend)
        {
            if (sPath.Length <= 1)
                return "";

            string sDrive = m_Sessions.GetTemporaryVariable(sSessionID, "Drive");
            string sParentPath = sPath.Substring(0, sPath.Length - 1);
            if(sParentPath.LastIndexOf('\\') > 0)
                sParentPath = sParentPath.Substring(0, sParentPath.LastIndexOf('\\'));
            if (sParentPath.Substring(1) == ":") //Add \ after a drive for instance D: to D:\
                sParentPath += "\\";
            if ((sParentPath.Count() <= sDrive.Count()) &&
                sPath.Count() <= sDrive.Count()) //This odd implementation is include parent if it is a drive, for instance D:\
                sParentPath = "";

            //Generate filelist and 
            ArrayList aFileList = m_Sessions.GenerateFilelist(sSessionID, sPath, sParentPath);
            //WebsocketSendClass WSSend = new WebsocketSendClass();

            //Send the directory list
            WSSend.command = "FILELIST";
            WSSend.aArray = aFileList;
            WSSend.count = WSSend.aArray.Count;
            return fastJSON.JSON.ToJSON(WSSend);
        }

        //All commands is for callbacks and events
        public override void SendCommand(string sCommand, ArrayList aParameters)
        {
            //This object is used to send info
            WebsocketSendClass WSSend = new WebsocketSendClass();

            if (sCommand == "QUEUEREFRESHBYEVENT")
            {
                //Send the directory list
                WSSend.command = "QUEUEREFRESH";
                WSSend.aArray = m_Queue.GetQueue();
                WSSend.count = WSSend.aArray.Count;
                this.Send(fastJSON.JSON.ToJSON(WSSend));
            }
            if(sCommand == "QUEUEPROCENTAGE")
            {
                WSSend.command = "UPDATEQUEUEPROCENTAGE";
                WSSend.aArray = aParameters;
                WSSend.count = WSSend.aArray.Count;
                this.Send(fastJSON.JSON.ToJSON(WSSend));
            }
            if (sCommand == "MOBILEDOWNLOADCHECK")
            {
                WSSend.command = "MOBILEDOWNLOADCHECK";

                //Get the downloadid that we want to download
                string MobileID = m_Sessions.GetTemporaryVariable(sSessionID, "MobileID");

                //
                if (MobileID.Length > 0)
                {
                    int nMobileID = Convert.ToInt32(MobileID);

                    //Check if a download exists for this mobile
                    string sPath = m_Downloads.GetMobileDownload(nMobileID);
                    if (sPath.Length > 0)
                    {
                        m_Downloads.AddDownload(sSessionID, sPath, false); //Adds the download, from download is "unknown", we use the DownloadID from mobile insted
                        aParameters.Add(m_Downloads.GetMobileDownload(nMobileID, true)); //Just add to downloads and return, this is not for down.load (!)
                        string sFileName = sPath.Substring(sPath.LastIndexOf('\\') + 1);
                        aParameters.Add(sFileName);
                        FileInfo fileInfo = new FileInfo(sPath);
                        aParameters.Add(fileInfo.Length.ToString());
                    }
                }
                
                WSSend.aArray = aParameters;
                WSSend.count = WSSend.aArray.Count;
                this.Send(fastJSON.JSON.ToJSON(WSSend));
            }
        }

        //This function sends "MOBILEDOWNLOAD" to an websocket if it is connected
        public override bool SendMobileDownload(int nMobileID, int UserID)
        {
            //Not logged in, then just return
            if (!bIsLoggedIn)
                return false;

            //Get the UserID for the session
            int SessionUserID = Convert.ToInt32(m_Sessions.GetTemporaryVariable(sSessionID, "UserID"));

            //If the user is equal, then check if the mobileid was set
            if(SessionUserID == UserID)
            {
                string MobileID = m_Sessions.GetTemporaryVariable(sSessionID, "MobileID");
                if (MobileID.Length > 0)
                {
                    int SessionMobileID = Convert.ToInt32(MobileID);
                    if(SessionMobileID == nMobileID)
                    {
                        _logger.Debug(typeof(MOTR_DirectoryWebsocket), "Mobiledownload: Websocketsession connected to mobile: " + nMobileID.ToString());
                        return true;
                    }
                }
            }

            return false;
        }

        //Triggers an event to the MOTR-Webserver class, it will trigger to update all websockets connected
        public void QueueUpdate()
        {
            //Trigger an event back to the main class
            EventHandler handler = OnQueueUpdate;

            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }


    }
}

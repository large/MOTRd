#define net4

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using WebSockets.Common;
using WebSockets.Server.WebSocket;
using System.Collections;


namespace MOTRd
{
    class MOTR_AdminWebsocket : WebSocketService
    {
        private readonly IWebSocketLogger _logger;
        private readonly MOTR_Sessions m_Sessions;
        private readonly MOTR_Dirs m_Dirs;
        private readonly MOTR_Admin m_Admin;
        private readonly MOTR_Users m_Users;
        private string sSession;

        public event EventHandler OnRestartWebserver;

        public MOTR_AdminWebsocket(Stream stream, TcpClient tcpClient, string header, IWebSocketLogger logger, MOTR_Sessions _sessions, MOTR_Dirs _dirs, MOTR_Users _users, MOTR_Admin _admin)
            : base(stream, tcpClient, header, true, logger)
        {
            _logger = logger;
            m_Sessions = _sessions;
            m_Dirs = _dirs;
            m_Users = _users;
            m_Admin = _admin;
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

            //Every object is linked to this
            //WebSocketCommandClass WSCommand = (WebSocketCommandClass)fastJSON.JSON.ToObject(text);

            //Every object is linked to this
            WebSocketCommandClass WSCommand = new WebSocketCommandClass();

            //Try & catch on the parsing
            try
            {
                dynamic WSTemp = fastJSON.JSON.ToDynamic(text);
                WSCommand.command = WSTemp.command;
                WSCommand.parameter = WSTemp.parameter;
                //WSCommand.sessionid = WSTemp.sessionid;
            }
            catch (Exception ex)
            {
                _logger.Error(typeof(MOTR_AdminWebsocket), "Admin->Error parsing JSON: " + text + " - Returnstring: " + ex.ToString());
                return;
            }

            //Check if the user is logged in
//            if (!m_Sessions.SessionLoggedIn(WSCommand.sessionid))
//                return;

            //Just temp info
//            _logger.Information(typeof(MOTR_AdminWebsocket), "session: " + WSCommand.sessionid);
            _logger.Information(typeof(MOTR_AdminWebsocket), "command: " + WSCommand.command);

            //This object is used to send info
            WebsocketSendClass WSSend = new WebsocketSendClass();

            //Return the state of the user trying to log in
            if (WSCommand.command == "ISLOGGEDIN")
            {
                //Store the session for later
                sSession = WSCommand.parameter;

                WSSend.command = "LOGGEDIN";
                WSSend.aArray = new ArrayList();
                WSSend.aArray.Add(m_Admin.IsSessionLoggedIn(sSession));
                WSSend.count = WSSend.aArray.Count;
                jsonText = fastJSON.JSON.ToJSON(WSSend);
                base.Send(jsonText);
                return;
            }

            //Return a ISLOGGEDIN if seesoin it not set
            if (sSession.Length == 0)
            {
                WSSend.command = "ISLOGGEDIN";
                WSSend.aArray = new ArrayList();
                WSSend.aArray.Add("You need to sens ISLOGGEDIN first");
                WSSend.count = WSSend.aArray.Count;
                jsonText = fastJSON.JSON.ToJSON(WSSend);
                base.Send(jsonText);
                return;
            }
            _logger.Information(typeof(MOTR_AdminWebsocket), "Session trying to admin: " + sSession);

            //Check password
            if (WSCommand.command == "LOGIN")
            {
                WSSend.command = "LOGIN";
                WSSend.aArray = new ArrayList();
                WSSend.aArray.Add(m_Admin.CheckAdminPassword(WSCommand.parameter, sSession));
                WSSend.count = WSSend.aArray.Count;
                jsonText = fastJSON.JSON.ToJSON(WSSend);
                base.Send(jsonText);
                return;
            }

            //=========================================================================
            //Everything below here is not available if not logged in
            if (!m_Admin.IsSessionLoggedIn(sSession))
                return;

            //Return an array of users
            if (WSCommand.command == "ADDUSER")
            {
                //Add the user
                string[] aInfo = WSCommand.parameter.Split(',');
                string sUsername = aInfo[0];
                string sPassword = aInfo[1];
                bool bRet = m_Users.AddUserName(sUsername, sPassword);
                if (!bRet)
                    m_Admin.SetErrorString("Username exists or wrong parameters");

                //Send the userlist
                WSCommand.command = "USERLIST";
            }
            if(WSCommand.command == "CHANGEUSER")
            {
                //Add the user
                string[] aInfo = WSCommand.parameter.Split(',');
                string sID = aInfo[0];
                string sPassword = aInfo[1];
                int nID = Convert.ToInt32(sID);
                
                WSSend.command = "CHANGEUSER";
                WSSend.aArray = new ArrayList();
                WSSend.aArray.Add(m_Users.ChangePassword(nID, sPassword));
                WSSend.count = WSSend.aArray.Count;
                jsonText = fastJSON.JSON.ToJSON(WSSend);
            }
            //Return an array of users
            if (WSCommand.command == "DELETEUSER")
            {
                //Delete the user
                int nID = Convert.ToInt32(WSCommand.parameter);
                m_Users.RemoveUser(nID);

                //Send the userlist back
                WSCommand.command = "USERLIST";
            }

            //Sends the userlist, just names
            if (WSCommand.command == "USERLIST")
            {
                //Send new userlist
                WSSend.command = "USERLIST";
                WSSend.aArray = m_Users.GetUserArray();
                WSSend.count = WSSend.aArray.Count;
                jsonText = fastJSON.JSON.ToJSON(WSSend);
            }

            //Adds a directory to the pathslist
            if (WSCommand.command == "ADMINADDIRECTORY")
            {
                string sDisplayName, sPath, sUncUser, sUncPass;
                string[] sTmp = WSCommand.parameter.Split(',');

                //Only add if the parameters are correct
                if (sTmp.Length > 2)
                {
                    sDisplayName = sTmp[0];
                    sPath = sTmp[1];
                    sUncUser = sTmp[2];
                    sUncPass = sTmp[3];

                    //Now add the directory
                    m_Dirs.AddDirectory(sDisplayName, sPath, sUncUser, sUncPass);
                }

                //Send directories available
                WSCommand.command = "DIRLIST";
            }

            //Removes a directory and return new dirlist
            if (WSCommand.command == "ADMINREMOVEDIRECTORY")
            {
                //Delete the user
                int nID = Convert.ToInt32(WSCommand.parameter);
                m_Dirs.RemoveDirectory(nID);

                //Send the userlist back
                WSCommand.command = "DIRLIST";
            }

            //Sends paths list
            if (WSCommand.command == "DIRLIST")
            {
                //Send new userlist
                WSSend.command = "DIRLIST";
                WSSend.aArray = m_Dirs.GetDirsArray();
                WSSend.count = WSSend.aArray.Count;
                jsonText = fastJSON.JSON.ToJSON(WSSend);
                //Console.WriteLine("DIRLIST: " + jsonText);
            }

            //Sends drives and network list to the browser
            if(WSCommand.command == "ADMINDRIVES")
            {
                //Send new userlist
                WSSend.command = "ADMINDRIVES";
                WSSend.aArray = m_Admin.GetDrivesAndNetwork();
                WSSend.count = WSSend.aArray.Count;
                jsonText = fastJSON.JSON.ToJSON(WSSend);
                //Console.WriteLine("ADMINDRIVES: " + jsonText);
            }
            
            //Set the drive we are going to browse
            if (WSCommand.command == "ADMINSETDRIVE")
            {
                m_Admin.SetBasePath(WSCommand.parameter);
                WSCommand.command = "ADMINFILELIST";
            }

            //Set the drive we are going to browse
            if (WSCommand.command == "ADMINSETPATH")
            {
                m_Admin.SetCurrentPath(WSCommand.parameter);
                WSCommand.command = "ADMINFILELIST";
            }

            //Return the filelist based on the current path
            if (WSCommand.command == "ADMINFILELIST")
            {
                //Send new userlist
                WSSend.command = "ADMINFILELIST";
                WSSend.aArray = m_Admin.GetDirectoryList();
                WSSend.count = WSSend.aArray.Count;
                jsonText = fastJSON.JSON.ToJSON(WSSend);
                //Console.WriteLine("ADMINFILELIST: " + jsonText);
            }

            //Set the drive we are going to browse
            if (WSCommand.command == "ADMINCHECKFORTOOLUPDATE")
            {
                //Create return message
                WSSend.command = "ADMINCHECKFORTOOLUPDATE";
                WSSend.aArray = new ArrayList();

                //Check if the tool is updated
                string sTool = WSCommand.parameter;
                string sLocalVersion = MOTR_Settings.GetCurrentToolVersion(sTool);
                string sWebVersion = MOTR_Settings.GetWebsiteToolVersion(sTool);
                if (sLocalVersion != sWebVersion)
                {
                    WSSend.aArray.Add("Updating " + sTool + " to v" + sWebVersion);
                    bool bRet = MOTR_Settings.UpdateTool(sTool, sWebVersion);
                    if (bRet)
                        WSSend.aArray.Add(sTool + " updated ok");
                    else
                        WSSend.aArray.Add(sTool + " failed to update");
                }
                else
                    WSSend.aArray.Add("Newest version installed for " + sTool);

                //Set the number of items added
                WSSend.count = WSSend.aArray.Count;
                jsonText = fastJSON.JSON.ToJSON(WSSend);
            }

            //Restarts the server with given ports
            if (WSCommand.command == "ADMINRESTARTSERVER")
            {
                //Get ports available
                string[] ports = WSCommand.parameter.Split(';');
                int http = MOTR_Settings.GetNumber("http");
                int https = MOTR_Settings.GetNumber("https");
                bool bError = false;
                if (ports.Length > 1)
                {
                    //Temp store ports to check if changed
                    int httptemp = http;
                    int httpstemp = https;
                    http = Convert.ToInt32(ports[0]);
                    https = Convert.ToInt32(ports[1]);

                    //Check if the ports are available
                    if (httptemp != http)
                    {
                        if (WindowsService.IsPortOpen(http))
                        {
                            m_Admin.SetErrorString("http port is already taken, please choose another available port");
                            bError = true;
                        }
                    }
                    if (httpstemp != https)
                    {
                        if (WindowsService.IsPortOpen(https))
                        {
                            m_Admin.SetErrorString("https port is already taken, please choose another available port");
                            bError = true;
                        }
                    }
                }

                //No error, then we go fishing
                if (!bError)
                {
                    //Store the new ports
                    MOTR_Settings.SetNumber("http", http);
                    MOTR_Settings.SetNumber("https", https);

                    //Restart server
                    OnRestartWebserver(this, EventArgs.Empty);
                }

                //Send response back to server
                WSSend.command = "ADMINRESTARTSERVER";
                WSSend.aArray = new ArrayList();
                WSSend.aArray.Add(bError);
                WSSend.aArray.Add(http);
                WSSend.aArray.Add(https);
                WSSend.count = WSSend.aArray.Count;
                jsonText = fastJSON.JSON.ToJSON(WSSend);
            }


            //Send if the jsonText is set
            if (jsonText != null)
                base.Send(jsonText);
            else
            {
                _logger.Information(typeof(MOTR_AdminWebsocket), "Unknown admin command from websocket: " + WSCommand.command);
                WSSend.command = "UNKNOWN";
                WSSend.count = 2;
                WSSend.aArray = new ArrayList();
                WSSend.aArray.Add(WSCommand.command);
                WSSend.aArray.Add(WSCommand.parameter);
                jsonText = fastJSON.JSON.ToJSON(WSSend);
                base.Send(jsonText);
            }

            //Check if the admin has errors, send it if nessesary
            if(m_Admin.HasError)
            {
                WSSend.command = "ADMINERRORMESSAGE";
                WSSend.aArray = new ArrayList();
                WSSend.aArray.Add(m_Admin.GetErrorString());
                WSSend.count = WSSend.count;
                jsonText = fastJSON.JSON.ToJSON(WSSend);
                base.Send(jsonText);
            }
        }

    }
}

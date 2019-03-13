using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Net.Sockets;
using System.Diagnostics;
using WebSockets.Server;
using WebSockets.Server.Http;
using WebSockets.Common;
using WebSockets.Server.WebSocket;
using System.Net;

namespace MOTRd
{
    internal class MOTR_WebserverFactory : IServiceFactory, IDisposable
    {
        private readonly IWebSocketLogger _logger;
        private readonly string _webRoot;
        private readonly MOTR_Sessions m_Sessions;
        private readonly MOTR_Users m_Users;
        private readonly MOTR_Dirs m_Dirs;
        private readonly MOTR_Queue m_Queue;
        private readonly MOTR_Webserver m_WebServer; //Used for event
        private readonly MOTR_Admin m_Admin;
        private readonly MOTR_Downloads m_Downloads;

        public event EventHandler OnRestartWebserver;

        private enum LoginReturn
        {
            NOT_LOGGED_IN,
            LOGGED_IN,
            NO_CONTENT
        }
        private string GetWebRoot()
        {
            if (!string.IsNullOrWhiteSpace(_webRoot) && Directory.Exists(_webRoot))
            {
                return _webRoot;
            }

            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetName().CodeBase).Replace(@"file:\", string.Empty);
        }

        public MOTR_WebserverFactory(string webRoot, MOTR_Sessions sessions, MOTR_Users users, MOTR_Dirs dirs, MOTR_Queue queue, MOTR_Admin _admin, MOTR_Downloads _downloads, IWebSocketLogger logger, MOTR_Webserver _webserver)
        {
            _logger = logger;
            _webRoot = string.IsNullOrWhiteSpace(webRoot) ? GetWebRoot() : webRoot;
            m_Sessions = sessions;
            m_Users = users;
            m_Dirs = dirs;
            m_Queue = queue;
            m_WebServer = _webserver;
            m_Admin = _admin;
            m_Downloads = _downloads;

            if (!Directory.Exists(_webRoot))
            {
                _logger.Warning(this.GetType(), "Web root not found: {0}", _webRoot);
            }
            else
            {
                _logger.Debug(this.GetType(), "Web root: " + _webRoot);
            }
        }

        public void Dispose()
        {
        }



        //Returns value of a cookie based on the header
        public string GetCookie(string sHeader, string sCookie)
        {
            //Regex x = new Regex("Cookie: (.*)" + sCookie + "=(.*)");
            Regex x = new Regex(@"Cookie: (.*)(;|$)(.*)", RegexOptions.Multiline);
            MatchCollection m = x.Matches(sHeader);

            //If we found it, the cookies are on a string now
            if (m.Count > 0)
            {
                string sCookies = m[0].Value.Replace("Cookie: ", "");
                sCookies = sCookies.Replace("\r", "");
                string[] sAllCookies = sCookies.Split(';');

                for (int i = 0; i < sAllCookies.Count(); i++)
                {
                    string[] sCookieSplit = sAllCookies[i].Split('=');
                    string sInteralCookie = sCookieSplit[0];
                    if (sInteralCookie[0] == ' ')
                        sInteralCookie = sInteralCookie.Substring(1);
                    //Console.WriteLine("Cookie " + sCookie + " mathing " + sInteralCookie);
                    if (sInteralCookie == sCookie)
                    {
                        if (sCookie == "TempID")
                        {
                            Debug.WriteLine(sHeader);
                            Console.WriteLine("Temp check");
                        }

                        return sCookieSplit[1];
                    }
                }
            }
            else
            {
                if (sCookie == "TempID")
                {
                    Debug.WriteLine(sHeader);
                    Console.WriteLine("Temp check");
                }
            }

            return "";
        }


        //Returns the contentlength of a header
        public int GetContentLength(string sHeader)
        {
            Regex x = new Regex("Content-Length: (.*)");
            MatchCollection m = x.Matches(sHeader);

            //1 or more hits :)
            if (m.Count > 0)
            {
                int iLength = Convert.ToInt32(m[0].Value.Replace("Content-Length: ", ""));
                return iLength;
            }
            else
                return 0;
        }

/*
        private LoginReturn LoginValid(string sHeader)
        {
            //Hent ut info fra svaret
            int iLength = GetContentLength(sHeader);
            int nEndPos = sHeader.IndexOf("\r\n\r\n");
            if (nEndPos+4 > (sHeader.Length - iLength))
            {
                Console.WriteLine("No userdata ffs: " + sHeader);
                return LoginReturn.NO_CONTENT;
            }
            string sPostData = sHeader.Substring(sHeader.Length - iLength);
            _logger.Information(typeof(MOTR_WebserverFactory), "Header in POST: " + sHeader);
            _logger.Information(typeof(MOTR_WebserverFactory), "PostData: " + sPostData);
            
            //Data needs to be at least u=x&p=x
            if (sPostData.Length < 7)
                return LoginReturn.NOT_LOGGED_IN;

            //Split the chars
            char[] delimiterChars = { '=', '&'};
            string[] verbs= sPostData.Split(delimiterChars);

            //Fill username and password
            string sUsername="";
            string sPassword="";
            for(int i=0;i<verbs.Count();i++)
            {
                //Find username
                if (verbs[i].ToUpper() == "U")
                    if(verbs.Count() > i+1)
                        sUsername = verbs[i + 1];
                if (verbs[i].ToUpper() == "P")
                    if (verbs.Count() > i + 1)
                        sPassword = verbs[i + 1];
            }

            //Console.WriteLine("Username: " + sUsername);
            //Console.WriteLine("Password: " + sPassword);

            //If the username and password match, then return that everything is OK...
            if (m_Users.UsernameAndPasswordMatch(sUsername, sPassword))
            {
                sDisplayName = sUsername; //Store username as displayname for session handling
                return LoginReturn.LOGGED_IN;
            }
            else
                return LoginReturn.NOT_LOGGED_IN;
        }
*/

        public IService CreateInstance(ConnectionDetails connectionDetails)
            {
                switch (connectionDetails.ConnectionType)
                {
                    case ConnectionType.WebSocket:
                        // you can support different kinds of web socket connections using a different path
                        if (connectionDetails.Path == "/directory")
                        {
                            MOTR_DirectoryWebsocket pWS = new MOTR_DirectoryWebsocket(connectionDetails.Stream, connectionDetails.TcpClient, connectionDetails.Header, _logger, m_Sessions, m_Dirs, m_Queue, m_Users, m_Downloads);
                            pWS.OnQueueUpdate += m_WebServer.HandleEvent;
                            return pWS;
                        }
                        if(connectionDetails.Path == "/admin")
                        {
                            MOTR_AdminWebsocket pWS = new MOTR_AdminWebsocket(connectionDetails.Stream, connectionDetails.TcpClient, connectionDetails.Header, _logger, m_Sessions, m_Dirs, m_Users, m_Admin);
                            pWS.OnRestartWebserver += this.OnRestartWebserver;
                            return pWS; 
                        }
                    break;

                    case ConnectionType.Http:
                    // this path actually refers to the reletive location of some html file or image
                    string extension = connectionDetails.Path;
                    //_logger.Information(this.GetType(), "Header: {0}", connectionDetails.Header);
                    //_logger.Information(this.GetType(), "Path: {0}", connectionDetails.Path);
                    if (extension.Length > 4)
                    {
                        extension = extension.Substring(extension.Length - 4);
                        extension = extension.ToUpper();

                        string sIP = connectionDetails.TcpClient.Client.RemoteEndPoint.ToString();
                        //_logger.Information(this.GetType(), "IP adress: {0}", sIP);
                    }

                    //Special extension for our file
                    if (extension == "MOTR" || extension == "LOAD")
                    {
                        //_logger.Information(this.GetType(), "Handling MOTR or LOAD: " + extension);

                        //Sjekk at brukeren er logget inn (SessionID = gullgutten), TempID er for å sette gullgutten ;)
                        string sSessionID = GetCookie(connectionDetails.Header, "SessionID");
                        if (sSessionID.Length > 0)
                        {
                            if (!m_Sessions.SessionLoggedIn(sSessionID, "[WEBBASEDAUTH, NOT USER]")) 
                                return new HttpRedirectService(connectionDetails.Stream, "/", connectionDetails.Path, "[DELETE]", _logger);
                            else if(GetCookie(connectionDetails.Header, "TempID").Length > 0)
                                return new HttpRedirectService(connectionDetails.Stream, connectionDetails.Path, connectionDetails.Path, sSessionID, _logger);
                        }
                        else //Just forward when we don't have any session
                        {
                            string sTempID = GetCookie(connectionDetails.Header, "TempID");
                            if(sTempID.Length == 0)
                                return new HttpRedirectService(connectionDetails.Stream, "/", connectionDetails.Path, "", _logger);
                            else
                            {
                                //Get the session, if it is real, set the session to login
                                sSessionID = m_Sessions.GetSessionByTempID(sTempID);
                                if (sSessionID.Length == 0)
                                    return new HttpRedirectService(connectionDetails.Stream, "/", connectionDetails.Path, "", _logger);
                                else
                                    return new HttpRedirectService(connectionDetails.Stream, connectionDetails.Path, connectionDetails.Path, sSessionID, _logger);
                            }
                        }

                        //Sjekk om det er utlogging som skal foregå
                        bool bRemoveCookies = false;
                        if (connectionDetails.Path.ToUpper().Contains("LOGOFF.MOTR"))
                        {
                            //Slett sesjonen fra listen, la parseren håndtere resten
                            m_Sessions.RemoveSession(sSessionID);
                            bRemoveCookies = true;
                        }

                        //Her skal vi parse fila før vi sender avgårde html'n...
                        if (extension == "MOTR") //Ignore if we are going to remove cookies, normally logging out
                        {
                            MOTR_Parser m_Parser = new MOTR_Parser(_logger, sSessionID, m_Sessions);
                            return new HttpTextService(connectionDetails.Stream, m_Parser.ParseFile(_webRoot + connectionDetails.Path), _logger, bRemoveCookies);
                        }
                        else //Handling "DOWN.LOAD"
                        {
                            string[] aTemp = connectionDetails.Path.Split('/');
                            if (aTemp.Count() > 0)
                            {
                                string nID = aTemp[1];
                                string sPath = m_Sessions.GetTemporaryVariable(sSessionID, "DrivePosition");
                                string sFile = m_Sessions.GetPathByID(sSessionID, Convert.ToInt32(nID));

                                string sOneHour = DateTime.Now.AddHours(1).ToString();
                                string sDownloadID = m_Downloads.AddDownload(sSessionID, sPath + sFile, false, sOneHour);

                                return new HttpRedirectService(connectionDetails.Stream, "/MOTR-download/" + sDownloadID + "/" + sFile, "", "", _logger);
                            }
                            else
                                return new HttpTextService(connectionDetails.Stream, "Did not provide correct nID for file", _logger);
                        }
                    }

                    //Here we handle the tag "/motr-download/" to ensure we download the file binary
                    if (connectionDetails.Path.Contains("/MOTR-download/"))
                    {
                        //Check if we have a session (This blocks several clients, temp disabled)
                        //string sSessionID = GetCookie(connectionDetails.Header, "SessionID");
                        //if(sSessionID.Length == 0)
                        //    return new HttpTextService(connectionDetails.Stream, "Session is no longer valid, unable to download", _logger);

                        string[] aDownloads = connectionDetails.Path.Split('/');
                        string sDownloadID = "";
                        if (aDownloads.Count() > 2)
                            sDownloadID = aDownloads[2];
                        if (sDownloadID.Length > 0)
                        {
                            //Get the filename
                            //string sFileName = connectionDetails.Path.Substring(connectionDetails.Path.LastIndexOf('/') + 1);
                            string sFileName = m_Downloads.GetDownload(sDownloadID);

                            //No filename, no love...
                            if (sFileName.Length == 0)
                            {
                                _logger.Warning(typeof(MOTR_WebserverFactory), "Download is no longer valid...");
                                return new BadRequestService(connectionDetails.Stream, connectionDetails.Header, _logger);
                            }

                            sFileName = Uri.UnescapeDataString(sFileName);

                            

                            if (connectionDetails != null)
                                if (connectionDetails.Header != null)
                                    _logger.Debug(typeof(MOTR_WebserverFactory), "MOTR-download: " + WebUtility.HtmlDecode(connectionDetails.Header));

                            //This is where the file actually is 
                            //string sBasePath = m_Sessions.GetTemporaryVariable(sSessionID, "DrivePosition");
                            //return new HttpBinaryService(connectionDetails.Stream, sBasePath + sFileName, _logger);
                            return new HttpBinaryService(connectionDetails.Stream, sFileName, _webRoot, connectionDetails.Header, _logger);
                        }
                    } //END: /MOTR-download
                    else if (connectionDetails.Path.Contains("/kodi/")) //Kodi directory is special
                    {
                        extension = extension.ToUpper();
                        if(extension == ".ZIP")
                            return new HttpBinaryService(connectionDetails.Stream, _webRoot + connectionDetails.Path, _webRoot, connectionDetails.Header, _logger);
                    }
                    else if (connectionDetails.Path.Contains("/MovieInfo/")) //Kodi directory is special
                    {
                        int nSlashPos = connectionDetails.Path.LastIndexOf('/');
                        string sImageName = connectionDetails.Path.Substring(nSlashPos, connectionDetails.Path.Length - (nSlashPos));
                        sImageName = sImageName.Substring(1, sImageName.Length - 1);
                        sImageName = sImageName.Replace("..", "");
                        sImageName = sImageName.Replace("%", "");
                        string movieInfoPath = MOTR_Settings.GetGlobalApplicationPath("MovieImages");
                        return new HttpBinaryService(connectionDetails.Stream, movieInfoPath + sImageName, _webRoot, connectionDetails.Header, _logger);
                    }//Redirect to front page on initalsetup

                    if (connectionDetails.Path.Contains("/initalsetup/"))
                        return new HttpRedirectService(connectionDetails.Stream, "/", connectionDetails.Path, "", _logger);

                    //Everything else is served here (but limited to the mimelist...)
                    return new HttpService(connectionDetails.Stream, connectionDetails.Path, _webRoot, _logger);
                    

                    case ConnectionType.Head:
                    //Only handle MOTR-download on head requests
                    //_logger.Information(typeof(MOTR_WebserverFactory), "HEAD: " + connectionDetails.Header);
                    if (connectionDetails.Path.Contains("/MOTR-download/"))
                    {
                        string[] aDownloads = connectionDetails.Path.Split('/');
                        string sDownloadID = "";
                        if (aDownloads.Count() > 2)
                            sDownloadID = aDownloads[2];
                        if (sDownloadID.Length > 0)
                        {
                            string sFileName = m_Downloads.GetDownload(sDownloadID, true);
                            return new HttpBinaryService(connectionDetails.Stream, sFileName, _webRoot, connectionDetails.Header, _logger, true); //True is head only!
                        }
                    }
                    else if(connectionDetails.Path.Contains("/kodi/")) //Kodi directory is special
                        return new HttpBinaryService(connectionDetails.Stream, _webRoot + connectionDetails.Path, _webRoot, connectionDetails.Header, _logger, true); //True is head only!
                    else if (connectionDetails.Path.Contains("/MovieInfo/")) //Kodi directory is special
                    {
                        int nSlashPos = connectionDetails.Path.LastIndexOf('/');
                        string sImageName = connectionDetails.Path.Substring(nSlashPos, connectionDetails.Path.Length - (nSlashPos));
                        sImageName = sImageName.Substring(1, sImageName.Length - 1);
                        sImageName = sImageName.Replace("..", "");
                        sImageName = sImageName.Replace("%", "");
                        string movieInfoPath = MOTR_Settings.GetGlobalApplicationPath("MovieImages");
                        return new HttpBinaryService(connectionDetails.Stream, movieInfoPath + sImageName, _webRoot, connectionDetails.Header, _logger, true); //True is head only!
                    }//Redirect to front page on initalsetup

                    _logger.Warning(typeof(MOTR_WebserverFactory), "Head requested, not supported for all paths: " + connectionDetails.Header);
                    return new BadRequestService(connectionDetails.Stream, connectionDetails.Header, _logger);
                    //return new HttpTextService(connectionDetails.Stream, "HEAD is not supported", _logger);
                }

                return new BadRequestService(connectionDetails.Stream, connectionDetails.Header, _logger);
            }
        }
}

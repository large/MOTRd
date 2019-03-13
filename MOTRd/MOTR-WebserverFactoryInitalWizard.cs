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


namespace MOTRd
{
    class MOTR_WebserverFactoryInitalWizard : IServiceFactory
    {
        private readonly IWebSocketLogger _logger;
        private readonly string _webRoot;
        private readonly MOTR_Users m_Users;
        private readonly MOTR_Admin m_Admin;

        public event EventHandler OnInitalsetupComplete;




        private string GetWebRoot()
        {
            if (!string.IsNullOrWhiteSpace(_webRoot) && Directory.Exists(_webRoot))
            {
                return _webRoot;
            }

            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetName().CodeBase).Replace(@"file:\", string.Empty);
        }

        public MOTR_WebserverFactoryInitalWizard(string webRoot, MOTR_Users users, MOTR_Admin _admin, IWebSocketLogger logger)
        {
            _logger = logger;
            _webRoot = string.IsNullOrWhiteSpace(webRoot) ? GetWebRoot() : webRoot;
            m_Users = users;
            m_Admin = _admin;

            if (!Directory.Exists(_webRoot))
            {
                _logger.Warning(this.GetType(), "InitalWizard: Web root not found: {0}", _webRoot);
            }
            else
            {
                _logger.Information(this.GetType(), "InitalWizard: Web root: {0}", _webRoot);
            }
        }

        public IService CreateInstance(ConnectionDetails connectionDetails)
        {
            switch (connectionDetails.ConnectionType)
            {
                case ConnectionType.WebSocket:
                    break;

                case ConnectionType.Http:
                    //First time setup is only allowed by local clients
                    string sIP = connectionDetails.TcpClient.Client.RemoteEndPoint.ToString();
                    if(sIP.Contains("localhost") || sIP.Contains("127.0.0.1") )
                    {
                        //If the path does not contain /initalsetup/ or /jquery/ you'll be forced to see the index-file
                        string sPathToBeShown = @"/initalsetup/index.html";
                        if (connectionDetails.Path.Contains(@"initalsetup") || connectionDetails.Path.Contains(@"jquery"))
                            sPathToBeShown = connectionDetails.Path;
                        else
                            return new HttpRedirectService(connectionDetails.Stream, sPathToBeShown, "/", "", _logger);

                        //Everything else is served here (but limited to the mimelist...)
                        return new HttpService(connectionDetails.Stream, sPathToBeShown, _webRoot, _logger);
                    }
                    else
                        return new HttpTextService(connectionDetails.Stream, "MOTR admin setup is only available on the computer it is running on, 127.0.0.1 or localhost!", _logger);

                //Håndtering av innsending
                case ConnectionType.HttpPost:
                    //Her går man tilbake til login siden
                    if(!ParsePostAndCreateUser(connectionDetails.Header))
                        return new HttpRedirectService(connectionDetails.Stream, "/", "/", "", _logger);

                    //Trigger complete
                    InitalsetupIsComplete();
                    return new HttpRedirectService(connectionDetails.Stream, "/initalsetup/thanks.html", "/", "", _logger);
            }

            return new BadRequestService(connectionDetails.Stream, connectionDetails.Header, _logger);
        }

        //Returns the contentlength of a header
        private int GetContentLength(string sHeader)
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

        private bool ParsePostAndCreateUser(string sHeader)
        {
            int iLength = GetContentLength(sHeader);
            string sPostData = sHeader.Substring(sHeader.Length - iLength);
            _logger.Information(typeof(MOTR_WebserverFactoryInitalWizard), "PostData from init: " + sPostData);

            string[] sTextSplit = sPostData.Split('&');

            string sUsername = "";
            string sPassword = "";
            string sAdminPassword = "";

            //Loop through
            for (int i=0;i<sTextSplit.Count();i++)
            {
                string[] sItem = sTextSplit[i].Split('=');
                if (sItem.Count() > 1)
                {
                    if (sItem[0].ToLower() == "username")
                        sUsername = sItem[1];
                    if (sItem[0].ToLower() == "password")
                        sPassword = sItem[1];
                    if (sItem[0].ToLower() == "adminpassword")
                        sAdminPassword = sItem[1];
                }
            }

            //Check that we got what we came for
            if (sUsername.Length < 1 || sPassword.Length < 6 || sAdminPassword.Length < 8)
            {
                WindowsService.LogEventError(string.Format("One or more items is empty or to short: User: {0} - Pass: {1} - Admin: {2}", sUsername, sPassword, sAdminPassword));
                return false;
            }

            //Now create a user
            m_Users.AddUserName(sUsername, sPassword);

            //Now create an admin password
            m_Admin.CreateAdminPassword(sAdminPassword);
            if (m_Admin.GetErrorString().Length > 0)
                WindowsService.LogEventError(m_Admin.GetErrorString());
            return true;
        }

        //Triggers an event to the MOTR-Webserver class, it will trigger to update all websockets connected
        public void InitalsetupIsComplete()
        {
            //Trigger an event back to the main class
            OnInitalsetupComplete?.Invoke(this, EventArgs.Empty);
        }
    }
}

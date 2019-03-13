using System;
using System.IO;
using WebSockets;
using WebSockets.Common;
using System.Security.Cryptography.X509Certificates;
using System.Collections;
using System.Diagnostics;
using System.Timers;
using System.Collections.Generic;

namespace MOTRd
{
    public class MOTR_Webserver
    {
        private readonly IWebSocketLogger _logger;
        private readonly MOTR_Sessions _sessions;
        private readonly MOTR_Users _users;
        private readonly MOTR_Dirs _dirs;
        private readonly MOTR_Queue _queue;
        private readonly MOTR_Admin _admin;
        private readonly MOTR_Downloads _downloads;

        //Static variable of this instance, ensure to call restartserver and so on
        static MOTR_Webserver pMe = null;

        //This class stores all the web servers created
        private class MOTR_WebserverObject
        {
            public MOTR_WebserverFactory serviceFactory;
            public WebServer server;
            public MOTR_WebserverFactoryInitalWizard initalServiceFactory;
            public int LastPortNumber;
            public string LastPath;
            public bool LastSSL;
            public string LastCert;
            public string LastCertPassword;
        }

        //Array of objects to handle all webservers opened
        private List<MOTR_WebserverObject> aWebservers;

        //Stored when created classed
        public MOTR_Webserver(IWebSocketLogger logger, MOTR_Sessions sessions, MOTR_Users users, MOTR_Dirs dirs, MOTR_Queue queue, MOTR_Admin admin, MOTR_Downloads downloads)
        {
            _logger = logger;
            _sessions = sessions;
            _users = users;
            _dirs = dirs;
            _queue = queue;
            _admin = admin;
            _downloads = downloads;
            pMe = this; //Stored for the static calls
            aWebservers = new List<MOTR_WebserverObject>();
        }

        ~MOTR_Webserver()
        {
        }

        //If we are using SSL
        private X509Certificate2 GetCertificate(string certFile, string certPassword)
        {
            // it is clearly WRONG to store the certificate and password insecurely on disk like this but this is a demo
            // you would normally use the built in windows certificate store
            if (!File.Exists(certFile))
            {
                throw new FileNotFoundException("Certificate file not found: " + certFile);
            }

            var cert = new X509Certificate2(certFile, certPassword);
            _logger.Debug(typeof(MOTR_Webserver), "Successfully loaded certificate");
            return cert;
        }

        //Here we create the webserver and starts listening
        public bool CreateWebServer(int iPortNumber, string webRoot, bool bSSL = false, string sCertPath = "", string sCertPassWord = "")
        {
            //Store information for recalling after a initalwizard has been launched
            MOTR_WebserverObject m_WebServer = new MOTR_WebserverObject();
            m_WebServer.LastPortNumber = iPortNumber;
            m_WebServer.LastPath = webRoot;
            m_WebServer.LastSSL = bSSL;
            m_WebServer.LastCert = sCertPath;
            m_WebServer.LastCertPassword = sCertPassWord;

            try
            {
                //Get the path or set the default path of where the program was runned
                if (!Directory.Exists(webRoot))
                {
                    _logger.Error(typeof(WindowsService), "Webroot folder {0} not found. Directory needs to exist to start webserver", webRoot);
                    return false;
                }
                if(!File.Exists(sCertPath) && sCertPath.Length != 0)
                {
                    _logger.Error(typeof(WindowsService), "Certificate {0} not found, needed for https on port {1}, generate by entering motrd.exe -cert.", sCertPath, iPortNumber);
                    return false;
                }

                //Check if there are users available.
                //If not, create a inital wizard mode so the user are able to create their own username and password
                if (_users.Count() == 0)
                {
                    //Changing the root to only include the initalsetup directory

                    m_WebServer.initalServiceFactory = new MOTR_WebserverFactoryInitalWizard(webRoot, _users, _admin, _logger);
                    m_WebServer.initalServiceFactory.OnInitalsetupComplete += HandleEventInitalSetupComplete;
                    m_WebServer.server = new WebServer(m_WebServer.initalServiceFactory, _logger);
                    if (!bSSL)
                        m_WebServer.server.Listen(iPortNumber);
                    else
                    {
                        X509Certificate2 cert = GetCertificate(sCertPath, sCertPassWord);
                        m_WebServer.server.Listen(iPortNumber, cert);
                    }
                    aWebservers.Add(m_WebServer);
                    return true;
                }

                //Create the servicefactory for handling each connection
                m_WebServer.serviceFactory = new MOTR_WebserverFactory(webRoot, _sessions, _users, _dirs, _queue, _admin, _downloads, _logger, this);
                m_WebServer.serviceFactory.OnRestartWebserver += HandleRestartServer;
                m_WebServer.server = new WebServer(m_WebServer.serviceFactory, _logger);
                if (!bSSL)
                    m_WebServer.server.Listen(iPortNumber);
                else
                {
                    X509Certificate2 cert = GetCertificate(sCertPath, sCertPassWord);
                    m_WebServer.server.Listen(iPortNumber, cert);
                }
                aWebservers.Add(m_WebServer);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(typeof(WindowsService), ex);
                return false;
            }
        }

        private void InitalServiceFactory_OnInitalsetupComplete(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        public void HandleEvent(object sender, EventArgs args)
        {
            //_logger.Information(typeof(MOTR_Webserver), "QUEUE UPDATE FFS!");
             for(int i=0;i<aWebservers.Count;i++)
                 aWebservers[i].server.SendAllWebsockets("QUEUEREFRESHBYEVENT", new ArrayList());
        }

        public void HandleEventUpdateProcentage(object sender, QueueIDEventArg args)
        {
            _logger.Debug(typeof(MOTR_Webserver), "Procentage: " + args.iProcentage + " - Queue: " + args.nQueueID);
            ArrayList aProc = new ArrayList();
            aProc.Add(args.nQueueID);
            aProc.Add(args.iProcentage);
            aProc.Add(args.sETA);
             for (int i = 0; i < aWebservers.Count; i++)
                aWebservers[i].server.SendAllWebsockets("QUEUEPROCENTAGE", aProc);
        }

        public void HandleEventSendMobileDownload(object sender, MobileDownloadEventArgs args)
        {
            _logger.Debug(typeof(MOTR_Webserver), "Mobile download!");
            for (int i = 0; i < aWebservers.Count; i++)
            {
                //If we found a connection with websocket, we will return now
                if (aWebservers[i].server.MobileDownload(args.MobileID, args.UserID))
                    return;
            }

            //If reached here we don't have a active connection, trigger a push message
            if (args.PushID.Length == 0)
            {
                _logger.Warning(typeof(MOTR_Webserver), "Mobile " + args.MobileID.ToString() + " does not have pushid registered, unable to download");
                return;
            }
            MOTR_PushMobile m_MobilePush = new MOTR_PushMobile();
            m_MobilePush.SendPush(args.PushID);
        }

        //What happens when the inital setup is complete
        private static Timer aTimer;
        public void HandleEventInitalSetupComplete(object sender, EventArgs args)
        {
            Console.WriteLine("Inital setup complete...");
            aTimer = new Timer();
            aTimer.Interval = 1000;
            aTimer.Elapsed += OnRestartServer;
            aTimer.AutoReset = false;
            aTimer.Enabled = true;
        }

        public void HandleRestartServer(object sender, EventArgs args)
        {
            Console.WriteLine("Restarting server by command...");
            aTimer = new Timer();
            aTimer.Interval = 1000;
            aTimer.Elapsed += OnRestartServer;
            aTimer.AutoReset = false;
            aTimer.Enabled = true;
        }

        //Here we trigger a server restart after 1 second (timer above)
        private static void OnRestartServer(Object source, System.Timers.ElapsedEventArgs e)
        {
            aTimer.Dispose();
            WindowsService.LogEventInformation("OnRestartServer called");
            pMe.RestartServer();
        }

        //Here we actually restart the server
        public void RestartServer()
        {
            MOTR_WebserverObject m_WebObject;

            for (int i = aWebservers.Count-1; i > -1; i--)
            {
                //Dispose the last server created
                if (aWebservers[i].server != null)
                {
                    aWebservers[i].server.Dispose();
                    aWebservers[i].server = null;
                }
                if (aWebservers[i].serviceFactory != null)
                {
                    aWebservers[i].serviceFactory.Dispose();
                    aWebservers[i].serviceFactory = null;
                }
                if (aWebservers[i].initalServiceFactory != null)
                {
                    aWebservers[i].initalServiceFactory = null;
                }

                //Get the object and remove it from the list
                m_WebObject = aWebservers[i];
                aWebservers.RemoveAt(i);

                //Retrive the port, if option not set retrive it from the last object
                int port = MOTR_Settings.GetNumber("http");
                if (port == 0)
                    port = m_WebObject.LastPortNumber;
                if (m_WebObject.LastSSL)
                    port = MOTR_Settings.GetNumber("https");
                if (port == 0)
                    port = m_WebObject.LastPortNumber;

                //Now create the new webserver with current options
                CreateWebServer(port, m_WebObject.LastPath, m_WebObject.LastSSL, m_WebObject.LastCert, m_WebObject.LastCertPassword);
                m_WebObject = null;
            }
        }
    }
}
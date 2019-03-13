using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading;
using WebSockets.Common;
using System.Timers;

namespace MOTRd
{
    class WindowsService : ServiceBase
    {
        protected Thread m_thread;
        protected ManualResetEvent m_shutdownEvent;
        static protected WindowsService m_TheService;
        static MutexSecurity oMutexSecurity;
        protected System.Timers.Timer m_OnStopTimer;

        //Port opened when 
        static int iHTTP { get; set; }
        static int iHTTPS { get; set; }

        /// <summary>
        /// Public Constructor for WindowsService.
        /// - Put all of your Initialization code here.
        /// </summary>
        public WindowsService()
        {
            this.ServiceName = Properties.Settings.Default.Servicename;
            this.EventLog.Source = Properties.Settings.Default.Servicedisplayname;
            this.EventLog.Log = "Application";
            
            // These Flags set whether or not to handle that specific
            //  type of event. Set to true if you need it, false otherwise.
            this.CanHandlePowerEvent = false;
            this.CanHandleSessionChangeEvent = false;
            this.CanPauseAndContinue = false;
            this.CanShutdown = true;
            this.CanStop = true;

            //Create a timer for handling the "OnStop" after return in base service
            m_OnStopTimer = new System.Timers.Timer();
            m_OnStopTimer.Interval = 2000; //2 seconds
            m_OnStopTimer.Elapsed += new System.Timers.ElapsedEventHandler(this.OnTimer);

            try
            {
                if (!EventLog.SourceExists(Properties.Settings.Default.Servicedisplayname))
                    EventLog.CreateEventSource(Properties.Settings.Default.Servicedisplayname, "Application");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        //Sends quit signal to the created thread directly
        static void InitalSetupComplete()
        {

        }


        /// <summary>
        /// The Main Thread: This is where your Service is Run.
        /// </summary>
        static void Main(string[] args)
        {
            //Check if the global application path exists, if not create it
            string sGlobalPath = MOTR_Settings.GetGlobalApplicationPath();
            if(!Directory.Exists(sGlobalPath))
                Directory.CreateDirectory(sGlobalPath);

            //Static service variable to handle the service
            m_TheService = new WindowsService();

            //Default values before startup
            iHTTP = MOTR_Settings.GetNumber("http");
            if (iHTTP == 0)
            {
                iHTTP = 80;
                MOTR_Settings.SetNumber("http", iHTTP);
            }
            iHTTPS = MOTR_Settings.GetNumber("https");
            //If iHTTPS = 0 then the https will not open

            //This is function is before Mutex, so it will be runned each time!
            //If one of the parameters is to generate a cert, then we are creating in the same path
            for (int i=0;i<args.Length;i++)
            {
                string sArg = args[i].ToUpper();
                if (sArg == "-CERT")
                {
                    CertGenerator m_Generator = new CertGenerator();
                    if (m_Generator.GenerateAndSave("MOTRd"))
                        LogEventInformation("MOTR certificate generated success");
                    else
                        LogEventError("MOTR certification generation error");
                    return;
                }

                //Wait 5 seconds
                if (sArg == "-WAIT")
                {
                    Thread.Sleep(3000);
                    return;
                }

                //Port override with parameters
                if(sArg.Contains("HTTPS"))
                {
                    string[] aString = sArg.Split('=');
                    if (aString.Length > 1)
                        iHTTPS = Convert.ToInt32(aString[1]);
                }
                else if (sArg.Contains("HTTP"))
                {
                    string[] aString = sArg.Split('=');
                    if (aString.Length > 1)
                        iHTTP = Convert.ToInt32(aString[1]);
                }

                //Check for tool update
                if(sArg == "-TOOLUPDATE")
                {
                    Console.WriteLine("Update tools...");
                    ArrayList aTools = new ArrayList();
                    aTools.Add("handbreak");
                    aTools.Add("unrar");

                    //Update all the tools used
                    for(int o=0;o<aTools.Count;o++)
                    {
                        string sLocalVersion = MOTR_Settings.GetCurrentToolVersion(aTools[o].ToString());
                        string sWebVersion = MOTR_Settings.GetWebsiteToolVersion(aTools[o].ToString());

                        //Updates
                        if (sLocalVersion != sWebVersion)
                        {
                            Console.Write("Updating " + aTools[o].ToString() + " to v" + sWebVersion + "... ");
                            bool bRet = MOTR_Settings.UpdateTool(aTools[o].ToString(), sWebVersion);
                            if (bRet)
                                Console.WriteLine("success");
                            else
                                Console.WriteLine("failed");
                        }
                        else
                            Console.WriteLine(aTools[o].ToString() + " already in latest version");
                    }
                    return;
                }
            }


            //======================================================
            //Create the global mutex and set its security
            bool bFirstInstance = false;
            Mutex mutex = null;

            try
            {
                //Create a mutex with security globally
                oMutexSecurity = new MutexSecurity();
                oMutexSecurity.AddAccessRule(new MutexAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), MutexRights.FullControl, AccessControlType.Allow));

                mutex = new Mutex(true, "Global\\MOTRD Mutex for single instances", out bFirstInstance);
                mutex.SetAccessControl(oMutexSecurity);
            }
            catch(Exception ex)
            {
                LogEventError("Only one instance of MOTRd is allowed, please check if service is running or taskmanager for motrd.exe. Only one instance is allowed!");
                Console.WriteLine("Error: " + ex.Message.ToString());
                m_TheService.Stop();
                return;
            }

            //Check if we are going to run as service or not :)
            bool bRunAsService = true;
            if (Environment.UserInteractive)
                bRunAsService = false;

            if (bRunAsService)
            {
                //Check if there are other instances
                if (mutex.WaitOne(TimeSpan.Zero, true))
                    ServiceBase.Run(m_TheService);
                else
                {
                    LogEventError("Only one instance of MOTRd is allowed, please stop service or check taskmanager for motrd.exe");
                    m_TheService.Stop();
                    return;
                }
            }
            else
            {
                //Check if there are other instances
                if (mutex.WaitOne(TimeSpan.Zero, true))
                {
                    Console.ForegroundColor = ConsoleColor.DarkBlue;
                    Console.WriteLine("Starting in console...");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Press Q to quit");
                    Console.Write("Args: ");
                    for (int i = 0; i<args.Length ;i++)
                        Console.Write(args[i]+", ");
                    Console.WriteLine("");
                    Console.ResetColor();
                    MOTR_Settings.ShowAllSettings(); //test
                    //Lager en "fake" service og starter den lik en normal service vil kjøre
                    m_TheService.StartServiceAsConsole(args);
                    while (true)
                    {
                        char cKey = Console.ReadKey().KeyChar;
                        if (cKey == 'Q' || cKey == 'q')
                            break;
                    }
                    m_TheService.StopServiceAsConsole();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("ERROR: MOTRd is already running.");
                    Console.ResetColor();
                    Console.WriteLine("Could be running as service, also check taskmanager for motrd.exe");
                    Console.WriteLine("Press any key to quit");
                    Console.ReadKey();
                }
            }
        }

        /// <summary>
        /// Dispose of objects that need it here.
        /// </summary>
        /// <param name="disposing">Whether or not disposing is going on.</param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        /// <summary>
        /// OnStart: Put startup code here
        ///  - Start threads, get inital data, etc.
        /// </summary>
        /// <param name="args"></param>
        protected override void OnStart(string[] args)
        {
            // create our threadstart object to wrap our delegate method
            ThreadStart ts = new ThreadStart(this.ServiceMain);

            // create the manual reset event and
            // set it to an initial state of unsignaled
            m_shutdownEvent = new ManualResetEvent(false);

            // create the worker thread
            m_thread = new Thread(ts);

            // go ahead and start the worker thread
            m_thread.Start();

            //Event logging
            LogEventInformation("MOTRd service started");

            base.OnStart(args);
        }

        public void OnTimer(object sender, System.Timers.ElapsedEventArgs args)
        {
            // TODO: Insert monitoring activities here.  
            m_OnStopTimer.Stop();
            //this.OnStop();
            this.Stop();
        }

        /// <SUMMARY>
        /// 
        /// </SUMMARY>
        protected void ServiceMain()
        {
            //First of all, check the port is available
            if (IsPortOpen(iHTTP) == true)
            {
                LogEventError("Port for http " + iHTTP + " is already open, MOTR cannot run on this port");
                m_OnStopTimer.Start();
                return;
            }
            if (IsPortOpen(iHTTPS) == true)
            {
                LogEventError("Port for https " + iHTTPS + " is already open, MOTR cannot run on this port");
                m_OnStopTimer.Start();
                return;
            }

            //Here we are ready to go
            bool bSignaledThreadToStop = false;

            IWebSocketLogger _logger = new WebSocketLogger();
            //_logger.Information(typeof(WindowsService), "-----------------------------------------------------");
            //        private static MOTR_Webserver m_WebServerSSL;
            MOTR_Users m_Users = new MOTR_Users();
            MOTR_Sessions m_Sessions = new MOTR_Sessions(m_Users);
            MOTR_Dirs m_Dirs = new MOTR_Dirs();
            MOTR_Queue m_Queue = new MOTR_Queue();
            MOTR_Admin m_Admin = new MOTR_Admin();
            MOTR_Downloads m_Downloads = new MOTR_Downloads(m_Dirs);
            if(m_Admin.HasError)
            {
                _logger.Error(typeof(MOTR_Admin), m_Admin.GetErrorString());
            }
            MOTR_Webserver m_WebServer = new MOTR_Webserver(_logger, m_Sessions, m_Users, m_Dirs, m_Queue, m_Admin, m_Downloads);
            m_Queue.OnQueueProcentage += m_WebServer.HandleEventUpdateProcentage;
            m_Queue.OnQueueUpdate += m_WebServer.HandleEvent;
            m_Downloads.OnMobileDownload += m_WebServer.HandleEventSendMobileDownload;

            //Create the webserver
            //Note: HTTPS is only started when there are users available
#if DEBUG
            m_WebServer.CreateWebServer(iHTTP, @"..\..\WebFiles");
            if (iHTTPS > 0 && iHTTPS <= 0x0000FFFF)
                m_WebServer.CreateWebServer(iHTTPS, @"..\..\WebFiles", true, @".\motrd.pfx", "");
#else
            //Get directory where the motrd.exe file is located...
            string webDirectory = AppDomain.CurrentDomain.BaseDirectory;
            m_WebServer.CreateWebServer(iHTTP, webDirectory + @"WebFiles");
            if(iHTTPS > 0 && iHTTPS <= 0x0000FFFF)
                m_WebServer.CreateWebServer(iHTTPS, webDirectory + @"WebFiles", true, webDirectory + @"motrd.pfx", "");
#endif
            LogEventInformation("MOTRd everything created, program started...");
            _logger.Debug(typeof(WindowsService), "MOTRd everything created, program started git version...");

            //Wait forever for the shutdown event
            bSignaledThreadToStop = m_shutdownEvent.WaitOne(-1, true);

            _logger.Information(typeof(WindowsService), "--- ENDING -----------------------------------------------------");

            //Clear the queue items and let it run out of scope...
            m_Queue.ClearProcessListAndClean();
            _logger.Information(this.GetType(), "Finished with ClearProcessListAndClean()");
        }

        /// <summary>
        /// OnStop: Put your stop code here
        /// - Stop threads, set final data, etc.
        /// </summary>
        protected override void OnStop()
        {
            // signal the event to shutdown
            m_shutdownEvent.Set();

            // wait for the thread to stop giving it 10 seconds
            m_thread.Join(10000);

            //Event logging
            LogEventInformation("MOTRd stopped");

            base.OnStop();
        }

        /// <summary>
        /// OnPause: Put your pause code here
        /// - Pause working threads, etc.
        /// </summary>
        protected override void OnPause()
        {
            LogEventInformation("MOTRd paused");
            base.OnPause();
        }

        /// <summary>
        /// OnContinue: Put your continue code here
        /// - Un-pause working threads, etc.
        /// </summary>
        protected override void OnContinue()
        {
            LogEventInformation("MOTRd continued");
            base.OnContinue();
        }

        /// <summary>
        /// OnShutdown(): Called when the System is shutting down
        /// - Put code here when you need special handling
        ///   of code that deals with a system shutdown, such
        ///   as saving special data before shutdown.
        /// </summary>
        protected override void OnShutdown()
        {
            LogEventInformation("MOTRd shutdown");
            base.OnShutdown();
        }

        /// <summary>
        /// OnCustomCommand(): If you need to send a command to your
        ///   service without the need for Remoting or Sockets, use
        ///   this method to do custom methods.
        /// </summary>
        /// <param name="command">Arbitrary Integer between 128 & 256</param>
        protected override void OnCustomCommand(int command)
        {
            //  A custom command can be sent to a service by using this method:
            //#  int command = 128; //Some Arbitrary number between 128 & 256
            //#  ServiceController sc = new ServiceController("NameOfService");
            //#  sc.ExecuteCommand(command);
            LogEventInformation("MOTRd custom command: " + command.ToString());
            base.OnCustomCommand(command);
        }

        /// <summary>
        /// OnPowerEvent(): Useful for detecting power status changes,
        ///   such as going into Suspend mode or Low Battery for laptops.
        /// </summary>
        /// <param name="powerStatus">The Power Broadcase Status (BatteryLow, Suspend, etc.)</param>
        protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
        {
            LogEventInformation("MOTRd powerevent " + powerStatus.ToString());
            return base.OnPowerEvent(powerStatus);
        }

        /// <summary>
        /// OnSessionChange(): To handle a change event from a Terminal Server session.
        ///   Useful if you need to determine when a user logs in remotely or logs off,
        ///   or when someone logs into the console.
        /// </summary>
        /// <param name="changeDescription"></param>
        protected override void OnSessionChange(SessionChangeDescription changeDescription)
        {
            LogEventInformation("MOTRd on session change: " + changeDescription.ToString());
            base.OnSessionChange(changeDescription);
        }

        public void StartServiceAsConsole(string[] args)
        {
            LogEventInformation("MOTRd starting as console");
            OnStart(args);
        }

        public void StopServiceAsConsole()
        {
            LogEventInformation("MOTRd stopping as console");
            OnStop();
        }


        ///========================================
        //Handle eventlogging
        static public void LogEventInformation(string sInformation)
        {
            if (Environment.UserInteractive)
                Console.WriteLine("Information: " + sInformation);
            else
                m_TheService.EventLog.WriteEntry(sInformation, EventLogEntryType.Information);
        }
        static public void LogEventError(string sError)
        {
            if (Environment.UserInteractive)
                Console.WriteLine("Error: " + sError);
            else
                m_TheService.EventLog.WriteEntry(sError, EventLogEntryType.Error);
        }

        //======================================
        // Port checker
        public static bool IsPortOpen(int nPort)
        {
            //Validate valid port range
            if (nPort < 0 || nPort > 65535)
                return true;

            // Evaluate current system tcp connections. This is the same information provided
            // by the netstat command line application, just in .Net strongly-typed object
            // form.  We will look through the list, and if our port we would like to use
            // in our TcpClient is occupied, we will set isAvailable to false.
            IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpListeners();

            foreach (IPEndPoint tcpi in tcpConnInfoArray)
            {
                if (tcpi.Port == nPort)
                    return true;
            }
            return false;
        }
    }
}

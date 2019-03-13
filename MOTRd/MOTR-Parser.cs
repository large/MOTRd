using System;
using WebSockets.Common;
using System.IO;

namespace MOTRd
{
    class MOTR_Parser
    {
        private readonly IWebSocketLogger _logger;
        private readonly string sSessionID;
        private readonly MOTR_Sessions m_Sessions;

        public MOTR_Parser(IWebSocketLogger iLogger, string session, MOTR_Sessions sessionshandler)
        {
            _logger = iLogger;
            sSessionID = session;
            m_Sessions = sessionshandler;
        }

        //Parseren må vite hvilken session som gjelder, slik at man får lov til å utføre handling

        public string ParseFile(string sFile)
        {
            FileInfo fi = new FileInfo(sFile);

            if (fi.Exists)
            {
                string ext = fi.Extension.ToLower();

                string[] sFileText = File.ReadAllLines(fi.FullName);
                //RespondSuccess(contentType, bytes.Length);

                //Set default values if not exists
                int iHTTP = MOTR_Settings.GetNumber("http");
                int iHTTPS = MOTR_Settings.GetNumber("https");
                if (iHTTP == 0)
                {
                    iHTTP = 80;
                    MOTR_Settings.SetNumber("http", iHTTP);
                }

                string sReturn = "";
                //int i = 1;
                foreach (string s in sFileText)
                {
                    if (s.Contains("</@DISPLAYNAME@>"))
                        sReturn += s.Replace("</@DISPLAYNAME@>", m_Sessions.GetDisplayName(sSessionID)) + Environment.NewLine;
                    else if (s.Contains("</@HANDBREAKVERSION@>"))
                        sReturn += s.Replace("</@HANDBREAKVERSION@>", MOTR_Settings.GetCurrentToolVersion("handbreak")) + Environment.NewLine;
                    else if (s.Contains("</@UNRARVERSION@>"))
                        sReturn += s.Replace("</@UNRARVERSION@>", MOTR_Settings.GetCurrentToolVersion("unrar")) + Environment.NewLine;
                    else if (s.Contains("</@HTTPPORT@>"))
                        sReturn += s.Replace("</@HTTPPORT@>", iHTTP.ToString()) + Environment.NewLine;
                    else if (s.Contains("</@HTTPSPORT@>"))
                        sReturn += s.Replace("</@HTTPSPORT@>", iHTTPS.ToString()) + Environment.NewLine;
                    else
                        sReturn += s + Environment.NewLine;
                    //sReturn += i.ToString() + ": " + s + Environment.NewLine;
                    //i += 1;
                }

                return sReturn;
            }

            return "No file exists with that name: " + DateTime.Now.ToString();
        }
    }
}

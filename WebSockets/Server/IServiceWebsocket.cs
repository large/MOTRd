using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebSockets.Server;

namespace WebSockets
{
    interface IServiceWebsocket : IService, IDisposable
    {
        /// <summary>
        /// Sends data back to the client. This is built using the IConnectionFactory
        /// </summary>
        void SendCommand(string sCommand, ArrayList aParameters);
        bool SendMobileDownload(int nMobileID, int UserID);
    }
}

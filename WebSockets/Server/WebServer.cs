using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Threading;
using System.Collections;
using System.Diagnostics;
using WebSockets.Exceptions;
using WebSockets.Server;
using WebSockets.Server.Http;
using WebSockets.Common;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace WebSockets
{
    public class WebServer : IDisposable
    {
        // maintain a list of open connections so that we can notify the client if the server shuts down
        private readonly List<IDisposable> _openConnections;
        private readonly IServiceFactory _serviceFactory;
        private readonly IWebSocketLogger _logger;
        private X509Certificate2 _sslCertificate;
        private TcpListener _listener;
        private bool _isDisposed = false;


        public WebServer(IServiceFactory serviceFactory, IWebSocketLogger logger)
        {
            _serviceFactory = serviceFactory;
            _logger = logger;
            _openConnections = new List<IDisposable>();
        }

        public void Listen(int port, X509Certificate2 sslCertificate)
        {
            try
            {
                _sslCertificate = sslCertificate;
                IPAddress localAddress = IPAddress.Any;
                _listener = new TcpListener(localAddress, port);
                _listener.Start();
                if(sslCertificate==null)
                    _logger.Debug(this.GetType(), string.Format("Server started listening on port {0} for http", port));
                else
                    _logger.Debug(this.GetType(), string.Format("Secure server started listening on port {0} using cert for https", port));
                StartAccept("void Webserver:Listen with cert param");
            }
            catch (SocketException ex)
            {
                string message = string.Format("Error listening on port {0}. Make sure IIS or another application is not running and blocking your portselection.", port);
                throw new ServerListenerSocketException(message, ex);
            }
        }

        /// <summary>
        /// Listens on the port specified
        /// </summary>
        public void Listen(int port)
        {
            Listen(port, null);
        }

        /// <summary>
        /// Gets the first available port and listens on it. Returns the port
        /// </summary>
        public int Listen()
        {
            IPAddress localAddress = IPAddress.Any;
            _listener = new TcpListener(localAddress, 0);
            _listener.Start();
            StartAccept("int Webserver:Listen");
            int port = ((IPEndPoint) _listener.LocalEndpoint).Port;
            _logger.Information(this.GetType(), string.Format("Server started listening on port {0}", port));
            return port;
        }

        private void StartAccept(string CalledFrom)
        {
            // this is a non-blocking operation. It will consume a worker thread from the threadpool
            //_logger.Information(typeof(WebServer), "Called StartAccept() in {0}", CalledFrom);
            AsyncCallback asyncCallback = new AsyncCallback(HandleAsyncConnection);
            IAsyncResult iRes = _listener.BeginAcceptTcpClient(asyncCallback, null);
        }

        private ConnectionDetails GetConnectionDetails(Stream stream, TcpClient tcpClient)
        {
            // read the header and check that it is a GET request
            string header = HttpHelper.ReadHttpHeader(stream);

            //Added by Lars just to handle unknowns...
            if (header.Length == 0)
            {
                return new ConnectionDetails(stream, tcpClient, "", ConnectionType.Unknown, header);
            }

            //Dette er en bare en test mot safari
/*            if (((NetworkStream)stream).DataAvailable)
            {
                StringBuilder myCompleteMessage = new StringBuilder();
                byte[] buffer = new byte[2048];
                int count = stream.Read(buffer,0,2048);
                myCompleteMessage.AppendFormat("{0}", Encoding.ASCII.GetString(buffer, 0, count));
                Debug.WriteLine("There are more sucking available...");
            }*/

            Regex getRegex = new Regex(@"^GET(.*)HTTP\/1\.1", RegexOptions.IgnoreCase);

            Match getRegexMatch = getRegex.Match(header);
            if (getRegexMatch.Success)
            {
                // extract the path attribute from the first line of the header
                string path = getRegexMatch.Groups[1].Value.Trim();

                // check if this is a web socket upgrade request
                Regex webSocketUpgradeRegex = new Regex("Upgrade: websocket", RegexOptions.IgnoreCase);
                Match webSocketUpgradeRegexMatch = webSocketUpgradeRegex.Match(header);

                if (webSocketUpgradeRegexMatch.Success)
                {
                    return new ConnectionDetails(stream, tcpClient, path, ConnectionType.WebSocket, header);
                }
                else
                {
                    return new ConnectionDetails(stream, tcpClient, path, ConnectionType.Http, header);
                }
            }
            else
            {
                //Check if we are in a POST-situation
                Regex getRegex2 = new Regex(@"^POST(.*)HTTP\/1\.1", RegexOptions.IgnoreCase);
                Match getRegexMatch2 = getRegex2.Match(header);
                if (getRegexMatch2.Success)
                {
                    string path = getRegexMatch2.Groups[1].Value.Trim();
                    return new ConnectionDetails(stream, tcpClient, path, ConnectionType.HttpPost, header);
                }
                else
                {
                    Regex getRegex3 = new Regex(@"^HEAD(.*)HTTP\/1\.1", RegexOptions.IgnoreCase);
                    Match getRegexMatch3 = getRegex3.Match(header);
                    if (getRegexMatch3.Success)
                    {
                        string path = getRegexMatch3.Groups[1].Value.Trim();
                        return new ConnectionDetails(stream, tcpClient, path, ConnectionType.Head, header);
                    }
                }

                //No love for this connection
                return new ConnectionDetails(stream, tcpClient, string.Empty, ConnectionType.Unknown, header); 
            }
        }

        private Stream GetStream(TcpClient tcpClient)
        {
            Stream stream = tcpClient.GetStream();

            // we have no ssl certificate
            if (_sslCertificate == null)
            {
                //_logger.Information(this.GetType(), "Connection not secure");
                return stream;
            }

            try
            {
                SslStream sslStream = new SslStream(stream, false);
                //_logger.Information(this.GetType(), "Attempting to secure connection...");
                sslStream.AuthenticateAsServer(_sslCertificate, false, SslProtocols.Tls12, true);
                //_logger.Information(this.GetType(), "Connection successfully secured");
                return sslStream;
            }
            catch (AuthenticationException e)
            {
                // TODO: send 401 Unauthorized
                _logger.Error(typeof(WebServer), $"401 unauth on {tcpClient.Client.LocalEndPoint} with error {e.Message}");
                return null;
                //throw e;
            }
        }

        private void HandleAsyncConnection(IAsyncResult res)
        {
            TcpClient tcpClient = null;
            try
            {

                if (_isDisposed)
                {
                    return;
                }
                // this worker thread stays alive until either of the following happens:
                // Client sends a close conection request OR
                // An unhandled exception is thrown OR
                // The server is disposed
                //_logger.Information(this.GetType(), "Webserver: HandleAsyncConnection, res: " + res.ToString());
                tcpClient = _listener.EndAcceptTcpClient(res);

                using (tcpClient)
                {
                    // we are ready to listen for more connections (on another thread)
                    StartAccept("Webserver: HandleAsyncConnection inside");
                    //_logger.Information(this.GetType(), "Server: Connection opened");

                    // get a secure or insecure stream
                    Stream stream = GetStream(tcpClient);
                    if (stream == null)
                    {
                        tcpClient.Close();
                        return;
                    }
                    // extract the connection details and use those details to build a connection
                    ConnectionDetails connectionDetails = GetConnectionDetails(stream, tcpClient);
                    using (IService service = _serviceFactory.CreateInstance(connectionDetails))
                    {
                        try
                        {
                            //_logger.Information(this.GetType(), "openConnections lock");
                            // record the connection so we can close it if something goes wrong
                            lock (_openConnections)
                            {
                                //_logger.Information(this.GetType(), "Opening connection: " + service.ToString());
                                _openConnections.Add(service);
                            }

                            // respond to the http request.
                            // Take a look at the WebSocketConnection or HttpConnection classes
                            service.Respond();
                        }
                        finally
                        {
                            // forget the connection, we are done with it
                            //_logger.Information(this.GetType(), "openConnections lock finally");
                            lock (_openConnections)
                            {
                                //_logger.Information(this.GetType(), "Removing connection: " + service.ToString());
                                _openConnections.Remove(service);
                            }
                        }
                    }
                }

                _logger.Debug(this.GetType(), "Connection closed");
            }
            catch (SocketException sEx)
            {
                _logger.Error(this.GetType(), "Socketexception in HandleAsyncConnection: " + sEx.ToString());
                StartAccept("SocketException HandleAsyncConnection");
            }
            catch (ObjectDisposedException)
            {
                // do nothing. This will be thrown if the Listener has been stopped
                _logger.Error(this.GetType(), "Listener is closed or disposed...");
            }
            catch (EndOfStreamException eofex)
            {
                _logger.Information(this.GetType(), "EOF HandleAsyncConnection (connection closed): " + eofex);
            }
            catch (Exception ex)
            {
                _logger.Error(this.GetType(), "In HandleAsyncConnection: " + ex);
            }
        }

        private void CloseAllConnections()
        {
            _logger.Debug(this.GetType(), "Closing all connections...");
            IDisposable[] openConnections;

            _logger.Debug(this.GetType(), "openConnections lock in closeallconnections");
            lock (_openConnections)
            {
                openConnections = _openConnections.ToArray();
                _openConnections.Clear();
            }

            // safely attempt to close each connection
            foreach (IDisposable openConnection in openConnections)
            {
                try
                {
                    openConnection.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.Error(this.GetType(), ex);
                }
            }
        }

        public void Dispose()
        {
            _logger.Debug(this.GetType(), "Dispose...");

            if (!_isDisposed)
            {
                _isDisposed = true;

                // safely attempt to shut down the listener
                try
                {
                    if (_listener != null)
                    {
                        if (_listener.Server != null)
                        {
                            _logger.Debug(this.GetType(), "Webserver:Dispose");
                            _listener.Server.Close();
                        }

                        _listener.Stop();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(this.GetType(), ex);
                }

                CloseAllConnections();
                _logger.Debug(this.GetType(), "Web Server disposed");
            }
        }

        //Check if the classes we have available is of type 
        public bool SendAllWebsockets(string sCommand, ArrayList aParameters)
        {
            //Webserver running?
            if (_isDisposed)
                return false;

            try
            {
                _logger.Debug(this.GetType(), "openConnections lock in SendAllWebsockets");

                // record the connection so we can close it if something goes wrong
                lock (_openConnections)
                {
                    //_openConnections.Add(service);
                    for (int i = 0; i < _openConnections.Count(); i++)
                    {
                        IService oService = (IService)_openConnections[i];
                        Type tType = oService.GetType();
                        if (tType.IsSubclassOf(typeof(WebSocketBase)))
                        {
                            IServiceWebsocket oServiceWS = (IServiceWebsocket)_openConnections[i];
                            oServiceWS.SendCommand(sCommand, aParameters);
                        }
                        //else
                        //    _logger.Error(typeof(WebServer), "Not websocket... This error message should not happend");
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                // do nothing. This will be thrown if the Listener has been stopped
                _logger.Warning(this.GetType(), "Object is already disposed in SensAllWebsocket...");
            }
            catch (Exception ex)
            {
                _logger.Error(this.GetType(), ex);
            }

            return true;
        }


        //Check if the classes we have available is of type 
        public bool MobileDownload(int nMobileID, int nUserID)
        {
            //Webserver running?
            if (_isDisposed)
                return false;

            try
            {
                _logger.Debug(this.GetType(), "openConnections lock in MobileDownload");

                // record the connection so we can close it if something goes wrong
                lock (_openConnections)
                {
                    //_openConnections.Add(service);
                    for (int i = 0; i < _openConnections.Count(); i++)
                    {
                        IService oService = (IService)_openConnections[i];
                        Type tType = oService.GetType();
                        if (tType.IsSubclassOf(typeof(WebSocketBase)))
                        {
                            IServiceWebsocket oServiceWS = (IServiceWebsocket)_openConnections[i];
                            if (oServiceWS.SendMobileDownload(nMobileID, nUserID))
                            {
                                _logger.Information(this.GetType(), "--> Command: MOBILEDOWNLOADCHECK (background)");
                                ArrayList aParameter = new ArrayList();
                                //aParameter.Add("This is a text to look for");
                                oServiceWS.SendCommand("MOBILEDOWNLOADCHECK", aParameter); //Ask mobile to check for downloads 
                                return true;
                            }
                        }
                        //else
                        //    _logger.Error(typeof(WebServer), "Not websocket... This error message should not happend");
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                // do nothing. This will be thrown if the Listener has been stopped
                _logger.Warning(this.GetType(), "Object is already disposed in MobileDownload...");
            }
            catch (Exception ex)
            {
                _logger.Error(this.GetType(), ex);
            }

            return false;
        }


    }
}

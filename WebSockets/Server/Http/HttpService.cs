using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Reflection;
using System.Configuration;
using System.Xml;
using WebSockets.Common;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

namespace WebSockets.Server.Http
{
    public class HttpBinaryService : IService
    {
        private readonly Stream _stream;
        private readonly IWebSocketLogger _logger;
        private readonly string sFilePath;
        private string sWebroot;
        private readonly MimeTypes _mimeTypes;
        private readonly bool bSendHeadOnly;
        private readonly string sHTTPHeader;
        private long lRangeStart;
        private long lRangeStop;

        //Buffersize for the download
        private const int BufferSize = 5 * 1024 * 1024; //5MB buffer

        public HttpBinaryService(Stream stream, string _sFilePath, string sRoot, string sHeader, IWebSocketLogger logger, bool bIsHead = false)
        {
            _stream = stream;
            _logger = logger;
            sFilePath = _sFilePath;
            sWebroot = sRoot;
            _mimeTypes = MimeTypesFactory.GetMimeTypes(sWebroot);
            bSendHeadOnly = bIsHead;
            sHTTPHeader = sHeader;
        }

        //Send text
        public void Respond()
        {
            //Handle requests with a better error than exception ;)
            if(!File.Exists(sFilePath))
            {
                _logger.Error(this.GetType(), "Binary file does not exist: " + sFilePath);
                return;
            }

            FileInfo fi = new FileInfo(sFilePath);
            string ext = fi.Extension.ToLower();
            string contentType;
            if (!_mimeTypes.TryGetValue(ext, out contentType))
                contentType = "application/octet-stream";
            long _fileLength = fi.Length;
            string sFileName = fi.Name;

            //Checking if we are going todo partial
            bool bPartial = this.CheckRangeInHeader(sHTTPHeader);
            //bPartial = true; //Just a test

            //Set the end to filesize if not set
            if (bPartial)
            {
                if (lRangeStop <= lRangeStart)
                    lRangeStop = _fileLength; //-1 of filelength is standard of some kind?
            }
            else
                lRangeStop = _fileLength;

            //Write the header
            RespondBinaryHeader(_fileLength, sFileName, sFilePath, contentType, bPartial, lRangeStart, lRangeStop, fi.LastWriteTimeUtc);

            //If we are requested with only head, return now
            if(bSendHeadOnly)
            {
                _logger.Debug(this.GetType(), "Sending only HEAD for the binary file");
                return;
            }
            else
                _logger.Debug(this.GetType(), "Sending binary header from: " + lRangeStart.ToString());

            byte[] SendingBuffer = null;
            FileStream Fs = new FileStream(sFilePath, FileMode.Open, FileAccess.Read);

            _logger.Debug(this.GetType(), "Sending binary file: " + sFilePath);

            //Try to get a BPS value
            long _start = Environment.TickCount;
            long _bytesread = 0;

            try
            {
                long lFileLength = Fs.Length;
                if (bPartial)
                {
                    lFileLength = lRangeStop - lRangeStart;
                    SeekOrigin seekOrigin;
                    seekOrigin = new SeekOrigin();
                    if (Fs.CanSeek)
                        Fs.Seek(lRangeStart, seekOrigin);
                           
                }
                int NoOfPackets = Convert.ToInt32(Math.Ceiling(Convert.ToDouble(lFileLength) / Convert.ToDouble(BufferSize)));
                _logger.Debug(this.GetType(), "Binary file packets: " + NoOfPackets.ToString() + " - ispartial: " + bPartial.ToString()  + " - From: " + lRangeStart.ToString() + " - To: " + lRangeStop.ToString() );
                //progressBar1.Maximum = NoOfPackets;
                long TotalLength = lFileLength, CurrentPacketLength;
                for (int i = 0; i < NoOfPackets; i++)
                {
                    if (TotalLength > BufferSize)
                    {
                        CurrentPacketLength = BufferSize;
                        TotalLength = TotalLength - CurrentPacketLength;
                    }
                    else
                        CurrentPacketLength = TotalLength;

                    SendingBuffer = new byte[CurrentPacketLength];
                    Fs.Read(SendingBuffer, 0, Convert.ToInt32(CurrentPacketLength) );
                    _stream.Write(SendingBuffer, 0, SendingBuffer.Length);

                    //Get the Bytes Pr Second
                    _bytesread += CurrentPacketLength;
                    long elapsedMilliseconds = Environment.TickCount - _start;
                    if (elapsedMilliseconds > 4)
                    {
                        // Calculate the current bps.
                        long bps = _bytesread * 1000L / elapsedMilliseconds;
                        _logger.Debug(this.GetType(), "Sending with speed " + PrintNiceBPS(bps) + " - percentage: " + ((double)_bytesread/lFileLength).ToString("0.00%") );
                    }
                }

                //Here we are finished with the filereading
                Fs.Close();
                _logger.Debug(this.GetType(), "Finished sending binary file: " + sFilePath);
            }
            catch (Exception ex)
            {
                _logger.Error(this.GetType(), "HTTPBinarySender: " + ex.Message);
            }
             finally
             {
                //                 //_stream.Close();
                //                 //client.Close();
                Fs.Close();
                _logger.Debug(this.GetType(), "Binarysend: Finally close file");
            }
        }

        private bool CheckRangeInHeader(string sHeader)
        {
            Regex x = new Regex(@"bytes=(?<from>.*?)-(?<to>.*).*", RegexOptions.IgnoreCase);
            MatchCollection mc = x.Matches(sHeader);

            //If nothing was found, try the rar-way
            if (mc.Count == 0)
                return false;

            lRangeStart = Convert.ToInt64(mc[0].Groups["from"].Value);

            if (mc[0].Groups["to"].Value.Length > 0)
                if (mc[0].Groups["to"].Value != "\r")
                    lRangeStop = Convert.ToInt64(mc[0].Groups["to"].Value);

            //If regexp is true then we always retorn true :)
            return true;
        }


        private string PrintNiceBPS(long BPS)
        {
            var ordinals = new[] { "", "K", "M", "G", "T", "P", "E" };
            decimal rate = (decimal)BPS;

            var ordinal = 0;
            while (rate > 1024)
            {
                rate /= 1024;
                ordinal++;
            }

            //Now return the nice string
            return String.Format("Bandwidth: {0} {1}b/s",
               Math.Round(rate, 2, MidpointRounding.AwayFromZero),
               ordinals[ordinal]);
        }

        //No resume support (yet)
        public void RespondBinaryHeader(long iFileSize, string sFileName, string sFilePath, string sMime, bool bPartial = false, long lRangeFrom = 0, long lRangeTo = 0, DateTime dateFileModified = default(DateTime))
        {
            string sResponse = "HTTP/1.1 200 OK";
            string sRange = "Accept-Ranges: bytes";
            long lContentLength = iFileSize;
            if (bPartial)
            {
                sResponse = "HTTP/1.1 206 Partial Content";
                if (lRangeTo == 0) //Just a precaution
                    lRangeTo = 1;
                sRange = String.Format("Content-Range: bytes {0}-{1}/{2}", lRangeFrom.ToString(), (lRangeTo-1).ToString(), iFileSize.ToString());
                lContentLength = (lRangeTo - lRangeFrom);
            }

            if (!sHTTPHeader.Contains("CAST-DEVICE-CAPABILITIES"))
            {
                string response = sResponse + Environment.NewLine +
                                  "Pragma: no-cache" + Environment.NewLine +
                                  "Server: MOTRd/1.1" + Environment.NewLine +
                                  "ETag: " + GetBase64EncodedSHA1Hash(sFilePath) + Environment.NewLine +
                                  "Expires: 0" + Environment.NewLine +
                                  "Connection: close" + Environment.NewLine +
                                  "Accept-Encoding: identity" + Environment.NewLine +
                                  "Access-Control-Allow-Origin: *" + Environment.NewLine + //Needed for Chromecast: CORS headers: https://stackoverflow.com/questions/22207867/how-to-enable-cors-for-streaming-on-chromecast-using-media-player-library
                                  "Cache-Control: no-cache, no-store, must-revalidate" + Environment.NewLine +
                                  "Content-Length: " + lContentLength + Environment.NewLine +
                                    sRange + Environment.NewLine +
                                  "Content-Description: File Transfer" + Environment.NewLine +
                                  "Content-Type: " + sMime + Environment.NewLine +
                                  "Content-Disposition: attachment; filename=\"" + sFileName + "\"" + Environment.NewLine +
                                  "Content-Transfer-Encoding: binary" + Environment.NewLine;
                _logger.Debug(this.GetType(), "Sending header: " + response);

                HttpHelper.WriteHttpHeader(response, _stream);
            }
            else //Here is chromecast header
            {
                string response = sResponse + Environment.NewLine +
                                  sRange + Environment.NewLine +
                                  "Last-Modified: " + dateFileModified.ToString("r") + Environment.NewLine +
                                  "Date: " + DateTime.UtcNow.ToString("r") + Environment.NewLine +
                                  "Server: MOTRd/1.1" + Environment.NewLine +
                                  "Content-Length: " + lContentLength + Environment.NewLine +
                                  "Content-Type: " + sMime + Environment.NewLine +
                                  "Connection: close" + Environment.NewLine;

                _logger.Debug(this.GetType(), "Sending CC-header: " + response);

                HttpHelper.WriteHttpHeader(response, _stream);
            }
        }

        string GetBase64EncodedSHA1Hash(string filename)
        {
            //Reading the whole file sucked balls
            /*using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (SHA1Managed sha1 = new SHA1Managed())
            {
                return Convert.ToBase64String(sha1.ComputeHash(fs));
            }*/

            //Doing a simpler way, filename+creationdate+lastmodifieddate --> ETag
            FileInfo fi = new FileInfo(filename);
            DateTime created = fi.CreationTime;
            DateTime lastmodified = fi.LastWriteTime;

            string sHashMe = filename + created.ToLongDateString() + lastmodified.ToLongDateString();
            byte[] aHashMe = Encoding.UTF8.GetBytes(sHashMe);
            SHA1Managed sha1 = new SHA1Managed();
            string sReturn = Convert.ToBase64String(sha1.ComputeHash(aHashMe, 0, aHashMe.Length));
            return sReturn;

        }

        //Dispose the connection
        public void Dispose()
        {
            // do nothing. The network stream will be closed by the WebServe
        }
    }


    public class HttpRedirectService : IService
    {
        private readonly Stream _stream;
        private readonly IWebSocketLogger _logger;
        private readonly string sRedirectTo;
        private readonly string sReturnTo;
        private string sCookie;

        public HttpRedirectService(Stream stream, string sUrl, string sReturnUrl, string sSetCookie, IWebSocketLogger logger)
        {
            _stream = stream;
            _logger = logger;
            sRedirectTo = sUrl;
            sReturnTo = sReturnUrl;
            sCookie = sSetCookie;
        }


        //Send text
        public void Respond()
        {
            _logger.Debug(this.GetType(), "Redirecting to: " + sRedirectTo);
            _logger.Debug(this.GetType(), "Return to: " + sReturnTo);
            RespondRedirect(sRedirectTo, sReturnTo);
            //_stream.Write(toBytes, 0, toBytes.Length);
        }

        public void RespondRedirect(string sRedirect, string sReturnToUrl)
        {
            //             string response = "HTTP/1.1 200 OK" + Environment.NewLine +
            //                               "Content-Type: text/html" + Environment.NewLine +
            //                               "Content-Length: " + contentLength + Environment.NewLine +
            //                               "Connection: close";
            string response = "HTTP/1.1 302 Found" + Environment.NewLine +
                              "Location: " + sRedirect + Environment.NewLine;
            response += "Cache-Control: no-cache, no-store, must-revalidate" + Environment.NewLine +
                        "Pragma: no-cache" + Environment.NewLine +
                        "Expires: 0" + Environment.NewLine;

            if (sCookie.Length > 0)
            {
                //Delete the cookie if requested
                if (sCookie == "[DELETE]")
                    sCookie = "; expires=Thu, 01 Jan 1970 00:00:00 GMT";
            }
            
            //Do not reset session if we are downloading
            if(!sRedirect.Contains("/MOTR-download/"))
                response += "Set-Cookie: SessionID = " + sCookie + "; HttpOnly; SameSite=strict; path=/;" + Environment.NewLine;

            response += "Set-Cookie: TempID = ; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/;" + Environment.NewLine;

            if (sReturnToUrl.Length > 0)
                response += "Set-Cookie: ReturnTo = " + sReturnToUrl + Environment.NewLine;
                response += "Connection: close";

            HttpHelper.WriteHttpHeader(response, _stream);
        }

        //Dispose the connection
        public void Dispose()
        {
            // do nothing. The network stream will be closed by the WebServe
        }
    }

    public class HttpTextService : IService
    {
        private readonly Stream _stream;
        private readonly IWebSocketLogger _logger;
        private readonly string sHtmlText;
        private bool bDeleteSession;

        public HttpTextService(Stream stream, string text, IWebSocketLogger logger, bool bRemoveCookie = false)
        {
            _stream = stream;
            _logger = logger;
            sHtmlText = text;
            bDeleteSession = bRemoveCookie;
        }


        //Send text
        public void Respond()
        {
            //_logger.Debug(this.GetType(), "Sending parsed HTML-text: " + sHtmlText);
            byte[] toBytes = Encoding.ASCII.GetBytes(sHtmlText);
            RespondSuccess(toBytes.Length);
            _stream.Write(toBytes, 0, toBytes.Length);
            _logger.Debug(this.GetType(), "Served parsed HTML-text");
        }

        public void RespondSuccess(int contentLength)
        {
            string response = "HTTP/1.1 200 OK" + Environment.NewLine +
                              "Content-Type: text/html" + Environment.NewLine +
                              "Content-Length: " + contentLength + Environment.NewLine;

            //Remove all the cookies on demand
            if (bDeleteSession)
            {
                response += "Set-Cookie: SessionID=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/;" + Environment.NewLine;
                response += "Set-Cookie: TempID=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/;" + Environment.NewLine;
                response += "Set-Cookie: ReturnTo=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/;" + Environment.NewLine;
            }

            response += "Connection: close";

            HttpHelper.WriteHttpHeader(response, _stream);
        }

        //Dispose the connection
        public void Dispose() {
            // do nothing. The network stream will be closed by the WebServe
        }
    }

    public class HttpService : IService
    {
        private readonly Stream _stream;
        private readonly string _path;
        private readonly string _webRoot;
        private readonly IWebSocketLogger _logger;
        private readonly MimeTypes _mimeTypes;

        public HttpService(Stream stream, string path, string webRoot, IWebSocketLogger logger)
        {
            _stream = stream;
            _path = path;
            _webRoot = webRoot;
            _logger = logger;
            _mimeTypes = MimeTypesFactory.GetMimeTypes(webRoot);
        }

        private static bool IsDirectory(string file)
        {
            if (Directory.Exists(file))
            {
                //detect whether its a directory or file
                FileAttributes attr = File.GetAttributes(file);
                return ((attr & FileAttributes.Directory) == FileAttributes.Directory);
            }

            return false;
        }

        public void Respond()
        {
            _logger.Debug(this.GetType(), "Request: {0}", _path);
            string file = GetSafePath(_path);

            // default to index.html is path is supplied
            if (IsDirectory(file))
            {
                file += "index.html";
            }

            FileInfo fi = new FileInfo(file);

            if (fi.Exists)
            {
                string ext = fi.Extension.ToLower();

                string contentType;
                if (_mimeTypes.TryGetValue(ext, out contentType))
                {
                    Byte[] bytes = File.ReadAllBytes(fi.FullName);
                    RespondSuccess(contentType, bytes.Length);
                    _stream.Write(bytes, 0, bytes.Length);
                    _logger.Debug(this.GetType(), "Served file: {0}", file);
                }
                else
                {
                    RespondMimeTypeFailure(file);
                }
            }
            else
            {
                RespondNotFoundFailure(file);
            }
        }

        /// <summary>
        /// I am not convinced that this function is indeed safe from hacking file path tricks
        /// </summary>
        /// <param name="path">The relative path</param>
        /// <returns>The file system path</returns>
        private string GetSafePath(string path)
        {
            path = path.Trim().Replace("/", "\\");
            if (path.Contains("..") || !path.StartsWith("\\") || path.Contains(":"))
            {
                return string.Empty;
            }

            string file = _webRoot + path;
            return file;
        }

        public void RespondMimeTypeFailure(string file)
        {
            HttpHelper.WriteHttpHeader("415 Unsupported Media Type", _stream);
            _logger.Warning(this.GetType(), "File extension not found MimeTypes.config: {0}", file);
        }

        public void RespondNotFoundFailure(string file)
        {
            HttpHelper.WriteHttpHeader("HTTP/1.1 404 Not Found", _stream);
            _logger.Warning(this.GetType(), "File not found: {0}", file);
        }

        public void RespondSuccess(string contentType, int contentLength)
        {
            string response = "HTTP/1.1 200 OK" + Environment.NewLine +
                              "Cache-Control: no-cache, no-store, must-revalidate" + Environment.NewLine +
                              "Pragma: no-cache" + Environment.NewLine +
                              "Expires: 0" + Environment.NewLine +
                              "Content-Type: " + contentType + "; charset=utf-8" + Environment.NewLine +
                              "Content-Length: " + contentLength + Environment.NewLine;


            response +="Connection: close";
            HttpHelper.WriteHttpHeader(response, _stream);
        }

        public void Dispose()
        {
            // do nothing. The network stream will be closed by the WebServer
        }
    }

    public class HttpPostService : IService
    {
        private readonly Stream _stream;
        private readonly IWebSocketLogger _logger;
        private readonly int nErrorCode;

        public HttpPostService(Stream stream, int nError, IWebSocketLogger logger)
        {
            _stream = stream;
            _logger = logger;
            nErrorCode = nError;
        }

        //Send text
        public void Respond()
        {
            _logger.Debug(this.GetType(), "Sending POST response");
//            byte[] toBytes = Encoding.ASCII.GetBytes(sHtmlText);
            RespondSuccess(0);
  //          _stream.Write(toBytes, 0, toBytes.Length);
        }

        public void RespondSuccess(int contentLength)
        {
            string nErrorString = "HTTP/1.1 204 No Content";
            string response = nErrorString + Environment.NewLine +
                              "Content-Type: text/html" + Environment.NewLine +
                              "Content-Length: " + contentLength + Environment.NewLine +
                              "Connection: close";
            HttpHelper.WriteHttpHeader(response, _stream);
        }

        //Dispose the connection
        public void Dispose()
        {
            // do nothing. The network stream will be closed by the WebServe
        }
    }
}

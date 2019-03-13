using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Threading;
using WebSockets.Exceptions;
using System.Net.Security;


namespace WebSockets.Server.Http
{
    public class HttpHelper
    {
        public static string ReadHttpHeader(Stream stream)
        {
            //Get type of stream
            string sType = stream.GetType().Name.ToString().ToUpper();
            string header = string.Empty;
            if (sType != "SSLSTREAM")
            {
                NetworkStream myStream = (NetworkStream)stream;

                StringBuilder myCompleteMessage = new StringBuilder();
                if (myStream.CanRead)
                {
                    byte[] myReadBuffer = new byte[1024];
                    int numberOfBytesRead = 0;

                    // Incoming message may be larger than the buffer size. 
                    do
                    {
                        numberOfBytesRead = myStream.Read(myReadBuffer, 0, myReadBuffer.Length);
                        myCompleteMessage.AppendFormat("{0}", Encoding.UTF8.GetString(myReadBuffer, 0, numberOfBytesRead));
                        Thread.Sleep(10); //Added for the Safari does not handle the quick load ;)
                    }
                    while (myStream.DataAvailable);

                    // Print out the received message to the console.
                    //Console.WriteLine("You received the following message : " +
                    //                             myCompleteMessage);
                }
                else
                {
                    Console.WriteLine("Sorry.  You cannot read from this NetworkStream.");
                }

                //Store header
                header = myCompleteMessage.ToString();
            }
            else //Handle SSL
            {
                SslStream myStream = (SslStream)stream;

                int length = 1024 * 16; // 16KB buffer more than enough for http header (IIS default max value!)
                //byte[] buffer = new byte[length];
                byte[] buffer = new byte[length];
                int offset = 0;
                int bytesRead = 0;
                int bytesStartPos = 0;
                bool bDataAvailable = true;

                do
                {
                    if (offset >= length)
                    {
                        throw new EntityTooLargeException("Http header message too large to fit in buffer (16KB)");
                    }

                    //Console.WriteLine("Reading from socket...");
                    int lastbytesRead = bytesRead;
                    bytesRead = myStream.Read(buffer, offset, length - offset);

                    //When we don't have any data available then we move out of the loop
                    if (bytesRead == 0 || (lastbytesRead == 1 && bytesRead < length) || (lastbytesRead == 0 && bytesRead>1 && bytesRead < length) )
                        bDataAvailable = false;

                    //Set the header based on the data
                    if (bytesRead > 0)
                    {
                        bytesStartPos = offset;
                        offset += bytesRead;
                        header += Encoding.UTF8.GetString(buffer, bytesStartPos, offset - bytesStartPos);
                    }

                    Console.WriteLine("bytesRead: {0}, header-length: {1} and bDataAvailable: {2} - Type {3}", bytesRead, header.Length, bDataAvailable, sType);
                    Thread.Sleep(5);

                } while (bDataAvailable);
            }

            //Returner hvis alt er ok, gjelder alle strømmer
            //Console.WriteLine("Header: " + header);

            if (header.Contains("\r\n\r\n"))
                return header;
            return string.Empty;
        }

        public static void WriteHttpHeader(string response, Stream stream)
        {
            response = response.Trim() + Environment.NewLine + Environment.NewLine;
            Byte[] bytes = Encoding.UTF8.GetBytes(response);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
        }
    }
}

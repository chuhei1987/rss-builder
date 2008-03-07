// edtFTPnet
// 
// Copyright (C) 2004 Enterprise Distributed Technologies Ltd
// 
// www.enterprisedt.com
// 
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later version.
// 
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
// 
// Bug fixes, suggestions and comments should posted on 
// http://www.enterprisedt.com/forums/index.php
// 
// Change Log:
// 
// $Log: FTPControlSocket.cs,v $
// Revision 1.23  2006/06/14 10:37:27  hans
// Control channel encoding and .NET 2.0 compatibility
//
// Revision 1.22  2006/05/27 10:22:58  bruceb
// active port range default now uses 0 port
//
// Revision 1.21  2006/05/01 02:30:27  bruceb
// add retry for active mode ports re port range
//
// Revision 1.20  2006/04/13 04:32:03  bruceb
// increment ActivePortRange even if exception
//
// Revision 1.19  2006/02/09 10:35:28  hans
// Added a comment for controlPort
//
// Revision 1.18  2005/12/13 19:52:41  hans
// Added AutoPassiveIPSubstitution
//
// Revision 1.17  2005/09/30 06:34:41  bruceb
// allow 230 when initiate connection
//
// Revision 1.16  2005/09/20 10:24:23  bruceb
// check for null in reply
//
// Revision 1.15  2005/08/05 13:45:52  bruceb
// active mode port/ip address setting
//
// Revision 1.14  2005/08/04 21:57:43  bruceb
// throw change
//
// Revision 1.13  2005/06/10 15:48:13  bruceb
// error checking
//
// Revision 1.12  2005/04/08 12:05:32  bruceb
// Skip blank lines reading control socket replies
//
// Revision 1.11  2004/11/20 22:33:00  bruceb
// removed full classnames
//
// Revision 1.10  2004/11/15 23:27:03  hans
// *** empty log message ***
//
// Revision 1.9  2004/11/13 19:05:13  bruceb
// GetStream removed arg
//
// Revision 1.8  2004/11/06 11:10:02  bruceb
// tidied namespaces, changed IOException to SystemException
//
// Revision 1.7  2004/11/05 20:00:13  bruceb
// events added
//
// Revision 1.6  2004/11/04 22:32:26  bruceb
// made many protected methods internal
//
// Revision 1.5  2004/11/04 21:18:13  hans
// *** empty log message ***
//
// Revision 1.4  2004/10/29 14:30:31  bruceb
// BaseSocket changes
//
//

using System;
using System.IO;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using EnterpriseDT.Net;
using System.Text;
using EnterpriseDT.Util.Debug;

namespace EnterpriseDT.Net.Ftp
{    
    /// <summary>  Supports client-side FTP operations
    /// 
    /// </summary>
    /// <author>              Bruce Blackshaw
    /// </author>
    /// <version>         $Revision: 1.23 $
    /// 
    /// </version>
    public class FTPControlSocket
    {
        /// <summary>
        /// Event for notifying start of a transfer
        /// </summary> 
        internal event FTPMessageHandler CommandSent;
        
        /// <summary>
        /// Event for notifying start of a transfer
        /// </summary> 
        internal event FTPMessageHandler ReplyReceived;

        /// <summary> 
        /// Get/Set strict checking of FTP return codes. If strict 
        /// checking is on (the default) code must exactly match the expected 
        /// code. If strict checking is off, only the first digit must match.
        /// </summary>
        virtual internal bool StrictReturnCodes
        {
            set
            {
                this.strictReturnCodes = value;
            }
            get
            {
                return strictReturnCodes;
            }
            
        }
        /// <summary>   
        /// Get/Set the TCP timeout on the underlying control socket.
        /// </summary>
        virtual internal int Timeout
        {
            set
            {
                timeout = value;
                if (controlSock == null)
                    throw new System.SystemException("Failed to set timeout - no control socket");
                SetSocketTimeout(controlSock, value);
            }
            get
            {
                return timeout;
            }
        }
                
        /// <summary>   Standard FTP end of line sequence</summary>
        internal const string EOL = "\r\n";

        /// <summary>   Maximum number of auto retries in active mode</summary>
        internal const int MAX_ACTIVE_RETRY = 100;
        
        /// <summary>   The default and standard control port number for FTP</summary>
        public const int CONTROL_PORT = 21;
        
        /// <summary>   Used to flag messages</summary>
        private const string DEBUG_ARROW = "---> ";
        
        /// <summary>   Start of password message</summary>
        private static readonly string PASSWORD_MESSAGE = DEBUG_ARROW + "PASS";
        
        /// <summary> Logging object</summary>
        private Logger log;
        
        /// <summary> Use strict return codes if true</summary>
        private bool strictReturnCodes = true;
        
        /// <summary>Address of the remote host</summary>
        protected IPAddress remoteHost = null;

		/// <summary>FTP port of the remote host</summary>
		protected int controlPort = -1;
        
        /// <summary>  The underlying socket.</summary>
        protected BaseSocket controlSock = null;
        
        /// <summary>  
        /// The timeout for the control socket
        /// </summary>
        protected int timeout = 0;
        
        /// <summary>  The write that writes to the control socket</summary>
        protected StreamWriter writer = null;
        
        /// <summary>  The reader that reads control data from the
        /// control socket
        /// </summary>
        protected StreamReader reader = null;
                
        /// <summary>
        /// Port range for active mode
        /// </summary>
        private PortRange activePortRange = null;
        
        /// <summary>
        /// IP address to send with PORT command
        /// </summary>
        private IPAddress activeIPAddress = null;

        /// <summary>
        /// The next port number to use if activePortRange is set
        /// </summary>
        private int nextPort = 0;

		/// <summary>
		/// If true, uses the original host IP if an internal IP address
		/// is returned by the server in PASV mode.
		/// </summary>
		private bool autoPassiveIPSubstitution = false;
        
        /// <summary>
        /// Constructor. Performs TCP connection and
        /// sets up reader/writer. Allows different control
        /// port to be used
        /// </summary>
        /// <param name="remoteHost">      
        /// Remote inet address
        /// </param>
        /// <param name="controlPort">     
        /// port for control stream
        /// </param>
        /// <param name="timeout">          
        /// the length of the timeout, in milliseconds
        /// </param>
        /// <param name="encoding">          
        /// encoding to use for control channel
        /// </param>
        internal FTPControlSocket(IPAddress remoteHost, int controlPort, int timeout, Encoding encoding)            
        {
            Initialize(
                new StandardSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp),
                remoteHost, controlPort, timeout, encoding);
        }

        /// <summary>   
        /// Default constructor
        /// </summary>
        internal FTPControlSocket()
        {
        }
        
        /// <summary>   
        /// Performs TCP connection and sets up reader/writer. 
        /// Allows different control port to be used
        /// </summary>
        /// <param name="sock">
        ///  Socket instance
        /// </param>
        /// <param name="remoteHost">     
        /// address of remote host
        /// </param>
        /// <param name="controlPort">     
        /// port for control stream
        /// </param>
        /// <param name="timeout">    
        /// the length of the timeout, in milliseconds      
        /// </param>
        /// <param name="encoding">          
        /// encoding to use for control channel
        /// </param>
        internal void Initialize(BaseSocket sock, IPAddress remoteHost, int controlPort, 
			int timeout, Encoding encoding)
        {
            this.remoteHost = remoteHost;
            this.controlPort = controlPort;
            this.timeout = timeout;

            log = Logger.GetLogger(typeof(FTPControlSocket));
            
            // establish socket connection & set timeouts
            controlSock = sock;
            ConnectSocket(controlSock, remoteHost, controlPort);
            Timeout = timeout;
            
            InitStreams(encoding);
            ValidateConnection();
        }

        /// <summary>   
        /// Establishes the socket connection
        /// </summary>
        /// <param name="socket">
        ///  Socket instance
        /// </param>
        /// <param name="address">     
        /// IP address to connect to
        /// </param>
        /// <param name="port">    
        /// port to connect to     
        /// </param>
        internal virtual void ConnectSocket(BaseSocket socket, IPAddress address, int port)
        {
            socket.Connect(new IPEndPoint(address, port));
        }
        
        /// <summary>   Checks that the standard 220 reply is returned
        /// following the initiated connection. Allow 230 as well, some proxy
        /// servers return it.
        /// </summary>
        internal void ValidateConnection()
        {           
            FTPReply reply = ReadReply();
            string[] validCodes = new string[]{"220", "230"};
            ValidateReply(reply, validCodes);
        } 
        
        
        /// <summary>  Obtain the reader/writer streams for this
        /// connection
        /// </summary>
        internal void InitStreams(Encoding encoding)
        {
            Stream stream = controlSock.GetStream();
            writer = new StreamWriter(stream, encoding);
            reader = new StreamReader(stream, encoding);
        }
        
        /// <summary>
        /// Set the port range to use in active mode
        /// </summary>
        /// <param name="portRange">port range to use</param>
        internal void SetActivePortRange(PortRange portRange)
        {
            activePortRange = portRange;
            if (!portRange.UseOSAssignment)
            {
                Random rand = new Random();
                nextPort = rand.Next(activePortRange.LowPort,activePortRange.HighPort);
                log.Debug("SetActivePortRange("+ activePortRange.LowPort + "," + activePortRange.HighPort + "). NextPort=" + nextPort);
            }
        }
        
        /// <summary>
        /// Set an IP address to use for PORT commands
        /// </summary>
        /// <param name="address">IP address to use for PORT command</param>
        internal void SetActiveIPAddress(IPAddress address)
        {
            activeIPAddress = address;
        }
        
        /// <summary>  
        /// Quit this FTP session and clean up.
        /// </summary>
        internal virtual void Logout()
        {
            
            SystemException ex = null;
            try
            {
                writer.Close();
            }
            catch (SystemException e)
            {
                ex = e;
            }
            try
            {
                reader.Close();
            }
            catch (SystemException e)
            {
                ex = e;
            }
            try
            {
                controlSock.Close();
            }
            catch (SystemException e)
            {
                ex = e;
            }
            if (ex != null) 
            {
                log.Error("Caught exception", ex);
                throw ex;
            }
        }
        
        /// <summary>  
        /// Request a data socket be created on the
        /// server, connect to it and return our
        /// connected socket.
        /// </summary>
        /// <param name="connectMode">  
        /// The mode to connect in
        /// </param>
        /// <returns>  
        /// connected data socket
        /// </returns>
        internal virtual FTPDataSocket CreateDataSocket(FTPConnectMode connectMode)
        {            
            if (connectMode == FTPConnectMode.ACTIVE)
            {
                return CreateDataSocketActive();
            }
            else
            {
                // PASV
                return CreateDataSocketPASV();
            }
        }
        
        /// <summary>  
        /// Request a data socket be created on the Client
        /// client on any free port, do not connect it to yet.
        /// </summary>
        /// <returns>  
        /// not connected data socket
        /// </returns>
        internal virtual FTPDataSocket CreateDataSocketActive()
        {
            try 
            {
                int count = 0;
                int maxCount = MAX_ACTIVE_RETRY;
                if (activePortRange != null) 
                {
                    int range = activePortRange.HighPort-activePortRange.LowPort+1;
                    if (range < MAX_ACTIVE_RETRY)
                        maxCount = range;
                }
                while (count < maxCount)
                {
                    count++;
                    try
                    {
                        return NewActiveDataSocket(nextPort);
                    }
                    catch (SocketException) 
                    {
                        // check ok to retry
                        if (count < maxCount)
                        {
                            log.Warn("Detected socket in use - retrying and selecting new port");
                            SetNextAvailablePortFromRange();
                        }
                    }
                }
                throw new FTPException("Exhausted active port retry count - giving up");
                
            }
            finally // even if exception thrown, we want to increment the range
            {
                SetNextAvailablePortFromRange();
            }
        }

        /// <summary>
        /// Increment port number to use to next in range, or else recycle
        /// from lowPort again, making sure we avoid the current port
        /// </summary>
        private void SetNextAvailablePortFromRange() 
        {
            // keep using 0 if using OS ports
            if (activePortRange == null || activePortRange.UseOSAssignment)
                return;

            nextPort++;

            // if exceeded the high port drop to low
            if (nextPort > activePortRange.HighPort)
                nextPort = activePortRange.LowPort;

            log.Debug("Next active port will be: " + nextPort);
        }
                
        /// <summary>  
        /// Sets the data port on the server, i.e. sends a PORT
        /// command        
        /// </summary>
        /// <param name="ep">local endpoint
        /// </param>
        internal void SetDataPort(IPEndPoint ep)
        {
#if NET20
            byte[] hostBytes = ep.Address.GetAddressBytes();
#else
            byte[] hostBytes = BitConverter.GetBytes(ep.Address.Address);
#endif
            if (activeIPAddress != null)
            {
                log.Info("Forcing use of fixed IP for PORT command");
                hostBytes = activeIPAddress.GetAddressBytes();
            }
                            
            // This is a .NET 1.1 API
            // byte[] hostBytes = ep.Address.GetAddressBytes();
            
            byte[] portBytes = ToByteArray((ushort)ep.Port);
            
            // assemble the PORT command
            string cmd = new StringBuilder("PORT ").
                Append((short)hostBytes[0]).Append(",").
                Append((short)hostBytes[1]).Append(",").
                Append((short)hostBytes[2]).Append(",").
                Append((short)hostBytes[3]).Append(",").
                Append((short)portBytes[0]).Append(",").
                Append((short)portBytes[1]).ToString();
            
            // send command and check reply
            FTPReply reply = SendCommand(cmd);
            ValidateReply(reply, "200");
        }
        
        
        /// <summary>  
        /// Convert a short into a byte array
        /// </summary>
        /// <param name="val">  value to convert
        /// </param>
        /// <returns>  a byte array
        /// 
        /// </returns>
        internal byte[] ToByteArray(ushort val)
        {            
            byte[] bytes = new byte[2];
            bytes[0] = (byte) (val >> 8); // bits 1- 8
            bytes[1] = (byte) (val & 0x00FF); // bits 9-16
            return bytes;
        }                        
        
        /// <summary>  
        /// Request a data socket be created on the
        /// server, connect to it and return our
        /// connected socket.
        /// </summary>
        /// <returns>  connected data socket
        /// </returns>
        internal virtual FTPDataSocket CreateDataSocketPASV()
        {            
            // PASSIVE command - tells the server to listen for
            // a connection attempt rather than initiating it
            FTPReply replyObj = SendCommand("PASV");
            ValidateReply(replyObj, "227");
            string reply = replyObj.ReplyText;
            
            // The reply to PASV is in the form:
            // 227 Entering Passive Mode (h1,h2,h3,h4,p1,p2).
            // where h1..h4 are the IP address to connect and
            // p1,p2 the port number
            // Example:
            // 227 Entering Passive Mode (128,3,122,1,15,87).
            // NOTE: PASV command in IBM/Mainframe returns the string
            // 227 Entering Passive Mode 128,3,122,1,15,87    (missing 
            // brackets)
            
            // extract the IP data string from between the brackets
            int startIP = reply.IndexOf((System.Char) '(');
            int endIP = reply.IndexOf((System.Char) ')');
            
            // allow for IBM missing brackets around IP address
            if (startIP < 0 && endIP < 0)
            {
                startIP = reply.ToUpper().LastIndexOf("MODE") + 4;
                endIP = reply.Length;
            }
            
            string ipData = reply.Substring(startIP + 1, (endIP) - (startIP + 1));
            int[] parts = new int[6];
            
            int len = ipData.Length;
            int partCount = 0;
            StringBuilder buf = new StringBuilder();
            
            // loop thru and examine each char
            for (int i = 0; i < len && partCount <= 6; i++)
            {
                
                char ch = ipData[i];
                if (System.Char.IsDigit(ch))
                    buf.Append(ch);
                else if (ch != ',')
                {
                    throw new FTPException("Malformed PASV reply: " + reply);
                }
                
                // get the part
                if (ch == ',' || i + 1 == len)
                {
                    // at end or at separator
                    try
                    {
                        parts[partCount++] = System.Int32.Parse(buf.ToString());
                        buf.Length = 0;
                    }
                    catch (FormatException)
                    {
                        throw new FTPException("Malformed PASV reply: " + reply);
                    }
                }
            }
            
            // assemble the IP address
            // we try connecting, so we don't bother checking digits etc
            string ipAddress = parts[0] + "." + parts[1] + "." + parts[2] + "." + parts[3];
            
            // assemble the port number
            int port = (parts[4] << 8) + parts[5];
                     
			string hostIP = ipAddress;
			if (autoPassiveIPSubstitution) 
			{
				hostIP = remoteHost.ToString();
				if (log.IsEnabledFor(Level.DEBUG))
					log.Debug(string.Format("Substituting server supplied IP ({0}) with remote host IP ({1})", 
						ipAddress, hostIP));
			}

            // create the socket
            return NewPassiveDataSocket(hostIP, port);
        }
        
        /// <summary> Constructs a new <code>FTPDataSocket</code> object (client mode) and connect
        /// to the given remote host and port number.
        /// 
        /// </summary>
        /// <param name="ipAddress">IP Address to connect to.
        /// </param>
        /// <param name="port">Remote port to connect to.
        /// </param>
        /// <returns> A new <code>FTPDataSocket</code> object (client mode) which is
        /// connected to the given server.
        /// </returns>
        /// <throws>  SystemException Thrown if no TCP/IP connection could be made.  </throws>
        internal virtual FTPDataSocket NewPassiveDataSocket(string ipAddress, int port)
        {
            IPAddress ad = IPAddress.Parse(ipAddress);  
            IPEndPoint ipe = new IPEndPoint(ad, port); 
            BaseSocket sock = 
                new StandardSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                SetSocketTimeout(sock, timeout);
                sock.Connect(ipe);
            }
            catch (Exception ex)
            {
                log.Error("Failed to create connecting socket", ex);
                sock.Close();
                throw ex;
            }
            return new FTPPassiveDataSocket(sock);
        }
        
        /// <summary> 
        /// Constructs a new <code>FTPDataSocket</code> object (server mode) which will
        /// listen on the given port number.
        /// </summary>
        /// <param name="port">Remote port to listen on.
        /// </param>
        /// <returns> A new <code>FTPDataSocket</code> object (server mode) which is
        /// configured to listen on the given port.
        /// </returns>
        /// <throws>  SystemException Thrown if an error occurred when creating the socket.  </throws>
        internal virtual FTPDataSocket NewActiveDataSocket(int port)
        {                        
            // create listening socket at a system allocated port
            BaseSocket sock = 
                new StandardSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                log.Debug("NewActiveDataSocket(" + port + ")");
                // choose specified port
                IPEndPoint endPoint = new IPEndPoint(((IPEndPoint)controlSock.LocalEndPoint).Address, port);
                sock.Bind(endPoint);     

                // queue up to 5 connections
                sock.Listen(5);
                
                // find out ip & port we are listening on            
                SetDataPort((IPEndPoint)sock.LocalEndPoint);
            }
            catch (Exception ex)
            {
                log.Error("Failed to create listening socket", ex);
                sock.Close();
                throw ex;
            }

            return new FTPActiveDataSocket(sock);
        }
        
        /// <summary>  Send a command to the FTP server and
        /// return the server's reply as a structured
        /// reply object
        /// </summary>
        /// <param name="command">  
        /// command to send
        /// </param>
        /// <returns>  reply to the supplied command
        /// </returns>
        public virtual FTPReply SendCommand(string command)
        {            
            WriteCommand(command);
            
            // and read the result
            return ReadReply();
        }
        
        /// <summary>  Send a command to the FTP server. Don't
        /// read the reply
        /// 
        /// </summary>
        /// <param name="command">  command to send
        /// </param>
        internal virtual void WriteCommand(string command)
        {            
            Log(DEBUG_ARROW + command, true);
            
            // send it
            writer.Write(command + EOL);
            writer.Flush();
        }
        
        /// <summary>  Read the FTP server's reply to a previously
        /// issued command. RFC 959 states that a reply
        /// consists of the 3 digit code followed by text.
        /// The 3 digit code is followed by a hyphen if it
        /// is a muliline response, and the last line starts
        /// with the same 3 digit code.
        /// 
        /// </summary>
        /// <returns>  structured reply object
        /// </returns>
        internal virtual FTPReply ReadReply()
        {            
            string line = reader.ReadLine();
            while (line != null && line.Length == 0)
                line = reader.ReadLine();
            
            if (line == null)
                throw new SystemException("Unexpected null reply received");
            
            Log(line, false);
            
            if (line.Length < 3)
                throw new SystemException("Short reply received");
            
            string replyCode = line.Substring(0, 3);
            StringBuilder reply = new StringBuilder("");
            if (line.Length > 3)
                reply.Append(line.Substring(4));
            
            ArrayList dataLines = null;
            
            // check for multiline response and build up
            // the reply
            if (line[3] == '-')
            {
                dataLines = ArrayList.Synchronized(new ArrayList(10));
                bool complete = false;
                while (!complete)
                {
                    line = reader.ReadLine();
                    if (line == null)
                        throw new SystemException("Unexpected null reply received");
                    
                    if (line.Length == 0)
                        continue;
                    
                    Log(line, false);
                    
                    if (line.Length > 3 && line.Substring(0, (3) - (0)).Equals(replyCode) && 
                        line[3] == ' ')
                    {
                        reply.Append(line.Substring(3));
                        complete = true;
                    }
                    else
                    {
                        // not the last line.
                        reply.Append(" ").Append(line);
                        dataLines.Add(line);
                    }
                } // end while
            } // end if
            
            if (dataLines != null)
            {
                string[] data = new string[dataLines.Count];
                dataLines.CopyTo(data);
                return new FTPReply(replyCode, reply.ToString(), data);
            }
            else
            {
                return new FTPReply(replyCode, reply.ToString());
            }
        }
        
        
        /// <summary>  
        /// Validate the response the host has supplied against the
        /// expected reply. If we get an unexpected reply we throw an
        /// exception, setting the message to that returned by the
        /// FTP server
        /// </summary>
        /// <param name="reply">             the entire reply string we received
        /// </param>
        /// <param name="expectedReplyCode"> the reply we expected to receive
        /// 
        /// </param>
        internal virtual FTPReply ValidateReply(string reply, string expectedReplyCode)
        {
            
            FTPReply replyObj = new FTPReply(reply);
            
            if (ValidateReplyCode(replyObj, expectedReplyCode))
                return replyObj;
            
            // if unexpected reply, throw an exception
            throw new FTPException(replyObj);
        }
        
        
        /// <summary>  Validate the response the host has supplied against the
        /// expected reply. If we get an unexpected reply we throw an
        /// exception, setting the message to that returned by the
        /// FTP server
        /// 
        /// </summary>
        /// <param name="reply">              the entire reply string we received
        /// </param>
        /// <param name="expectedReplyCodes"> array of expected replies
        /// </param>
        /// <returns>  an object encapsulating the server's reply
        /// 
        /// </returns>
        public virtual FTPReply ValidateReply(string reply, string[] expectedReplyCodes)
        {            
            FTPReply replyObj = new FTPReply(reply);
            return ValidateReply(replyObj, expectedReplyCodes);
        }
        
        
        /// <summary>  Validate the response the host has supplied against the
        /// expected reply. If we get an unexpected reply we throw an
        /// exception, setting the message to that returned by the
        /// FTP server
        /// 
        /// </summary>
        /// <param name="reply">              reply object
        /// </param>
        /// <param name="expectedReplyCodes"> array of expected replies
        /// </param>
        /// <returns>  reply object
        /// 
        /// </returns>
        public virtual FTPReply ValidateReply(FTPReply reply, string[] expectedReplyCodes)
        {            
            for (int i = 0; i < expectedReplyCodes.Length; i++)
                if (ValidateReplyCode(reply, expectedReplyCodes[i]))
                    return reply;
            
            // got this far, not recognised
            throw new FTPException(reply);
        }
        
        /// <summary>  Validate the response the host has supplied against the
        /// expected reply. If we get an unexpected reply we throw an
        /// exception, setting the message to that returned by the
        /// FTP server
        /// 
        /// </summary>
        /// <param name="reply">              reply object
        /// </param>
        /// <param name="expectedReplyCode">  expected reply
        /// </param>
        /// <returns>  reply object
        /// 
        /// </returns>
        public virtual FTPReply ValidateReply(FTPReply reply, string expectedReplyCode)
        {            
            if (ValidateReplyCode(reply, expectedReplyCode))
                return reply;
            
            // got this far, not recognised
            throw new FTPException(reply);
        }
        
        /// <summary> 
        /// Validate reply object
        /// </summary>
        /// <param name="reply">               reference to reply object
        /// </param>
        /// <param name="expectedReplyCode">   expect reply code
        /// </param>
        /// <returns> true if valid, false if invalid
        /// </returns>
        private bool ValidateReplyCode(FTPReply reply, string expectedReplyCode)
        {
            
            string replyCode = reply.ReplyCode;
            if (strictReturnCodes)
            {
                if (replyCode.Equals(expectedReplyCode))
                    return true;
                else
                    return false;
            }
            else
            {
                // non-strict - match first char
                if (replyCode[0] == expectedReplyCode[0])
                    return true;
                else
                    return false;
            }
        }
                
        /// <summary>  
        /// Log a message, checking for passwords
        /// </summary>
        /// <param name="msg">
        /// message to log
        /// </param>
        /// <param name="command"> 
        /// true if a response, false otherwise
        /// </param>
        internal virtual void Log(string msg, bool command)
        {
            if (msg.StartsWith(PASSWORD_MESSAGE))
                msg = PASSWORD_MESSAGE + " ********";
            log.Debug(msg);
            if (command) 
            {
                if (CommandSent != null)
                    CommandSent(this, new FTPMessageEventArgs(msg));
            }
            else
            {
                if (ReplyReceived != null)
                    ReplyReceived(this, new FTPMessageEventArgs(msg)); 
            }
        }
        
        /// <summary>  
        /// Helper method to set a socket's timeout value
        /// </summary>
        /// <param name="sock">
        /// socket to set timeout for
        /// </param>
        /// <param name="timeout">
        /// timeout value to set
        /// </param>
        internal void SetSocketTimeout(BaseSocket sock, int timeout)
        {
            if (timeout > 0) 
            {
                sock.SetSocketOption(SocketOptionLevel.Socket, 
                    SocketOptionName.ReceiveTimeout, timeout);
                sock.SetSocketOption(SocketOptionLevel.Socket, 
                    SocketOptionName.SendTimeout, timeout);                
            }
        }

		/// <summary>
		/// Use <c>AutoPassiveIPSubstitution</c> to ensure that 
		/// data-socket connections are made to the same IP address
		/// that the control socket is connected to.
		/// </summary>
		/// <remarks>
		/// <c>AutoPassiveIPSubstitution</c> can be useful when connecting
		/// to FTP servers that request data connections be connected to an
		/// IP address other than the one to which the connection was 
		/// initially made.  This usually happens when an FTP server is behind
		/// a NAT router and has not been configured to reflect the fact that
		/// its internal (LAN) IP address is different from the address that
		/// external (Internet) machines connect to.
		/// </remarks>
		internal bool AutoPassiveIPSubstitution
		{
			get
			{
				return this.autoPassiveIPSubstitution;
			}
			set
			{
				this.autoPassiveIPSubstitution = value;
			}
		}
    }
}

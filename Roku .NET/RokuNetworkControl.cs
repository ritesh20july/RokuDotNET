
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.IO;
using System.Drawing;
using System.Collections;

namespace Roku.NET
{
    #region Main Class
    /// <summary>
    /// Main class for Roku .NET
    /// </summary>
    public class RokuNetworkControl
    {
        #region Consts
        private const string IP_Regex = @"(?<=http://)((\d{1,3}\.){3})\d{1,3}(?=\:)",
                     Port_Regex = @"(?<=\:)\d{1,5}(?=/)",
                     RokuKeyPOST = "POST /key[TYPE]/[KEY] HTTP/1.1\r\n\r\n",        // Format for sending key commands.
                     RokuTouchPOST = "POST /touch[TYPE]/[POINT] HTTP/1.1\r\n\r\n";  // Format for sending touch commands.
        /// <summary>
        /// Default Roku Port.
        /// </summary>
        public const int DefaultRokuPort = 8060;
        #endregion Consts
        #region Keys
        /// <summary>
        /// Key Commands
        /// </summary>
        public enum Keys { Home = 0, Rev, Fwd, Play, Select, Left, Right, Down, Up, Back, InstantReplay, Info, Backspace, Search, Enter, Lit_, NULL };
        
        /// <summary>
        /// Types of hits.
        /// </summary>
        public enum KeyType { Press = 0, Up, Down };

        /// <summary>
        /// Types of touches.
        /// </summary>
        public enum TouchType { Drag = 0, Up, Down };
        private string[] sKeys = { "Home", "Rev", "Fwd", "Play", "Select", "Left", "Right", "Down", "Up", "Back", "InstantReplay", "Info", "Backspace", "Search", "Enter", "Lit_", string.Empty },
                         sKeyType = { "press", "up", "down" },
                         sTouchType = { "drag", "up", "down" };
        #endregion Keys

        /// <summary>
        /// Stores info about the current Roku.
        /// </summary>
        public RokuInfo Roku_Info { get; set; }

        #region Class Inits
        public RokuNetworkControl()
        {
            Roku_Info = new RokuInfo();
        }

        public RokuNetworkControl(IPAddress RokuIP)
        {
            Roku_Info = new RokuInfo(RokuIP);
        }

        public RokuNetworkControl(IPAddress RokuIP, int RokuPort)
        {
            Roku_Info = new RokuInfo(RokuIP, RokuPort);
        }
        #endregion Init

        /// <summary>
        /// Send a Special Key to the Roku
        /// </summary>
        /// <param name="type">What the key is doing. Press, Up, Down.</param>
        /// <param name="key">The Key to send.</param>
        /// <returns>Returns successful</returns>
        public bool SendKey(KeyType type, Keys key)
        {
            Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            if (key == Keys.NULL)
            {
                s.SendTimeout = 500;
                s.ReceiveTimeout = 500;
            }
            bool ret;
            try
            {
                #region Socket Connection
                s.Connect(Roku_Info.IP, Roku_Info.Port);
                Byte[] b = Encoding.UTF8.GetBytes(RokuKeyPOST.Replace("[TYPE]", sKeyType[(int)type]).Replace("[KEY]", sKeys[(int)key]));
                s.Send(b);
                b = new byte[71]; // 71 bytes are all we need to see if our letter was recieved.
                s.Receive(b);
                string response = Encoding.UTF8.GetString(b);
                s.Close();
                GC.Collect();
                #endregion Socket Connection
                ret = System.Text.RegularExpressions.Regex.Match(response, key.Equals(Keys.NULL) ? @"Roku" : @"(HTTP(.+?)200\sOK)").Success;
            }
            catch
            {
                ret = false;
            }
            return ret;
        }

        /// <summary>
        /// Send a Litteral to the Roku.
        /// </summary>
        /// <param name="type">What the key is doing. Press, Up, Down.</param>
        /// <param name="Letter">The char to send.</param>
        /// <returns>Returns successful</returns>
        public bool SendLit(KeyType type, char Letter)
        {
            Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            bool ret = false;
            try
            {
                #region Key Code Builder
                string key = sKeys[(int)Keys.Lit_], letter = Letter.ToString();
                if (System.Text.RegularExpressions.Regex.Match(letter, "[a-zA-Z0-9]").Success)
                { // YAY! It's a normal letter
                    key += letter;
                }
                else
                { // Convert the Special Character to HEX so we can send it to the Roku
                    Byte[] bytes = Encoding.ASCII.GetBytes(letter);
                    foreach (Byte by in bytes)
                    {
                        key += "%" + by.ToString("X");
                    }
                }
                #endregion Key Code Builder
                #region Socket Connection
                s.Connect(Roku_Info.IP, Roku_Info.Port);
                Byte[] b = Encoding.ASCII.GetBytes(RokuKeyPOST.Replace("[TYPE]", sKeyType[(int)type]).Replace("[KEY]", key));
                s.Send(b);
                b = new byte[71]; // 71 bytes are all we need to see if our letter was recieved.
                s.Receive(b);
                string response = Encoding.UTF8.GetString(b);
                s.Close();
                GC.Collect();
                #endregion Socket Connection
                ret = System.Text.RegularExpressions.Regex.Match(response, @"(HTTP(.+?)200\sOK)").Success;
            }
            catch
            {
                ret = false;
            }
            return ret;
        }

        /// <summary>
        /// Get the home screen image of the app.
        /// </summary>
        /// <param name="id">App id.</param>
        /// <returns>Returns bitmap image of app home-screen icon.</returns>
        public Bitmap GetAppImage(int id)
        {
            HttpWebRequest wreq;
            HttpWebResponse wresp = null;
            Bitmap bmp = null;

            try
            {
                wreq = (HttpWebRequest)WebRequest.Create("http://" + Roku_Info.IP.ToString() + ":" + Roku_Info.Port.ToString() + "/query/icon/" + id.ToString());
                wreq.AllowWriteStreamBuffering = true;

                wresp = (HttpWebResponse)wreq.GetResponse();

                using (Stream s = wresp.GetResponseStream())
                {
                    bmp = new Bitmap(s);
                }
            }
            finally
            {
                if (wresp != null)
                    wresp.Close();
            }

            return bmp;
        }

        /// <summary>
        /// Get Roku home-screen apps.
        /// </summary>
        /// <returns>Returns an array of AppInfo</returns>
        public List<AppInfo> GetAppIDs()
        {
            HttpWebRequest wreq;
            HttpWebResponse wresp = null;
            Byte[] data;
            string sData;
            List<AppInfo> appInfo = new List<AppInfo>();
            try
            {
                wreq = (HttpWebRequest)WebRequest.Create("http://" + Roku_Info.IP.ToString() + ":" + Roku_Info.Port.ToString() + "/query/apps");
                wreq.AllowWriteStreamBuffering = true;

                wresp = (HttpWebResponse)wreq.GetResponse();
                data = new Byte[wresp.ContentLength];
                using (Stream s = wresp.GetResponseStream())
                {
                    s.Read(data, 0, data.Length);
                    sData = Encoding.ASCII.GetString(data);
                    s.Close();
                }
                MatchCollection mc = Regex.Matches(sData,"(?<=\\<app\\s).+?(?=/app\\>)");
                foreach (Match m in mc)
                {
                    string raw = m.Value,
                           id = Regex.Match(raw, "(?<=id\\=\")\\d+?(?=\")").Value,
                           ver = Regex.Match(raw, "(?<=version\\=\").+?(?=\")").Value,
                           name = Regex.Match(raw, "(?<=\"\\>).+?(?=\\<)").Value;
                    appInfo.Add(new AppInfo(name, ver, int.Parse(id)));
                }
            }
            finally
            {
                if (wresp != null)
                    wresp.Close();
            }

            return appInfo;
        }

        /// <summary>
        /// Launch an app from id.
        /// </summary>
        /// <param name="id">ID of app to launch.</param>
        /// <returns>Returns successful</returns>
        public bool LaunchApp(int id)
        {
            Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            bool ret;
            try
            {
                #region Socket Connection
                s.Connect(Roku_Info.IP, Roku_Info.Port);
                Byte[] b = Encoding.UTF8.GetBytes("POST /launch/[ID] HTTP/1.1\r\n\r\n".Replace("[ID]", id.ToString()));
                s.Send(b);
                b = new byte[71]; // 71 bytes are all we need to see if our letter was recieved.
                s.Receive(b);
                string response = Encoding.UTF8.GetString(b);
                s.Close();
                GC.Collect();
                #endregion Socket Connection
                ret = System.Text.RegularExpressions.Regex.Match(response, @"(HTTP(.+?)200\sOK)").Success;
            }
            catch
            {
                ret = false;
            }
            return ret;
        }

        /// <summary>
        /// Send a touch command to the Roku.
        /// </summary>
        /// <param name="type">Type of touch to preform.</param>
        /// <param name="point">Point to touch.</param>
        /// <returns>Returns successful</returns>
        public bool SendTouch(TouchType type, Point point)
        {
            Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            bool ret;
            try
            {
                #region Socket Connection
                s.Connect(Roku_Info.IP, Roku_Info.Port);
                Byte[] b = Encoding.UTF8.GetBytes(RokuTouchPOST.Replace("[TYPE]", sTouchType[(int)type]).Replace("[POINT]", point.X.ToString() + "." + point.Y.ToString()));
                s.Send(b);
                b = new byte[71]; // 71 bytes are all we need to see if our touch was recieved.
                s.Receive(b);
                string response = Encoding.UTF8.GetString(b);
                s.Close();
                GC.Collect();
                #endregion Socket Connection
                ret = System.Text.RegularExpressions.Regex.Match(response, @"(HTTP(.+?)200\sOK)").Success;
            }
            catch
            {
                ret = false;
            }
            return ret;
        }

        /// <summary>
        /// Send a custom command to the Roku Box via POST.
        /// </summary>
        /// <param name="command">POST command. What is sent: POST  + command +  HTTP/1.1\r\n\r\n</param>
        /// <returns></returns>
        public bool SendCustomPOST(string command)
        {
            Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            bool ret;
            try
            {
                #region Socket Connection
                s.Connect(Roku_Info.IP, Roku_Info.Port);
                Byte[] b = Encoding.UTF8.GetBytes("POST " + command + " HTTP/1.1\r\n\r\n");
                s.Send(b);
                b = new byte[71]; // 71 bytes are all we need to see if our letter was recieved.
                s.Receive(b);
                string response = Encoding.UTF8.GetString(b);
                s.Close();
                GC.Collect();
                #endregion Socket Connection
                ret = System.Text.RegularExpressions.Regex.Match(response, @"(HTTP(.+?)200\sOK)").Success;
            }
            catch
            {
                ret = false;
            }
            return ret;
        }

        /// <summary>
        /// Send a custom command to the Roku Box via POST.
        /// </summary>
        /// <param name="command">GET command.</param>
        /// <returns></returns>
        public bool SendCustomGET(string command)
        {
            HttpWebRequest wreq;
            HttpWebResponse wresp = null;
            Byte[] data;
            string sData;
            try
            {
                wreq = (HttpWebRequest)WebRequest.Create("http://" + Roku_Info.IP.ToString() + ":" + Roku_Info.Port.ToString() + "/" + command);
                wreq.AllowWriteStreamBuffering = true;

                wresp = (HttpWebResponse)wreq.GetResponse();
                data = new Byte[wresp.ContentLength];
                using (Stream s = wresp.GetResponseStream())
                {
                    s.Read(data, 0, data.Length);
                    sData = Encoding.ASCII.GetString(data);
                    s.Close();
                }
            }
            finally
            {
                if (wresp != null)
                    wresp.Close();
            }

            return Regex.Match(sData, @"(HTTP(.+?)200\sOK)").Success;
        }
    }
    #endregion Main Class
    #region Sub-Classes
    /// <summary>
    /// Stores info about a Roku
    /// </summary>
    public class RokuInfo
    {
        public const int DefaultRokuPort = 8060;
        public IPAddress IP { get; set; }
        public int Port { get; set; }
        public string ID { get; set; }
        public string NickName { get; set; }

        public RokuInfo(IPAddress RokuIP, int RokuPort, string RokuID, string RokuNickName)
        {
            IP = RokuIP;
            Port = RokuPort;
            ID = RokuID;
            NickName = RokuNickName;
        }

        public RokuInfo(IPAddress RokuIP, int RokuPort)
        {
            IP = RokuIP;
            Port = RokuPort;
            ID = NickName = string.Empty;
        }

        public RokuInfo(IPAddress RokuIP)
        {
            IP = RokuIP;
            Port = DefaultRokuPort;
            ID = NickName = string.Empty;
        }

        public RokuInfo(IPAddress RokuIP, int RokuPort, string RokuNickName)
        {
            IP = RokuIP;
            Port = RokuPort;
            ID = string.Empty;
            NickName = RokuNickName;
        }

        public RokuInfo(IPAddress RokuIP, string RokuNickName)
        {
            IP = RokuIP;
            Port = DefaultRokuPort;
            ID = string.Empty;
            NickName = RokuNickName;
        }

        public RokuInfo(string RokuNickName)
        {
            IP = IPAddress.None;
            Port = DefaultRokuPort;
            ID = string.Empty;
            NickName = RokuNickName;
        }

        public RokuInfo()
        {
            IP = IPAddress.None;
            Port = DefaultRokuPort;
            ID = NickName = string.Empty;
        }
    }

    /// <summary>
    /// Stores info about an App/Channel
    /// </summary>
    public class AppInfo
    {
        public string Name { get; set; }
        public string Ver { get; set; }
        public int ID { get; set; }

        public AppInfo(string name, string ver, int id)
        {
            Name = name;
            Ver = ver;
            ID = id;
        }

        public AppInfo()
        {
            Name = string.Empty;
            Ver = string.Empty;
            ID = 0;
        }
    }

    /// <summary>
    /// Retrieves local network roku's
    /// </summary>
    public class SSDP
    {
        private static string LocationRegex = @"(?<=http://)((\d{1,3}\.){3})\d{1,3}(?=:)",
                              PortRegex = @"(?<=:)\d{1,5}(?=/)",
                              IDRegex = @"(?<=roku:ecp:)[a-zA-Z0-9]{12}",
                              SSDP_Request = "M-SEARCH * HTTP/1.1\r\nHOST: " + IPAddress.Broadcast.ToString() + ":1900\r\nST: roku:ecp\r\nMAN: \"ssdp:discover\"\r\n\r\n";

        /// <summary>
        /// Returns a List of RokuInfo.
        /// </summary>
        /// <param name="SendTimeout">Timeout of the send command.</param>
        /// <param name="ReceiveTimeout">Timeout of the receive command.</param>
        /// <param name="ConType">Connection Type.</param>
        /// <returns></returns>
        public static List<RokuInfo> Discover(int SendTimeout, int ReceiveTimeout, ConnectionType ConType)
        {
            List<RokuInfo> DiscoveredRokus = new List<RokuInfo>();
            IPEndPoint ipe;

            Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
            s.SendTimeout = SendTimeout;
            s.ReceiveTimeout = ReceiveTimeout;
            if (ConType != null &&
                ConType.GetUserIP != null &&
                ConType.GetUserIP != IPAddress.None && 
                ConType.GetUserIP.AddressFamily == AddressFamily.InterNetwork &&
                ConType.IPprot == ConnectionType.IPprotocal.Manual)
            {
                ipe = new IPEndPoint(ConType.GetUserIP, 0);
                s.Bind(ipe);
            }
            byte[] data = Encoding.UTF8.GetBytes(SSDP_Request);
            ipe = new IPEndPoint(IPAddress.Broadcast, 1900);
            byte[] buffer = new byte[0x1000];
            
            int length;
            bool more = true;

            do
            {
                s.SendTo(data, ipe);
                length = 0;
                do
                {
                    try
                    {
                        length = s.Receive(buffer);

                        string resp = Encoding.ASCII.GetString(buffer, 0, length).ToLower();
                        if (resp.Contains("roku:ecp"))
                        {
                            Match IPMatch = Regex.Match(resp, LocationRegex),
                                  PortMatch = Regex.Match(resp, PortRegex),
                                  IDMatch = Regex.Match(resp, IDRegex);
                            if (IPMatch.Success && PortMatch.Success && IDMatch.Success)
                            {
                                DiscoveredRokus.Add(new RokuInfo(IPAddress.Parse(IPMatch.Value),
                                                                              int.Parse(PortMatch.Value),
                                                                              IDMatch.Value.ToUpper(),""));
                            }
                        }
                    }
                    catch { length = 0; more = false; }
                } while (length > 0);
            } while (more);
            return DiscoveredRokus;
        }

        /// <summary>
        /// Returns a List of RokuInfo.
        /// </summary>
        /// <param name="SendTimeout">Timeout of the send command.</param>
        /// <param name="ReceiveTimeout">Timeout of the receive command.</param>
        /// <returns></returns>
        public static List<RokuInfo> Discover(int SendTimeout, int ReceiveTimeout)
        {
            return Discover(SendTimeout, ReceiveTimeout, null);
        }

        /// <summary>
        /// Returns a List of RokuInfo.
        /// </summary>
        /// <returns></returns>
        public static List<RokuInfo> Discover()
        {
            return Discover(500, 500);
        }

        /// <summary>
        /// Returns a List of RokuInfo.
        /// </summary>
        /// <param name="Timeout">Send and Receive Timeout</param>
        /// <returns></returns>
        public static List<RokuInfo> Discover(int Timeout)
        {
            return Discover(Timeout, Timeout);
        }

        /// <summary>
        /// Returns a List of RokuInfo.
        /// </summary>
        /// <param name="SendTimeout">Timeout of the send command.</param>
        /// <param name="ReceiveTimeout">Timeout of the receive command.</param>
        /// <returns></returns>
        public static List<RokuInfo> Discover(int Timeout, bool SendORReceive)
        {
            if (SendORReceive)
                return Discover(Timeout, 500);
            else
                return Discover(500, Timeout);
        }

        /// <summary>
        /// Returns a List of RokuInfo.
        /// </summary>
        /// <param name="ConType">Connection Type</param>
        /// <returns></returns>
        public static List<RokuInfo> Discover(ConnectionType ConType)
        {
            return Discover(500, 500, ConType);
        }

        /// <summary>
        /// Returns a List of RokuInfo.
        /// </summary>
        /// <param name="Timeout">Send and Receive Timeout</param>
        /// <param name="ConType">Connection Type</param>
        /// <returns></returns>
        public static List<RokuInfo> Discover(int Timeout, ConnectionType ConType)
        {
            return Discover(Timeout, Timeout, ConType);
        }

        /// <summary>
        /// Returns a List of RokuInfo.
        /// </summary>
        /// <param name="Timeout">Send or Receive Timeout</param>
        /// <param name="SendORReceive">true = send. false = receive.</param>
        /// <param name="ConType">Connection Type</param>
        /// <returns></returns>
        public static List<RokuInfo> Discover(int Timeout, bool SendORReceive, ConnectionType ConType)
        {
            if (SendORReceive)
                return Discover(Timeout, 500, ConType);
            else
                return Discover(500, Timeout, ConType);
        }
    }

    /// <summary>
    /// How to connect to the network. Use the system default or define a network.
    /// </summary>
    public class ConnectionType
    {
        /// <summary>
        /// Use Default or Manual method for network connection.
        /// </summary>
        public enum IPprotocal { Default, Manual }

        /// <summary>
        /// The connection method being used.
        /// </summary>
        public IPprotocal IPprot { get; set; }

        /// <summary>
        /// The index of the selected network.
        /// </summary>
        public int IPindex { get; set; }

        /// <summary>
        /// Returns the selected network or IPAddress.None if index out of bounds.
        /// </summary>
        public IPAddress GetUserIP
        {
            get
            {
                if (IPprot == IPprotocal.Manual)
                {
                    if (IPs.Count > 0 && (IPindex > 0 && IPindex < IPs.Count)) // Only use IPv4, not IPv6
                        return IPs[IPindex];
                    else
                        return IPAddress.None;
                }
                else
                    return IPAddress.None;
            }
        }

        /// <summary>
        /// Returns a list of IPAddresses
        /// </summary>
        public List<IPAddress> IPs { get; set; }

        /// <summary>
        /// Init the IPAddress list.
        /// </summary>
        public ConnectionType()
        {
            IPs = new List<IPAddress>();
            foreach (IPAddress IP in Dns.GetHostAddresses(Dns.GetHostName()))
            {
                if (IP.AddressFamily == AddressFamily.InterNetwork)
                    IPs.Add(IP);
            }
            IPprot = IPprotocal.Default;
            IPindex = 0;
        }
    }
    #endregion Sub-Classes
}

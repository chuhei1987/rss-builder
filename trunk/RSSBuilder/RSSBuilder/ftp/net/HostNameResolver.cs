using System;
using System.Net;
using System.Text.RegularExpressions;

using EnterpriseDT.Util.Debug;

namespace EnterpriseDT.Net
{
    /// <summary>
    /// Utility class for resolving names on all versions of the .NET framework.
    /// </summary>
    internal class HostNameResolver
    {
        /// <summary>
        /// Logger
        /// </summary>
        private static Logger log = Logger.GetLogger(typeof(HostNameResolver));

        /// <summary>
        /// Used for determining whether a host-name is actually an IP address.
        /// </summary>
        private const string IP_ADDRESS_REGEX = @"[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}";

        /// <summary>
        /// Returns the IP address matching the given host-name or IP address-string.
        /// </summary>
        /// <param name="hostName">Host-name or IP address-string.</param>
        /// <returns></returns>
        public static IPAddress GetAddress(string hostName)
        {
            if (hostName == null)
                throw new ArgumentNullException();
            IPAddress address;
            if (Regex.IsMatch(hostName, IP_ADDRESS_REGEX))
                address = IPAddress.Parse(hostName);
            else
            {
#if NET20
                address = Dns.GetHostEntry(hostName).AddressList[0];
#else
                address = Dns.Resolve(hostName).AddressList[0];
#endif
            }
            if (log.DebugEnabled)
                log.Debug(hostName + " resolved to " + address.ToString());
            return address;
        }
    }
}

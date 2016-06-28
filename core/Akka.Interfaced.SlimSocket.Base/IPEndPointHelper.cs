using System;
using System.Linq;
using System.Net;

namespace Akka.Interfaced.SlimSocket
{
    // http://stackoverflow.com/a/12044845
    public static class IPEndPointHelper
    {
        public static IPEndPoint Parse(string endpointstring)
        {
            return Parse(endpointstring, -1);
        }

        public static IPEndPoint Parse(string endpointstring, int defaultport)
        {
            if (string.IsNullOrEmpty(endpointstring) || endpointstring.Trim().Length == 0)
            {
                throw new ArgumentException("Endpoint descriptor may not be empty.");
            }

            if (defaultport != -1 && (defaultport < IPEndPoint.MinPort || defaultport > IPEndPoint.MaxPort))
            {
                throw new ArgumentException(string.Format("Invalid default port '{0}'", defaultport));
            }

            string[] values = endpointstring.Split(new char[] { ':' });
            IPAddress ipaddy;
            int port = -1;

            // check if we have an IPv6 or ports
            if (values.Length <= 2)
            { // ipv4 or hostname
                if (values.Length == 1)
                    // no port is specified, default
                    port = defaultport;
                else
                    port = GetPort(values[1]);

                // try to use the address as IPv4, otherwise get hostname
                if (!IPAddress.TryParse(values[0], out ipaddy))
                    ipaddy = GetIPfromHost(values[0]);
            }
            else if (values.Length > 2)
            { // ipv6
                if (values[0].StartsWith("[") && values[values.Length - 2].EndsWith("]"))
                { // could [a:b:c]:d
                    string ipaddressstring = string.Join(":", values.Take(values.Length - 1).ToArray());
                    ipaddy = IPAddress.Parse(ipaddressstring);
                    port = GetPort(values[values.Length - 1]);
                }
                else
                { // [a:b:c] or a:b:c
                    ipaddy = IPAddress.Parse(endpointstring);
                    port = defaultport;
                }
            }
            else
            {
                throw new FormatException(string.Format("Invalid endpoint ipaddress '{0}'", endpointstring));
            }

            if (port == -1)
                throw new ArgumentException(string.Format("No port specified: '{0}'", endpointstring));

            return new IPEndPoint(ipaddy, port);
        }

        private static int GetPort(string p)
        {
            int port;

            if (!int.TryParse(p, out port) || port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort)
            {
                throw new FormatException(string.Format("Invalid end point port '{0}'", p));
            }

            return port;
        }

        private static IPAddress GetIPfromHost(string p)
        {
            var hosts = Dns.GetHostAddresses(p);

            if (hosts == null || hosts.Length == 0)
                throw new ArgumentException(string.Format("Host not found: {0}", p));

            return hosts[0];
        }
    }
}

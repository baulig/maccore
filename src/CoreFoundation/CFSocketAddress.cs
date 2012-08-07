using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using MonoMac.CoreFoundation;

namespace MonoMac.CFNetwork
{
	class CFSocketAddress : CFDataBuffer
	{
		public CFSocketAddress (IPEndPoint endpoint)
			: base (CreateData (endpoint))
		{
		}

		internal static IPEndPoint EndPointFromAddressPtr (IntPtr address)
		{
			using (var buffer = new CFDataBuffer (address)) {
				if (buffer [1] == 30) { // AF_INET6
					int port = (buffer [2] << 8) + buffer [3];
					var bytes = new byte [16];
					Buffer.BlockCopy (buffer.Data, 8, bytes, 0, 16);
					return new IPEndPoint (new IPAddress (bytes), port);
				} else if (buffer [1] == 2) { // AF_INET
					int port = (buffer [2] << 8) + buffer [3];
					var bytes = new byte [4];
					Buffer.BlockCopy (buffer.Data, 4, bytes, 0, 4);
					return new IPEndPoint (new IPAddress (bytes), port);
				} else {
					throw new ArgumentException ();
				}
			}
		}

		static byte[] CreateData (IPEndPoint endpoint)
		{
			if (endpoint.AddressFamily == AddressFamily.InterNetwork) {
				var buffer = new byte [16];
				buffer [0] = 16;
				buffer [1] = 2; // AF_INET
				buffer [2] = (byte)(endpoint.Port >> 8);
				buffer [3] = (byte)(endpoint.Port & 0xff);
				Buffer.BlockCopy (endpoint.Address.GetAddressBytes (), 0, buffer, 4, 4);
				return buffer;
			} else if (endpoint.AddressFamily == AddressFamily.InterNetworkV6) {
				var buffer = new byte [28];
				buffer [0] = 32;
				buffer [1] = 30; // AF_INET6
				buffer [2] = (byte)(endpoint.Port >> 8);
				buffer [3] = (byte)(endpoint.Port & 0xff);
				Buffer.BlockCopy (endpoint.Address.GetAddressBytes (), 0, buffer, 8, 16);
				return buffer;
			} else {
				throw new ArgumentException ();
			}
		}
	}
}


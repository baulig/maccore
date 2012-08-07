using System;
using System.Net;
using System.Net.Sockets;

namespace MonoMac.CFNetwork
{
	struct CFSocketSignature
	{
		int protocolFamily;
		int socketType;
		int protocol;
		IntPtr address;

		public CFSocketSignature (AddressFamily family, SocketType type, ProtocolType proto,
		                          CFSocketAddress address)
		{
			this.protocolFamily = AddressFamilyToInt (family);
			this.socketType = SocketTypeToInt (type);
			this.protocol = ProtocolToInt (proto);
			this.address = address.Handle;
		}

		internal static int AddressFamilyToInt (AddressFamily family)
		{
			switch (family) {
			case AddressFamily.Unspecified:
				return 0;
			case AddressFamily.Unix:
				return 1;
			case AddressFamily.InterNetwork:
				return 2;
			case AddressFamily.AppleTalk:
				return 16;
			case AddressFamily.InterNetworkV6:
				return 30;
			default:
				throw new ArgumentException ();
			}
		}

		internal static int SocketTypeToInt (SocketType type)
		{
			if ((int) type == 0)
				return 0;

			switch (type) {
			case SocketType.Unknown:
				return 0;
			case SocketType.Stream:
				return 1;
			case SocketType.Dgram:
				return 2;
			case SocketType.Raw:
				return 3;
			case SocketType.Rdm:
				return 4;
			case SocketType.Seqpacket:
				return 5;
			default:
				throw new ArgumentException ();
			}
		}

		internal static int ProtocolToInt (ProtocolType type)
		{
			return (int) type;
		}

	}
}


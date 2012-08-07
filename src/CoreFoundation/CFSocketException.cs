using System;

namespace MonoMac.CFNetwork
{
	public class CFSocketException : Exception
	{
		public CFSocketError Error {
			get;
			private set;
		}

		public CFSocketException (CFSocketError error)
		{
			this.Error = error;
		}
	}
}

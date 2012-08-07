using System;

namespace MonoMac.CFNetwork
{
	public struct CFSocketNativeHandle
	{
		internal readonly int handle;

		internal CFSocketNativeHandle (int handle)
		{
			this.handle = handle;
		}

		public override string ToString ()
		{
			return string.Format ("[CFSocketNativeHandle {0}]", handle);
		}
	}
}


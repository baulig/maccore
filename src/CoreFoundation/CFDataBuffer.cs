using System;
using System.Runtime.InteropServices;
using MonoMac.CoreFoundation;

namespace MonoMac.CFNetwork
{
	class CFDataBuffer : IDisposable
	{
		byte[] buffer;
		GCHandle handle;
		CFData data;
		bool owns;

		public CFDataBuffer (byte[] buffer)
		{
			this.buffer = buffer;
			this.handle = GCHandle.Alloc (this.buffer, GCHandleType.Pinned);

			data = CFData.FromDataNoCopy (handle.AddrOfPinnedObject (), buffer.Length);
			owns = true;
		}

		public CFDataBuffer (IntPtr ptr)
		{
			data = new CFData (ptr, false);
			buffer = data.GetBuffer ();
			owns = false;
		}

		~CFDataBuffer ()
		{
			Dispose (false);
		}
		
		public void Dispose ()
		{
			Dispose (true);
			GC.SuppressFinalize (this);
		}

		public IntPtr Handle {
			get { return data.Handle; }
		}

		public byte[] Data {
			get { return buffer; }
		}

		public byte this [int idx] {
			get { return buffer [idx]; }
		}
		
		protected virtual void Dispose (bool disposing)
		{
			if (data != null) {
				data.Dispose ();
				if (owns)
					handle.Free ();
				data = null;
			}
		}
	}
}


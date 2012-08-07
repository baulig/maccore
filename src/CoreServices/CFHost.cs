using System;
using System.Net;
using System.Runtime.InteropServices;
using MonoMac.CoreFoundation;
using MonoMac.Foundation;
using MonoMac.ObjCRuntime;

namespace MonoMac.CoreServices {	
	class CFHost : INativeObject, IDisposable {
		internal IntPtr handle;

		CFHost (IntPtr handle)
		{
			this.handle = handle;
		}

		~CFHost ()
		{
			Dispose (false);
		}
		
		public void Dispose ()
		{
			Dispose (true);
			GC.SuppressFinalize (this);
		}

		public IntPtr Handle {
			get { return handle; }
		}
		
		protected virtual void Dispose (bool disposing)
		{
			if (handle != IntPtr.Zero){
				CFObject.CFRelease (handle);
				handle = IntPtr.Zero;
			}
		}

		[DllImport (Constants.CFNetworkLibrary)]
		extern static IntPtr CFHostCreateWithAddress (IntPtr allocator, IntPtr address);

		public static CFHost CreateWithAddress (IPAddress address)
		{
			using (var data = new CFSocketAddress (new IPEndPoint (address, 0))) {
				return new CFHost (CFHostCreateWithAddress (IntPtr.Zero, data.Handle));
			}
		}
	}
	
}
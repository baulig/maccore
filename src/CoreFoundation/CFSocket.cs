using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using MonoMac.CoreFoundation;
using MonoMac.ObjCRuntime;

namespace MonoMac.CFNetwork
{
	public class CFSocket : CFType, INativeObject, IDisposable
	{
		IntPtr handle;
		GCHandle gch;

		~CFSocket ()
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
			if (disposing) {
				if (gch.IsAllocated)
					gch.Free ();
			}
			if (handle != IntPtr.Zero) {
				CFObject.CFRelease (handle);
				handle = IntPtr.Zero;
			}
		}

		delegate void CFSocketCallBack (IntPtr s, int type, IntPtr address, IntPtr data, IntPtr info);

		[MonoPInvokeCallback (typeof(CFSocketCallBack))]
		static void OnCallback (IntPtr s, int type, IntPtr address, IntPtr data, IntPtr info)
		{
			var socket = GCHandle.FromIntPtr (info).Target as CFSocket;
			CFSocketCallBackType cbType = (CFSocketCallBackType)type;

			if (cbType == CFSocketCallBackType.AcceptCallBack) {
				var ep = CFSocketAddress.EndPointFromAddressPtr (address);
				var handle = new CFSocketNativeHandle (Marshal.ReadInt32 (data));
				socket.OnAccepted (new CFSocketAcceptEventArgs (handle, ep));
			} else if (cbType == CFSocketCallBackType.ConnectCallBack) {
				CFSocketError result;
				if (data == IntPtr.Zero)
					result = CFSocketError.Success;
				else
					result = (CFSocketError)Marshal.ReadInt32 (data);
				socket.OnConnect (new CFSocketConnectEventArgs (result));
			}
		}

		[DllImport (Constants.CoreFoundationLibrary)]
		extern static IntPtr CFSocketCreate (IntPtr allocator, int family, int type, int proto,
		                                     CFSocketCallBackType callBackTypes,
		                                     CFSocketCallBack callout, IntPtr ctx);

		[DllImport (Constants.CoreFoundationLibrary)]
		extern static IntPtr CFSocketCreateWithNative (IntPtr allocator, CFSocketNativeHandle sock,
		                                               CFSocketCallBackType callBackTypes,
		                                               CFSocketCallBack callout, IntPtr ctx);

		[DllImport (Constants.CoreFoundationLibrary)]
		extern static IntPtr CFSocketCreateRunLoopSource (IntPtr allocator, IntPtr socket, int order);

		public CFSocket ()
			: this (0, 0, 0)
		{
		}

		public CFSocket (AddressFamily family, SocketType type, ProtocolType proto)
			: this (family, type, proto, CFRunLoop.Current)
		{
		}

		public CFSocket (AddressFamily family, SocketType type, ProtocolType proto, CFRunLoop loop)
			: this (CFSocketSignature.AddressFamilyToInt (family),
			        CFSocketSignature.SocketTypeToInt (type),
			        CFSocketSignature.ProtocolToInt (proto), loop)
		{
		}

		CFSocket (int family, int type, int proto, CFRunLoop loop)
		{
			var cbTypes = CFSocketCallBackType.DataCallBack | CFSocketCallBackType.ConnectCallBack;

			gch = GCHandle.Alloc (this);
			var ctx = new CFStreamClientContext ();
			ctx.Info = GCHandle.ToIntPtr (gch);

			var ptr = Marshal.AllocHGlobal (Marshal.SizeOf (typeof(CFStreamClientContext)));
			try {
				Marshal.StructureToPtr (ctx, ptr, false);
				handle = CFSocketCreate (
					IntPtr.Zero, family, type, proto, cbTypes, OnCallback, ptr);
			} finally {
				Marshal.FreeHGlobal (ptr);
			}

			if (handle == IntPtr.Zero)
				throw new CFSocketException (CFSocketError.Error);
			gch = GCHandle.Alloc (this);

			var source = new CFRunLoopSource (CFSocketCreateRunLoopSource (IntPtr.Zero, handle, 0));
			loop.AddSource (source, CFRunLoop.CFDefaultRunLoopMode);
		}

		internal CFSocket (CFSocketNativeHandle sock)
		{
			var cbTypes = CFSocketCallBackType.DataCallBack | CFSocketCallBackType.WriteCallBack;

			gch = GCHandle.Alloc (this);
			var ctx = new CFStreamClientContext ();
			ctx.Info = GCHandle.ToIntPtr (gch);

			var ptr = Marshal.AllocHGlobal (Marshal.SizeOf (typeof(CFStreamClientContext)));
			try {
				Marshal.StructureToPtr (ctx, ptr, false);
				handle = CFSocketCreateWithNative (
					IntPtr.Zero, sock, cbTypes, OnCallback, ptr);
			} finally {
				Marshal.FreeHGlobal (ptr);
			}

			if (handle == IntPtr.Zero)
				throw new CFSocketException (CFSocketError.Error);

			var source = new CFRunLoopSource (CFSocketCreateRunLoopSource (IntPtr.Zero, handle, 0));
			var loop = CFRunLoop.Current;
			loop.AddSource (source, CFRunLoop.CFDefaultRunLoopMode);
		}

		CFSocket (IntPtr handle)
		{
			this.handle = handle;
			gch = GCHandle.Alloc (this);

			var source = new CFRunLoopSource (CFSocketCreateRunLoopSource (IntPtr.Zero, handle, 0));
			var loop = CFRunLoop.Current;
			loop.AddSource (source, CFRunLoop.CFDefaultRunLoopMode);
		}

		[DllImport (Constants.CoreFoundationLibrary)]
		extern static IntPtr CFSocketCreateConnectedToSocketSignature (IntPtr allocator, ref CFSocketSignature signature,
		                                                               CFSocketCallBackType callBackTypes,
		                                                               CFSocketCallBack callout,
		                                                               IntPtr context, double timeout);

		public static CFSocket CreateConnectedToSocketSignature (AddressFamily family, SocketType type,
		                                                         ProtocolType proto, IPEndPoint endpoint,
		                                                         double timeout)
		{
			var cbTypes = CFSocketCallBackType.ConnectCallBack | CFSocketCallBackType.DataCallBack;
			using (var address = new CFSocketAddress (endpoint)) {
				var sig = new CFSocketSignature (family, type, proto, address);
				var handle = CFSocketCreateConnectedToSocketSignature (
					IntPtr.Zero, ref sig, cbTypes, OnCallback, IntPtr.Zero, timeout);
				if (handle == IntPtr.Zero)
					throw new CFSocketException (CFSocketError.Error);

				return new CFSocket (handle);
			}
		}

		[DllImport (Constants.CoreFoundationLibrary)]
		extern static CFSocketNativeHandle CFSocketGetNative (IntPtr handle);

		internal CFSocketNativeHandle GetNative ()
		{
			return CFSocketGetNative (handle);
		}

		[DllImport (Constants.CoreFoundationLibrary)]
		extern static CFSocketError CFSocketSetAddress (IntPtr handle, IntPtr address);

		public void SetAddress (IPAddress address, int port)
		{
			SetAddress (new IPEndPoint (address, port));
		}

		public void SetAddress (IPEndPoint endpoint)
		{
			EnableCallBacks (CFSocketCallBackType.AcceptCallBack);
			var flags = GetSocketFlags ();
			flags |= CFSocketFlags.AutomaticallyReenableAcceptCallBack;
			SetSocketFlags (flags);
			using (var address = new CFSocketAddress (endpoint)) {
				var error = CFSocketSetAddress (handle, address.Handle);
				if (error != CFSocketError.Success)
					throw new CFSocketException (error);
			}
		}

		[DllImport (Constants.CoreFoundationLibrary)]
		extern static CFSocketFlags CFSocketGetSocketFlags (IntPtr handle);

		public CFSocketFlags GetSocketFlags ()
		{
			return CFSocketGetSocketFlags (handle);
		}

		[DllImport (Constants.CoreFoundationLibrary)]
		extern static void CFSocketSetSocketFlags (IntPtr handle, CFSocketFlags flags);

		public void SetSocketFlags (CFSocketFlags flags)
		{
			CFSocketSetSocketFlags (handle, flags);
		}

		[DllImport (Constants.CoreFoundationLibrary)]
		extern static void CFSocketDisableCallBacks (IntPtr handle, CFSocketCallBackType types);

		public void DisableCallBacks (CFSocketCallBackType types)
		{
			CFSocketDisableCallBacks (handle, types);
		}

		[DllImport (Constants.CoreFoundationLibrary)]
		extern static void CFSocketEnableCallBacks (IntPtr handle, CFSocketCallBackType types);

		public void EnableCallBacks (CFSocketCallBackType types)
		{
			CFSocketEnableCallBacks (handle, types);
		}

		[DllImport (Constants.CoreFoundationLibrary)]
		extern static CFSocketError CFSocketSendData (IntPtr handle, IntPtr address, IntPtr data, double timeout);

		public void SendData (byte[] data, double timeout)
		{
			using (var buffer = new CFDataBuffer (data)) {
				var error = CFSocketSendData (handle, IntPtr.Zero, buffer.Handle, timeout);
				if (error != CFSocketError.Success)
					throw new CFSocketException (error);
			}
		}

		public class CFSocketAcceptEventArgs : EventArgs
		{
			internal CFSocketNativeHandle SocketHandle {
				get;
				private set;
			}

			public IPEndPoint RemoteEndPoint {
				get;
				private set;
			}

			public CFSocketAcceptEventArgs (CFSocketNativeHandle handle, IPEndPoint remote)
			{
				this.SocketHandle = handle;
				this.RemoteEndPoint = remote;
			}

			public CFSocket CreateSocket ()
			{
				return new CFSocket (SocketHandle);
			}

			public override string ToString ()
			{
				return string.Format ("[CFSocketAcceptEventArgs: RemoteEndPoint={0}]", RemoteEndPoint);
			}
		}

		public class CFSocketConnectEventArgs : EventArgs
		{
			public CFSocketError Result {
				get;
				private set;
			}

			public CFSocketConnectEventArgs (CFSocketError result)
			{
				this.Result = result;
			}

			public override string ToString ()
			{
				return string.Format ("[CFSocketConnectEventArgs: Result={0}]", Result);
			}
		}

		public event EventHandler<CFSocketAcceptEventArgs> AcceptEvent;
		public event EventHandler<CFSocketConnectEventArgs> ConnectEvent;

		void OnAccepted (CFSocketAcceptEventArgs args)
		{
			if (AcceptEvent != null)
				AcceptEvent (this, args);
		}

		void OnConnect (CFSocketConnectEventArgs args)
		{
			if (ConnectEvent != null)
				ConnectEvent (this, args);
		}

		[DllImport (Constants.CoreFoundationLibrary)]
		extern static CFSocketError CFSocketConnectToAddress (IntPtr handle, IntPtr address, double timeout);

		public void Connect (IPAddress address, int port, double timeout)
		{
			Connect (new IPEndPoint (address, port), timeout);
		}

		public void Connect (IPEndPoint endpoint, double timeout)
		{
			using (var address = new CFSocketAddress (endpoint)) {
				var error = CFSocketConnectToAddress (handle, address.Handle, timeout);
				if (error != CFSocketError.Success)
					throw new CFSocketException (error);
			}
		}
	}
}

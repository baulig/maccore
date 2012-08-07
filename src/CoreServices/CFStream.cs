using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using MonoMac.CoreFoundation;
using MonoMac.Foundation;
using MonoMac.ObjCRuntime;

namespace MonoMac.CFNetwork
{
	public abstract class CFStream : CFType, INativeObject, IDisposable
	{
		IntPtr handle;
		GCHandle gch;
		CFRunLoop loop;
		NSString loopMode;
		bool open, closed;

		#region Stream Constructors

		[DllImport (Constants.CoreFoundationLibrary)]
		extern static void CFStreamCreatePairWithSocket (IntPtr allocator, CFSocketNativeHandle socket,
		                                                 out IntPtr read, out IntPtr write);

		public static void CreatePairWithSocket (CFSocket socket, out CFReadStream readStream,
		                                         out CFWriteStream writeStream)
		{
			IntPtr read, write;
			CFStreamCreatePairWithSocket (IntPtr.Zero, socket.GetNative (), out read, out write);
			readStream = new CFReadStream (read);
			writeStream = new CFWriteStream (write);
		}

		[DllImport (Constants.CFNetworkLibrary)]
		extern static void CFStreamCreatePairWithPeerSocketSignature (IntPtr allocator, ref CFSocketSignature sig, out IntPtr read, out IntPtr write);

		public static void CreatePairWithPeerSocketSignature (AddressFamily family, SocketType type,
		                                                      ProtocolType proto, IPEndPoint endpoint,
		                                                      out CFReadStream readStream,
		                                                      out CFWriteStream writeStream)
		{
			using (var address = new CFSocketAddress (endpoint)) {
				var sig = new CFSocketSignature (family, type, proto, address);
				IntPtr read, write;
				CFStreamCreatePairWithPeerSocketSignature (IntPtr.Zero, ref sig, out read, out write);
				readStream = new CFReadStream (read);
				writeStream = new CFWriteStream (write);
			}
		}

		[DllImport (Constants.CFNetworkLibrary)]
		extern static void CFStreamCreatePairWithSocketToCFHost (IntPtr allocator, IntPtr host, int port,
		                                                         out IntPtr read, out IntPtr write);

		public static void CreatePairWithSocketToHost (IPEndPoint endpoint,
		                                               out CFReadStream readStream,
		                                               out CFWriteStream writeStream)
		{
			using (var host = CFHost.CreateWithAddress (endpoint.Address)) {
				IntPtr read, write;
				CFStreamCreatePairWithSocketToCFHost (
					IntPtr.Zero, host.Handle, endpoint.Port, out read, out write);
				readStream = new CFReadStream (read);
				writeStream = new CFWriteStream (write);
			}
		}

		[DllImport (Constants.CFNetworkLibrary)]
		extern static IntPtr CFReadStreamCreateForHTTPRequest (IntPtr alloc, IntPtr request);

		public static CFHTTPStream CreateForHTTPRequest (CFHTTPMessage request)
		{
			var handle = CFReadStreamCreateForHTTPRequest (IntPtr.Zero, request.Handle);
			if (handle == IntPtr.Zero)
				return null;

			return new CFHTTPStream (handle);
		}

		[DllImport (Constants.CFNetworkLibrary)]
		extern static IntPtr CFReadStreamCreateForStreamedHTTPRequest (IntPtr alloc, IntPtr request, IntPtr body);

		public static CFHTTPStream CreateForStreamedHTTPRequest (CFHTTPMessage request, CFReadStream body)
		{
			var handle = CFReadStreamCreateForStreamedHTTPRequest (IntPtr.Zero, request.Handle, body.Handle);
			if (handle == IntPtr.Zero)
				return null;

			return new CFHTTPStream (handle);
		}

		[DllImport (Constants.CFNetworkLibrary)]
		extern static void CFStreamCreateBoundPair (IntPtr alloc, out IntPtr readStream, out IntPtr writeStream, CFIndex transferBufferSize);

		public static void CreateBoundPair (out CFReadStream readStream, out CFWriteStream writeStream, int bufferSize)
		{
			IntPtr read, write;
			CFStreamCreateBoundPair (IntPtr.Zero, out read, out write, bufferSize);
			readStream = new CFReadStream (read);
			writeStream = new CFWriteStream (write);
		}

		#endregion

		#region Stream API

		public abstract CFException GetError ();

		protected void CheckError ()
		{
			var exc = GetError ();
			if (exc != null)
				throw exc;
		}

		public void Open ()
		{
			if (open || closed)
				throw new InvalidOperationException ();
			CheckHandle ();
			if (!DoOpen ()) {
				CheckError ();
				throw new InvalidOperationException ();
			}
			open = true;
		}

		protected abstract bool DoOpen ();

		public void Close ()
		{
			if (!open)
				return;
			CheckHandle ();
			if (loop != null) {
				DoSetClient (null, 0, IntPtr.Zero);
				UnscheduleFromRunLoop (loop, loopMode);
				loop = null;
				loopMode = null;
			}
			try {
				DoClose ();
			} finally {
				open = false;
				closed = true;
			}
		}

		protected abstract void DoClose ();

		public CFStreamStatus GetStatus ()
		{
			CheckHandle ();
			return DoGetStatus ();
		}

		protected abstract CFStreamStatus DoGetStatus ();

		internal IntPtr GetProperty (NSString name)
		{
			CheckHandle ();
			return DoGetProperty (name);
		}

		protected abstract IntPtr DoGetProperty (NSString name);

		protected abstract bool DoSetProperty (NSString name, INativeObject value);

		internal void SetProperty (NSString name, INativeObject value)
		{
			CheckHandle ();
			if (DoSetProperty (name, value))
				return;
			throw new InvalidOperationException (string.Format (
				"Cannot set property '{0}' on {1}.", name, GetType ().Name));
		}

		#endregion

		#region Events

		public class StreamEventArgs : EventArgs
		{
			public CFStreamEventType EventType {
				get;
				private set;
			}

			public StreamEventArgs (CFStreamEventType type)
			{
				this.EventType = type;
			}

			public override string ToString ()
			{
				return string.Format ("[StreamEventArgs: EventType={0}]", EventType);
			}
		}

		public event EventHandler<StreamEventArgs> OpenCompletedEvent;
		public event EventHandler<StreamEventArgs> HasBytesAvailableEvent;
		public event EventHandler<StreamEventArgs> CanAcceptBytesEvent;
		public event EventHandler<StreamEventArgs> ErrorEvent;
		public event EventHandler<StreamEventArgs> ClosedEvent;

		protected virtual void OnOpenCompleted (StreamEventArgs args)
		{
			if (OpenCompletedEvent != null)
				OpenCompletedEvent (this, args);
		}

		protected virtual void OnHasBytesAvailableEvent (StreamEventArgs args)
		{
			if (HasBytesAvailableEvent != null)
				HasBytesAvailableEvent (this, args);
		}

		protected virtual void OnCanAcceptBytesEvent (StreamEventArgs args)
		{
			if (CanAcceptBytesEvent != null)
				CanAcceptBytesEvent (this, args);
		}

		protected virtual void OnErrorEvent (StreamEventArgs args)
		{
			if (ErrorEvent != null)
				ErrorEvent (this, args);
		}

		protected virtual void OnClosedEvent (StreamEventArgs args)
		{
			if (ClosedEvent != null)
				ClosedEvent (this, args);
		}

		#endregion

		protected abstract void ScheduleWithRunLoop (CFRunLoop loop, NSString mode);

		protected abstract void UnscheduleFromRunLoop (CFRunLoop loop, NSString mode);

		protected delegate void CFStreamCallback (IntPtr s,CFStreamEventType type, IntPtr info);

		[MonoPInvokeCallback (typeof(CFStreamCallback))]
		static void OnCallback (IntPtr s, CFStreamEventType type, IntPtr info)
		{
			var stream = GCHandle.FromIntPtr (info).Target as CFStream;
			stream.OnCallback (type);
		}

		protected virtual void OnCallback (CFStreamEventType type)
		{
			var args = new StreamEventArgs (type);
			switch (type) {
			case CFStreamEventType.OpenCompleted:
				OnOpenCompleted (args);
				break;
			case CFStreamEventType.CanAcceptBytes:
				OnCanAcceptBytesEvent (args);
				break;
			case CFStreamEventType.HasBytesAvailable:
				OnHasBytesAvailableEvent (args);
				break;
			case CFStreamEventType.ErrorOccurred:
				OnErrorEvent (args);
				break;
			case CFStreamEventType.EndEncountered:
				OnClosedEvent (args);
				break;
			}
		}

		public void EnableEvents (CFRunLoop runLoop, NSString runLoopMode)
		{
			if (open || closed || (loop != null))
				throw new InvalidOperationException ();
			CheckHandle ();

			loop = runLoop;
			loopMode = runLoopMode;

			var ctx = new CFStreamClientContext ();
			ctx.Info = GCHandle.ToIntPtr (gch);

			var args = CFStreamEventType.OpenCompleted |
				CFStreamEventType.CanAcceptBytes | CFStreamEventType.HasBytesAvailable |
				CFStreamEventType.CanAcceptBytes | CFStreamEventType.ErrorOccurred |
				CFStreamEventType.EndEncountered;

			var ptr = Marshal.AllocHGlobal (Marshal.SizeOf (typeof(CFStreamClientContext)));
			try {
				Marshal.StructureToPtr (ctx, ptr, false);
				if (!DoSetClient (OnCallback, (int)args, ptr))
					throw new InvalidOperationException ("Stream does not support async events.");
			} finally {
				Marshal.FreeHGlobal (ptr);
			}

			ScheduleWithRunLoop (runLoop, runLoopMode);
		}

		protected abstract bool DoSetClient (CFStreamCallback callback, CFIndex eventTypes,
		                                     IntPtr context);

		protected CFStream (IntPtr handle)
		{
			this.handle = handle;
			gch = GCHandle.Alloc (this);
		}

		protected void CheckHandle ()
		{
			if (handle == IntPtr.Zero)
				throw new ObjectDisposedException (GetType ().Name);
		}

		~CFStream ()
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
				Close ();
				if (gch.IsAllocated)
					gch.Free ();
			}
			if (handle != IntPtr.Zero) {
				CFObject.CFRelease (handle);
				handle = IntPtr.Zero;
			}
		}
	}
}


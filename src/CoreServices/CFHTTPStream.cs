using System;
using MonoMac.Foundation;
using MonoMac.CoreFoundation;
using MonoMac.ObjCRuntime;

namespace MonoMac.CoreServices
{
	public class CFHTTPStream : CFReadStream
	{
		static readonly NSString _AttemptPersistentConnection;
		static readonly NSString _FinalURL;
		static readonly NSString _FinalRequest;
		static readonly NSString _Proxy;
		static readonly NSString _RequestBytesWrittenCount;
		static readonly NSString _ResponseHeader;
		static readonly NSString _ShouldAutoredirect;

		static CFHTTPStream ()
		{
			var handle = Dlfcn.dlopen (Constants.CFNetworkLibrary, 0);
			if (handle == IntPtr.Zero)
				throw new InvalidOperationException ();

			try {
				_AttemptPersistentConnection = GetStringConstant (handle, "kCFStreamPropertyHTTPAttemptPersistentConnection");
				_FinalURL = GetStringConstant (handle, "kCFStreamPropertyHTTPFinalURL");
				_FinalRequest = GetStringConstant (handle, "kCFStreamPropertyHTTPFinalRequest");
				_Proxy = GetStringConstant (handle, "kCFStreamPropertyHTTPProxy");
				_RequestBytesWrittenCount = GetStringConstant (handle, "kCFStreamPropertyHTTPRequestBytesWrittenCount");
				_ResponseHeader = GetStringConstant (handle, "kCFStreamPropertyHTTPResponseHeader");
				_ShouldAutoredirect = GetStringConstant (handle, "kCFStreamPropertyHTTPShouldAutoredirect");
			} finally {
				Dlfcn.dlclose (handle);
			}
		}

		static NSString GetStringConstant (IntPtr handle, string name)
		{
			var result = Dlfcn.GetStringConstant (handle, name);
			if (result == null)
				throw new InvalidOperationException (string.Format ("Cannot get '{0}' property.", name));
			return result;
		}

		internal CFHTTPStream (IntPtr handle)
			: base (handle)
		{
		}

		public Uri FinalURL {
			get {
				var handle = GetProperty (_FinalURL);
				if (handle == IntPtr.Zero)
					return null;

				if (CFType.GetTypeID (handle) != CFUrl.GetTypeID ()) {
					CFObject.CFRelease (handle);
					throw new InvalidCastException ();
				}

				using (var url = new CFUrl (handle))
					return new Uri (url.ToString ());
			}
		}

		public CFHTTPMessage GetFinalRequest ()
		{
			var handle = GetProperty (_FinalRequest);
			if (handle == IntPtr.Zero)
				return null;

			if (CFType.GetTypeID (handle) != CFHTTPMessage.GetTypeID ()) {
				CFObject.CFRelease (handle);
				throw new InvalidCastException ();
			}

			return new CFHTTPMessage (handle);
		}

		public CFHTTPMessage GetResponseHeader ()
		{
			var handle = GetProperty (_ResponseHeader);
			if (handle == IntPtr.Zero)
				return null;

			if (CFType.GetTypeID (handle) != CFHTTPMessage.GetTypeID ()) {
				CFObject.CFRelease (handle);
				throw new InvalidCastException ();
			}
			return new CFHTTPMessage (handle);
		}

		public bool AttemptPersistentConnection {
			get {
				var handle = GetProperty (_AttemptPersistentConnection);
				if (handle == IntPtr.Zero)
					return false;
				else if (handle == CFBoolean.False.Handle)
					return false;
				else if (handle == CFBoolean.True.Handle)
					return true;
				else
					throw new InvalidCastException ();
			}
			set {
				SetProperty (_AttemptPersistentConnection, CFBoolean.FromBoolean (value));
			}
		}

		public int RequestBytesWrittenCount {
			get {
				var handle = GetProperty (_RequestBytesWrittenCount);
				if (handle == IntPtr.Zero)
					return 0;

				using (var number = new NSNumber (handle))
					return number.Int32Value;
			}
		}

		public bool ShouldAutoredirect {
			get {
				var handle = GetProperty (_ShouldAutoredirect);
				if (handle == IntPtr.Zero)
					return false;
				else if (handle == CFBoolean.False.Handle)
					return false;
				else if (handle == CFBoolean.True.Handle)
					return true;
				else
					throw new InvalidCastException ();
			}
			set {
				SetProperty (_ShouldAutoredirect, CFBoolean.FromBoolean (value));
			}
		}

		internal CFDictionary Proxy {
			set {
				SetProperty (_Proxy, value);
			}
		}
	}
}


using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using MonoMac.Foundation;

namespace MonoMac.CFNetwork.Http
{
	public class Content : StreamContent
	{
		WebResponseStream responseStream;
		long? contentLength;

		internal Content (WebResponseStream stream)
			: base (stream)
		{
			this.responseStream = stream;
		}

		protected override bool TryComputeLength (out long length)
		{
			length = contentLength ?? 0;
			return contentLength != null;
		}

		protected override void Dispose (bool disposing)
		{
			if (disposing) {
				if (responseStream != null)
					responseStream.Dispose ();
				responseStream = null;
			}
		}

		#region Headers

		internal bool DecodeHeader (string key, string value)
		{
			if (key.Equals ("Content-Type")) {
				SetContentType (value);
				return true;
			} else if (key.EndsWith ("Content-Length")) {
				SetContentLength (value);
				return true;
			} else if (key.EndsWith ("Content-Language")) {
				Headers.ContentLanguage.Add (value);
				return true;
			} else if (key.Equals ("Content-Location")) {
				Headers.ContentLocation = new Uri (value);
				return true;
			} else if (key.Equals ("Allow")) {
				DecodeAllow (value);
				return true;
			} else if (key.Equals ("Expires")) {
				DecodeExpires (value);
				return true;
			} else if (key.Equals ("Last-Modified")) {
				DecodeLastModified (value);
				return true;
			} else {
				return false;
			}
		}

		void SetContentType (string value)
		{
			int pos = value.IndexOf (";");

			string type;
			if (pos < 0)
				type = value.Trim ();
			else
				type = value.Substring (0, pos).Trim ();
			Headers.ContentType = new MediaTypeHeaderValue (type);

			if (pos < 0)
				return;

			value = value.Substring (pos+1).Trim ();
			if (value.StartsWith ("charset=")) {
				var charset = value.Substring (8);
				Headers.ContentEncoding.Add (charset);
			}
		}

		void SetContentLength (string value)
		{
			contentLength = long.Parse (value);
			Headers.ContentLength = contentLength;
		}

		void DecodeAllow (string value)
		{
			foreach (var method in value.Split (','))
				Headers.Allow.Add (method);
		}

		void DecodeExpires (string value)
		{
			Headers.Expires = DateTimeOffset.Parse (value);
		}

		void DecodeLastModified (string value)
		{
			Headers.LastModified = DateTimeOffset.Parse (value);
		}

		#endregion
	}
}


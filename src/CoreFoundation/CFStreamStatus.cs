using System;

namespace MonoMac.CFNetwork
{
	public enum CFStreamStatus
	{
		NotOpen = 0,
		Opening,
		Open,
		Reading,
		Writing,
		AtEnd,
		Closed,
		Error
	}
}


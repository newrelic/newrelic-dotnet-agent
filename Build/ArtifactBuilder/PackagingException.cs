using System;

namespace ArtifactBuilder
{
	public class PackagingException : Exception
	{
		public PackagingException(string message) : base(message) { }
	}
}

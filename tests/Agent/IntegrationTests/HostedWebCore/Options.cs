using System;
using CommandLine;

namespace HostedWebCore
{
	internal class Options
	{
		[Option("port", Required = true)]
		public String Port { get; set; }
	}
}

using CommandLine;

namespace HostedWebCore
{
    internal class Options
    {
        [Option("port", Required = true)]
        public string Port { get; set; }
    }
}

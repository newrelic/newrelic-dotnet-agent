namespace NewRelic.Agent.Core.Config
{
    public interface IBootstrapConfig
    {
        string AgentEnabledAt { get; }

        ILogConfig LogConfig { get; }
    }
}

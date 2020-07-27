namespace NewRelic.Parsing.ConnectionString
{
    public interface IConnectionStringParser
    {
        ConnectionInfo GetConnectionInfo();
    }
}

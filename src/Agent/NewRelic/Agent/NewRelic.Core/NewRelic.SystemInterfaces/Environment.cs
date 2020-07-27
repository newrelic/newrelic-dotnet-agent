namespace NewRelic.SystemInterfaces
{
    public class Environment : IEnvironment
    {
        public string GetEnvironmentVariable(string variable)
        {
            return System.Environment.GetEnvironmentVariable(variable);
        }
    }
}

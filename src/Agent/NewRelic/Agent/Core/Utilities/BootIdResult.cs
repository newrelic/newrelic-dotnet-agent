namespace NewRelic.Agent.Core.Utilization
{
    public class BootIdResult
    {
        public string BootId { get; }
        public bool IsValid { get; }

        public BootIdResult(string bootId, bool isValid)
        {
            BootId = bootId;
            IsValid = isValid;
        }

    }
}

namespace NewRelic.Core.Caching
{
    public interface ICacheStats
    {
        int Size { get; }
        int Capacity { get; }
        int CountHits { get; }
        int CountMisses { get; }
        int CountEjections { get; }
        void ResetStats();
    }
}

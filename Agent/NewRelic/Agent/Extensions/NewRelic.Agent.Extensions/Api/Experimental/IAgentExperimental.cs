namespace NewRelic.Agent.Api.Experimental
{
	/// <summary>
	/// This interface contains methods we may eventually move to <see cref="Agent"/> once they have been sufficiently vetted.
	/// Methods on this interface are subject to refactoring or removal in future versions of the API.
	/// </summary>
	public interface IAgentExperimental
	{
		/// <summary>
		/// Records a supportability metrics
		/// </summary>
		/// <param name="metricName"></param>
		/// <param name="count">Defaults to 1.0f</param>
		void RecordSupportabilityMetric(string metricName, int count = 1);
	}
}

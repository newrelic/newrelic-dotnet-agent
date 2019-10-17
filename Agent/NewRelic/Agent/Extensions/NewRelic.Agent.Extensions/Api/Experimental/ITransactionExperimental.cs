namespace NewRelic.Agent.Api.Experimental
{
	/// <summary>
	/// This interface contains methods we may eventually move to <see cref="ITransaction"/> once they have been sufficiently vetted.
	/// Methods on this interface are subject to refactoring or removal in future versions of the API.
	/// </summary>
	public interface ITransactionExperimental
	{
		/// <summary>
		/// Returns the object that uniquely identifies the starting wrapper.
		/// </summary>
		/// <returns></returns>
		object GetWrapperToken();

		/// <summary>
		/// Set the object that uniquely identifies the starting wrapper.
		/// </summary>
		/// <param name="wrapperToken">Wrapper token.</param>
		void SetWrapperToken(object wrapperToken);
	}
}

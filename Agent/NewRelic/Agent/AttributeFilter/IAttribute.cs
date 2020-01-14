namespace NewRelic.Agent
{
	public interface IAttribute
	{
		string Key { get; }

		object Value { get; }

		AttributeDestinations DefaultDestinations { get; }
	}
}

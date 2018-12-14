namespace NewRelic.Agent.Core.SharedInterfaces
{
	/// <summary>
	/// Interface indicates that the serialization to JSON will occur via a manual method as opposed to
	/// using the Newtonsoft Standard serializer.  This is done mainly for performance reasons.  The interface
	/// allows us to build a unit test to ensure that the public members are being correctly serialized and
	/// that none are missed.
	/// </summary>
	public interface IManualSerializable
	{
		string ToJson();
	}
}

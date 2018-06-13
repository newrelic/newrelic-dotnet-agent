namespace NewRelic.Agent.Core
{
	public enum DataTransportResponseStatus
	{ 
		RequestSuccessful, 
		ConnectionError, 
		ServerError,
		PostTooBigError, 
		OtherError, 
		RequestTimeout,
		CommunicationError
	}
}

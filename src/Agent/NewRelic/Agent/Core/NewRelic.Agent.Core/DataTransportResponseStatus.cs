using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NewRelic.Agent.Core
{
	public enum DataTransportResponseStatus
	{ 
		RequestSuccessful, 
		ConnectionError, 
		ServiceUnavailableError,
		PostTooBigError, 
		OtherError 
	}
}

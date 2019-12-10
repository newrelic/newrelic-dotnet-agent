using System.Collections.Generic;

namespace NewRelic.OpenTracing.AmazonLambda.Events
{
	//[JsonConverter(typeof(ToStringJsonConverter))]
	internal class TransactionEvent : Event
	{
		private IDictionary<string, object> _intrinsics;
		private IDictionary<string, object> _distributedTraceIntrinsics;

		private IDictionary<string, object> _userAttributes;
		private IDictionary<string, object> _agentAttributes;

		private readonly string _transactionName;
		private readonly double _duration;
		private readonly long _timeStamp;
		private readonly bool _hasError;
		private readonly string _guid;

		public TransactionEvent(LambdaRootSpan rootSpan)
		{
			_duration = rootSpan.GetDurationInSeconds();
			_timeStamp = rootSpan.TimeStamp.ToUnixTimeMilliseconds();

			_transactionName = rootSpan.TransactionState.TransactionName;
			_guid = rootSpan.TransactionState.TransactionId;

			if (rootSpan.TransactionState.HasError())
			{
				_hasError = true;
			}

			_distributedTraceIntrinsics = rootSpan.Intrinsics;
			_userAttributes = new Dictionary<string, object>();

			_agentAttributes = rootSpan.Tags != null ? new Dictionary<string, object>(rootSpan.Tags) : new Dictionary<string, object>();
			_agentAttributes.Remove("http.status_code");
			if (_agentAttributes.Keys.Contains("response.status"))
			{
				return;
			}

			var status = rootSpan.GetTag("http.status_code")?.ToString();
			if (!string.IsNullOrEmpty(status))
			{
				_agentAttributes.Add("response.status", status);
			}
		}

		public override IDictionary<string, object> Intrinsics
		{
			get
			{
				if(_intrinsics != null)
				{
					return _intrinsics;
				}

				_intrinsics = new Dictionary<string, object>();
				_intrinsics.Add("type", "Transaction");
				_intrinsics.Add("timestamp", _timeStamp);
				_intrinsics.Add("duration", _duration);
				_intrinsics.Add("name", _transactionName);
				_intrinsics.Add("guid", _guid);

				if (_hasError)
				{
					_intrinsics.Add("error", true);
				}

				foreach (var keyValuePair in _distributedTraceIntrinsics)
				{
					if (!_intrinsics.ContainsKey(keyValuePair.Key))
					{
						_intrinsics[keyValuePair.Key] = keyValuePair.Value;
					}
				}

				return _intrinsics;
			}
		}

		public override IDictionary<string, object> UserAttributes => _userAttributes ?? (_userAttributes = new Dictionary<string, object>());

		public override IDictionary<string, object> AgentAttributes => _agentAttributes ?? (_agentAttributes = new Dictionary<string, object>());
	}
}

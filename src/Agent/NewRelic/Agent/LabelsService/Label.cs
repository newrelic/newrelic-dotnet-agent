using System;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace NewRelic.Agent
{
	public class Label
	{
		[NotNull]
		[JsonProperty(PropertyName = "label_type")]
		public readonly String Type;

		[NotNull]
		[JsonProperty(PropertyName = "label_value")]
		public readonly String Value;

		public Label([NotNull] String labelType, [NotNull] String labelValue)
		{
			if (labelType == null)
				throw new ArgumentNullException("labelType");
			if (labelValue == null)
				throw new ArgumentNullException("labelValue");

			Type = labelType;
			Value = labelValue;
		}
	}
}

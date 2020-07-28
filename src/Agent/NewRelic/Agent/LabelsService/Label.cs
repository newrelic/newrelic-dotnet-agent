using System;
using Newtonsoft.Json;

namespace NewRelic.Agent
{
    public class Label
    {
        [JsonProperty(PropertyName = "label_type")]
        public readonly String Type;
        [JsonProperty(PropertyName = "label_value")]
        public readonly String Value;

        public Label(String labelType, String labelValue)
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

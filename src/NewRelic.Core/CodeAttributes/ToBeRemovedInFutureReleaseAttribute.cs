using System;

namespace NewRelic.Core.CodeAttributes
{
    /// <summary>
    /// Identifies a piece of code that is planned to be removed in a future release of the agent.
    /// </summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = true)]
    public class ToBeRemovedInFutureReleaseAttribute : Attribute
    {
        public string Notes { get; private set; }

        public ToBeRemovedInFutureReleaseAttribute(string notes)
        {
            Notes = notes;
        }

        public ToBeRemovedInFutureReleaseAttribute()
        {
        }
    }
}

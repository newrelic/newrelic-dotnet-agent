using System;
using JetBrains.Annotations;

namespace NewRelic.SystemExtensions
{
	/// <summary>
	/// A set of helper methods for wrapper factories.
	/// </summary>
	public static class ObjectArrayExtensions
	{
		/// <summary>
		/// Tries to extract an object from the given array at the given index as the given type. Will throw if any condition makes that situation impossible (e.g. index is out of bounds, object at given index can not be cast to given type, etc).
		/// </summary>
		[CanBeNull]
		public static T ExtractAs<T>([CanBeNull] this Object[] objects, Int32 index) where T : class
		{
			if (objects == null)
			{
				var logMessage = $"Expected at least {(index + 1)} methodArguments";
				throw new NullReferenceException(logMessage);
			}

			if (objects.Length <= index)
			{
				var logMessage = $"Expected at least {(index + 1)} methodArguments but only found {objects.Length}";
				throw new IndexOutOfRangeException(logMessage);
			}

			var extractedValue = objects[index];
			if (extractedValue == null)
				return null;

			var castedValue = objects[index] as T;
			if (castedValue == null)
			{
				var logMessage = $"Expected argument {index} to be of type {typeof (T).FullName} (v{typeof(T).Assembly.GetName().Version}), but was of type {extractedValue.GetType().FullName} (v{extractedValue.GetType().Assembly.GetName().Version})";
				throw new InvalidCastException(logMessage);
			}

			return castedValue;
		}

		/// <summary>
		/// Tries to extract an object from the given array at the given index as the given type. Will throw if any condition makes that situation impossible (e.g. index is out of bounds, object at given index can not be cast to given type, etc) or if the extracted value is null.
		/// </summary>
		[NotNull]
		public static T ExtractNotNullAs<T>([CanBeNull] this Object[] objects, Int32 index) where T : class
		{
			var value = ExtractAs<T>(objects, index);
			if (value != null)
				return value;

			var logMessage = $"Argument {index} had null value";
			throw new NullReferenceException(logMessage);
		}
	}
}

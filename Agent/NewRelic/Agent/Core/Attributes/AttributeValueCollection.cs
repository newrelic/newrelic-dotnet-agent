using System.Collections.Concurrent;
using System.Collections.Generic;
using System;
using NewRelic.Core.Logging;
using NewRelic.Agent.Core.Utilities;
using System.Linq;

namespace NewRelic.Agent.Core.Attributes
{
	public interface IAttributeValueCollection
	{
		/// <summary>
		/// Even this enum is Flags, this should only be one attributedestination
		/// </summary>
		AttributeDestinations TargetModelType { get; }

		bool IsImmutable { get; }

		void AddRange(IAttributeValueCollection fromCollection);

		void MakeImmutable();

		bool TrySetValue<TInput, TOutput>(AttributeDefinition<TInput, TOutput> attribDef, TOutput value);

		bool TrySetValue<TInput, TOutput>(AttributeDefinition<TInput, TOutput> attribDef, Lazy<object> lazyImpl);

		bool TrySetValue(IAttributeValue attrib);

		IEnumerable<IAttributeValue> GetAttributeValues(AttributeClassification classification);
	}



	public abstract class AttributeValueCollection<TAttrib>: IAttributeValueCollection 	where TAttrib : IAttributeValue
	{
		private const int _MaxCountUserAttrib = 64;
		private const int _MaxCountAllAttrib = 255;

		private readonly Dictionary<AttributeClassification, InterlockedCounter> _attribValueCountsDic = new Dictionary<AttributeClassification, InterlockedCounter>()
		{
			{ AttributeClassification.Intrinsics, new InterlockedCounter() },
			{ AttributeClassification.UserAttributes, new InterlockedCounter() },
			{ AttributeClassification.AgentAttributes, new InterlockedCounter() }
		};

		private int _attribValueCountAll => _attribValueCountsDic[AttributeClassification.UserAttributes].Value
			+ _attribValueCountsDic[AttributeClassification.AgentAttributes].Value
			+ _attribValueCountsDic[AttributeClassification.Intrinsics].Value;

		public AttributeDestinations TargetModelType { get; private set; }

		private bool ValidateCollectionLimits(AttributeClassification classification, string name)
		{
			if (classification == AttributeClassification.UserAttributes && _attribValueCountsDic[classification].Value >= _MaxCountUserAttrib)
			{
				Log.Debug($"{TargetModelType} {classification} Attribute '{name}' was not recorded - A max of {_MaxCountUserAttrib} {classification} attributes may be supplied.");
				return false;
			}

			if (_attribValueCountAll >= _MaxCountAllAttrib)
			{
				Log.Debug($"{TargetModelType} {classification} Attribute '{name}' was not recorded - A max of {_MaxCountAllAttrib} attributes may be supplied.");
				return false;
			}

			return true;
		}

		public AttributeValueCollection(AttributeDestinations targetModelType)
		{
			TargetModelType = targetModelType;
		}

		public IEnumerable<IAttributeValue> GetAttributeValues(AttributeClassification classification)
		{
			return GetAttribValuesImpl(classification).Cast<IAttributeValue>();
		}

		public void AddRange(IAttributeValueCollection fromCollection)
		{
			foreach(var classification in _attribValueCountsDic.Keys)
			{
				foreach(var attribVal in fromCollection.GetAttributeValues(classification))
				{
					if(!attribVal.AttributeDefinition.AttributeDestinations.HasFlag(TargetModelType))
					{
						continue;
					}

					TrySetValue(attribVal);
				}
			}
		}
		
		public bool IsImmutable { get; private set; } = false;

		public bool TrySetValue<TInput, TOutput>(AttributeDefinition<TInput, TOutput> attribDef, TOutput val)
		{
			if (!ValidateCollectionLimits(attribDef.Classification, attribDef.Name))
			{
				return false;
			}
			
			return SetValueImpl(attribDef, val);
		}

		public bool TrySetValue(IAttributeValue attribValue)
		{
			if (!ValidateCollectionLimits(attribValue.AttributeDefinition.Classification, attribValue.AttributeDefinition.Name))
			{
				return false;
			}

			return SetValueImpl(attribValue);
		}

		public bool TrySetValue<TInput, TOutput>(AttributeDefinition<TInput, TOutput> attribDef, Lazy<object> lazyValueImpl)
		{
			if (!ValidateCollectionLimits(attribDef.Classification, attribDef.Name) || lazyValueImpl == null)
			{
				return false;
			}

			return SetValueImpl(attribDef, lazyValueImpl);
		}
		

		protected abstract bool SetValueImpl(IAttributeValue attribVal);

		protected abstract bool SetValueImpl(AttributeDefinition attribDef, object value);

		protected abstract bool SetValueImpl(AttributeDefinition attribDef, Lazy<object> lazyValue);

		protected abstract void RemoveItemsImpl(IEnumerable<TAttrib> itemsToRemove);

		protected abstract IEnumerable<TAttrib> GetAttribValuesImpl(AttributeClassification classification);


		public void MakeImmutable()
		{
			if(IsImmutable)
			{
				return;
			}
			
			foreach (var classification in _attribValueCountsDic.Keys)
			{
				var itemsToRemove = new List<TAttrib>();
				foreach (var attribVal in GetAttribValuesImpl(classification))
				{
					try
					{
						attribVal.MakeImmutable();

						if (attribVal.Value == null)
						{
							//Nothing to log here because the ResolveLazyValue function
							//records the error message
							itemsToRemove.Add(attribVal);
						}
					}
					catch(Exception ex)
					{

						//TODO:  Provide some sort of service to record this at intervals to a higher level log
						Log.Finest($"{this.TargetModelType} {attribVal.AttributeDefinition.Classification} Attribute '{attribVal.AttributeDefinition.Name}' was not recorded - exception occurred while resolving value (lazy) - {ex}");
						itemsToRemove.Add(attribVal);
					}
				}

				if(itemsToRemove.Count > 0)
				{
					RemoveItemsImpl(itemsToRemove);
				}
			}

			IsImmutable = true;
		}
	}

	public class NoOpAttributeValueCollection : IAttributeValueCollection
	{
		public AttributeDestinations TargetModelType { get; private set; } = AttributeDestinations.None;

		public bool IsImmutable => false;

		private static IDictionary<string, object> _emptyDic = new Dictionary<string, object>();
		private static IEnumerable<IAttributeValue> _emptyAttribValues = new List<IAttributeValue>();

		public void MakeImmutable()
		{
		}

		public IEnumerable<IAttributeValue> GetAttributeValues(AttributeClassification classification)
		{
			return _emptyAttribValues;
		}

		public void AddRange(IAttributeValueCollection fromCollection)
		{
			return;
		}

		public bool TrySetValue<TInput, TOutput>(AttributeDefinition<TInput, TOutput> attribDef, TOutput value)
		{
			return false;
		}

		public bool TrySetValue<TInput, TOutput>(AttributeDefinition<TInput, TOutput> attribDef, Lazy<object> lazyImpl)
		{
			return false;
		}

		public bool TrySetValue(IAttributeValue attrib)
		{
			return false;
		}
	}
}

﻿using System;
using JetBrains.Annotations;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Requests;

namespace NewRelic.Agent.Core.Utilities
{
	/// <summary>
	/// An abstract base class for handling all of the boilerplate associated with a service that automatically subscribes to configuration updates.
	/// </summary>
	public abstract class ConfigurationBasedService : DisposableService
	{
		[NotNull]
		private readonly EventSubscription<ConfigurationUpdatedEvent> _configurationUpdatedEventSubscription;

		/// <summary>
		/// The most up-to-date configuration available.
		/// </summary>
		[NotNull]
		protected IConfiguration _configuration { get; private set; }

		protected ConfigurationBasedService()
		{
			_configuration = RequestBus<GetCurrentConfigurationRequest, IConfiguration>.Post(new GetCurrentConfigurationRequest()) ?? DefaultConfiguration.Instance;

			_configurationUpdatedEventSubscription = new EventSubscription<ConfigurationUpdatedEvent>(OnConfigurationUpdatedInternal);

		}

		/// <summary>
		/// Override if you want to take action on configuration update.  e.g., alter subscriptions based on new configuration information.
		/// </summary>
		protected abstract void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource);

		protected void OnConfigurationUpdatedInternal([NotNull] ConfigurationUpdatedEvent eventData)
		{
			if (eventData.Configuration.ConfigurationVersion <= _configuration.ConfigurationVersion)
				return;

			_configuration = eventData.Configuration;
			OnConfigurationUpdated(eventData.ConfigurationUpdateSource);
		}

		/// <summary>
		/// Override if you need to dispose of anything.  Note: You don't have to dispose of _subscriptions here, it will be done for you.  Be sure to call base.Dispose()!
		/// </summary>
		public override void Dispose()
		{
			_configurationUpdatedEventSubscription.Dispose();
			base.Dispose();
		}
	}
}

// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using GreenPipes;
using MassTransit;

namespace NewRelic.Providers.Wrapper.MassTransitLegacy
{
    public  class NewRelicPipeSpecification : IPipeSpecification<ConsumeContext>, IPipeSpecification<PublishContext>, IPipeSpecification<SendContext>
    {
        Agent.Api.IAgent _agent;

        public NewRelicPipeSpecification(Agent.Api.IAgent agent)
        {
            _agent = agent;
        }

        public IEnumerable<ValidationResult> Validate()
        {
            return Enumerable.Empty<ValidationResult>();
        }

        public void Apply(IPipeBuilder<ConsumeContext> builder)
        {
            builder.AddFilter(new NewRelicFilter(_agent));
        }

        public void Apply(IPipeBuilder<PublishContext> builder)
        {
            builder.AddFilter(new NewRelicFilter(_agent));
        }

        public void Apply(IPipeBuilder<SendContext> builder)
        {
            builder.AddFilter(new NewRelicFilter(_agent));
        }
    }
}

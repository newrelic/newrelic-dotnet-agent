// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NET8_0_OR_GREATER

using System.Threading.Tasks;
using GrpcGreet;
using Grpc.Core;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.Grpc;

public class GreeterService : Greeter.GreeterBase
{
    public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
    {
        return Task.FromResult(new HelloReply { Message = "Hello " + request.Name });
    }
}

#endif

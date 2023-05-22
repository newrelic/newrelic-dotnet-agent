﻿// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using Grpc.Core;
using System.Threading;
using NUnit.Framework;
using NewRelic.Agent.Core.DataTransport;
using System.Threading.Tasks;
using GrpcChannel = Grpc.Core.Channel;

namespace NewRelic.Agent.Core.GrpcWrapper.Tests
{
    [TestFixture]
    public class GrpcWrapperTests
    {
        internal class FakeGrpcStreamWriter : IClientStreamWriter<FakeGrpcRequest>
        {
            Exception _ex = null;

            private bool _complete = false;
            public bool IsComplete { get => _complete; }

            public FakeGrpcStreamWriter(Exception ex = null)
            {
                _ex = ex;
            }
            WriteOptions _flags;
            public WriteOptions WriteOptions
            {
                get => _flags;
                set { _flags = value; }
            }

            public Task CompleteAsync()
            {
                if (_ex != null)
                    throw _ex;

                _complete = true;
                return Task.Delay(0);
            }

            public Task WriteAsync(FakeGrpcRequest message)
            {
                if (_ex != null)
                    throw _ex;
                return Task.Delay(0);
            }
        }
        internal class FakeGrpcStreamReader : IAsyncStreamReader<FakeGrpcResponse>
        {
            public FakeGrpcResponse Current => throw new NotImplementedException();

            public Task CompleteAsync() => Task.Delay(0);

            public Task<bool> MoveNext(CancellationToken cancellationToken) => throw new NotImplementedException();

            public Task WriteAsync(FakeGrpcResponse message) => Task.Delay(0);
        }
        internal class FakeGrpcWrapper : GrpcWrapper<FakeGrpcRequest, FakeGrpcResponse>
        {
            private Exception _ex;
            protected bool _shouldConnectSucceed = true;

            public FakeGrpcWrapper() { }

            public FakeGrpcWrapper(Exception ex)
            {
                _ex = ex;
            }
            public FakeGrpcWrapper(bool connectShouldSucceed, Exception ex)
            {
                _shouldConnectSucceed = connectShouldSucceed;
                _ex = ex;
            }

            protected override AsyncDuplexStreamingCall<FakeGrpcRequest, FakeGrpcResponse> CreateStreamsImpl(Channel channel, Metadata headers, int connectTimeoutMs, CancellationToken cancellationToken)
            {
                if (_ex != null)
                {
                    throw _ex;
                }
                
                return new AsyncDuplexStreamingCall<FakeGrpcRequest, FakeGrpcResponse>(new FakeGrpcStreamWriter(), new FakeGrpcStreamReader(), null, null, null, (o) => { }, null);
            }

            protected override bool TestConnect(GrpcChannel channel, int connectTimeoutMs, CancellationToken cancellationToken)
            {
                if (!_shouldConnectSucceed)
                {
                    // If we don't want the test to succeed, let it try the real thing, which should fail
                    return base.TestConnect(channel, connectTimeoutMs, cancellationToken);
                }
                return _shouldConnectSucceed;
            }
        }
        internal interface FakeGrpcRequest
        {
        }
        internal interface FakeGrpcResponse
        {
        }

        [Test]
        public void TestCreateChannel()
        {
            // Trying a real connection should fail
            var grpc = new FakeGrpcWrapper(false, null);
            Assert.IsFalse(grpc.CreateChannel("localhost", 0, false, null, 0, new CancellationToken()));
            Assert.IsFalse(grpc.IsConnected);

            // Even if it's SSL
            grpc = new FakeGrpcWrapper(false, null);
            Assert.IsFalse(grpc.CreateChannel("localhost", 0, true, null, 0, new CancellationToken()));
            Assert.IsFalse(grpc.IsConnected);

            // A fake connection should succeed
            grpc = new FakeGrpcWrapper();
            Assert.IsTrue(grpc.CreateChannel("localhost", 0, false, null, 0, new CancellationToken()));
            Assert.IsTrue(grpc.IsConnected);

            // A fake connection that throws an error should fail (error is eaten)
            grpc = new FakeGrpcWrapper(true, new Exception());
            Assert.IsFalse(grpc.CreateChannel("localhost", 0, false, null, 0, new CancellationToken()));
            Assert.IsFalse(grpc.IsConnected);
        }

        [Test]
        public void TestCreateStreams()
        {
            IClientStreamWriter<FakeGrpcRequest> requestStream;
            IAsyncStreamReader<FakeGrpcResponse> reponseStream;

            Assert.IsTrue(new FakeGrpcWrapper().CreateStreams(null, 0, new CancellationToken(), out requestStream, out reponseStream));

            Assert.IsFalse(new FakeGrpcWrapper(new GrpcWrapperChannelNotAvailableException()).CreateStreams(null, 0, new CancellationToken(), out requestStream, out reponseStream));

            Assert.Throws<GrpcWrapperException>(() => new FakeGrpcWrapper(true, new RpcException(Status.DefaultCancelled)).CreateStreams(null, 0, new CancellationToken(), out requestStream, out reponseStream));

            Assert.Throws<GrpcWrapperException>(() => new FakeGrpcWrapper(true, new Exception("Oh no!")).CreateStreams(null, 0, new CancellationToken(), out requestStream, out reponseStream));
        }

        [Test]
        public void TestTrySendData()
        {
            var grpc = new FakeGrpcWrapper();
            var stream = new FakeGrpcStreamWriter();

            Assert.IsTrue(grpc.TrySendData(stream, null, 0, new CancellationToken()));

            stream = new FakeGrpcStreamWriter(new InvalidOperationException("Request stream has already been completed."));
            Assert.Throws<GrpcWrapperStreamNotAvailableException>(() => grpc.TrySendData(stream, null, 0, new CancellationToken()));

            stream = new FakeGrpcStreamWriter(new RpcException(Status.DefaultCancelled));
            Assert.Throws<GrpcWrapperException>(() => grpc.TrySendData(stream, null, 0, new CancellationToken()));

            stream = new FakeGrpcStreamWriter(new Exception());
            Assert.Throws<GrpcWrapperException>(() => grpc.TrySendData(stream, null, 0, new CancellationToken()));

            stream = new FakeGrpcStreamWriter(new Exception());
            Assert.IsFalse(grpc.TrySendData(stream, null, 0, new CancellationToken(true)));

        }

        [Test]
        public void TestShutdown()
        {
            var grpc = new FakeGrpcWrapper();
            // No active connection, will just exit
            Assert.DoesNotThrow(() => grpc.Shutdown());
            Assert.IsFalse(grpc.IsConnected);

            // "Active" connection, will attempt to shut down
            grpc.CreateChannel("localhost", 0, false, null, 0, new CancellationToken());
            Assert.DoesNotThrow(() => grpc.Shutdown());
        }

        [Test]
        public void TestCloseStream()
        {
            // Close the fake stream successfully
            var grpc = new FakeGrpcWrapper();
            var goodStream = new FakeGrpcStreamWriter();
            Assert.DoesNotThrow(() => grpc.TryCloseRequestStream(goodStream));
            Assert.IsTrue(goodStream.IsComplete);

            // It eats the error
            var badStream = new FakeGrpcStreamWriter(new GrpcWrapperChannelNotAvailableException());
            Assert.DoesNotThrow(() => grpc.TryCloseRequestStream(badStream));
            Assert.IsFalse(badStream.IsComplete);

            badStream = new FakeGrpcStreamWriter(new GrpcWrapperChannelNotAvailableException("Oh no!", new Exception()));
            Assert.DoesNotThrow(() => grpc.TryCloseRequestStream(badStream));
            Assert.IsFalse(badStream.IsComplete);
        }
    }
}

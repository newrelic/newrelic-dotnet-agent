// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KafkaTestApp.Controllers;

[ApiController]
[Route("kafka")]
public class KafkaController : ControllerBase
{
    private readonly ILogger<KafkaController> _logger;
    private readonly Producer _producer;
    private readonly IConsumerSignalService _consumerSignal;
    private readonly IServiceProvider _serviceProvider;

    public KafkaController(ILogger<KafkaController> logger,
        Producer producer,
        IConsumerSignalService consumerSignal,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _producer = producer;
        _consumerSignal = consumerSignal;
        _serviceProvider = serviceProvider;
    }

    [HttpGet("produce")]
    public Task<IActionResult> Produce() => ProduceOnCluster(Program.PrimaryCluster);

    [HttpGet("produce2")]
    public Task<IActionResult> Produce2() => ProduceOnCluster(Program.SecondaryCluster);

    [HttpGet("produceasync")]
    public async Task<string> ProduceAsync()
    {
        await _producer.ProduceAsync();
        return "Complete";
    }

    [HttpGet("produceasyncwithexistingheaders")]
    public async Task<string> ProduceAsyncWithExistingHeaders()
    {
        await _producer.ProduceAsyncWithExistingHeaders();
        return "Complete";
    }

    [HttpGet("bootstrap_server")]
    public IActionResult GetBootstrapServer() => BootstrapServerOfCluster(Program.PrimaryCluster);

    [HttpGet("bootstrap_server2")]
    public IActionResult GetBootstrapServer2() => BootstrapServerOfCluster(Program.SecondaryCluster);

    [HttpGet("consumewithtimeout")]
    public Task<IActionResult> ConsumeWithTimeoutAsync() =>
        ConsumeOnCluster(Program.PrimaryCluster, ConsumptionMode.Timeout);

    [HttpGet("consumewithtimeout2")]
    public Task<IActionResult> ConsumeWithTimeout2Async() =>
        ConsumeOnCluster(Program.SecondaryCluster, ConsumptionMode.Timeout);

    [HttpGet("consumewithcancellationtoken")]
    public async Task<string> ConsumeWithCancellationTokenAsync()
    {
        await _consumerSignal.RequestConsumeAsync(ConsumptionMode.CancellationToken);
        return "Complete";
    }

    [HttpGet("producewithcustomstatistics")]
    public async Task<string> ProduceWithCustomStatistics()
    {
        await _producer.ProduceWithCustomStatistics();
        return "Complete";
    }

    [HttpGet("customstatisticsstatus")]
    public string GetCustomStatisticsStatus()
    {
        return $"Producer callbacks: {CustomerStatisticsCallbacks.ProducerCallbackCount}, Consumer callbacks: {CustomerStatisticsCallbacks.ConsumerCallbackCount}";
    }

    private async Task<IActionResult> ProduceOnCluster(int cluster)
    {
        var producer = ResolveProducer(cluster);
        if (producer == null)
            return StatusCode((int)HttpStatusCode.ServiceUnavailable);

        await producer.Produce();
        return Ok("Complete");
    }

    private async Task<IActionResult> ConsumeOnCluster(int cluster, ConsumptionMode mode)
    {
        var consumerSignal = ResolveConsumerSignal(cluster);
        if (consumerSignal == null)
            return StatusCode((int)HttpStatusCode.ServiceUnavailable);

        await consumerSignal.RequestConsumeAsync(mode);
        return Ok("Complete");
    }

    private IActionResult BootstrapServerOfCluster(int cluster)
    {
        var bootstrapServer = Program.GetBootstrapServer(cluster);
        if (bootstrapServer == null)
            return StatusCode((int)HttpStatusCode.ServiceUnavailable);

        return Ok(bootstrapServer);
    }

    // The primary cluster's clients are registered unkeyed; every other cluster is keyed by
    // its cluster number and is absent unless compose configured that broker.
    private Producer ResolveProducer(int cluster) =>
        cluster == Program.PrimaryCluster ? _producer : _serviceProvider.GetKeyedService<Producer>(cluster);

    private IConsumerSignalService ResolveConsumerSignal(int cluster) =>
        cluster == Program.PrimaryCluster ? _consumerSignal : _serviceProvider.GetKeyedService<Consumer>(cluster);
}

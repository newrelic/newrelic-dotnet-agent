// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Net.Http;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AzureFunctionInProcApplication
{
    public class ServiceBusTriggerFunction
    {
        [FunctionName("ServiceBusTriggerFunction")]
        public void Run([ServiceBusTrigger("func-test-queue")] ServiceBusReceivedMessage message, ILogger log)
        {
            var jsonMessage = JsonConvert.SerializeObject(message, Formatting.Indented);

            log.LogInformation($"C# ServiceBus queue trigger function processed message: {jsonMessage}");
        }

        /// <summary>
        /// Takes input from an HTTP trigger and sends a Service Bus message, which should then trigger ServiceBusTriggerFunction automagically
        /// </summary>
        [FunctionName("HttpTrigger_SendServiceBusMessage")]
        [return: ServiceBus("func-test-queue")]
        public async Task<ServiceBusMessage> ServiceBusOutput([HttpTrigger(AuthorizationLevel.Admin, "post", Route = null)] HttpRequestMessage requestMessage, ILogger log)
        {
            var input = await requestMessage!.Content!.ReadAsStringAsync();

            var serviceBusMessage = new ServiceBusMessage(input);

            log.LogInformation($"C# function processed: {input} and sent a ServiceBus message ");
            return serviceBusMessage;
        }
    }
}

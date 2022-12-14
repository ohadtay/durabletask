// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace DurableTask.Samples.MonitoringTest
{
    using System;
    using System.Net.NetworkInformation;
    using System.Threading.Tasks;
    using DurableTask.Core;

    public sealed class MonitoringInput
    {
        public string host;
        public string filePath;

        public DateTime scheduledTime;
    }

    public sealed class MonitoringOutput
    {
        public DateTime executionTime;
        public DateTime scheduledTime;
    }

    public sealed class MonitoringTask : AsyncTaskActivity<MonitoringInput, MonitoringOutput>
    {
        protected override async Task<MonitoringOutput> ExecuteAsync(TaskContext context, MonitoringInput monitoringInput)
        {
            try
            {
                using var pinger = new Ping();
                await pinger.SendPingAsync(monitoringInput.host, 30_000);
            }
            catch (PingException)
            {
                // Discard PingExceptions and return false;
            }

            return new MonitoringOutput
            {
                executionTime = DateTime.UtcNow,
                scheduledTime = monitoringInput.scheduledTime
            };
        }
    }
}
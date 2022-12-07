// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace DurableTask.Samples.MonitoringTest
{
    using System;
    using System.Net.NetworkInformation;
    using System.Threading;
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

    public sealed class MonitoringTask : TaskActivity<MonitoringInput, MonitoringOutput>
    {
        private static Ping pinger = new Ping();

        protected override MonitoringOutput Execute(DurableTask.Core.TaskContext context, MonitoringInput monitoringInput)
        {
            try
            {
                pinger.Send(monitoringInput.host);
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
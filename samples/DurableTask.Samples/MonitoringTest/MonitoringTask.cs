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
        protected override MonitoringOutput Execute(DurableTask.Core.TaskContext context, MonitoringInput monitoringInput)
        {
            //pinging to the host described 
             // Console.WriteLine($"Execute: instance {context.OrchestrationInstance}, Thread id: '{Thread.CurrentThread.ManagedThreadId}'");
             // Console.WriteLine($"Execute: Pinging to host {monitoringInput.host}, instance {context.OrchestrationInstance}");
             // Ping pinger = null;
            
             // try
             // {
             //     pinger = new Ping();
             //     pinger.Send(monitoringInput.host);
             // }
             // catch (PingException)
             // {
             //     // Discard PingExceptions and return false;
             // }
             // finally
             // {
             //     if (pinger != null)
             //     {
             //         pinger.Dispose();
             //     }
             // }
             // Console.WriteLine($"\t\tExecute: '{context.OrchestrationInstance.InstanceId}', now: '{DateTime.Now}'");

             //The thread is sleeping for 5 seconds
             // Thread.Sleep(5000);
            return new MonitoringOutput
            {
                executionTime = DateTime.UtcNow,
                scheduledTime = monitoringInput.scheduledTime
            };
        }
    }
}
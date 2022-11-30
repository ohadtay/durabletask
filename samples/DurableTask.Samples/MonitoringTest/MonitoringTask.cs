// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace DurableTask.Samples.MonitoringTest
{
    using System;
    using System.Net.NetworkInformation;
    using System.Text;
    using System.Threading;
    using DurableTask.Core;

    public sealed class MonitoringInput
    {
        public string host;
    }
    
    public sealed class MonitoringTask : TaskActivity<MonitoringInput, string>
    {
        protected override string Execute(DurableTask.Core.TaskContext context, MonitoringInput monitoringInput)
        {
            //pinging to the host described 
            Console.WriteLine($"Execute: instance {context.OrchestrationInstance}, Thread id: '{Thread.CurrentThread.ManagedThreadId}'");
            Console.WriteLine($"Execute: Pingnig to host {monitoringInput.host}, instance {context.OrchestrationInstance}");
            bool pingable = false;
            Ping pinger = null;

            try
            {
                pinger = new Ping();
                PingReply reply = pinger.Send(monitoringInput.host);
                pingable = reply.Status == IPStatus.Success;
            }
            catch (PingException)
            {
                // Discard PingExceptions and return false;
            }
            finally
            {
                if (pinger != null)
                {
                    pinger.Dispose();
                }
            }
            
            Console.WriteLine($"Execute: instance {context.OrchestrationInstance}, Thread id: '{Thread.CurrentThread.ManagedThreadId}' finished");
            return pingable.ToString();
        }
    }
}
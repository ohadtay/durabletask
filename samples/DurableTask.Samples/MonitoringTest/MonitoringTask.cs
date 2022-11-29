// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace DurableTask.Samples.MonitoringTest
{
    using System;
    using System.Net.NetworkInformation;
    using System.Threading;
    using DurableTask.Core;

    public class MonitoringTask : TaskActivity<string, string>
    {
        protected override string Execute(DurableTask.Core.TaskContext context, string host)
        {
            //pinging to the host described 
            Console.WriteLine($"Pingnig to host {host}, instance {context.OrchestrationInstance}");
            bool pingable = false;
            Ping pinger = null;

            try
            {
                pinger = new Ping();
                PingReply reply = pinger.Send(host);
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

            System.Threading.Thread.Sleep(100);
            return pingable.ToString();
        }
    }
}
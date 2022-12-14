// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace DurableTask.Samples.MonitoringTest
{
    using System;
    using System.Net.NetworkInformation;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using DurableTask.Core;
    using Kusto.Cloud.Platform.Security;
    using Kusto.Data;
    using Kusto.Data.Net.Client;

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
                var kcsb = new KustoConnectionStringBuilder("https://manage-kustodf3.dev.kusto.windows.net;Fed=True")
                    .WithAadApplicationCertificateAuthentication(
                        "77daa54b-ea23-4f3a-8836-f644ddf9dab7",
                        CertificateUtilities.TryLoadCertificate(StoreLocation.CurrentUser, StoreName.My, X509FindType.FindByThumbprint, "D3EA9FD63C04E268861CD8A8831BD103FFFD04FD", true),
                        "72f988bf-86f1-41af-91ab-2d7cd011db47",
                        true);
                using var client = KustoClientFactory.CreateCslAdminProvider(kcsb);
                using var reader = await client.ExecuteControlCommandAsync("", ".show version");

                while (reader.Read())
                {
                    Console.WriteLine("manage-kustodf3={0}", reader.GetString(0));
                }
                // using var pinger = new Ping();
                // await pinger.SendPingAsync(monitoringInput.host, 30_000);
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
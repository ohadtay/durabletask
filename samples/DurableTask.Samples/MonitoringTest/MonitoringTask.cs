// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace DurableTask.Samples.MonitoringTest
{
    using System;
    using System.Data;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using DurableTask.Core;
    using Kusto.Cloud.Platform.Security;
    using Kusto.Data;
    using Kusto.Data.Common;
    using Kusto.Data.Net.Client;

    public sealed class MonitoringInput
    {
        public string Host;

        public DateTime ScheduledTime;
    }

    public sealed class MonitoringOutput
    {
        public DateTime ExecutionTime;
        public DateTime ScheduledTime;
    }

    public sealed class MonitoringTask : AsyncTaskActivity<MonitoringInput, MonitoringOutput>
    {
        static readonly X509Certificate2 cert = CertificateUtilities.TryLoadCertificate(StoreLocation.CurrentUser, StoreName.My, X509FindType.FindByThumbprint, "D3EA9FD63C04E268861CD8A8831BD103FFFD04FD", true);

        protected override async Task<MonitoringOutput> ExecuteAsync(TaskContext context, MonitoringInput monitoringInput)
        {
            try
            {
                KustoConnectionStringBuilder kcsb = new KustoConnectionStringBuilder(monitoringInput.Host + ";Fed=True")
                    .WithAadApplicationCertificateAuthentication(
                        "77daa54b-ea23-4f3a-8836-f644ddf9dab7",
                        cert,
                        "72f988bf-86f1-41af-91ab-2d7cd011db47",
                        true);
                using ICslAdminProvider client = KustoClientFactory.CreateCslAdminProvider(kcsb);
                using IDataReader reader = await client.ExecuteControlCommandAsync("", ".show version");

                // while (reader.Read())
                // {
                //     Console.WriteLine("\t{0}={1}", monitoringInput.Host, reader.GetString(0));
                // }
            }
            catch
            {
                // Console.WriteLine("\t\t" + ex.Message);
                // ignored
            }

            return new MonitoringOutput
            {
                ExecutionTime = DateTime.UtcNow,
                ScheduledTime = monitoringInput.ScheduledTime
            };
        }
    }
}
//  ----------------------------------------------------------------------------------
//  Copyright Microsoft Corporation
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//  http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ----------------------------------------------------------------------------------

[assembly: System.Runtime.InteropServices.ComVisible(false)]

namespace DurableTask.Samples
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Threading.Tasks;
    using DurableTask.Core;
    using DurableTask.Netherite;
    using DurableTask.Samples.MonitoringTest;
    using Microsoft.Extensions.Logging;

    internal class Program
    {
        static readonly Options ArgumentOptions = new Options();

        [STAThread]
        static async Task Main(string[] args)
        {
            if (CommandLine.Parser.Default.ParseArgumentsStrict(args, ArgumentOptions))
            {
                var settings = new NetheriteOrchestrationServiceSettings
                {
                    PartitionCount = ArgumentOptions.NumberOfPartition,
                    HubName = "MonitoringHub",
                    MaxConcurrentOrchestratorFunctions = ArgumentOptions.MaxConcurrentTaskOrchestrationWorkItems,
                    MaxConcurrentActivityFunctions = ArgumentOptions.MaxConcurrentTaskActivityWorkItems,
                    StorageConnectionName = "StorageConnectionString",
                    EventHubsConnectionName = "EventHubConnectionString"
                };

                settings.Validate(ConfigurationManager.AppSettings.Get);

                var loggerFactory = new LoggerFactory();

                var orchestrationService = new NetheriteOrchestrationService(settings, loggerFactory);
                if (ArgumentOptions.CleanHub)
                {
                    try
                    {
                        Console.WriteLine("Deleting all storage information");
                        // Delete all Azure Storage tables, blobs, and queues in the task hub
                        await ((IOrchestrationService)orchestrationService).DeleteAsync();

                        // Wait for a minute since Azure Storage won't let us immediately
                        // recreate resources with the same names as before.
                        await Task.Delay(TimeSpan.FromMinutes(1));
                        Console.WriteLine("Finished deleting storage information");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Could not delete the orchestration. Error message: {e.Message}");
                    }
                }
                
                await ((IOrchestrationService)orchestrationService).CreateIfNotExistsAsync();

                if (ArgumentOptions.ShouldSetUpWorkers)
                {
                    var setUpTime = DateTime.UtcNow;
                    await WorkerMainTaskAsync(settings, loggerFactory, setUpTime);
                    Console.WriteLine("Press enter to exit the program");
                    Console.ReadLine();
                    Console.WriteLine("Execution is over");
                }
                else
                {
                    await OrchestrationsTaskAsync(new NetheriteOrchestrationService(settings, loggerFactory));
                }
            }
        }

        static async Task WorkerMainTaskAsync(NetheriteOrchestrationServiceSettings settings, ILoggerFactory loggerFactory, DateTime startTime)
        {
            var workersList = new List<TaskHubWorker>(ArgumentOptions.NumberOfWorkers);
            var workersTasks = new List<Task>();
            for (var i = 0; i < ArgumentOptions.NumberOfWorkers; i++)
            {
                //generating one instance of worker
                var taskHubWorker = new TaskHubWorker(new NetheriteOrchestrationService(settings, loggerFactory));

                taskHubWorker.AddTaskOrchestrations(
                    typeof(MonitoringOrchestration)
                );

                taskHubWorker.AddTaskActivities(
                    typeof(MonitoringTask)
                );

                workersList.Add(taskHubWorker);
                // Console.WriteLine($"Starting worker {taskHubWorker.GetHashCode()}");
                workersTasks.Add(taskHubWorker.StartAsync());
            }

            await Task.WhenAll(workersTasks);

            Console.WriteLine("Press any key to stop the worker and finish the run.");
            Console.ReadLine();

            Console.WriteLine($"Start time: {startTime}, Worker run for {DateTime.UtcNow - startTime}, "
                + $"Total number of orchestrations {MonitoringOrchestration.totalCounter}, "
                + $"Number of orchestrations failures {MonitoringOrchestration.failureOrchestrationCounter}, "
                + $"Number of timer failures {MonitoringOrchestration.failureTimerCounter}, "
                + $"Success rate {Decimal.ToDouble(MonitoringOrchestration.totalCounter - MonitoringOrchestration.failureOrchestrationCounter - MonitoringOrchestration.failureTimerCounter) * 100.0 / MonitoringOrchestration.totalCounter}");
            
            // waiting for all the workers
            try
            {
                Console.WriteLine("Stopping all the workers");
                foreach (TaskHubWorker taskHubWorker in workersList)
                {
                    Console.WriteLine($"Stopping worker {taskHubWorker.GetHashCode()}");
                    await taskHubWorker.StopAsync(true);
                }

                Console.WriteLine("Finished to stop the workers");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Worker got exception. Error message: {e.Message}");
            }
        }

        static async Task OrchestrationsTaskAsync(IOrchestrationServiceClient orchestrationServiceClient)
        {
            Console.WriteLine("Generating new orchestrations");
            Console.WriteLine("Press A for generating a new orchestration and D for delete one orchestration");
            var taskHubClient = new TaskHubClient(orchestrationServiceClient);
            var random = new Random();

            var instances = new List<OrchestrationInstance>();

            ConsoleKeyInfo input;
            do
            {
                input = Console.ReadKey();
                Console.WriteLine();

                if (input.Key == ConsoleKey.A)
                {
                    await AddInstances(instances, taskHubClient);
                    Console.WriteLine($"Total {instances.Count} orchestrations");
                }

                if (input.Key == ConsoleKey.D)
                {
                    Console.WriteLine();
                    if (instances.Count > 0)
                    {
                        await DeleteInstances(instances, random, taskHubClient);
                    }
                    else
                    {
                        Console.WriteLine($"Orchestration list is empty, removing nothing");
                    }
                }
            }
            while (input.Key != ConsoleKey.Q);

            //terminating all the orchestrations
            var terminatingTasks = new List<Task>();
            foreach (OrchestrationInstance instance in instances)
            {
                terminatingTasks.Add(taskHubClient.TerminateInstanceAsync(instance));
                Console.WriteLine($"Terminating instance: {instance.InstanceId}");
            }

            await Task.WhenAll(terminatingTasks);
        }

        static async Task AddInstances(List<OrchestrationInstance> instances, TaskHubClient taskHubClient)
        {
            Console.WriteLine("Enter number of instances to add, if want to quit press 'escape'");
            string numberInstances = Console.ReadLine();

            var tasks = new List<Task<OrchestrationInstance>>();
            if (Int32.TryParse(numberInstances, out int numberOfInstancesToAdd) && numberOfInstancesToAdd > 0)
            {
                ConsoleKeyInfo input;
                
                for (var j = 0; j < numberOfInstancesToAdd; j++)
                {
                    if (Console.KeyAvailable)
                    {
                        input = Console.ReadKey();
                        if (input.Key == ConsoleKey.Escape)
                        {
                            break;
                        }
                    }

                    Console.WriteLine($"Adding orchestration {j + 1}/{numberOfInstancesToAdd}");
                    string host = Hosts[(instances.Count + j + 1) % Hosts.Length];
                    
                    tasks.Add(GenerateNewOrchestration(taskHubClient, host));
                    
                    if (j % 10 == 9) await AddOrchestrationTasks(instances, tasks);
                }
            }

            if (tasks.Count > 0)
            {
                await AddOrchestrationTasks(instances, tasks);
            }
        }

        static async Task AddOrchestrationTasks(List<OrchestrationInstance> instances, List<Task<OrchestrationInstance>> tasks)
        {
            OrchestrationInstance[] results = await Task.WhenAll(tasks);
            foreach (OrchestrationInstance instance in results)
            {
                instances.Add(instance);
            }

            tasks.Clear();
        }

        static async Task DeleteInstances(List<OrchestrationInstance> instances, Random random, TaskHubClient taskHubClient)
        {
            Console.WriteLine("Enter number of instances to remove");
            string numberInstances = Console.ReadLine();

            if (Int32.TryParse(numberInstances, out int instancesToRemove) && instancesToRemove > 0 && instances.Count - instancesToRemove >= 0)
            {
                var tasks = new List<Task>();
                for (var j = 0; j < instancesToRemove; j++)
                {
                    int randomNumber = random.Next(0, instances.Count);
                    OrchestrationInstance instanceToRemove = instances[randomNumber];
                    Console.WriteLine($"Removing orchestration instance randomly. OrchestrationId: {instanceToRemove.InstanceId}");
                    //remove suspend
                    tasks.Add(taskHubClient.TerminateInstanceAsync(instanceToRemove));
                    instances.RemoveAt(randomNumber);
                    Console.WriteLine($"Removed orchestration instance: {instanceToRemove.InstanceId} successfully");
                    if (j % 9 == 0)
                    {
                        await Task.WhenAll(tasks);
                        tasks.Clear();
                    }
                }

                if (tasks.Count > 0)
                {
                    await Task.WhenAll(tasks);
                }
            }
        }

        static Task<OrchestrationInstance> GenerateNewOrchestration(TaskHubClient hubClient, string host)
        {
            string instanceId = ArgumentOptions.InstanceId ?? Guid.NewGuid().ToString();
            var monitoringInput = new MonitoringInput
            {
                Host = host,
                ScheduledTime = DateTime.UtcNow
            };
            string hostname = new Uri(host).Host;
            Console.WriteLine($"GenerateNewOrchestration: Creating new orchestration. Instance id: {instanceId} for host {hostname}");

            //creating a new instance
            Task<OrchestrationInstance> instance = hubClient.CreateOrchestrationInstanceAsync(typeof(MonitoringOrchestration), instanceId, monitoringInput);
            Console.WriteLine($"GenerateNewOrchestration: InstanceId {instanceId} for host {hostname}, created with status {instance.Status}");
            return instance;
        }

        static readonly string[] Hosts =
        {
            "https://dfvirtualcmhelper5.dev.kusto.windows.net",
            "https://dfwus2cmhelper.dev.kusto.windows.net",
            "https://dfwus3cmhelper.dev.kusto.windows.net",
            "https://dfwuscmhelper.westus.dev.kusto.windows.net",
            "https://eddiebkheetflwcmk.australiaeast.dev.kusto.windows.net",
            "https://elbirnbocore2.eastus.dev.kusto.windows.net",
            "https://elbirnbocore4.eastus.dev.kusto.windows.net",
            "https://elbirnbopecluster.westus2.dev.kusto.windows.net",
            "https://gallav3.eastus.dev.kusto.windows.net",
            "https://genevagdsrnd.westeurope.dev.kusto.windows.net",
            "https://hafeldba1.westeurope.dev.kusto.windows.net",
            "https://ingest-adiweidfeaaulx3.australiaeast.dev.kusto.windows.net",
            "https://ingest-afridman.eastus.dev.kusto.windows.net",
            "https://ingest-amitsha.westeurope.dev.kusto.windows.net",
            "https://ingest-ariadevint.eastus.dev.kusto.windows.net",
            "https://ingest-ariela.westeurope.dev.kusto.windows.net",
            "https://ingest-ariela3.westeurope.dev.kusto.windows.net",
            "https://ingest-asafdev.westeurope.dev.kusto.windows.net",
            "https://ingest-avivyanivdfeus1.eastus.dev.kusto.windows.net",
            "https://ingest-awsdemo.eastus.dev.kusto.windows.net",
            "https://ingest-bvteus2helper.dev.kusto.windows.net",
            "https://ingest-cmetoe10cmhelper.dev.kusto.windows.net",
            "https://ingest-cmetoe1cmhelper.dev.kusto.windows.net",
            "https://ingest-cmetoe2cmhelper.dev.kusto.windows.net",
            "https://ingest-cmetoe3cmhelper.dev.kusto.windows.net",
            "https://ingest-cmetoe4cmhelper.dev.kusto.windows.net",
            "https://ingest-cmetoe5cmhelper.dev.kusto.windows.net",
            "https://ingest-cmetoe6cmhelper.dev.kusto.windows.net",
            "https://ingest-cmetoe7cmhelper.dev.kusto.windows.net",
            "https://ingest-cmetoe9cmhelper.dev.kusto.windows.net",
            "https://ingest-df2cmhelper.dev.kusto.windows.net",
            "https://ingest-df3cmhelper.dev.kusto.windows.net",
            "https://ingest-df4cmhelper.dev.kusto.windows.net",
            "https://ingest-df5cmhelper.dev.kusto.windows.net",
            "https://ingest-df6cmhelper.dev.kusto.windows.net",
            "https://ingest-dfcmhelper.dev.kusto.windows.net",
            "https://ingest-dfeaaucmhelper.dev.kusto.windows.net",
            "https://ingest-dfeuscmhelper.dev.kusto.windows.net",
            "https://ingest-dfneuhelper.northeurope.dev.kusto.windows.net",
            "https://ingest-dfseacmhelper.dev.kusto.windows.net",
            "https://ingest-dfvirtualcmhelper5.dev.kusto.windows.net",
            "https://ingest-dfwus2cmhelper.dev.kusto.windows.net",
            "https://ingest-dfwus3cmhelper.dev.kusto.windows.net",
            "https://ingest-dfwuscmhelper.westus.dev.kusto.windows.net",
            "https://ingest-dolevtest12.westeurope.dev.kusto.windows.net",
            "https://ingest-eddiebkheetflwcmk.australiaeast.dev.kusto.windows.net",
            "https://ingest-elberytest.westeurope.dev.kusto.windows.net",
            "https://ingest-elbirnbocore2.eastus.dev.kusto.windows.net",
            "https://ingest-elbirnbocore4.eastus.dev.kusto.windows.net",
            "https://ingest-gallav3.eastus.dev.kusto.windows.net",
            "https://ingest-genevagdsrnd.westeurope.dev.kusto.windows.net",
            "https://ingest-hafeldba1.westeurope.dev.kusto.windows.net",
            "https://ingest-kustodmbvt.dev.kusto.windows.net",
            "https://ingest-kustomdperf.westeurope.dev.kusto.windows.net",
            "https://ingest-laaiintkusto.westeurope.dev.kusto.windows.net",
            "https://ingest-lihicluster.eastus.dev.kusto.windows.net",
            "https://ingest-linuxperf.westeurope.dev.kusto.windows.net",
            "https://ingest-logsbenchmarkdfperf.southeastasia.dev.kusto.windows.net",
            "https://ingest-lugoldbeforyoni.westeurope.dev.kusto.windows.net",
            "https://ingest-masha.westeurope.dev.kusto.windows.net",
            "https://ingest-mbrichko.westeurope.dev.kusto.windows.net",
            "https://ingest-michaelshikh02.australiaeast.dev.kusto.windows.net",
            "https://ingest-mispectodev.westeurope.dev.kusto.windows.net",
            "https://ingest-nasavion2.westeurope.dev.kusto.windows.net",
            "https://ingest-natinimn1.westeurope.dev.kusto.windows.net",
            "https://ingest-natinimn2.westeurope.dev.kusto.windows.net",
            "https://ingest-natinimn3.westeurope.dev.kusto.windows.net",
            "https://ingest-nibogerdev.eastus.dev.kusto.windows.net",
            "https://ingest-ohadtayvnet1.eastus.dev.kusto.windows.net",
            "https://ingest-omgharracluster.westeurope.dev.kusto.windows.net",
            "https://ingest-padftestcluster.eastus.dev.kusto.windows.net",
            "https://ingest-pafterfix32523.eastus.dev.kusto.windows.net",
            "https://ingest-parkerafdtest.eastus.dev.kusto.windows.net",
            "https://ingest-parkerelad.eastus.dev.kusto.windows.net",
            "https://ingest-parkerelad2.eastus.dev.kusto.windows.net",
            "https://ingest-ranclusternew.westeurope.dev.kusto.windows.net",
            "https://ingest-reflextestroyo.westeurope.dev.kusto.windows.net",
            "https://ingest-roshauli.australiaeast.dev.kusto.windows.net",
            "https://ingest-royo4.westeurope.dev.kusto.windows.net",
            "https://ingest-rpetoe1cmhelper.dev.kusto.windows.net",
            "https://ingest-rpetoe2cmhelper.dev.kusto.windows.net",
            "https://ingest-rpetoe3cmhelper.dev.kusto.windows.net",
            "https://ingest-rpetoe4cmhelper.dev.kusto.windows.net",
            "https://ingest-rpetoe5cmhelper.dev.kusto.windows.net",
            "https://ingest-rpetoe6cmhelper.dev.kusto.windows.net",
            "https://ingest-rpetoe7cmhelper.dev.kusto.windows.net",
            "https://ingest-rpetoeeus6cmhelper.dev.kusto.windows.net",
            "https://ingest-rpetoeeus7cmhelper.dev.kusto.windows.net",
            "https://ingest-sapirpaxton1.westeurope.dev.kusto.windows.net",
            "https://ingest-shanis0.westeurope.dev.kusto.windows.net",
            "https://ingest-shanis5.westeurope.dev.kusto.windows.net",
            "https://ingest-shikh01cmhelper.dev.kusto.windows.net",
            "https://ingest-shiraharnes0.westeurope.dev.kusto.windows.net",
            "https://ingest-tomerfx.westeurope.dev.kusto.windows.net",
            "https://ingest-tomerperf.eastus.dev.kusto.windows.net",
            "https://ingest-uridev5.westeurope.dev.kusto.windows.net",
            "https://ingest-valia.westeurope.dev.kusto.windows.net",
            "https://ingest-vberzinjuly1.eastus.dev.kusto.windows.net",
            "https://ingest-yahav2.westeurope.dev.kusto.windows.net",
            "https://ingest-yifatsweur.westeurope.dev.kusto.windows.net",
            "https://ingest-yifatsweurope.westeurope.dev.kusto.windows.net",
            "https://ingest-yonil3.westeurope.dev.kusto.windows.net",
            "https://ingest-zihamengine.westeurope.dev.kusto.windows.net",
            "https://ingest-zivckusto1.westeurope.dev.kusto.windows.net",
            "https://kuskusdf.dev.kusto.windows.net",
            "https://kustodmbvt.dev.kusto.windows.net",
            "https://laaiintkusto.westeurope.dev.kusto.windows.net",
            "https://lugoldbetestdf4.southeastasia.dev.kusto.windows.net",
            "https://lugoldbetestseaf2.southeastasia.dev.kusto.windows.net",
            "https://masha.westeurope.dev.kusto.windows.net",
            "https://mbrichko.westeurope.dev.kusto.windows.net",
            "https://michaelshikh02.australiaeast.dev.kusto.windows.net",
            "https://mispectodev.westeurope.dev.kusto.windows.net",
            "https://nasavion2.westeurope.dev.kusto.windows.net",
            "https://natinimn1.westeurope.dev.kusto.windows.net",
            "https://natinimn2.westeurope.dev.kusto.windows.net",
            "https://natinimn3.westeurope.dev.kusto.windows.net",
            "https://nibogerdev.eastus.dev.kusto.windows.net",
            "https://omgharra2.westeurope.dev.kusto.windows.net",
            "https://omgharracluster.westeurope.dev.kusto.windows.net",
            "https://ranclusternew.westeurope.dev.kusto.windows.net",
            "https://rannewcluster.westeurope.dev.kusto.windows.net",
            "https://reflextestroyo.westeurope.dev.kusto.windows.net",
            "https://roshauli.australiaeast.dev.kusto.windows.net",
            "https://royo4.westeurope.dev.kusto.windows.net",
            "https://rpetoe1cmhelper.dev.kusto.windows.net",
            "https://rpetoe2cmhelper.dev.kusto.windows.net",
            "https://rpetoe3cmhelper.dev.kusto.windows.net",
            "https://rpetoe4cmhelper.dev.kusto.windows.net",
            "https://rpetoe5cmhelper.dev.kusto.windows.net",
            "https://rpetoe6cmhelper.dev.kusto.windows.net",
            "https://rpetoe7cmhelper.dev.kusto.windows.net",
            "https://rpetoeeus6cmhelper.dev.kusto.windows.net",
            "https://rpetoeeus7cmhelper.dev.kusto.windows.net",
            "https://sapirpaxton1.westeurope.dev.kusto.windows.net",
            "https://sgitelman.westeurope.dev.kusto.windows.net",
            "https://shanis0.westeurope.dev.kusto.windows.net",
            "https://shanis5.westeurope.dev.kusto.windows.net",
            "https://shikh01cmhelper.dev.kusto.windows.net",
            "https://shiraharnes0.westeurope.dev.kusto.windows.net",
            "https://tomerfx.westeurope.dev.kusto.windows.net",
            "https://tomerperf.eastus.dev.kusto.windows.net",
            "https://uridev5.westeurope.dev.kusto.windows.net",
            "https://valia.westeurope.dev.kusto.windows.net",
            "https://vberzindecember1.westus2.dev.kusto.windows.net",
            "https://vberzinjuly1.eastus.dev.kusto.windows.net",
            "https://yahav2.westeurope.dev.kusto.windows.net",
            "https://yifatsweurope.westeurope.dev.kusto.windows.net",
            "https://yonil3.westeurope.dev.kusto.windows.net",
            "https://zihamengine.westeurope.dev.kusto.windows.net",
            "https://zivckusto1.westeurope.dev.kusto.windows.net"
        };
    }
}
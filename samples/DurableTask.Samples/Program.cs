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
    using System.Diagnostics.Tracing;
    using System.Threading.Tasks;
    using DurableTask.AzureStorage;
    using DurableTask.Core;
    using DurableTask.Core.Tracing;
    using DurableTask.Samples.MonitoringTest;
    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;

    internal class Program
    {
        static readonly Options ArgumentOptions = new Options();
        static ObservableEventListener eventListener;
        
        [STAThread]
        static async Task Main(string[] args)
        {
            eventListener = new ObservableEventListener();
            eventListener.LogToConsole();
            eventListener.EnableEvents(DefaultEventSource.Log, EventLevel.LogAlways);
            
            if (CommandLine.Parser.Default.ParseArgumentsStrict(args, ArgumentOptions))
            {
                string storageConnectionString = ConfigurationManager.AppSettings["StorageConnectionString"];
                string taskHubName = ConfigurationManager.AppSettings["taskHubName"];
                string filePath = ArgumentOptions.FilePath;

                var settings = new AzureStorageOrchestrationServiceSettings
                {
                    StorageAccountDetails = new StorageAccountDetails { ConnectionString = storageConnectionString },
                    TaskHubName = taskHubName,
                    MaxConcurrentTaskActivityWorkItems = ArgumentOptions.MaxConcurrentTaskActivityWorkItems,
                    MaxConcurrentTaskOrchestrationWorkItems = ArgumentOptions.MaxConcurrentTaskOrchestrationWorkItems,
                    MaxQueuePollingInterval = TimeSpan.FromSeconds(5),
                    PartitionCount = 16
                };
                
                var orchestrationServiceAndClient = new AzureStorageOrchestrationService(settings);
                
                if (ArgumentOptions.CreateHub)
                {
                    try
                    {
                        Console.WriteLine("Deleting all storage information");
                        // Delete all Azure Storage tables, blobs, and queues in the task hub
                        await orchestrationServiceAndClient.DeleteAsync();

                        // Wait for a minute since Azure Storage won't let us immediately
                        // recreate resources with the same names as before.
                        await Task.Delay(TimeSpan.FromMinutes(1));
                        Console.WriteLine("Finished deleting storage information");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Could not delete the orchestration. Error message: {e.Message}");
                    }
                    
                    // I want to throw exception if we do not succeed
                    await orchestrationServiceAndClient.CreateIfNotExistsAsync();
                }

                if (!ArgumentOptions.SkipWorker)
                {
                    await WorkerMainTaskAsync(orchestrationServiceAndClient);
                    
                    //Writing to the file will happen from the process of the worker
                    // UploadFileIntoBlob(storageConnectionString, filePath);
                }
                else
                {
                    await OrchestrationsTaskAsync(orchestrationServiceAndClient, filePath);
                }
            }
        }

        static async Task WorkerMainTaskAsync(AzureStorageOrchestrationService orchestrationServiceAndClient)
        {
            var workersList = new List<(Task, TaskHubWorker)>(ArgumentOptions.NumberOfWorkers);
            for (int i = 0; i < ArgumentOptions.NumberOfWorkers; i++)
            {
                //generating one instance of worker
                var taskHubWorker = new TaskHubWorker(orchestrationServiceAndClient);
                    
                taskHubWorker.AddTaskOrchestrations(
                    typeof(MonitoringOrchestration)
                );
                        
                taskHubWorker.AddTaskActivities(
                    new MonitoringTask()
                );

                var task = taskHubWorker.StartAsync();
                workersList.Add((task, taskHubWorker));
            }
            
            Console.WriteLine("Press any key to stop the worker and finish the run.");
            Console.ReadLine();
            
            // waiting for all the workers
            try
            {
                foreach (var pair in workersList)
                {
                    await pair.Item2.StopAsync(true);
                    await pair.Item1;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Worker got exception. Error message: {e.Message}");
            }

            Console.WriteLine("Press any to exit the program");
            Console.ReadLine();
            Console.WriteLine("Execution is over");
        }

        static async Task OrchestrationsTaskAsync(AzureStorageOrchestrationService orchestrationServiceAndClient, string filePath)
        {
            Console.WriteLine("Generating new orchestrations");
            Console.WriteLine("Press A for generating a new orchestration and D for delete one orchestration");
            var taskHubClient = new TaskHubClient(orchestrationServiceAndClient);
            var random = new Random();
            
            var instances = new List<OrchestrationInstance>();
            
            var hostList = new List<string>
            {
                "8.8.8.8", //google ping
                "168.63.129.16" //azure ping
            };

            var pingIndex = 0;
            ConsoleKeyInfo input;
            do
            {
                input = Console.ReadKey();
                Console.WriteLine();
                
                if (input.Key == ConsoleKey.A)
                {
                    pingIndex = AddingInstances(hostList, pingIndex, instances, taskHubClient, filePath);
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
            foreach (OrchestrationInstance instance in instances)
            {
                await taskHubClient.TerminateInstanceAsync(instance);
                Console.WriteLine($"Terminating instance: {instance.InstanceId}");
            }
        }

        static int AddingInstances(List<string> hostList, int i, List<OrchestrationInstance> instances, TaskHubClient taskHubClient, string filePath)
        {
            Console.WriteLine("Enter number of instances to add");
            string numberInstances = Console.ReadLine();

            if (Int32.TryParse(numberInstances, out int numberOfInstancesToAdd) && numberOfInstancesToAdd > 0)
            {
                for (int j = 0; j < numberOfInstancesToAdd; j++)
                {
                    string host = hostList[i];
                    var monitoringInput = new MonitoringInput
                    {
                        host = host,
                        filePath = filePath
                    };
                    
                    instances.Add(GenerateNewOrchestration(taskHubClient, monitoringInput));
                    i = i == 1 ? 0 : 1;
                }
            }

            return i;
        }

        static async Task DeleteInstances(List<OrchestrationInstance> instances, Random random, TaskHubClient taskHubClient)
        {
            Console.WriteLine("Enter number of instances to remove");
            string numberInstances = Console.ReadLine();

            if (Int32.TryParse(numberInstances, out int numberOfInstancesToAdd) && numberOfInstancesToAdd > 0 && instances.Count - numberOfInstancesToAdd >= 0)
            {
                for (int j = 0; j < numberOfInstancesToAdd; j++)
                {
                    int randomNumber = random.Next(0, instances.Count);
                    OrchestrationInstance instanceToRemove = instances[randomNumber];
                    Console.WriteLine($"Removing orchestration instance randomly. OrchestrationId: {instanceToRemove.InstanceId}");
                    await taskHubClient.SuspendInstanceAsync(instanceToRemove);
                    await taskHubClient.TerminateInstanceAsync(instanceToRemove);
                    instances.RemoveAt(randomNumber);
                    Console.WriteLine($"Removed orchestration instance: {instanceToRemove.InstanceId} successfully");
                }
            }
        }

        static OrchestrationInstance GenerateNewOrchestration(TaskHubClient hubClient, MonitoringInput input)
        {
            string instanceId = ArgumentOptions.InstanceId ?? Guid.NewGuid().ToString();
            Console.WriteLine($"GenerateNewOrchestration: Creating new orchestration. Instance id: {instanceId} with input {input}");

            //creating a new instance
            var instance = hubClient.CreateOrchestrationInstanceAsync(typeof(MonitoringOrchestration), instanceId, input).Result;
            Console.WriteLine($"GenerateNewOrchestration: InstanceId {instanceId}, created with status {instance}");
            return instance;
        }
    }
}

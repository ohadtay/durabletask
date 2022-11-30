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
    
    internal class ProgramTest
    {
        static readonly Options ArgumentOptions = new Options();
        static ObservableEventListener eventListener;
        
        static OrchestrationInstance GenerateNewOrchestration(TaskHubClient hubClient, MonitoringInput input, int i)
        {
            string instanceId = ArgumentOptions.InstanceId ?? Guid.NewGuid().ToString();
            instanceId += $"_{i}";
            Console.WriteLine($"GenerateNewOrchestration: Creating new orchestration. Instance id: {instanceId} with input {input}");

            //creating a new instance
            var instance = hubClient.CreateOrchestrationInstanceAsync(typeof(MonitoringOrchestration), instanceId, input).Result;
            Console.WriteLine($"GenerateNewOrchestration: InstanceId {instanceId}, created with status {instance}");
            return instance;
        }
        
        [STAThread]
        static void Main(string[] args)
        {
            eventListener = new ObservableEventListener();
            eventListener.LogToConsole();
            eventListener.EnableEvents(DefaultEventSource.Log, EventLevel.LogAlways);
            
            var instances = new List<OrchestrationInstance>();
            
            var pingList = new List<string>
            {
                "8.8.8.8",
                "168.63.129.16"
            };
            
            if (CommandLine.Parser.Default.ParseArgumentsStrict(args, ArgumentOptions))
            {
                string storageConnectionString = GetSetting("StorageConnectionString");
                string taskHubName = ConfigurationManager.AppSettings["taskHubName"];
                
                var settings = new AzureStorageOrchestrationServiceSettings
                {
                    StorageAccountDetails = new StorageAccountDetails { ConnectionString = storageConnectionString },
                    TaskHubName = taskHubName,
                    MaxConcurrentTaskActivityWorkItems = 101,
                    MaxConcurrentTaskOrchestrationWorkItems = 12
                };
        
                var orchestrationServiceAndClient = new AzureStorageOrchestrationService(settings);
                var taskHubClient = new TaskHubClient(orchestrationServiceAndClient);
                var taskHubWorker = new TaskHubWorker(orchestrationServiceAndClient);
                
                if (ArgumentOptions.CreateHub)
                {
                    orchestrationServiceAndClient.CreateIfNotExistsAsync().Wait();
                }
                
                if (!ArgumentOptions.SkipWorker)
                {
                    //generating one instance of worker
                    
                    taskHubWorker.AddTaskOrchestrations(
                        typeof(MonitoringOrchestration)
                    );
                        
                    taskHubWorker.AddTaskActivities(
                        new MonitoringTask()
                    );
                }
                else
                {
                    Console.WriteLine("Skip Worker");
                }
                
                Task worker = taskHubWorker.StartAsync();
                int shouldStop = 0;
                int i = 0;
                //generating new instances of orchestration 
                do
                {
                    i = i == 1 ? 0 : 1;
                    string ping = pingList[i];
                    
                    var monitoringInput = new MonitoringInput
                    {
                        host = ping,
                    };
                    
                    instances.Add(GenerateNewOrchestration(taskHubClient, monitoringInput, shouldStop));
                    shouldStop++;
                }
                while (shouldStop < 5);

                System.Threading.Thread.Sleep(50000);
                // waiting for all the workers
                try
                {
                    taskHubWorker.StopAsync(true).Wait();
                    worker.Wait();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Worker got exception. Error message: {e.Message}");
                }
                
                //terminating all the orchestrations
                foreach (var instance in instances)
                {
                    taskHubClient.TerminateInstanceAsync(instance).Wait();
                    Console.WriteLine($"Terminating instance: {instance.InstanceId}");
                }

                Console.WriteLine("Execution is over");
                Console.WriteLine("Press any key to quit.");
                Console.ReadLine();
            }
        }
        
        public static string GetSetting(string name)
        {
            string value = Environment.GetEnvironmentVariable("DurableTaskTest" + name);
            if (string.IsNullOrWhiteSpace(value))
            {
                value = ConfigurationManager.AppSettings.Get(name);
            }
        
            return value;
        }
    }
}

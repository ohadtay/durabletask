// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace DurableTask.Samples.MonitoringTest
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DurableTask.Core;

    public sealed class LastOrchestrationData
    {
        public bool shouldVerify;
        public DateTime? lastTimeOperationEnded;
    }
    
    public class MonitoringOrchestration : TaskOrchestration<string,MonitoringInput>
    {
        static Dictionary<string, LastOrchestrationData> s_LastOrchestrationFinishedTime = new Dictionary<string, LastOrchestrationData>();
        
        public override async Task<string> RunTask(OrchestrationContext context, MonitoringInput input)
        {
            try
            {
                var instanceId = context.OrchestrationInstance.InstanceId;
                
                var taskDedicatedTimeSpan = !s_LastOrchestrationFinishedTime.ContainsKey(instanceId)
                    ? TimeSpan.FromSeconds(10) 
                    : TimeSpan.FromSeconds(10) - (context.CurrentUtcDateTime - (s_LastOrchestrationFinishedTime[instanceId].lastTimeOperationEnded ?? context.CurrentUtcDateTime));
                
                Console.WriteLine($"Instance: '{context.OrchestrationInstance}' taskDedicatedTimeSpan: '{taskDedicatedTimeSpan}'");
                
                // var workerDedicatedTimeSpan = TimeSpan.FromSeconds(10);

                // await VerifyWorkerTime(context, input, workerDedicatedTimeSpan); // checks if we manage to pick the orchestration on time (worker check)

                if (!s_LastOrchestrationFinishedTime.ContainsKey(instanceId) || s_LastOrchestrationFinishedTime[instanceId].shouldVerify)
                {
                    await FileWriter.FileWriteAsync(input.filePath, $"Orchestration id '{instanceId}', start time contextCurrentTime: '{context.CurrentUtcDateTime}'");
                    if (!s_LastOrchestrationFinishedTime.ContainsKey(instanceId))
                    {
                        s_LastOrchestrationFinishedTime.Add(instanceId, new LastOrchestrationData()
                        {
                            shouldVerify = false,
                            lastTimeOperationEnded = null
                        });
                    }
                    else
                    {
                        s_LastOrchestrationFinishedTime[instanceId].shouldVerify =  false;
                    }
                }

                var output = context.ScheduleTask<string>(typeof(MonitoringTask), input);
                Task timer = context.CreateTimer(context.CurrentUtcDateTime.Add(taskDedicatedTimeSpan),
                    "timer1");

                await Task.WhenAll(timer);
                
                // Checking if we manage to complete the orchestration on time.
                await FileWriter.FileWriteAsync(input.filePath, $"Orchestration id '{instanceId}', finish time contextCurrentTime: '{context.CurrentUtcDateTime}'");
                s_LastOrchestrationFinishedTime[instanceId].shouldVerify = true;
                s_LastOrchestrationFinishedTime[instanceId].lastTimeOperationEnded = context.CurrentUtcDateTime;

                if (!output.IsCompleted)
                {
                    // request timed out, do some compensating action
                    await FileWriter.FileWriteAsync(input.filePath, $"RunTask: Timer got timed out and the task did not complete. instance {context.OrchestrationInstance}");
                }
                else
                {
                    // orchestration completion
                    Console.WriteLine($"RunTask: Pinging to host {input}, result {output.Result}, instance {context.OrchestrationInstance}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"RunTask: Error occured {e.Message}");
            }
            finally
            {
                context.ContinueAsNew(input);
            }

            return null;
        }

        // static async Task VerifyWorkerTime(OrchestrationContext context, MonitoringInput input, TimeSpan workerDedicatedTimeSpan)
        // {
        //     if (s_LastOrchestrationFinishedTime.ContainsKey(context.OrchestrationInstance.InstanceId))
        //     {
        //         if (s_LastOrchestrationFinishedTime[context.OrchestrationInstance.InstanceId].shouldVerfiy == false)
        //         {
        //             return;
        //         }
        //         
        //         s_LastOrchestrationFinishedTime[context.OrchestrationInstance.InstanceId].shouldVerfiy = false;
        //
        //         var instanceId = context.OrchestrationInstance.InstanceId;
        //         // var workerPickingTime = context.CurrentUtcDateTime - s_LastOrchestrationFinishedTime[instanceId].operationEndedDateTime;
        //         //
        //         // if (workerPickingTime > workerDedicatedTimeSpan)
        //         // {
        //         //     await FileWriter.FileWriteAsync(input.filePath, $"$$ALERT RunTask: OrchestrationId: worker didn't manage to take orchestration {instanceId} on dedicated time: {workerDedicatedTimeSpan}");
        //         //     s_LastOrchestrationFinishedTime[instanceId].numberOfWorkerFailures++;
        //         // }
        //
        //         // var numberOfFailures = s_LastOrchestrationFinishedTime[instanceId].numberOfWorkerFailures;
        //         // var numberOfRuns = s_LastOrchestrationFinishedTime[instanceId].numberOfRuns;
        //         // var numberOfTaskFailures = s_LastOrchestrationFinishedTime[instanceId].numberOfTaskFailures;
        //         
        //         // await FileWriter.FileWriteAsync(input.filePath, $"Orchestration id '{instanceId}', number of worker failures: '{numberOfFailures}', number of task failures: '{numberOfTaskFailures}', total runs for this orchestration '{numberOfRuns}', contextCurrentTime: '{context.CurrentUtcDateTime}'");
        //     }
        //     else
        //     {
        //         s_LastOrchestrationFinishedTime[context.OrchestrationInstance.InstanceId] = new OrchestrationInformation()
        //         {
        //             // numberOfWorkerFailures = 0,
        //             shouldVerfiy = false,
        //             // numberOfTaskFailures = 0,
        //             // numberOfRuns = 0
        //         };
        //     }
        // }
    }
}
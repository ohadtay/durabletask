// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace DurableTask.Samples.MonitoringTest
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DurableTask.Core;

    public class MonitoringOrchestration : TaskOrchestration<string,MonitoringInput>
    {
        static Dictionary<string, DateTime> s_LastOrchestrationTime = new Dictionary<string, DateTime>();
        
        public override async Task<string> RunTask(OrchestrationContext context, MonitoringInput input)
        {
            try
            {
                var maxDelta = TimeSpan.FromSeconds(9);
                var expectedDelta = maxDelta + TimeSpan.FromSeconds(1);
                
                await FileWriter.FileWriteAsync(input.filePath, $"Test ExecutionId '{context.OrchestrationInstance.ExecutionId}'");

                if (s_LastOrchestrationTime.ContainsKey(context.OrchestrationInstance.InstanceId))
                {
                    if (context.CurrentUtcDateTime - s_LastOrchestrationTime[context.OrchestrationInstance.InstanceId] > expectedDelta)
                    {
                        await FileWriter.FileWriteAsync(input.filePath, $"$$ALERT RunTask: OrchestrationId: {context.OrchestrationInstance.InstanceId} did not manage to run at {expectedDelta} between runs");
                    }
                    
                    s_LastOrchestrationTime[context.OrchestrationInstance.InstanceId] = context.CurrentUtcDateTime;
                }
                else
                {
                    s_LastOrchestrationTime.Add(context.OrchestrationInstance.InstanceId, context.CurrentUtcDateTime);
                }
                
                Console.WriteLine($"RunTask: Pinging to host {input.host}, instance {context.OrchestrationInstance}, timeStamp:{context.CurrentUtcDateTime}");
                DateTime startTime = context.CurrentUtcDateTime;
                Console.WriteLine($"RunTask: Operation started at {startTime}, instance {context.OrchestrationInstance}");
                
                Task timer = context.CreateTimer(
                    context.CurrentUtcDateTime.Add(maxDelta),
                    "timer1");

                Task<string> output = context.ScheduleTask<string>(typeof(MonitoringTask), input);

                await Task.WhenAll(timer);

                if (!output.IsCompleted)
                {
                    // request timed out, do some compensating action
                    await FileWriter.FileWriteAsync(input.filePath, $"RunTask: Timer got timed out and the task did not complete. instance {context.OrchestrationInstance}");
                    Console.WriteLine();
                }
                else
                {
                    // orchestration completion
                    Console.WriteLine($"RunTask: Pinging to host {input}, result {output.Result}, instance {context.OrchestrationInstance}");
                }

                DateTime endTime = context.CurrentUtcDateTime;
                bool isBigger = (endTime - startTime) > maxDelta;
                Console.WriteLine($"RunTask: Operation ended at {endTime}, deltaIsBiggerThanNeeded: '{isBigger}', instance {context.OrchestrationInstance}");
            }
            catch (Exception e)
            {
                await FileWriter.FileWriteAsync(input.filePath, $"RunTask: An exception occured {e.Message}");
                Console.WriteLine($"RunTask: Error occured {e.Message}");
            }
            finally
            {
                context.ContinueAsNew(input);
            }

            return null;
        }
    }
}
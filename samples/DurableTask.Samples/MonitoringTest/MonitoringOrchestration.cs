// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace DurableTask.Samples.MonitoringTest
{
    using System;
    using System.Threading.Tasks;
    using DurableTask.Core;

    public class MonitoringOrchestration : TaskOrchestration<string,MonitoringInput>
    {
        public override async Task<string> RunTask(OrchestrationContext context, MonitoringInput input)
        {
            try
            {
                Console.WriteLine($"RunTask: Pinging to host {input.host}, instance {context.OrchestrationInstance}, timeStamp:{context.CurrentUtcDateTime}");
                
                var startTime = context.CurrentUtcDateTime;
                var maxDelta = TimeSpan.FromSeconds(10);
                Console.WriteLine($"RunTask: Operation started at {startTime}, instance {context.OrchestrationInstance}");
                
                Task timer = context.CreateTimer(
                    context.CurrentUtcDateTime.Add(maxDelta),
                    "timer1");

                Task<string> output = context.ScheduleTask<string>(typeof(MonitoringTask), input);

                await Task.WhenAll(timer);

                if (!output.IsCompleted)
                {
                    // request timed out, do some compensating action
                    Console.WriteLine($"RunTask: Timer got timed out and the task did not complete. instance {context.OrchestrationInstance}");
                }
                else
                {
                    // orchestration completion
                    Console.WriteLine($"RunTask: Pinging to host {input}, result {output.Result}, instance {context.OrchestrationInstance}");
                }

                var endTime = context.CurrentUtcDateTime;
                var isBigger = (endTime - startTime) > maxDelta;
                Console.WriteLine($"RunTask: Operation ended at {endTime}, deltaIsBiggerThanNeeded: '{isBigger}', instance {context.OrchestrationInstance}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"RunTask: An exception occured {e.Message}");
            }
            finally
            {
                context.ContinueAsNew(input);
            }
            return null;
        }
    }
}
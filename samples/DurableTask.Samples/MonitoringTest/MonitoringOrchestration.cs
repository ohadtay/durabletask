// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace DurableTask.Samples.MonitoringTest
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DurableTask.Core;
    using Kusto.Cloud.Platform.Utils;

    public class MonitoringOrchestration : TaskOrchestration<string, MonitoringInput>
    {
        public static int totalCounter = 0;
        public static int failureOrchestrationCounter = 0;
        public static int failureTimerCounter = 0;

        public override async Task<string> RunTask(OrchestrationContext context, MonitoringInput input)
        {
            var consoleColor = ConsoleColor.Green;
            DateTime nextIterationScheduleTime = context.CurrentUtcDateTime.Add(TimeSpan.FromMinutes(1));

            try
            {
                Interlocked.Increment(ref totalCounter);
                var showVersionOutput = await context.ScheduleTask<MonitoringOutput>(
                    typeof(MonitoringTask),
                    new MonitoringInput
                    {
                        Host = input.Host,
                        ScheduledTime = context.CurrentUtcDateTime
                    });
                string result = showVersionOutput.Success ? "Succeeded" : "Failed";
                string hostname = input.Host.Substring(8, input.Host.IndexOf('.') - 8);

                if (!context.IsReplaying)
                {
                    if (context.CurrentUtcDateTime - input.ScheduledTime >= TimeSpan.FromMinutes(1))
                    {
                        consoleColor = ConsoleColor.Red;
                        ExtendedConsole.WriteLine(consoleColor, $"Execution {hostname,-30} timing: Orc total: {context.CurrentUtcDateTime - input.ScheduledTime}, Task execution: {showVersionOutput.TaskExecutionFinishTime - input.ScheduledTime}, {result},  failureType: execution");
                        Interlocked.Increment(ref failureOrchestrationCounter);
                        throw new Exception($"Error!");
                    }
                }

                var timeBeforeTimer = context.CurrentUtcDateTime;
                nextIterationScheduleTime = context.CurrentUtcDateTime.Add(TimeSpan.FromMinutes(1) - (timeBeforeTimer - input.ScheduledTime));
                await context.CreateTimer(nextIterationScheduleTime, context.OrchestrationInstance.InstanceId);
                
                if (!context.IsReplaying)
                {
                    if (context.CurrentUtcDateTime - nextIterationScheduleTime > TimeSpan.FromSeconds(3))
                    {
                        consoleColor = ConsoleColor.Yellow;
                        ExtendedConsole.WriteLine(consoleColor, $"Execution {hostname,-30} timing: Orc total: {context.CurrentUtcDateTime - input.ScheduledTime}, Task execution: {showVersionOutput.TaskExecutionFinishTime - input.ScheduledTime}, {result}, failureType: timer");
                        Interlocked.Increment(ref failureTimerCounter);
                        throw new Exception($"Error!");
                    }
                    
                    ExtendedConsole.WriteLine(consoleColor, $"Execution {hostname,-30} timing: Orc total: {context.CurrentUtcDateTime - input.ScheduledTime}, Task execution: {showVersionOutput.TaskExecutionFinishTime - input.ScheduledTime}, {result}");
                }
            }
            catch (Exception)
            {
                // ignored
            }
            finally
            {
                // context.ContinueAsNew(input);
                context.ContinueAsNew(new MonitoringInput
                {
                    Host = input.Host,
                    ScheduledTime = nextIterationScheduleTime
                });
            }

            return null;
        }
    }
}
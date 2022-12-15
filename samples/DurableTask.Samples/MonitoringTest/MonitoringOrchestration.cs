// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace DurableTask.Samples.MonitoringTest
{
    using System;
    using System.Threading.Tasks;
    using DurableTask.Core;
    using Kusto.Cloud.Platform.Utils;

    public class MonitoringOrchestration : TaskOrchestration<string, MonitoringInput>
    {
        public override async Task<string> RunTask(OrchestrationContext context, MonitoringInput input)
        {
            var consoleColor = ConsoleColor.Green;

            try
            {
                var instanceId = context.OrchestrationInstance.InstanceId.ToString();

                var showVersionOutput = await context.ScheduleTask<MonitoringOutput>(
                    typeof(MonitoringTask),
                    new MonitoringInput
                    {
                        Host = input.Host,
                        ScheduledTime = context.CurrentUtcDateTime
                    });
                string result = showVersionOutput.Success ? "Succeeded" : "Failed";
                string hostname = instanceId.Substring(0, instanceId.IndexOf('.'));

                if (!context.IsReplaying)
                {
                    if (context.CurrentUtcDateTime - input.ScheduledTime >= TimeSpan.FromMinutes(1))
                    {
                        consoleColor = ConsoleColor.Red;
                        ExtendedConsole.WriteLine(consoleColor, $"Execution {hostname,-30} timing: Orc total: {context.CurrentUtcDateTime - input.ScheduledTime}, Task execution: {showVersionOutput.TaskExecutionFinishTime - input.ScheduledTime}, {result}");

                        throw new Exception($"Error!");
                    }
                }

                await context.CreateTimer(context.CurrentUtcDateTime.Add(TimeSpan.FromMinutes(1) - (context.CurrentUtcDateTime - input.ScheduledTime)), context.OrchestrationInstance.InstanceId);
                if (!context.IsReplaying)
                {
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
                    ScheduledTime = context.CurrentUtcDateTime
                });
            }

            return null;
        }
    }
}
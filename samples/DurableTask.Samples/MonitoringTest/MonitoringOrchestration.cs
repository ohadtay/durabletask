// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace DurableTask.Samples.MonitoringTest
{
    using System;
    using System.Threading.Tasks;
    using DurableTask.Core;

    public class MonitoringOrchestration : TaskOrchestration<string, MonitoringInput>
    {
        public override async Task<string> RunTask(OrchestrationContext context, MonitoringInput input)
        {
            var instanceId = context.OrchestrationInstance.InstanceId.ToString();
            try
            {
                var showVersionOutput = await context.ScheduleTask<MonitoringOutput>(
                    typeof(MonitoringTask),
                    new MonitoringInput
                    {
                        Host = input.Host,
                        ScheduledTime = context.CurrentUtcDateTime
                    });

                TimeSpan showVersionExecutionDeltaTime = context.CurrentUtcDateTime - showVersionOutput.ScheduledTime;
                if (!context.IsReplaying)
                {
                    if (showVersionExecutionDeltaTime >= TimeSpan.FromMinutes(1))
                    {
                        throw new Exception($"\tError! Instance, '{instanceId}', ExecutionId ,'{context.OrchestrationInstance.ExecutionId}', - Ping took ,'{showVersionExecutionDeltaTime}'");
                    }
                }

                await context.CreateTimer(context.CurrentUtcDateTime.Add(TimeSpan.FromMinutes(1) - showVersionExecutionDeltaTime), context.OrchestrationInstance.InstanceId);
                if (!context.IsReplaying)
                {
                    Console.WriteLine($"Execution {context.OrchestrationInstance.ExecutionId} timing: S: {showVersionOutput.ScheduledTime}, E: {showVersionOutput.ExecutionTime}, C: {context.CurrentUtcDateTime}, D: {showVersionExecutionDeltaTime}, DT: {context.CurrentUtcDateTime - showVersionOutput.ScheduledTime}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failure: '{e.Message}'");
            }
            finally
            {
                context.ContinueAsNew(input);
            }

            return null;
        }
    }
}
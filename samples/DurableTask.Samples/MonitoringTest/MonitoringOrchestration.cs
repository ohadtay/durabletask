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
            string instanceId = context.OrchestrationInstance.InstanceId.ToString();
            try
            {
                for (int i = 0; i < 60; i++)
                {
                    var showVersionOutput = await context.ScheduleTask<MonitoringOutput>(typeof(MonitoringTask), new MonitoringInput
                    {
                        host = input.host,
                        scheduledTime = context.CurrentUtcDateTime
                    });

                    var showVersionExecutionDeltaTime = context.CurrentUtcDateTime - showVersionOutput.scheduledTime;
                    if (!context.IsReplaying)
                    {
                        if (showVersionExecutionDeltaTime >= TimeSpan.FromMinutes(1))
                        {
                            throw new Exception($"Error! Instance, '{instanceId}', ExecutionId ,'{context.OrchestrationInstance.ExecutionId}', - Ping took ,'{showVersionExecutionDeltaTime}'");
                        }
                    }

                    await context.CreateTimer(context.CurrentUtcDateTime.Add(TimeSpan.FromMinutes(1) - showVersionExecutionDeltaTime), context.OrchestrationInstance.InstanceId);
                    if (!context.IsReplaying)
                    {
                        Console.WriteLine($"Execution {context.OrchestrationInstance.ExecutionId} Loop {i} timing (Replying {context.IsReplaying}): S: {showVersionOutput.scheduledTime}, E: {showVersionOutput.executionTime}, C: {context.CurrentUtcDateTime}, D: {showVersionExecutionDeltaTime}, DT: {context.CurrentUtcDateTime-showVersionOutput.scheduledTime}");
                    }
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
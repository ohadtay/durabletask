// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace DurableTask.Samples.MonitoringTest
{
    using System;
    using System.Threading.Tasks;
    using DurableTask.Core;

    public class MonitoringOrchestration : TaskOrchestration<string,string>
    {
        public override async Task<string> RunTask(OrchestrationContext context, string input)
        {
            // generating the command 10 times

            try
            {
              //  for (var i = 0; i < 10; i++)
                {
                    Console.WriteLine($"Pinging to host {input}, instance {context.OrchestrationInstance}");
                    var output = await context.ScheduleTask<string>(typeof(MonitoringTask), input);
                    Console.WriteLine($"Pinging to host {input}, result {output}, instance {context.OrchestrationInstance}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"An exception occured {e.Message}");
            }

            System.Threading.Thread.Sleep(1000);
            context.ContinueAsNew(input);
            return null;
        }
    }
}
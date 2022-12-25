﻿//  ----------------------------------------------------------------------------------
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

namespace DurableTask.Samples
{
    using CommandLine;
    using CommandLine.Text;

    internal class Options
    {
        [Option('c', "clean-hub", DefaultValue = false,
            HelpText = "Clean Orchestration Hub.")]
        public bool CleanHub { get; set; }

        [Option('i', "instance-id",
            HelpText = "Instance id for new orchestration instance.")]
        public string InstanceId { get; set; }
        
        [Option('m', "max-concurrent-task-activity-work-items", DefaultValue = 400,
            HelpText = "max tasks for a worker")]
        public int MaxConcurrentTaskActivityWorkItems { get; set; }
        
        [Option('n', "number-of-workers", DefaultValue = 1,
            HelpText = "max tasks for a worker")]
        public int NumberOfWorkers { get; set; }

        [Option('k', "number-of-partitions", DefaultValue = 2,
            HelpText = "max tasks for a worker")]
        public int NumberOfPartition { get; set; }
        
        [Option('a', "max-concurrent-task-orchestration-work-items", DefaultValue = 400,
            HelpText = "max orchestration for a worker")]
        public int MaxConcurrentTaskOrchestrationWorkItems { get; set; }

        [Option('w', "start-worker", DefaultValue = false,
            HelpText = "Start workers")]
        public bool ShouldSetUpWorkers { get; set; }
        
        [HelpOption]
        public string GetUsage()
        {
            // this without using CommandLine.Text
            //  or using HelpText.AutoBuild

            var help = new HelpText
            {
                Heading = new HeadingInfo("DurableTaskSamples", "1.0"),
                AdditionalNewLineAfterOption = true,
                AddDashesToOption = true
            };
            help.AddPreOptionsLine("Usage: DurableTaskSamples.exe -c");
            help.AddOptions(this);
            return help;
        }
    }
}

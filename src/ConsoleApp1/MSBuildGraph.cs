using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;
//using Microsoft.Build.Experimental.Graph;

using ProjectGraphEntryPoint = Microsoft.Build.Graph.ProjectGraphEntryPoint;

namespace ConsoleApp1
{
    internal class MSBuildGraph
    {
        private static EvaluationContext EvaluationContext = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);

        public void Go(string entryPoint)
        {
            Dictionary<string, string> globalProperties = new(StringComparer.OrdinalIgnoreCase)
            {
                ["AddTransitiveProjectReferencesInStaticGraph"] = bool.TrueString,
                ["DisableCheckingDuplicateNuGetItems"] = bool.TrueString,
                ["DisableTransitiveProjectReferences"] = bool.FalseString,
                ["ExcludeRestorePackageImports"] = bool.TrueString,
                ["ImportProjectExtensionProps"] = bool.FalseString,
                ["ImportProjectExtensionTargets"] = bool.FalseString,
                ["MSBuildRestoreSessionId"] = Guid.NewGuid().ToString("X"),
            };

            using ProjectCollection projectCollection = new(
                globalProperties: null,
                loggers: null,
                remoteLoggers: null,
                toolsetDefinitionLocations: ToolsetDefinitionLocations.Default,
                maxNodeCount: 1,
                onlyLogCriticalEvents: false,
                loadProjectsReadOnly: true)
            {
                DisableMarkDirty = true,
            };

            //ProjectGraph projectGraph = ProjectGraph.Create([ new ProjectGraphEntryPoint(entryPoint, globalProperties) ], projectCollection, CreateProjectInstance);
        }

        private ProjectInstance CreateProjectInstance(string path, Dictionary<string, string> globalProperties, ProjectCollection projectCollection)
        {
            ProjectOptions projectOptions = new()
            {
                EvaluationContext = EvaluationContext,
                GlobalProperties = globalProperties,
                LoadSettings = ProjectLoadSettings.DoNotEvaluateElementsWithFalseCondition | ProjectLoadSettings.IgnoreMissingImports | ProjectLoadSettings.IgnoreEmptyImports | ProjectLoadSettings.IgnoreInvalidImports | ProjectLoadSettings.FailOnUnresolvedSdk,
                ProjectCollection = projectCollection,
            };

            return ProjectInstance.FromFile(path, projectOptions);
        }
    }
}
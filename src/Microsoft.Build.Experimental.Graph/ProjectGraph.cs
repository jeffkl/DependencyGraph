using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.DependencyGraph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

using ProjectGraphEntryPoint = Microsoft.Build.Graph.ProjectGraphEntryPoint;

namespace Microsoft.Build.Experimental.Graph
{
    public sealed class ProjectGraph
    {
        // internal ProjectItemInstance(ProjectInstance project, string itemType, string includeEscaped, IEnumerable<KeyValuePair<string, string>> directMetadata, string definingFileEscaped)
        private static readonly ConstructorInfo ProjectItemInstanceConstructor = typeof(ProjectItemInstance).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            [ typeof(ProjectInstance), typeof(string), typeof(string), typeof(IEnumerable<KeyValuePair<string, string>>), typeof(string) ],
            null);

        private ProjectGraph(GraphBuilder<ConfigurationMetadata, ProjectGraphNode, ProjectItemInstance> graphBuilder)
        {
        }

        public static ProjectGraph Create(IEnumerable<ProjectGraphEntryPoint> entryPoints, ProjectCollection projectCollection, Func<string, Dictionary<string, string>, ProjectCollection, ProjectInstance> projectFactory) => Create(entryPoints, projectCollection, projectFactory, Environment.ProcessorCount);

        public static ProjectGraph Create(
            IEnumerable<ProjectGraphEntryPoint> entryPoints,
            ProjectCollection projectCollection,
            Func<string, Dictionary<string, string>, ProjectCollection, ProjectInstance> projectFactory,
            int degreeOfParallelism)
        {
            List<ConfigurationMetadata> configurationMetadata = GetConfigurationMetadataForEntryPoints(entryPoints);

            GraphBuilder<ConfigurationMetadata, ProjectGraphNode, ProjectItemInstance> graphBuilder = new(
                configurationMetadata,
                EqualityComparer<ConfigurationMetadata>.Default,
                CreateProjectInstance,
                GetProjectReferences,
                degreeOfParallelism,
                CancellationToken.None);

            graphBuilder.Build();

            return new ProjectGraph(graphBuilder);

            ProjectGraphNode CreateProjectInstance(ConfigurationMetadata configurationMetadata)
            {
                ProjectInstance projectInstance = projectFactory(configurationMetadata.ProjectFullPath, configurationMetadata.GlobalProperties, projectCollection);

                if (projectInstance == null)
                {
                    throw new InvalidOperationException();
                }

                return new ProjectGraphNode(projectInstance);
            }
        }

        private static IEnumerable<(ConfigurationMetadata, ProjectItemInstance)> GetProjectReferences(ProjectGraphNode projectGraphNode)
        {
            IEnumerable<(ProjectItemInstance projectItemInstance, Dictionary<string, string> referenceGlobalProperties)> projectReferenceItems = null;

            ProjectInstance requesterInstance = projectGraphNode.ProjectInstance;

            switch (projectGraphNode.ProjectType)
            {
                case ProjectInterpretation.ProjectType.OuterBuild:
                    projectReferenceItems = ConstructInnerBuildReferences(requesterInstance);
                    break;
                case ProjectInterpretation.ProjectType.InnerBuild:
                    //globalPropertiesModifiers = ModifierForNonMultitargetingNodes.Add((parts, reference) => parts.AddPropertyToUndefine(GetInnerBuildPropertyName(requesterInstance)));
                    //projectReferenceItems = requesterInstance.GetItems(ItemTypeNames.ProjectReference);
                    break;
                case ProjectInterpretation.ProjectType.NonMultitargeting:
                    //globalPropertiesModifiers = ModifierForNonMultitargetingNodes;
                    //projectReferenceItems = requesterInstance.GetItems(ItemTypeNames.ProjectReference);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            if (projectReferenceItems == null)
            {
                yield break;
            }

            foreach ((ProjectItemInstance projectItemInstance, Dictionary<string, string> referenceGlobalProperties) in projectReferenceItems)
            {
                string projectReferenceFullPath = projectItemInstance.GetMetadataValue("FullPath");
                
                ConfigurationMetadata referenceConfig = new(projectReferenceFullPath, referenceGlobalProperties);

                yield return (referenceConfig, projectItemInstance);
            }

            IEnumerable<(ProjectItemInstance, Dictionary<string, string>)> ConstructInnerBuildReferences(ProjectInstance outerBuild)
            {
                string globalPropertyName = ProjectInterpretation.GetInnerBuildPropertyName(outerBuild);
                string globalPropertyValues = ProjectInterpretation.GetInnerBuildPropertyValues(outerBuild);

                foreach (string globalPropertyValue in new SemiColonTokenizer(globalPropertyValues))
                {
                    Dictionary<string, string> globalProperties = new(outerBuild.GlobalProperties, StringComparer.OrdinalIgnoreCase);
                    
                    globalProperties[globalPropertyName] = globalPropertyValue;

                    yield return (
                        (ProjectItemInstance)ProjectItemInstanceConstructor.Invoke(
                        [
                            outerBuild, // project
                            ProjectInterpretation.InnerBuildReferenceItemName, // itemType
                            outerBuild.FullPath, // includeEscaped
                            new[] { new KeyValuePair<string, string>("Properties", $"{globalPropertyName}={globalPropertyValue}") }, // directMetadata
                            outerBuild.FullPath
                        ]),
                        globalProperties);
                }
            }
        }



        private static List<ConfigurationMetadata> GetConfigurationMetadataForEntryPoints(IEnumerable<ProjectGraphEntryPoint> entryPoints)
        {
            List<ConfigurationMetadata> entryPointConfigurationMetadata = new();

            foreach (ProjectGraphEntryPoint entryPoint in entryPoints)
            {
                entryPointConfigurationMetadata.Add(new(entryPoint.ProjectFile, GetDictionary(entryPoint.GlobalProperties)));
            }

            return entryPointConfigurationMetadata;

            Dictionary<string, string> GetDictionary(IDictionary<string, string> globalProperties)
            {
                Dictionary<string, string> cloned = new(globalProperties, StringComparer.OrdinalIgnoreCase);

                cloned["IsGraphBuild"] = bool.TrueString;

                return cloned;
            }
        }
    }
}
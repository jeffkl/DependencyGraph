using Microsoft.Build.Evaluation;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.Experimental.Graph
{
    public readonly struct ProjectGraphEntryPoint1
    {
        public ProjectGraphEntryPoint1(string projectFile)
            : this(projectFile, null, ProjectCollection.GlobalProjectCollection)
        {
        }

        public ProjectGraphEntryPoint1(string projectFile, IDictionary<string, string> globalProperties, ProjectCollection projectCollection)
        {
            ProjectFile = Path.GetFullPath(projectFile);
            GlobalProperties = globalProperties;
            ProjectCollection = projectCollection;
        }

        public string ProjectFile { get; }

        public IDictionary<string, string> GlobalProperties { get; }

        public ProjectCollection ProjectCollection { get; }

        internal static IEnumerable<ProjectGraphEntryPoint1> CreateEnumerable(IEnumerable<string> entryProjectFiles)
        {
            foreach (string entryProjectFile in entryProjectFiles)
            {
                yield return new ProjectGraphEntryPoint1(entryProjectFile);
            }
        }

        internal static IEnumerable<ProjectGraphEntryPoint1> CreateEnumerable(IEnumerable<string> entryProjectFiles, IDictionary<string, string> globalProperties, ProjectCollection projectCollection)
        {
            foreach (string entryProjectFile in entryProjectFiles)
            {
                yield return new ProjectGraphEntryPoint1(entryProjectFile, globalProperties, projectCollection);
            }
        }

        internal readonly IEnumerable<ProjectGraphEntryPoint1> AsEnumerable()
        {
            yield return this;
        }
    }
}

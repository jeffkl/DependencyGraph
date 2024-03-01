using Microsoft.Build.Execution;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Experimental.Graph
{
    internal class ProjectInterpretation
    {
        internal const string InnerBuildProperty = nameof(InnerBuildProperty);
        internal const string InnerBuildPropertyValues = nameof(InnerBuildPropertyValues);
        internal const string InnerBuildReferenceItemName = "_ProjectSelfReference";

        internal enum ProjectType
        {
            OuterBuild,
            InnerBuild,
            NonMultitargeting,
        }

        internal static string GetInnerBuildPropertyName(ProjectInstance project)
        {
            return project.GetPropertyValue(InnerBuildProperty);
        }

        internal static string GetInnerBuildPropertyValue(ProjectInstance project)
        {
            return project.GetPropertyValue(GetInnerBuildPropertyName(project));
        }

        internal static string GetInnerBuildPropertyValues(ProjectInstance project)
        {
            return project.GetPropertyValue(project.GetPropertyValue(InnerBuildPropertyValues));
        }

        internal static ProjectType GetProjectType(ProjectInstance project)
        {
            bool isOuterBuild = string.IsNullOrWhiteSpace(GetInnerBuildPropertyValue(project)) && !string.IsNullOrWhiteSpace(GetInnerBuildPropertyValues(project));
            bool isInnerBuild = !string.IsNullOrWhiteSpace(GetInnerBuildPropertyValue(project));

            return isOuterBuild
                ? ProjectType.OuterBuild
                : isInnerBuild
                    ? ProjectType.InnerBuild
                    : ProjectType.NonMultitargeting;
        }
    }
}
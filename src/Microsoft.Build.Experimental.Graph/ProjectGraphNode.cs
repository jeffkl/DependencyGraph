using Microsoft.Build.Execution;
using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.Experimental.Graph
{
    public sealed class ProjectGraphNode
    {
        private readonly ProjectInstance _projectInstance;

        private readonly ProjectInterpretation.ProjectType _projectType;

        internal ProjectGraphNode(ProjectInstance projectInstance)
        {
            _projectInstance = projectInstance ?? throw new ArgumentNullException();

            _projectType = ProjectInterpretation.GetProjectType(_projectInstance);
        }

        public ProjectInstance ProjectInstance => _projectInstance;

        internal ProjectInterpretation.ProjectType ProjectType => _projectType;
    }
}

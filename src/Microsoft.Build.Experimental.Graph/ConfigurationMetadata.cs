using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.Build.Experimental.Graph
{
    [DebuggerDisplay(@"{DebugString()}")]
    internal sealed class ConfigurationMetadata : IEqualityComparer<ConfigurationMetadata>, IEquatable<ConfigurationMetadata>
    {
        private readonly Dictionary<string, string> _globalProperties;

        private string _projectFullPath;

        private string _toolsVersion = "Current";

        public ConfigurationMetadata(string projectFullPath, Dictionary<string, string> globalProperties)
        {
            _projectFullPath = projectFullPath;
            _globalProperties = globalProperties;
        }

        public Dictionary<string, string> GlobalProperties => _globalProperties;

        public string ProjectFullPath => _projectFullPath;

        public string ToolsVersion => _toolsVersion;

        public bool Equals(ConfigurationMetadata x, ConfigurationMetadata y) => x is not null && y is not null && x.Equals(y);

        public bool Equals(ConfigurationMetadata other) => ReferenceEquals(this, other) || string.Equals(ProjectFullPath, other.ProjectFullPath, StringComparison.OrdinalIgnoreCase) && GlobalProperties.SequenceEqual(other.GlobalProperties) && string.Equals(ToolsVersion, other.ToolsVersion, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object obj) => obj is not null && Equals(obj as ConfigurationMetadata);

        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(ProjectFullPath) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(ToolsVersion);

        public int GetHashCode(ConfigurationMetadata obj) => obj.GetHashCode();

        private string DebugString() => $"{ProjectFullPath}, #GlobalProps={GlobalProperties.Count}";
    }
}
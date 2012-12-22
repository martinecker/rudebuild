using System;
using System.Collections.Generic;
using System.IO;

namespace RudeBuild
{
    public enum VisualStudioVersion
    {
        VSUnknown,
        VS2005,
        VS2008,
        VS2010,
        VS2012
    }

    public class SolutionInfo
    {
        public string FilePath { get; private set; }
        public string Name { get; private set; }
        public VisualStudioVersion Version { get; private set; }
        public string Contents { get; private set; }

        public IList<string> ProjectNames { get; private set; }
        public IList<string> ProjectFileNames { get; private set; }
        public IDictionary<string, ProjectInfo> Projects { get; private set; } 

        public SolutionInfo(string filePath, VisualStudioVersion version, IList<string> projectFileNames, string contents)
        {
            FilePath = Path.GetFullPath(filePath);
            Name = Path.GetFileNameWithoutExtension(filePath);
            ProjectFileNames = projectFileNames;
            Version = version;
            Contents = contents;
            ProjectNames = new List<string>();
            Projects = new Dictionary<string, ProjectInfo>();

            foreach (string projectFileName in projectFileNames)
            {
                ProjectNames.Add(Path.GetFileNameWithoutExtension(projectFileName));
            }
        }

        public void AddProject(ProjectInfo projectInfo)
        {
            if (!ProjectNames.Contains(projectInfo.Name))
                throw new ArgumentException("Trying to add a project of name " + projectInfo.Name + " to a solution that doesn't contain that project.");

            Projects.Add(projectInfo.Name, projectInfo);
        }

        public ProjectInfo GetProjectInfo(string projectName)
        {
            ProjectInfo projectInfo = null;
            Projects.TryGetValue(projectName, out projectInfo);
            return projectInfo;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RudeBuild
{
    public enum VisualStudioVersion
    {
        VSUnknown,
        VS2005,
        VS2008,
        VS2010,
        VS2012,
        VS2013,
        VS2015
    }

    public sealed class SolutionConfigManager
    {
        public sealed class ProjectConfig
        {
            public string ProjectGuid;
            public string ProjectFileName;
            public IDictionary<string, string> SolutionToProjectConfigMap;

            public string GetProjectConfig(string solutionConfig)
            {
                if (SolutionToProjectConfigMap != null)
                {
                    string projectConfig;
                    if (SolutionToProjectConfigMap.TryGetValue(solutionConfig, out projectConfig))
                        return projectConfig;
                }
                return null;
            }
        }

        public IList<ProjectConfig> Projects { get; private set; }
        public IList<string> SolutionConfigs { get; private set; }

        public SolutionConfigManager()
        {
            Projects = new List<ProjectConfig>();
            SolutionConfigs = new List<string>();
        }

        public void AddProject(string projectFileName, string projectGuid)
        {
            if (GetProjectByGuid(projectGuid) != null)
                throw new InvalidDataException(string.Format("Project {0} with GUID {1} occurs twice in the solution!", projectFileName, projectGuid));

            if (GetProjectByFileName(projectFileName) != null)
                throw new InvalidDataException(string.Format("Project {0} with GUID {1} occurs twice in the solution!", projectFileName, projectGuid));

            var config = new ProjectConfig
            {
                ProjectGuid = projectGuid,
                ProjectFileName = projectFileName,
                SolutionToProjectConfigMap = new Dictionary<string, string>()
            };
            Projects.Add(config);
        }

        public ProjectConfig GetProjectByGuid(string projectGuid)
        {
            return Projects.FirstOrDefault(project => project.ProjectGuid == projectGuid);
        }

        public ProjectConfig GetProjectByFileName(string projectFileName)
        {
            return Projects.FirstOrDefault(project => project.ProjectFileName == projectFileName);
        }

        public void AddSolutionConfig(string solutionConfig)
        {
            if (SolutionConfigs.Contains(solutionConfig))
                throw new InvalidDataException("Solution file is corrupt. It contains two solution configs called " + solutionConfig + "!");
            SolutionConfigs.Add(solutionConfig);
        }

        public void SetProjectConfig(string projectGuid, string solutionConfig, string projectConfig)
        {
            var project = GetProjectByGuid(projectGuid);
            if (null == project)        // If the project doesn't exist, it means that we're dealing with a non-C++ project, and so it didn't get parsed out of the solution file.
                return;
            if (!SolutionConfigs.Contains(solutionConfig))
                throw new InvalidDataException(string.Format("Cannot associate project config {0} of project GUID {1} with solution config {2}! The solution config doesn't exist!", projectConfig, projectGuid, solutionConfig));

            project.SolutionToProjectConfigMap.Add(solutionConfig, projectConfig);
        }
    }

    public sealed class SolutionInfo
    {
        public string FilePath { get; private set; }
        public string Name { get; private set; }
        public VisualStudioVersion Version { get; private set; }
        public string Contents { get; private set; }

        public SolutionConfigManager ConfigManager { get; private set; }
        public IEnumerable<string> ProjectFileNames { get { return from project in ConfigManager.Projects select project.ProjectFileName; } }

        public IDictionary<string, ProjectInfo> Projects { get; private set; }
        public IEnumerable<string> ProjectNames { get { return from projectName in Projects.Keys select projectName; } }

        public SolutionInfo(string filePath, VisualStudioVersion version, SolutionConfigManager configManager, string contents)
        {
            FilePath = Path.GetFullPath(filePath);
            Name = Path.GetFileNameWithoutExtension(filePath);
            Version = version;
            Contents = contents;
            ConfigManager = configManager;
            Projects = new Dictionary<string, ProjectInfo>();
        }

        public void AddProject(ProjectInfo projectInfo)
        {
            if (!ProjectFileNames.Contains(projectInfo.FileName))
                throw new ArgumentException("Trying to add a project of name " + projectInfo.Name + " to a solution that doesn't contain that project.");

            Projects.Add(projectInfo.Name, projectInfo);
        }

        public ProjectInfo GetProjectInfo(string projectName)
        {
            ProjectInfo projectInfo;
            Projects.TryGetValue(projectName, out projectInfo);
            return projectInfo;
        }
    }
}

using System;
using System.IO;

namespace RudeBuildAddIn
{
    public abstract class BuildCommandBase : CommandBase
    {
        public enum Mode
        {
            Build,
            Rebuild,
            Clean,
            CleanCache
        }

        public Builder Builder { get; private set; }
        public Mode BuildMode { get; private set; }

        public BuildCommandBase(Builder builder, Mode buildMode)
        {
            Builder = builder;
            BuildMode = buildMode;
        }

        public static bool IsSolutionOpen(CommandManager commandManager)
        {
            return commandManager.Application.Solution.IsOpen;
        }

        public static FileInfo GetSolutionFileInfo(CommandManager commandManager)
        {
            if (IsSolutionOpen(commandManager))
            {
                string solutionPath = commandManager.Application.Solution.FullName;
                return new FileInfo(solutionPath);
            }
            return null;
        }

        public static string GetActiveSolutionConfig(CommandManager commandManager)
        {
            if (IsSolutionOpen(commandManager))
            {
                EnvDTE80.SolutionConfiguration2 config = (EnvDTE80.SolutionConfiguration2)commandManager.Application.Solution.SolutionBuild.ActiveConfiguration;
                return config.Name + "|" + config.PlatformName;
            }
            return null;
        }

        public static string GetActiveProjectName(CommandManager commandManager)
        {
            EnvDTE.Window solutionExplorerWindow = commandManager.Application.Windows.Item(EnvDTE.Constants.vsWindowKindSolutionExplorer);
            if (solutionExplorerWindow != null && solutionExplorerWindow.Caption != null)
            {
                Array projects = (Array)commandManager.Application.ActiveSolutionProjects;
                if (projects.Length > 0)
                {
                    EnvDTE.Project activeProject = projects.GetValue(0) as EnvDTE.Project;
                    if (null != activeProject)
                        return activeProject.Name;
                }
            }
            return null;
        }

        public override bool IsEnabled(CommandManager commandManager)
        {
            return IsSolutionOpen(commandManager) && !Builder.IsBuilding;
        }
    }
}
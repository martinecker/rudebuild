using System;
using System.IO;

namespace RudeBuildVSShared
{
    public interface ICommand
    {
        string Name { get; }
        string Caption { get; }
        string ToolTip { get; }
        string Icon { get; }
        EnvDTE.Command VSCommand { get; }

        void Initialize(string name, string caption, string toolTip, string icon, EnvDTE.Command vsCommand);
        void Execute(CommandManager commandManager);
        bool IsEnabled(CommandManager commandManager);
    }

    public abstract class CommandBase : ICommand
    {
        public string Name { get; private set; }
        public string Caption { get; private set; }
        public string ToolTip { get; private set; }
        public string Icon { get; private set; }
        public EnvDTE.Command VSCommand { get; private set; }

        public void Initialize(string name, string caption, string toolTip, string icon, EnvDTE.Command vsCommand)
        {
            Name = name;
            Caption = caption;
            ToolTip = toolTip;
            Icon = icon;
            VSCommand = vsCommand;
        }

        public abstract void Execute(CommandManager commandManager);
        public abstract bool IsEnabled(CommandManager commandManager);

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
                var config = (EnvDTE80.SolutionConfiguration2)commandManager.Application.Solution.SolutionBuild.ActiveConfiguration;
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
    }
}
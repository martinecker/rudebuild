using RudeBuild;

namespace RudeBuildAddIn
{
    public class BuildProjectCommand : BuildCommandBase
    {
        public BuildProjectCommand(Builder builder, Mode buildMode)
            : base(builder, buildMode)
        {
        }

        public override void Execute(CommandManager commandManager)
        {
            BuildOptions options = new BuildOptions();
            options.Project = GetActiveProjectName(commandManager);
            if (null == options.Project)
                return;
            options.Solution = GetSolutionFileInfo(commandManager);
            options.Config = GetActiveSolutionConfig(commandManager);
            options.Clean = BuildMode == Mode.Clean;
            options.Rebuild = BuildMode == Mode.Rebuild;
            Builder.Build(options);
        }

        public override bool IsEnabled(CommandManager commandManager)
        {
            return base.IsEnabled(commandManager) && GetActiveProjectName(commandManager) != null;
        }
    }
}

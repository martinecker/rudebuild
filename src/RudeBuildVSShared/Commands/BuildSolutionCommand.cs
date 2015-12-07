using RudeBuild;

namespace RudeBuildVSShared
{
    public class BuildSolutionCommand : BuildCommandBase
    {
        public BuildSolutionCommand(Builder builder, Mode buildMode)
            :   base(builder, buildMode)
        {
        }

        public override void Execute(CommandManager commandManager)
        {
            base.Execute(commandManager);

            BuildOptions options = new BuildOptions();
            options.Solution = GetSolutionFileInfo(commandManager);
            options.Config = GetActiveSolutionConfig(commandManager);
            options.Clean = BuildMode == Mode.Clean;
            options.Rebuild = BuildMode == Mode.Rebuild;
            Builder.Build(options);
        }
    }
}
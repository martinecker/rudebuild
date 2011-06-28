using RudeBuild;

namespace RudeBuildAddIn
{
    public class CleanCacheCommand : BuildCommandBase
    {
        public CleanCacheCommand(Builder builder)
            : base(builder, Mode.CleanCache)
        {
        }

        public override void Execute(CommandManager commandManager)
        {
            BuildOptions options = new BuildOptions();
            options.Solution = GetSolutionFileInfo(commandManager);
            options.Config = GetActiveSolutionConfig(commandManager);
            options.CleanCache = true;
            Builder.Build(options);
        }
    }
}

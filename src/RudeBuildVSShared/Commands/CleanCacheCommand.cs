using RudeBuild;

namespace RudeBuildVSShared
{
    public class CleanCacheCommand : BuildCommandBase
    {
        public CleanCacheCommand(Builder builder)
            : base(builder, Mode.CleanCache)
        {
        }

        public override void Execute(CommandManager commandManager)
        {
			if (!IsEnabled(commandManager))
				return;

			BuildOptions options = new BuildOptions();
            options.Solution = GetSolutionFileInfo(commandManager);
            options.Config = GetActiveSolutionConfig(commandManager);
            options.CleanCache = true;
            Builder.Build(options);
        }
    }
}

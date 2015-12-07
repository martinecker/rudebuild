namespace RudeBuildVSShared
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

        public override void Execute(CommandManager commandManager)
        {
            commandManager.Application.ExecuteCommand("File.SaveAll");
        }

        public override bool IsEnabled(CommandManager commandManager)
        {
            return IsSolutionOpen(commandManager) && !Builder.IsBuilding;
        }
    }
}
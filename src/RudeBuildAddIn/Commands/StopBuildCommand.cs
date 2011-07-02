namespace RudeBuildAddIn
{
    public class StopBuildCommand : CommandBase
    {
        private Builder _builder;

        public StopBuildCommand(Builder builder)
        {
            _builder = builder;
        }

        public override void Execute(CommandManager commandManager)
        {
            _builder.Stop();
        }

        public override bool IsEnabled(CommandManager commandManager)
        {
            return IsSolutionOpen(commandManager) && _builder.IsBuilding;
        }
    }
}
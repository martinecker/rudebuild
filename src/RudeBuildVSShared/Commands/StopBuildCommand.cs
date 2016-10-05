namespace RudeBuildVSShared
{
    public sealed class StopBuildCommand : CommandBase
    {
        private Builder _builder;

        public StopBuildCommand(Builder builder)
        {
            _builder = builder;
        }

        public override void Execute(CommandManager commandManager)
        {
			if (!IsEnabled(commandManager))
				return;

			_builder.Stop();
        }

        public override bool IsEnabled(CommandManager commandManager)
        {
            return IsSolutionOpen(commandManager) && _builder.IsBuilding;
        }
    }
}
using RudeBuild;

namespace RudeBuildVSShared
{
    public sealed class GlobalSettingsCommand : CommandBase
    {
        private Builder _builder;

        public GlobalSettingsCommand(Builder builder)
        {
            _builder = builder;
        }

        public override void Execute(CommandManager commandManager)
        {
			if (!IsEnabled(commandManager))
				return;

			GlobalSettingsDialog dialog = new GlobalSettingsDialog(_builder.Output);
            try
            {
                dialog.ShowDialog();
            }
            finally
            {
                dialog.Close();
            }
        }

        public override bool IsEnabled(CommandManager commandManager)
        {
            return !_builder.IsBuilding;
        }
    }
}

using RudeBuild;

namespace RudeBuildAddIn
{
    public class SolutionSettingsCommand : CommandBase
    {
        private Builder _builder;

        public SolutionSettingsCommand(Builder builder)
        {
            _builder = builder;
        }

        public override void Execute(CommandManager commandManager)
        {
        }

        public override bool IsEnabled(CommandManager commandManager)
        {
            return IsSolutionOpen(commandManager) && !_builder.IsBuilding;
        }
    }
}

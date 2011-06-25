using RudeBuild;

namespace RudeBuildAddIn
{
    public class GlobalSettingsCommand : CommandBase
    {
        public override void Execute(CommandManager commandManager)
        {
            GlobalSettings globalSettings = new GlobalSettings();
            GlobalSettingsDialog dialog = new GlobalSettingsDialog(globalSettings);
            dialog.ShowDialog();
        }

        public override bool IsEnabled(CommandManager commandManager)
        {
            return true;
        }
    }
}

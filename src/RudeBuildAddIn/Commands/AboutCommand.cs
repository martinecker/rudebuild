using System.Windows;

namespace RudeBuildAddIn
{
    public class AboutCommand : CommandBase
    {
        public override void Execute(CommandManager commandManager)
        {
            string aboutMessage = @"
RudeBuild, Version 1.0

A unity C++ build tool for Visual Studio developed by Martin Ecker.
This is free, open source software under the zlib license.";

            MessageBox.Show(aboutMessage, "RudeBuild", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public override bool IsEnabled(CommandManager commandManager)
        {
            return true;
        }
    }
}
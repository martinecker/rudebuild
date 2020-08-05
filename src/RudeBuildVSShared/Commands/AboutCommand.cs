using System.Windows;

namespace RudeBuildVSShared
{
    public sealed class AboutCommand : CommandBase
    {
        public override void Execute(CommandManager commandManager)
        {
            string aboutMessage = @"
RudeBuild, Version 1.6

A unity C++ build tool for Visual Studio developed by Martin Ecker.
This is free, open source software under the zlib license.

For more information and latest updates please visit:
http://www.martinecker.com/rudebuild/
";

            MessageBox.Show(aboutMessage, "RudeBuild", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public override bool IsEnabled(CommandManager commandManager)
        {
            return true;
        }
    }
}
using System.Windows;
using RudeBuild;

namespace RudeBuildVSShared
{
    public sealed partial class GlobalSettingsDialog : Window
    {
        private readonly GlobalSettings _globalSettings;

        public GlobalSettingsDialog(IOutput output)
        {
            InitializeComponent();
            _globalSettings = GlobalSettings.Load(output);
            _window.DataContext = _globalSettings;
        }

        private void OnOK(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_globalSettings.FileNamePrefix) && string.IsNullOrEmpty(_globalSettings.FileNameSuffix))
            {
                ErrorMessage.Content = "Please specify either a file name prefix, suffix, or both.";
            }
            else
            {
                DialogResult = true;
                _globalSettings.Save();
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}

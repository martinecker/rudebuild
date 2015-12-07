using System.Windows;
using RudeBuild;

namespace RudeBuildVSShared
{
    public partial class GlobalSettingsDialog : Window
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
            _globalSettings.Save();
            DialogResult = true;
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}

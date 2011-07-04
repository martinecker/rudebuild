using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using RudeBuild;

namespace RudeBuildAddIn
{
    public partial class SolutionSettingsDialog : Window
    {
        private Settings _settings;
        private SolutionInfo _solutionInfo;

        public SolutionSettingsDialog(Settings settings, SolutionInfo solutionInfo)
        {
            _settings = settings;
            _solutionInfo = solutionInfo;

            InitializeComponent();
            _window.DataContext = _settings.SolutionSettings;
        }

        private void OnOK(object sender, RoutedEventArgs e)
        {
            _settings.SolutionSettings.Save(_settings, _solutionInfo);
            DialogResult = true;
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}

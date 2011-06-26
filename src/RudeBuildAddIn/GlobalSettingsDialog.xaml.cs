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
    public partial class GlobalSettingsDialog : Window
    {
        private GlobalSettings _globalSettings;

        public GlobalSettingsDialog()
        {
            InitializeComponent();
            _globalSettings = GlobalSettings.Load();
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

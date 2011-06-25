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
    /// <summary>
    /// Interaction logic for GlobalSettingsDialog.xaml
    /// </summary>
    public partial class GlobalSettingsDialog : Window
    {
        private GlobalSettings _globalSettings;

        public GlobalSettingsDialog(GlobalSettings globalSettings)
        {
            _globalSettings = globalSettings;
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

        }
    }
}

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
        private SolutionSettings _solutionSettings;

        public SolutionSettingsDialog(Settings settings, SolutionInfo solutionInfo)
        {
            _settings = settings;
            _solutionInfo = solutionInfo;
            _solutionSettings = SolutionSettings.Load(settings, solutionInfo);

            InitializeComponent();
            _window.DataContext = _solutionSettings;
        }

        private void OnOK(object sender, RoutedEventArgs e)
        {
            _settings.SolutionSettings = _solutionSettings;
            _solutionSettings.Save(_settings, _solutionInfo);
            DialogResult = true;
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            TreeViewItem treeViewItem = VisualUpwardSearch<TreeViewItem>(e.OriginalSource as DependencyObject) as TreeViewItem;
            if (null != treeViewItem)
            {
                treeViewItem.Focus();
                e.Handled = true;
            }
        }

        private static DependencyObject VisualUpwardSearch<T>(DependencyObject source)
        {
            while (source != null && source.GetType() != typeof(T))
                source = VisualTreeHelper.GetParent(source);
            return source;
        }

        private ProjectInfo GetSelectedProjectInfo()
        {
            if (null == _treeViewExcludedFileNames.SelectedValue)
                return null;
            string projectName = _treeViewExcludedFileNames.SelectedValue as string;
            if (string.IsNullOrEmpty(projectName))
                return null;
            ProjectInfo projectInfo = _solutionInfo.GetProjectInfo(projectName);
            return projectInfo;
        }

        private void OnAddExcludedCppFileNameForProject(object sender, RoutedEventArgs e)
        {
            ProjectInfo projectInfo = GetSelectedProjectInfo();
            if (null == projectInfo)
                return;

            SolutionSettingsAddExcludedCppFileNameDialog dialog = new SolutionSettingsAddExcludedCppFileNameDialog(projectInfo, _solutionSettings);
            try
            {
                dialog.ShowDialog();

                if (dialog.DialogResult == true)
                {
                    foreach (string fileName in dialog.FileNamesToExclude)
                    {
                        _solutionSettings.ExcludeCppFileNameForProject(projectInfo, fileName);
                    }
                }
            }
            finally
            {
                dialog.Close();
            }
        }
    }
}

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

        private void OnAddExcludedCppFileNameFromProject(object sender, RoutedEventArgs e)
        {
            ProjectInfo projectInfo = GetSelectedProjectInfo();
            if (null == projectInfo)
                return;

            _settings.SolutionSettings.ExcludeCppFileNameForProject(projectInfo, "hugo.cpp");
        }
    }
}

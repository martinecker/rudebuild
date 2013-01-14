using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    public partial class SolutionSettingsAddExcludedCppFileNameDialog : Window
    {
        private readonly ProjectInfo _projectInfo;
        private readonly SolutionSettings _solutionSettings;

        public IList<string> FileNamesToExclude;

        public SolutionSettingsAddExcludedCppFileNameDialog(ProjectInfo projectInfo, SolutionSettings solutionSettings)
        {
            _solutionSettings = solutionSettings;
            _projectInfo = projectInfo;

            InitializeComponent();

            var existingExcludedFileNames = _solutionSettings.GetExcludedCppFileNamesForProject(_projectInfo.Name);
            var fileNames = new ReadOnlyCollection<string>(
                (from fileName in _projectInfo.CppFileNames
                 where !existingExcludedFileNames.Contains(fileName)
                 select fileName).ToList());
            _window.DataContext = fileNames;
        }

        private void OnOK(object sender, RoutedEventArgs e)
        {
            DialogResult = true;

            FileNamesToExclude = new List<string>();
            foreach (string fileName in _listBoxFileNames.SelectedItems)
            {
                FileNamesToExclude.Add(fileName);
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void OnFileNameMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_listBoxFileNames.SelectedItems.Count == 1)
            {
                FileNamesToExclude = new List<string>() { (string)_listBoxFileNames.SelectedItems[0] };
                DialogResult = true;
                Close();
            }
        }

        private void OnFileNamePreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var clickedControl = e.OriginalSource as DependencyObject;
            if (null != clickedControl)
            {
                var listBox = clickedControl.VisualUpwardSearch<ListBox>();
                if (_listBoxFileNames == listBox)
                {
                    var clickedListBoxItem = clickedControl.VisualUpwardSearch<ListBoxItem>();
                    listBox.UnselectAll();
                    clickedListBoxItem.IsSelected = true;
                }
            }
        }
    }
}

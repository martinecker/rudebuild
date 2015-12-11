using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell.Interop;
using RudeBuild;

namespace RudeBuildVSShared
{
	public class SolutionHierarchy
	{
		public class Item
		{
			public enum ItemType
			{
				Folder,
				CppFile
			}

			public ItemType Type { get; private set; }
			public string Name { get; private set; }
			public List<Item> Items { get; private set; }

			public Item(string folderName, List<Item> items)
			{
				Type = ItemType.Folder;
				Name = folderName;
				Items = items;
			}

			public Item(string cppFileName)
			{
				Type = ItemType.CppFile;
				Name = cppFileName;
				Items = null;
			}
		}

		public enum IconType
		{
			Project = 0,
			Folder = 1,
			CppFile = 2
		}

		private readonly Settings _settings;
		private IDictionary<IconType, BitmapSource> _icons = new Dictionary<IconType, BitmapSource>();

		public Dictionary<string, List<Item>> ProjectNameToCppFileNameMap { get; private set; }

		private const int MAX_PATH = 260;
		private const int S_OK = 0;
		private const uint VSITEMID_NIL = 0xFFFFFFFF;
		private const uint VSITEMID_ROOT = 0xFFFFFFFE;

		[StructLayout(LayoutKind.Sequential)]
		private struct SHFILEINFO
		{
			internal IntPtr hIcon;
			internal IntPtr iIcon;
			internal uint dwAttributes;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
			internal string szDisplayName;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
			internal string szTypeName;
		};

		[DllImport("shell32.dll", EntryPoint = "SHGetFileInfoW", ExactSpelling = true, CharSet = CharSet.Unicode)]
		private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

		[DllImport("comctl32.dll", CharSet = CharSet.None, ExactSpelling = false)]
		private static extern IntPtr ImageList_GetIcon(IntPtr imageListHandle, int iconIndex, int flags);

		[DllImport("user32.dll")]
		private static extern bool DestroyIcon(IntPtr hIcon);

		[DllImport("gdi32.dll")]
		public static extern bool DeleteObject(IntPtr hObject);

		public SolutionHierarchy(CommandManager commandManager, Settings settings)
		{
			_settings = settings;
			_icons[IconType.Project] = null;
			_icons[IconType.Folder] = null;
			_icons[IconType.CppFile] = null;

			ProjectNameToCppFileNameMap = new Dictionary<string, List<Item>>();

			try
			{
				ProcessSolution(commandManager);
			}
			catch
			{
				ProjectNameToCppFileNameMap.Clear();
			}
		}

		public BitmapSource GetIcon(IconType type)
		{
			return _icons[type];
		}

		private bool GetProjectItemId(IVsHierarchy projectHierarchy, EnvDTE.ProjectItem projectItem, out uint projectItemId)
		{
			projectItemId = 0;

			string projectItemName = null;
			try { projectItemName = projectItem.get_FileNames(0); } catch { }
			if (string.IsNullOrEmpty(projectItemName))
				return false;

			if (projectHierarchy.ParseCanonicalName(projectItemName, out projectItemId) != 0)
				return false;

			return true;
		}

		private enum ShouldDestroyIcon
		{
			No = 0,
			Yes = 1
		}

		private enum FolderState
		{
			Open = 1,
			Closed = 2
		}

		private BitmapSource CreateBitmapSourceFromIconHandle(IntPtr iconHandle, ShouldDestroyIcon shouldDestroyIcon)
		{
			BitmapSource result = null;

			using (var icon = System.Drawing.Icon.FromHandle(iconHandle))
			{
				var bitmap = new System.Drawing.Bitmap(icon.Width, icon.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
				using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
				{
					graphics.DrawIcon(icon, 0, 0);
				}

				IntPtr bitmapHandle = bitmap.GetHbitmap();
				try
				{
					result = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(bitmapHandle,
						IntPtr.Zero, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(icon.Width, icon.Height));
				}
				finally
				{
					DeleteObject(bitmapHandle);
				}
			}

			if (shouldDestroyIcon == ShouldDestroyIcon.Yes)
			{
				DestroyIcon(iconHandle);
			}

			return result;
		}

		private BitmapSource ExtractIconWithImageList(IVsHierarchy hierarchy, uint itemId, FolderState folderState)
		{
			const int ILD_NORMAL = 0;

			object imageListHandleObject = null;
			int hr = hierarchy.GetProperty(VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_IconImgList, out imageListHandleObject);
			if (hr != S_OK)
				return null;

			IntPtr imageListHandle = new IntPtr(Convert.ToInt64(imageListHandleObject));

			// Get the icon index
			object iconIndexObject = null;
			switch (folderState)
			{
			case FolderState.Closed:
				hr = hierarchy.GetProperty(itemId, (int)__VSHPROPID.VSHPROPID_IconIndex, out iconIndexObject);
				break;
			case FolderState.Open:
				hr = hierarchy.GetProperty(itemId, (int)__VSHPROPID.VSHPROPID_OpenFolderIconIndex, out iconIndexObject);
				break;
			}
			if (hr != S_OK)
				return null;

			int iconIndex = Convert.ToInt32(iconIndexObject);
			IntPtr iconHandle = ImageList_GetIcon(imageListHandle, iconIndex, ILD_NORMAL);
			if (iconHandle == IntPtr.Zero)
				return null;

			return CreateBitmapSourceFromIconHandle(iconHandle, ShouldDestroyIcon.Yes);
		}

		private BitmapSource ExtractIconWithoutImageList(IVsHierarchy hierarchy, uint itemId, FolderState folderState)
		{
			int hr = S_OK;
			object iconHandleObject = null;
			switch (folderState)
			{
			case FolderState.Closed:
				hr = hierarchy.GetProperty(itemId, (int)__VSHPROPID.VSHPROPID_IconHandle, out iconHandleObject);
				break;
			case FolderState.Open:
				hr = hierarchy.GetProperty(itemId, (int)__VSHPROPID.VSHPROPID_OpenFolderIconHandle, out iconHandleObject);
				break;
			}
			if (hr != S_OK)
				return null;

			var iconHandle = new IntPtr(Convert.ToInt64(iconHandleObject));
			if (iconHandle == IntPtr.Zero)
				return null;

			return CreateBitmapSourceFromIconHandle(iconHandle, ShouldDestroyIcon.No);
		}

		private BitmapSource ExtractIconFromShell(string fullFileName)
		{
			const int SHGFI_ICON = 0x100;
			const int SHGFI_SMALLICON = 0x001;

			if (string.IsNullOrEmpty(fullFileName))
				return null;

			var shellFileInfo = new SHFILEINFO();
			uint size = (uint)Marshal.SizeOf(shellFileInfo);
			SHGetFileInfo(fullFileName, 0, ref shellFileInfo, size, SHGFI_ICON | SHGFI_SMALLICON);
			if (shellFileInfo.hIcon == IntPtr.Zero)
				return null;

			return CreateBitmapSourceFromIconHandle(shellFileInfo.hIcon, ShouldDestroyIcon.Yes);
		}

		private void ExtractIcon(IconType type, IVsHierarchy projectHierarchy, EnvDTE.ProjectItem projectItem)
		{
			if (projectHierarchy == null || projectItem == null)
				return;

			uint projectItemId;
			if (!GetProjectItemId(projectHierarchy, projectItem, out projectItemId))
				return;

			ExtractIcon(type, projectHierarchy, projectItemId);
		}

		private void ExtractIcon(IconType type, IVsHierarchy projectHierarchy, uint projectItemId)
		{
			if (projectHierarchy == null)
				return;

			bool isValidProjectItemId = projectItemId != 0 && projectItemId != VSITEMID_NIL;
			if (!isValidProjectItemId)
				return;

			if (GetIcon(type) != null)
				return;

			// Case 1: Try to get the icon from the hierachy with imagelist
			BitmapSource icon = ExtractIconWithImageList(projectHierarchy, projectItemId, FolderState.Open);
			if (null == icon)
			{
				// Case 2: Try to get the icon from the hierachy without imagelist
				// This is the case, for example, of files of project VS 2010, Database > SQL Server > SQL Server 2005 Database Project
				icon = ExtractIconWithoutImageList(projectHierarchy, projectItemId, FolderState.Open);
				if (null == icon)
				{
					// Case 3: Try to get the icon from the Windows shell
					string canonicalName;
					projectHierarchy.GetCanonicalName(projectItemId, out canonicalName);
					icon = ExtractIconFromShell(canonicalName);
				}
			}

			if (null != icon)
				_icons[type] = icon;
		}

		private void ProcessSolution(CommandManager commandManager)
		{
			var solutionService = commandManager.GetService<IVsSolution>(typeof(SVsSolution));

			foreach (EnvDTE.Project project in commandManager.Application.Solution.Projects)
			{
				var projectData = new List<Item>();
				ProcessProject(solutionService, project, projectData);
				if (projectData.Count > 0)
					ProjectNameToCppFileNameMap[project.Name] = projectData;
			}
		}

		private void ProcessProject(IVsSolution solutionService, EnvDTE.Project project, List<Item> projectData)
		{
			IVsHierarchy projectHierarchy = null;

			if (solutionService.GetProjectOfUniqueName(project.UniqueName, out projectHierarchy) == 0)
			{
				if (projectHierarchy != null)
				{
					uint projectItemId;
					if (projectHierarchy.ParseCanonicalName(project.FileName, out projectItemId) == 0)
						ExtractIcon(IconType.Project, projectHierarchy, projectItemId);

					ProcessProjectItems(solutionService, projectHierarchy, project.ProjectItems, projectData);
				}
			}
		}

		private void ProcessProjectItems(IVsSolution solutionService, IVsHierarchy projectHierarchy, EnvDTE.ProjectItems projectItems, List<Item> projectData)
		{
			if (projectItems != null)
			{
				foreach (EnvDTE.ProjectItem projectItem in projectItems)
				{
					if (projectItem.SubProject != null)
					{
						var folderItems = new List<Item>();

						ProcessProject(solutionService, projectItem.SubProject, folderItems);

						if (folderItems.Count > 0)
						{
							projectData.Add(new Item(projectItem.SubProject.Name, folderItems));
						}
					}
					else
					{
						string name = projectItem.Name;
						if (!string.IsNullOrEmpty(name))
						{
							if (projectItem.FileCount == 1)
							{
								if (_settings.IsValidCppFileName(name))
								{
									ExtractIcon(IconType.CppFile, projectHierarchy, projectItem);
									projectData.Add(new Item(name));
								}
							}
							else if (projectItem.FileCount > 1)
							{
								var folderItems = new List<Item>();

								ExtractIcon(IconType.Folder, projectHierarchy, projectItem);

								// Enter folder recursively
								ProcessProjectItems(solutionService, projectHierarchy, projectItem.ProjectItems, folderItems);

								if (folderItems.Count > 0)
									projectData.Add(new Item(name, folderItems));
							}
						}
					}
				}
			}
		}
	}

    public partial class SolutionSettingsDialog : Window
    {
		private readonly CommandManager _commandManager;
		private readonly Settings _settings;
        private readonly SolutionInfo _solutionInfo;
        private readonly SolutionSettings _solutionSettings;

		private TreeViewItem _selectedTreeViewItem;
		private SolutionHierarchy _solutionHierarchy;

        public SolutionSettingsDialog(CommandManager commandManager, Settings settings, SolutionInfo solutionInfo)
        {
			_commandManager = commandManager;
            _settings = settings;
            _solutionInfo = solutionInfo;
            _solutionSettings = SolutionSettings.Load(settings, solutionInfo);

			_solutionHierarchy = new SolutionHierarchy(commandManager, settings);

			InitializeComponent();
            _window.DataContext = _solutionSettings;

			RefreshProjectsTreeView();
        }

		private void AddProjectsTreeViewItem(ItemCollection items, string projectName, IList<SolutionHierarchy.Item> projectItems)
		{
			ProjectInfo projectInfo = _solutionInfo.GetProjectInfo(projectName);
			if (null == projectInfo)
				return;

			var treeViewItem = new TreeViewItem() { FontSize = 11, FontWeight = FontWeights.Bold };

			var image = new Image() { Source = _solutionHierarchy.GetIcon(SolutionHierarchy.IconType.Project) };
			var label = new Label() { Content = projectName };

			var stack = new StackPanel() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, -2, 0, -2) };
			stack.Children.Add(image);
			stack.Children.Add(label);
			treeViewItem.Header = stack;

			items.Add(treeViewItem);

			foreach (var item in projectItems)
			{
				if (item.Type == SolutionHierarchy.Item.ItemType.CppFile)
					AddCppFileTreeViewItem(projectInfo, treeViewItem.Items, item.Name);
				else
					AddFolderTreeViewITem(projectInfo, treeViewItem.Items, item.Name, item.Items);
			}
		}

		private void AddFolderTreeViewITem(ProjectInfo projectInfo, ItemCollection items, string folderName, IList<SolutionHierarchy.Item> folderItems)
		{
			var treeViewItem = new TreeViewItem() { FontWeight = FontWeights.Normal };

			var image = new Image() { Source = _solutionHierarchy.GetIcon(SolutionHierarchy.IconType.Folder) };
			var label = new Label() { Content = folderName };

			var stack = new StackPanel() { Orientation = Orientation.Horizontal };
			stack.Children.Add(image);
			stack.Children.Add(label);
			treeViewItem.Header = stack;

			items.Add(treeViewItem);

			foreach (var item in folderItems)
			{
				if (item.Type == SolutionHierarchy.Item.ItemType.CppFile)
					AddCppFileTreeViewItem(projectInfo, treeViewItem.Items, item.Name);
				else
					AddFolderTreeViewITem(projectInfo, treeViewItem.Items, item.Name, item.Items);
			}
		}

		private void AddCppFileTreeViewItem(ProjectInfo projectInfo, ItemCollection items, string cppFileName)
		{
			var treeViewItem = new TreeViewItem() { FontWeight = FontWeights.Normal };

			var checkBox = new CheckBox()
			{
				IsChecked = !_solutionSettings.IsExcludedCppFileNameForProject(projectInfo, cppFileName),
				VerticalAlignment = VerticalAlignment.Center
			};
			checkBox.Checked += (sender, eventArgs) => { _solutionSettings.RemoveExcludedCppFileNameForProject(projectInfo, cppFileName); };
			checkBox.Unchecked += (sender, eventArgs) => { _solutionSettings.ExcludeCppFileNameForProject(projectInfo, cppFileName); };

			var image = new Image() { Source = _solutionHierarchy.GetIcon(SolutionHierarchy.IconType.CppFile) };
			var label = new Label() { Content = cppFileName };

			var stack = new StackPanel() { Orientation = Orientation.Horizontal };
			stack.Children.Add(checkBox);
			stack.Children.Add(image);
			stack.Children.Add(label);
			treeViewItem.Header = stack;

			items.Add(treeViewItem);
		}

		private void RefreshProjectsTreeView()
		{
			_treeViewProjects.Items.Clear();
			foreach (var project in _solutionHierarchy.ProjectNameToCppFileNameMap)
			{
				AddProjectsTreeViewItem(_treeViewProjects.Items, project.Key, project.Value);
			}
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

        private void OnTreeViewItemSelected(object sender, RoutedEventArgs e)
        {
            _selectedTreeViewItem = e.OriginalSource as TreeViewItem;
        }

        private void OnTreeViewItemUnselected(object sender, RoutedEventArgs e)
        {
            _selectedTreeViewItem = null;
        }
		
		private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
			var sh = new SolutionHierarchy(_commandManager, _settings);

			var clickedControl = e.OriginalSource as DependencyObject;
            if (null != clickedControl)
            {
                var treeViewItem = clickedControl.VisualUpwardSearch<TreeViewItem>();
                if (null != treeViewItem)
                {
                    treeViewItem.Focus();
                    e.Handled = true;
                }
            }
        }

        private string GetSelectedFileName()
        {
            if (null == _treeViewExcludedFileNames.SelectedItem)
                return null;
            string fileName = _treeViewExcludedFileNames.SelectedItem as string;
            if (string.IsNullOrEmpty(fileName))
                return null;
            return fileName;
        }

        private ProjectInfo GetProjectInfoFromTreeViewItem(TreeViewItem projectTreeViewItem)
        {
            var textBlock = projectTreeViewItem.VisualDownwardSearch<TextBlock>();
            if (null == textBlock)
                return null;
            string projectName = textBlock.Text;
            ProjectInfo projectInfo = _solutionInfo.GetProjectInfo(projectName);
            if (null == projectInfo)
                return null;
            return projectInfo;
        }

        private void RefreshTreeViewBinding()
        {
            BindingOperations.ClearBinding(_treeViewExcludedFileNames, ItemsControl.ItemsSourceProperty);
            _treeViewExcludedFileNames.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("ProjectNameToExcludedCppFileNameMap"));
        }

        private void OnAddExcludedCppFileNameForProject(object sender, RoutedEventArgs e)
        {
            if (null == _selectedTreeViewItem)
                return;
            ProjectInfo projectInfo = GetProjectInfoFromTreeViewItem(_selectedTreeViewItem);
            if (null == projectInfo)
                return;

            var dialog = new SolutionSettingsAddExcludedCppFileNameDialog(projectInfo, _solutionSettings);
            try
            {
                dialog.ShowDialog();

                if (dialog.DialogResult == true)
                {
                    foreach (string fileName in dialog.FileNamesToExclude)
                    {
                        _solutionSettings.ExcludeCppFileNameForProject(projectInfo, fileName);
                    }
                    RefreshTreeViewBinding();
                }
            }
            finally
            {
                dialog.Close();
            }
        }

        private void OnRemoveExcludedCppFileNameForProject(object sender, RoutedEventArgs e)
        {
            if (null == _selectedTreeViewItem)
                return;

            string fileName = GetSelectedFileName();
            if (null == fileName)
                return;

            DependencyObject parentControl = VisualTreeHelper.GetParent(_selectedTreeViewItem);
            if (null == parentControl)
                return;
            var projectTreeViewItem = parentControl.VisualUpwardSearch<TreeViewItem>();
            if (null == projectTreeViewItem)
                return;
            ProjectInfo projectInfo = GetProjectInfoFromTreeViewItem(projectTreeViewItem);
            if (null == projectInfo)
                return;

            _solutionSettings.RemoveExcludedCppFileNameForProject(projectInfo, fileName);
            RefreshTreeViewBinding();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
        private readonly SolutionInfo _solutionInfo;

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

        public SolutionHierarchy(CommandManager commandManager, SolutionInfo solutionInfo, Settings settings)
        {
            _solutionInfo = solutionInfo;
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

            try
            {
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
            catch
            {
                // Ignore exceptions. If we couldn't extract the icon that's not the end of the world.
            }
        }

        private void ProcessSolution(CommandManager commandManager)
        {
            var solutionService = commandManager.GetService<IVsSolution>(typeof(SVsSolution));

            foreach (EnvDTE.Project project in commandManager.Application.Solution.Projects)
            {
                var projectInfo = _solutionInfo.GetProjectInfo(project.Name);
                if (null != projectInfo)
                {
                    var projectData = new List<Item>();
                    ProcessProject(solutionService, project, projectInfo, projectData);
                    if (projectData.Count > 0)
                        ProjectNameToCppFileNameMap[project.Name] = projectData;
                }
            }
        }

        private void ProcessProject(IVsSolution solutionService, EnvDTE.Project project, ProjectInfo projectInfo, List<Item> projectData)
        {
            IVsHierarchy projectHierarchy = null;

            if (solutionService.GetProjectOfUniqueName(project.UniqueName, out projectHierarchy) == 0)
            {
                if (projectHierarchy != null)
                {
                    uint projectItemId;
                    if (projectHierarchy.ParseCanonicalName(project.FileName, out projectItemId) == 0)
                        ExtractIcon(IconType.Project, projectHierarchy, projectItemId);

                    ProcessProjectItems(solutionService, projectHierarchy, projectInfo, project.ProjectItems, projectData);
                }
            }
        }

        private void ProcessProjectItems(IVsSolution solutionService, IVsHierarchy projectHierarchy, ProjectInfo projectInfo, EnvDTE.ProjectItems projectItems, List<Item> projectData)
        {
            if (projectItems != null)
            {
                foreach (EnvDTE.ProjectItem projectItem in projectItems)
                {
                    if (projectItem.SubProject != null)
                    {
                        var folderItems = new List<Item>();

                        ProcessProject(solutionService, projectItem.SubProject, projectInfo, folderItems);

                        if (folderItems.Count > 0)
                        {
                            projectData.Add(new Item(projectItem.SubProject.Name, folderItems));
                        }
                    }
                    else if (projectItem.FileCount >= 1)
                    {
                        string name = null;
                        try { name = projectItem.get_FileNames(0); } catch { }
                        if (!string.IsNullOrEmpty(name))
                        {
                            if (projectItem.FileCount == 1)
                            {
                                if (_settings.IsValidCppFileName(name))
                                {
                                    ExtractIcon(IconType.CppFile, projectHierarchy, projectItem);

                                    // Certain projects contain absolute file names, also handle those. Usually though, the file names in a project are project-relative.
                                    if (projectInfo.AllCppFileNames.Contains(name))
                                    {
                                        projectData.Add(new Item(name));
                                    }
                                    else
                                    {
                                        string projectRelativeName = projectInfo.GetProjectRelativePathFromAbsolutePath(name);
                                        if (projectInfo.AllCppFileNames.Contains(projectRelativeName))
                                            projectData.Add(new Item(projectRelativeName));
                                    }
                                }
                            }
                            else
                            {
                                ExtractIcon(IconType.Folder, projectHierarchy, projectItem);

                                // Enter folder recursively
                                var folderItems = new List<Item>();
                                ProcessProjectItems(solutionService, projectHierarchy, projectInfo, projectItem.ProjectItems, folderItems);
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

        private SolutionHierarchy _solutionHierarchy;

        public SolutionSettingsDialog(CommandManager commandManager, Settings settings, SolutionInfo solutionInfo)
        {
            _commandManager = commandManager;
            _settings = settings;
            _solutionInfo = solutionInfo;
            _solutionSettings = SolutionSettings.Load(settings, solutionInfo);

            _solutionHierarchy = new SolutionHierarchy(commandManager, solutionInfo, settings);

            InitializeComponent();
            _window.DataContext = _solutionSettings;    // Used to bind the checkboxes in the dialog to the solution settings.

            RefreshProjectsTreeView();      // The projects tree view is explicitly created in code, no WPF data binding is used for simplicity.
            InitializeProjectsTreeViewButtons();
        }

        private void PerformActionOnTreeViewItems(TreeViewItem treeViewItem, Action<TreeViewItem> action)
        {
            foreach (TreeViewItem childTreeViewItem in treeViewItem.Items)
            {
                PerformActionOnTreeViewItems(childTreeViewItem, action);
                action(childTreeViewItem);
            }

            action(treeViewItem);
        }

        private void ExpandTreeViewItemRecursively(TreeViewItem startTreeViewItem)
        {
            PerformActionOnTreeViewItems(startTreeViewItem, (treeViewItem) => { treeViewItem.IsExpanded = true; });
        }

        private void CollapseTreeViewItemRecursively(TreeViewItem startTreeViewItem)
        {
            PerformActionOnTreeViewItems(startTreeViewItem, (treeViewItem) => { treeViewItem.IsExpanded = false; });
        }

        private void SetIncludeStateForAllFilesForTreeViewItemRecursively(TreeViewItem startTreeViewItem, bool isIncluded)
        {
            PerformActionOnTreeViewItems(startTreeViewItem, (treeViewItem) => 
            {
                foreach (var child in LogicalTreeHelper.GetChildren((StackPanel)treeViewItem.Header))
                {
                    CheckBox checkBox = child as CheckBox;
                    if (checkBox != null)
                    {
                        checkBox.IsChecked = isIncluded;
                    }
                }
            });
        }

        private void AddProjectOrFolderTreeViewItemContextMenu(ContextMenu contextMenu, TreeViewItem treeViewItem)
        {
            var contextMenuItemExpandAll = new MenuItem() { Header = "Expand All" };
            var contextMenuItemCollapseAll = new MenuItem() { Header = "Collapse All" };
            contextMenuItemExpandAll.Click += (sender, eventArgs) => { ExpandTreeViewItemRecursively(treeViewItem); };
            contextMenuItemCollapseAll.Click += (sender, eventArgs) => { CollapseTreeViewItemRecursively(treeViewItem); };
            contextMenu.Items.Add(contextMenuItemExpandAll);
            contextMenu.Items.Add(contextMenuItemCollapseAll);

            contextMenu.Items.Add(new Separator());

            var contextMenuItemIncludeAll = new MenuItem() { Header = "Include All Files in Unity Build" };
            var contextMenuItemExcludeAll = new MenuItem() { Header = "Exclude All Files from Unity Build" };
            contextMenuItemIncludeAll.Click += (sender, eventArgs) => { SetIncludeStateForAllFilesForTreeViewItemRecursively(treeViewItem, true); };
            contextMenuItemExcludeAll.Click += (sender, eventArgs) => { SetIncludeStateForAllFilesForTreeViewItemRecursively(treeViewItem, false); };
            contextMenu.Items.Add(contextMenuItemIncludeAll);
            contextMenu.Items.Add(contextMenuItemExcludeAll);
        }

        private void AddProjectTreeViewItem(TreeView treeView, string projectName, IList<SolutionHierarchy.Item> projectItems)
        {
            ProjectInfo projectInfo = _solutionInfo.GetProjectInfo(projectName);
            if (null == projectInfo)
                return;

            var treeViewItem = new TreeViewItem() { FontSize = 11, FontWeight = FontWeights.Bold };
            treeViewItem.DataContext = projectInfo;
            treeViewItem.ContextMenu = new ContextMenu();
            AddProjectOrFolderTreeViewItemContextMenu(treeViewItem.ContextMenu, treeViewItem);

            var image = new Image() { Source = _solutionHierarchy.GetIcon(SolutionHierarchy.IconType.Project) };
            var label = new Label() { Content = projectName };

            var stack = new StackPanel() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, -2, 0, -2) };
            stack.Children.Add(image);
            stack.Children.Add(label);
            treeViewItem.Header = stack;

            treeView.Items.Add(treeViewItem);

            foreach (var item in projectItems)
            {
                if (item.Type == SolutionHierarchy.Item.ItemType.CppFile)
                    AddCppFileTreeViewItem(projectInfo, treeViewItem, item);
                else
                    AddFolderTreeViewItem(projectInfo, treeViewItem, item.Name, item.Items);
            }
        }

        private void AddFolderTreeViewItem(ProjectInfo projectInfo, TreeViewItem parentTreeViewItem, string folderName, IList<SolutionHierarchy.Item> folderItems)
        {
            var treeViewItem = new TreeViewItem() { FontWeight = FontWeights.Normal };
            treeViewItem.ContextMenu = new ContextMenu();
            AddProjectOrFolderTreeViewItemContextMenu(treeViewItem.ContextMenu, treeViewItem);

            var image = new Image() { Source = _solutionHierarchy.GetIcon(SolutionHierarchy.IconType.Folder) };
            var label = new Label() { Content = folderName };

            var stack = new StackPanel() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, -2, 0, -2) };
            stack.Children.Add(image);
            stack.Children.Add(label);
            treeViewItem.Header = stack;

            parentTreeViewItem.Items.Add(treeViewItem);

            foreach (var item in folderItems)
            {
                if (item.Type == SolutionHierarchy.Item.ItemType.CppFile)
                    AddCppFileTreeViewItem(projectInfo, treeViewItem, item);
                else
                    AddFolderTreeViewItem(projectInfo, treeViewItem, item.Name, item.Items);
            }
        }

        private void AddCppFileTreeViewItem(ProjectInfo projectInfo, TreeViewItem parentTreeViewItem, SolutionHierarchy.Item item)
        {
            const string kExcludeFromUnityBuild = "Exclude from Unity Build";
            const string kIncludeInUnityBuild = "Include in Unity Build";

            string cppFileName = item.Name;

            var treeViewItem = new TreeViewItem() { FontWeight = FontWeights.Normal };
            treeViewItem.DataContext = item;
            treeViewItem.ContextMenu = new ContextMenu();

            var checkBox = new CheckBox()
            {
                IsChecked = !_solutionSettings.IsExcludedCppFileNameForProject(projectInfo, cppFileName),
                VerticalAlignment = VerticalAlignment.Center
            };

            var contextMenuItemExclude = new MenuItem()
            {
                Header = checkBox.IsChecked.Value ? kExcludeFromUnityBuild : kIncludeInUnityBuild,
            };
            contextMenuItemExclude.Click += (sender, eventArgs) =>
            {
                checkBox.IsChecked = !checkBox.IsChecked.Value;
            };
            treeViewItem.ContextMenu.Items.Add(contextMenuItemExclude);

            checkBox.Checked += (sender, eventArgs) => 
            {
                _solutionSettings.RemoveExcludedCppFileNameForProject(projectInfo, cppFileName);
                contextMenuItemExclude.Header = kExcludeFromUnityBuild;
            };
            checkBox.Unchecked += (sender, eventArgs) => 
            {
                _solutionSettings.ExcludeCppFileNameForProject(projectInfo, cppFileName);
                contextMenuItemExclude.Header = kIncludeInUnityBuild;
            };

            var image = new Image() { Source = _solutionHierarchy.GetIcon(SolutionHierarchy.IconType.CppFile) };
            var label = new Label() { Content = System.IO.Path.GetFileName(cppFileName) };

            var stack = new StackPanel() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, -2, 0, -2) };
            stack.Children.Add(checkBox);
            stack.Children.Add(image);
            stack.Children.Add(label);
            treeViewItem.Header = stack;

            parentTreeViewItem.Items.Add(treeViewItem);
        }

        private void RefreshProjectsTreeView()
        {
            _treeViewProjects.Items.Clear();
            foreach (var project in _solutionHierarchy.ProjectNameToCppFileNameMap)
            {
                AddProjectTreeViewItem(_treeViewProjects, project.Key, project.Value);
            }
        }

        private void CollapseTreeViewItemIfAllChildrenCollapsed(TreeViewItem treeViewItem)
        {
            bool allCollapsed = true;
            foreach (TreeViewItem childTreeViewItem in treeViewItem.Items)
            {
                if (childTreeViewItem.Visibility == Visibility.Visible)
                {
                    allCollapsed = false;
                    break;
                }
            }
            if (allCollapsed)
                treeViewItem.Visibility = Visibility.Collapsed;
        }

        private ContextMenu CreateFilterButtonContextMenu()
        {
            var filterContextMenu = new ContextMenu();
            filterContextMenu.Placement = PlacementMode.Bottom;
            filterContextMenu.PlacementTarget = _buttonFilter;
            _buttonFilter.ContextMenu = filterContextMenu;

            var filterContextMenuItemAll = new MenuItem() { Header = "Show All" };
            filterContextMenuItemAll.Click += (sender, eventArgs) =>
            {
                foreach (TreeViewItem projectTreeViewItem in _treeViewProjects.Items)
                {
                    PerformActionOnTreeViewItems(projectTreeViewItem, (treeViewItem) => { treeViewItem.Visibility = Visibility.Visible; });
                }
                filterContextMenu.IsOpen = false;
            };
            filterContextMenu.Items.Add(filterContextMenuItemAll);

            var filterContextMenuItemOnlyExcluded = new MenuItem() { Header = "Show Only Excluded Files" };
            filterContextMenuItemOnlyExcluded.Click += (sender, eventArgs) =>
            {
                foreach (TreeViewItem projectTreeViewItem in _treeViewProjects.Items)
                {
                    ProjectInfo projectInfo = (ProjectInfo)projectTreeViewItem.DataContext;

                    PerformActionOnTreeViewItems(projectTreeViewItem, (treeViewItem) => { treeViewItem.Visibility = Visibility.Visible; });

                    PerformActionOnTreeViewItems(projectTreeViewItem,
                        (treeViewItem) =>
                        {
                            SolutionHierarchy.Item item = treeViewItem.DataContext as SolutionHierarchy.Item;
                            if (null != item && item.Type == SolutionHierarchy.Item.ItemType.CppFile)
                            {
                                treeViewItem.Visibility = _solutionSettings.IsExcludedCppFileNameForProject(projectInfo, item.Name) ?
                                    Visibility.Visible : Visibility.Collapsed;
                            }
                            else
                            {
                                CollapseTreeViewItemIfAllChildrenCollapsed(treeViewItem);
                            }
                        });
                }
                filterContextMenu.IsOpen = false;
            };
            filterContextMenu.Items.Add(filterContextMenuItemOnlyExcluded);

            var filterContextMenuItemOnlyIncluded = new MenuItem() { Header = "Show Only Included Files" };
            filterContextMenuItemOnlyIncluded.Click += (sender, eventArgs) =>
            {
                foreach (TreeViewItem projectTreeViewItem in _treeViewProjects.Items)
                {
                    ProjectInfo projectInfo = (ProjectInfo)projectTreeViewItem.DataContext;

                    PerformActionOnTreeViewItems(projectTreeViewItem, (treeViewItem) => { treeViewItem.Visibility = Visibility.Visible; });

                    PerformActionOnTreeViewItems(projectTreeViewItem,
                        (treeViewItem) =>
                        {
                            SolutionHierarchy.Item item = treeViewItem.DataContext as SolutionHierarchy.Item;
                            if (null != item && item.Type == SolutionHierarchy.Item.ItemType.CppFile)
                            {
                                treeViewItem.Visibility = !_solutionSettings.IsExcludedCppFileNameForProject(projectInfo, item.Name) ?
                                    Visibility.Visible : Visibility.Collapsed;
                            }
                            else
                            {
                                CollapseTreeViewItemIfAllChildrenCollapsed(treeViewItem);
                            }
                        });
                }
                filterContextMenu.IsOpen = false;
            };
            filterContextMenu.Items.Add(filterContextMenuItemOnlyIncluded);

            return filterContextMenu;
        }

        private void InitializeProjectsTreeViewButtons()
        {
            var filterContextMenu = CreateFilterButtonContextMenu();

            _buttonFilter.Click += (sender, eventsArgs) =>
            {
                filterContextMenu.IsOpen = true;
            };
            _buttonExpandAll.Click += (sender, eventArgs) =>
            {
                foreach (TreeViewItem treeViewItem in _treeViewProjects.Items)
                    ExpandTreeViewItemRecursively(treeViewItem);
            };
            _buttonCollapseAll.Click += (sender, eventArgs) =>
            {
                foreach (TreeViewItem treeViewItem in _treeViewProjects.Items)
                    CollapseTreeViewItemRecursively(treeViewItem);
            };
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
    }
}

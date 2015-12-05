using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using System.Xml;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Deployment.WindowsInstaller;
using WixSharp;
using WixSharp.CommonTasks;
using EnvDTE80;

class Script
{
    static void Main(string[] args)
    {
        var customActionInstall = new ManagedAction("OnInstall", Return.ignore, When.After, Step.InstallFinalize, Condition.NOT_Installed, Sequence.InstallExecuteSequence);
        var customActionUninstall = new ManagedAction("OnUninstall", Return.ignore, When.Before, Step.RemoveFiles, Condition.Installed, Sequence.InstallExecuteSequence);
        customActionInstall.RefAssemblies = customActionUninstall.RefAssemblies = new string[] { "CommandLineParser.dll", "RudeBuild.dll", "RudeBuildAddIn.dll" };

        var project = new Project()
        {
            Name = "RudeBuild",

            UI = WUI.WixUI_InstallDir,
            LicenceFile = "LICENSE.rtf",
            PreserveTempFiles = true,
            Platform = Platform.x86,

            // The UpgradeCode is the unique GUID for RudeBuild. All versions of RudeBuild should have the same GUID
            // so that newer version MSI installers will detect the previous version before installing a new version.
            // The ProductId is a GUID for the current version of RudeBuild and should be regenerated whenever the version changes!
            // See http://blogs.msdn.com/b/pusu/archive/2009/06/10/understanding-msi.aspx
            // In Wix# the project GUID is an essentially an alias for UpgradeCode, so it's enough to set it.
            Version = new Version("1.4.0.0"),
            GUID = new Guid("DA0E1E6E-57AE-45B1-8D10-A546E1BCF2E6"),
            ProductId = new Guid("D46693C3-3BF1-4761-8A5F-077CEA2F7A86"),
           
            Dirs = new[]
            {
                new Dir(@"%ProgramFiles%\RudeBuild", 
                    new WixEntity[]
                    {
                        new File(@"CommandLineParser.dll"),
                        new File(@"RudeBuild.AddIn"),
                        new File(@"RudeBuild.dll"),
                        new File(@"RudeBuildAddIn.dll"),
                        new File(@"RudeBuildConsole.exe"),
                        new File(@"LICENSE.rtf"),
                        new File(@"LICENSE.txt"),
                        new File(@"README.txt"),
                        new Dir(@"en-US", new File(@"en-US\RudeBuild.resources.dll"))
                    }
                )
            },

            Actions = new []
            {
                customActionInstall,
                customActionUninstall
            }
        };

        project.ControlPanelInfo = new ProductInfo()
        {
            InstallLocation = "[INSTALLDIR]",
            Comments = "RudeBuild - A bulk/unity C++ build tool for Visual Studio, developed by Martin Ecker.",
            Contact = "Martin Ecker",
            Manufacturer = "Martin Ecker",
            HelpLink = "http://rudebuild.sourceforge.net",
            UrlInfoAbout = "http://www.martinecker.com/rudebuild/about/",
            UrlUpdateInfo = "http://rudebuild.sourceforge.net",
            Readme = @"
A bulk/unity C++ build tool for Visual Studio, developed by Martin Ecker.
This is free, open source software under the zlib license.

For more information and latest updates please visit:
http://rudebuild.sourceforge.net

RudeBuild is a non-intrusive bulk/unity C++ build tool that seamlessly integrates into Visual Studio 2008, 2010, and 2012 as an add-in.  It can speed up build times of large C++ projects by a factor of 5 or more.  RudeBuild also supports the use of IncrediBuild to speed up your build times even more.

RudeBuild comes in two flavors, as a command-line tool that works on Visual Studio solution files and as a Visual Studio add-in complete with toolbar and menus.

When used as an add-in the toolbar acts and looks just like the regular build toolbar but behind the scenes a bulk/unity build of C++ projects is triggered, automatically combining the .cpp files into unity files in a cache location and running devenv to build the modified solution/project.  Using RudeBuild in this manner is transparent to the developer and requires no modification to the original source code or project files whatsoever given that the codebase is bulk/unity build-safe.  Being bulk/unity-build safe means that there are no symbols with the same name in two different translation units.  For example, it is invalid to have a static function called GetFileTime in both File1.cpp and File2.cpp.

The command line version of RudeBuild is useful for automated builds, for example on build servers.  A solution file, build configuration and optionally a project name must be specified on the command line.
"
        };

        project.MajorUpgrade = new MajorUpgrade()
        {
            AllowDowngrades = false,
            DowngradeErrorMessage = "A later version of [ProductName] is already installed. [ProductName] setup will now exit.",
            AllowSameVersionUpgrades = false,
            Disallow = true,
            DisallowUpgradeErrorMessage = "A different version of [ProductName] is already installed. Please first uninstall that version and then re-run this setup.",
            IgnoreRemoveFailure = true,
            Schedule = UpgradeSchedule.afterInstallInitialize
        };

        project.SetNetFxPrerequisite("NETFRAMEWORK35='#1'", "Please install .NET 3.5 first.");

        Compiler.BuildMsi(project, "RudeBuildSetup.msi");
    }
}

public class CustomActions
{
    private const string AddInFileName = "RudeBuild.AddIn";
    private const string GlobalSettingsFileName = "RudeBuild.GlobalSettings.config";
    private const string RudeBuildSetupRegistryPath = @"SOFTWARE\RudeBuildSetup";

    private static void PatchAddInFile(string filePath, string installationPath)
    {
        XDocument document = XDocument.Load(filePath);
        if (null == document || null == document.Root)
        {
            throw new System.IO.InvalidDataException("Couldn't load required add-in file '" + filePath + "'.");
        }

        XNamespace ns = document.Root.Name.Namespace;
        XElement assemblyElement = document.Descendants(ns + "Assembly").Single();
        assemblyElement.Value = System.IO.Path.Combine(installationPath, "RudeBuildAddIn.dll");
        document.Save(filePath);
    }

    private static bool IsVisualStudioInstalled(RudeBuild.VisualStudioVersion version)
    {
        string registryPath = RudeBuild.ProcessLauncher.GetDevEvnBaseRegistryKey(version);
        RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(registryPath);
        return registryKey != null;
    }

    private static string GetUserPersonalFolder(string versionString, RudeBuild.VisualStudioVersion version)
    {
        string userPersonalFolder = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        if (!string.IsNullOrEmpty(userPersonalFolder))
            return userPersonalFolder;

        string registryPath = RudeBuild.ProcessLauncher.GetDevEvnBaseRegistryKey(version);
        RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(registryPath);
        if (null == registryKey)
            throw new ArgumentException("Couldn't open Visual Studio registry key. Your version of Visual Studio is unsupported by this tool or Visual Studio is not installed properly.");

        userPersonalFolder = registryKey.GetValue("MyDocumentsLocation") as string;
        if (!string.IsNullOrEmpty(userPersonalFolder))
            return userPersonalFolder;

        throw new ApplicationException("Couldn't determine the user's Documents folder for Visual Studio " + versionString);
    }

    private static void InstallAddInFile(string filePath, string versionString, RudeBuild.VisualStudioVersion version)
    {
        if (!IsVisualStudioInstalled(version))
            return;

        try
        {
            string userPersonalFolder = GetUserPersonalFolder(versionString, version);
            string installDirectory = System.IO.Path.Combine(userPersonalFolder, "Visual Studio " + versionString);
            installDirectory = System.IO.Path.Combine(installDirectory, "Addins");

            if (!System.IO.Directory.Exists(installDirectory))
                System.IO.Directory.CreateDirectory(installDirectory);

            string installPath = System.IO.Path.Combine(installDirectory, AddInFileName);
            System.IO.File.Copy(filePath, installPath, true);

            RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(RudeBuildSetupRegistryPath);
            if (null == registryKey)
                throw new ArgumentException("Couldn't open RudeBuild setup registry key.");
            registryKey.SetValue("AddInInstallPath" + versionString, installPath);
            registryKey.Close();
        }
        catch (Exception ex)
        {
            string installDirectory = System.IO.Path.Combine("Visual Studio " + versionString, "Addins");
            System.Windows.MessageBox.Show(
                "Couldn't install RudeBuild.AddIn file to Visual Studio add-ins folder '" + installDirectory + "'.\n" +
                "You might be able to simply copy the file manually to the add-ins folder and Visual Studio should pick it up.\n" +
                "Alternatively, you can manually install the add-in in Visual Studio by going to Tools/Options/Add-in and adding the RudeBuild installation folder as add-in folder.\n\n" +
                "Exception message: " + ex.Message,
                "RudeBuild",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private static void UninstallAddInFile(string versionString, RudeBuild.VisualStudioVersion version)
    {
        RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(RudeBuildSetupRegistryPath);
        if (null == registryKey)
            return;

        string installPath = registryKey.GetValue("AddInInstallPath" + versionString) as string;
        registryKey.Close();
        if (string.IsNullOrEmpty(installPath))
            return;

        if (System.IO.File.Exists(installPath))
        {
            System.IO.File.Delete(installPath);
        }
    }

    private static object CreateComObjectFromProgId(string progId)
    {
        try
        {
            Type type = Type.GetTypeFromProgID(progId);
            if (type != null)
            {
                return Activator.CreateInstance(type);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }

        return null;
    }

    private static void DebugBreak()
    {
        // If you want to debug this installer, follow these steps:
        // - Uncomment the following line.
        // - Rebuild the installer.
        // - Run the installer.
        // - When the first installer dialog comes up, attach the debugger to the installer process.
        // - You need to attach to msiexec.exe. Often times there will be multiple processes with this name.
        //   Find one that is managed (instead of fully native).
        //   Alternatively, use Spy++ to find the process for the message box and then attach to that.
        //   When you attach in Visual Studio, make sure to check "Show processes from all users" and enabled attaching to managed processes.
        // - Continue normally through the installation process. You will hit this breakpoint eventually.
        //System.Windows.MessageBox.Show("Attach debugger now!");
        //Debugger.Break();
    }

    private static string GetDevEnvProgId(RudeBuild.VisualStudioVersion version)
    {
        switch (version)
        {
            case RudeBuild.VisualStudioVersion.VS2005: return "VisualStudio.DTE.8.0";
            case RudeBuild.VisualStudioVersion.VS2008: return "VisualStudio.DTE.9.0";
            case RudeBuild.VisualStudioVersion.VS2010: return "VisualStudio.DTE.10.0";
            case RudeBuild.VisualStudioVersion.VS2012: return "VisualStudio.DTE.11.0";
            case RudeBuild.VisualStudioVersion.VS2013: return "VisualStudio.DTE.12.0";
            case RudeBuild.VisualStudioVersion.VS2015: return "VisualStudio.DTE.14.0";
            default: return null;
        }
    }

    private static void RemoveCommands(string versionString, RudeBuild.VisualStudioVersion version)
    {
        DTE2 application = null;
        try
        {
            string devEnvProgId = GetDevEnvProgId(version);
            if (string.IsNullOrEmpty(devEnvProgId))
                return;
            application = CreateComObjectFromProgId(devEnvProgId) as DTE2;
            if (null == application)
                return;

            MessageFilter.Register();

            var connect = new RudeBuildAddIn.Connect();
            connect.OnUninstall(application);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("RudeBuild uninstall error while removing commands from Visual Studio " + versionString + "!\n" + ex.Message,
                "RudeBuild", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            if (null != application)
            {
                application.Quit();
                MessageFilter.Revoke();
            }
        }
    }

    private void ResetAddIn(RudeBuild.VisualStudioVersion version)
    {
        try
        {
            var process = new Process();
            ProcessStartInfo info = process.StartInfo;
            info.CreateNoWindow = true;
            info.UseShellExecute = false;
            info.WindowStyle = ProcessWindowStyle.Hidden;
            info.ErrorDialog = false;
            info.FileName = RudeBuild.ProcessLauncher.GetDevEnvPath(version);
            info.Arguments = " /ResetAddIn RudeBuildAddIn.Connect /Command File.Exit";
            if (process.Start())
            {
                process.WaitForExit();
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("RudeBuild uninstall error! Couldn't reset RudeBuild add-in!\n" + ex.Message, 
                "RudeBuild", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [CustomAction]
    public static ActionResult OnInstall(Session session)
    {
        DebugBreak();

        try
        {
            string installationPath = session.Property("INSTALLDIR");
            string addInFilePath = System.IO.Path.Combine(installationPath, AddInFileName);
            PatchAddInFile(addInFilePath, installationPath);
            InstallAddInFile(addInFilePath, "2008", RudeBuild.VisualStudioVersion.VS2008);
            InstallAddInFile(addInFilePath, "2010", RudeBuild.VisualStudioVersion.VS2010);
            InstallAddInFile(addInFilePath, "2012", RudeBuild.VisualStudioVersion.VS2012);
            InstallAddInFile(addInFilePath, "2013", RudeBuild.VisualStudioVersion.VS2013);
            InstallAddInFile(addInFilePath, "2015", RudeBuild.VisualStudioVersion.VS2015);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("RudeBuild install error trying to patch and copy the RudeBuild.AddIn file to the user's Visual Studio add-in folder!\n" + ex.Message,
                "RudeBuild", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }

        return ActionResult.Success;
    }

    [CustomAction]
    public static ActionResult OnUninstall(Session session)
    {
        DebugBreak();

        try
        {
            RemoveCommands("2008", RudeBuild.VisualStudioVersion.VS2008);
            RemoveCommands("2010", RudeBuild.VisualStudioVersion.VS2010);
            RemoveCommands("2012", RudeBuild.VisualStudioVersion.VS2012);
            RemoveCommands("2013", RudeBuild.VisualStudioVersion.VS2013);
            RemoveCommands("2015", RudeBuild.VisualStudioVersion.VS2015);

            UninstallAddInFile("2008", RudeBuild.VisualStudioVersion.VS2008);
            UninstallAddInFile("2010", RudeBuild.VisualStudioVersion.VS2010);
            UninstallAddInFile("2012", RudeBuild.VisualStudioVersion.VS2012);
            UninstallAddInFile("2013", RudeBuild.VisualStudioVersion.VS2013);
            UninstallAddInFile("2015", RudeBuild.VisualStudioVersion.VS2015);

            //ResetAddIn(VisualStudioVersion.VS2008);
            //ResetAddIn(VisualStudioVersion.VS2010);
            //ResetAddIn(VisualStudioVersion.VS2012);
            //ResetAddIn(VisualStudioVersion.VS2013);
            //ResetAddIn(VisualStudioVersion.VS2015);

            string installationPath = session.Property("INSTALLDIR");
            string globalSettingsPath = System.IO.Path.Combine(installationPath, GlobalSettingsFileName);
            if (System.IO.File.Exists(globalSettingsPath))
            {
                System.IO.File.Delete(globalSettingsPath);
            }

            Registry.CurrentUser.DeleteSubKeyTree(RudeBuildSetupRegistryPath);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("RudeBuild uninstall error!\n" + ex.Message, 
                "RudeBuild", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }

        return ActionResult.Success;
    }
}

// The following message filter class is from https://msdn.microsoft.com/en-us/library/ms228772.aspx 
//   "How to: Fix 'Application is Busy' and 'Call was Rejected By Callee' Errors"
// to fix spurious RPC_E_SERVERCALL_RETRYLATER errors from RemoveCommands() when calling into devenv
// to remove the registered RudeBuild commands.

// Class containing the IOleMessageFilter thread error-handling functions.
public class MessageFilter : IOleMessageFilter
{
    // Start the filter.
    public static void Register()
    {
        IOleMessageFilter newFilter = new MessageFilter();
        IOleMessageFilter oldFilter = null;
        CoRegisterMessageFilter(newFilter, out oldFilter);
    }

    // Done with the filter, close it.
    public static void Revoke()
    {
        IOleMessageFilter oldFilter = null;
        CoRegisterMessageFilter(null, out oldFilter);
    }

    // Handle incoming thread requests.
    int IOleMessageFilter.HandleInComingCall(int dwCallType, System.IntPtr hTaskCaller, int dwTickCount, System.IntPtr lpInterfaceInfo)
    {
        //Return the flag SERVERCALL_ISHANDLED.
        return 0;
    }

    // Thread call was rejected, so try again.
    int IOleMessageFilter.RetryRejectedCall(System.IntPtr hTaskCallee, int dwTickCount, int dwRejectType)
    {
        if (dwRejectType == 2)
        // flag = SERVERCALL_RETRYLATER.
        {
            // Retry the thread call immediately if return >=0 & 
            // <100.
            return 99;
        }
        // Too busy; cancel call.
        return -1;
    }

    int IOleMessageFilter.MessagePending(System.IntPtr hTaskCallee, int dwTickCount, int dwPendingType)
    {
        //Return the flag PENDINGMSG_WAITDEFPROCESS.
        return 2;
    }

    // Implement the IOleMessageFilter interface.
    [DllImport("Ole32.dll")]
    private static extern int CoRegisterMessageFilter(IOleMessageFilter newFilter, out IOleMessageFilter oldFilter);
}

[ComImport(), Guid("00000016-0000-0000-C000-000000000046"),
InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
interface IOleMessageFilter
{
    [PreserveSig]
    int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo);

    [PreserveSig]
    int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType);

    [PreserveSig]
    int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType);
}

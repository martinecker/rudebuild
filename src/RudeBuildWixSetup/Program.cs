using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Deployment.WindowsInstaller;
using WixSharp;
using WixSharp.CommonTasks;

class Script
{
    static void Main(string[] args)
    {
        var customActionInstall = new ManagedAction(CustomActions.OnInstall, Return.ignore, When.After, Step.InstallFinalize, Condition.NOT_Installed, Sequence.InstallExecuteSequence);
        var customActionUninstall = new ManagedAction(CustomActions.OnUninstall, Return.ignore, When.Before, Step.RemoveFiles, Condition.Installed, Sequence.InstallExecuteSequence);
        customActionInstall.RefAssemblies = customActionUninstall.RefAssemblies = new string[] { "CommandLineParser.dll", "RudeBuild.dll", "RudeBuildVSShared.dll" };

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
            GUID = new Guid("DA0E1E6E-57AE-45B1-8D10-A546E1BCF2E6"),

            Version = new Version("1.5.0.0"),
            ProductId = new Guid("D6090F57-83BD-45B2-8349-10A84C7C2FA3"),   // Change this whenever you change the version above!

            Dirs = new[]
            {
                new Dir(@"%ProgramFiles%\RudeBuild",
                    new WixEntity[]
                    {
                        new File(@"CommandLineParser.dll"),
                        new File(@"RudeBuild.dll"),
                        new File(@"RudeBuildVSShared.dll"),
                        new File(@"RudeBuildConsole.exe"),
                        new File(@"RudeBuildVSIX.vsix"),
                        new File(@"LICENSE.rtf"),
                        new File(@"LICENSE.txt"),
                        new File(@"README.txt"),
                    }
                )
            },

            Actions = new[]
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
            HelpLink = "http://www.martinecker.com/rudebuild/",
            UrlInfoAbout = "http://www.martinecker.com/rudebuild/about/",
            UrlUpdateInfo = "http://www.martinecker.com/rudebuild/",
            Readme = @"
A bulk/unity C++ build tool for Visual Studio, developed by Martin Ecker.
This is free, open source software under the zlib license.

For more information and latest updates please visit:
http://www.martinecker.com/rudebuild/

RudeBuild is a non-intrusive bulk/unity C++ build tool that seamlessly integrates into Visual Studio 2008, 2010, and 2012 as an add-in.  It can speed up build times of large C++ projects by a factor of 5 or more.  RudeBuild also supports the use of IncrediBuild to speed up your build times even more.

RudeBuild comes in two flavors, as a command-line tool that works on Visual Studio solution files and as a Visual Studio add-in complete with toolbar and menus.

When used as an add-in the toolbar acts and looks just like the regular build toolbar but behind the scenes a bulk/unity build of C++ projects is triggered, automatically combining the .cpp files into unity files in a cache location and running devenv to build the modified solution/project.  Using RudeBuild in this manner is transparent to the developer and requires no modification to the original source code or project files whatsoever given that the codebase is bulk/unity build-safe.  Being bulk/unity-build safe means that there are no symbols with the same name in two different translation units.  For example, it is invalid to have a static function called GetFileTime in both File1.cpp and File2.cpp.

The command line version of RudeBuild is useful for automated builds, for example on build servers.  A solution file, build configuration and optionally a project name must be specified on the command line.
"
        };

        project.MajorUpgradeStrategy = MajorUpgradeStrategy.Default;
        project.MajorUpgradeStrategy.UpgradeVersions = VersionRange.ThisAndOlder;
        project.MajorUpgradeStrategy.RemoveExistingProductAfter = Step.InstallInitialize;

        // Different way to handle upgrades. To use this can't set up MajorUpgradeStrategy.
        //project.MajorUpgrade = MajorUpgrade.Default;
        //project.MajorUpgrade.AllowDowngrades = false;
        //project.MajorUpgrade.DowngradeErrorMessage = "A later version of [ProductName] is already installed. [ProductName] setup will now exit.";
        //project.MajorUpgrade.AllowSameVersionUpgrades = false;
        //project.MajorUpgrade.IgnoreRemoveFailure = true;
        //project.MajorUpgrade.Schedule = UpgradeSchedule.afterInstallInitialize;

        project.SetNetFxPrerequisite(Condition.Net471_Installed, "Please install .NET Framework 4.7.1 first");

        // We're writing the target executable of this setup project into ..\..\bin\Release or Debug.
        // That's also where all the other .dll files that need to be packaged into the installer are located,
        // so set the source base directory for these files to be the executable target path.
        project.SourceBaseDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        // Get nuget packages folder, which should have the WixSharp.wix.bin package installed
        // that we need to reference to get the wix compiler/linker executables to build the installer.
        var settings = NuGet.Configuration.Settings.LoadDefaultSettings(null);
        string packagesFolder = NuGet.Configuration.SettingsUtility.GetGlobalPackagesFolder(settings);
        Compiler.WixLocation = System.IO.Path.Combine(packagesFolder, @"wixsharp.wix.bin\3.11.2\tools\bin");

        Compiler.BuildMsi(project, "RudeBuildSetup.msi");
    }
}

public class CustomActions
{
    private const string VSIXFileName = "RudeBuildVSIX.vsix";
    private const string GlobalSettingsFileName = "RudeBuild.GlobalSettings.config";
    private const string RudeBuildSetupRegistryPath = @"SOFTWARE\RudeBuildSetup";

    [DllImport("User32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);

    private static bool IsVisualStudioInstalled(RudeBuild.VisualStudioVersion version)
    {
        try
        {
            var devEnvPath = System.IO.Path.Combine(RudeBuild.ProcessLauncher.GetDevEnvDir(version), "devenv.com");
            return System.IO.File.Exists(devEnvPath);
        }
        catch (Exception)
        {
            return false;
        }
    }
     
    private static RudeBuild.VisualStudioVersion[] supportedVSIXVersions = new RudeBuild.VisualStudioVersion[]
        {
            // sort from latest to oldest version
            RudeBuild.VisualStudioVersion.VS2019,
            RudeBuild.VisualStudioVersion.VS2017,
        };

    private static RudeBuild.VisualStudioVersion? GetHighestInstalledVSIXVisualStudio()
    {
        // Even though VSPackage support started with Visual Studio 2010, the RudeBuild VSIX
        // only supports 2017 and beyond, so we only run the VSIX installer if 2017+ is installed.
        foreach (var version in supportedVSIXVersions)
        {
            if (IsVisualStudioInstalled(version))
                return version;
        }
        return null;
    }

    private static void InstallVSIX(string filePath)
    {
        RudeBuild.VisualStudioVersion? highestVSVersion = GetHighestInstalledVSIXVisualStudio();
        if (highestVSVersion == null)
            throw new System.Exception("Couldn't find a supported version of Visual Studio to install the RudeBuild VSIX. Visual Studio 2017 or higher is required.");

        try
        {
            var devEnvDir = RudeBuild.ProcessLauncher.GetDevEnvDir(highestVSVersion.Value);
            var vsixInstallerPath = System.IO.Path.Combine(devEnvDir, "VSIXInstaller.exe");
            var vsixInstallerCommandLine = "\"" + filePath + "\"";

            Process process = Process.Start(vsixInstallerPath, vsixInstallerCommandLine);
            SetForegroundWindow(process.MainWindowHandle);
            process.WaitForExit();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                "Couldn't successfully run the RudeBuild extension installer file '" + filePath + "'.\n" +
                "You might be able to simply run this file manually to install the extension in Visual Studio.\n" +
                "Exception message: " + ex.Message,
                "RudeBuild",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private static void UninstallVSIX(string filePath)
    {
        RudeBuild.VisualStudioVersion? highestVSVersion = GetHighestInstalledVSIXVisualStudio();
        if (highestVSVersion == null)
            return;

        try
        {
            var devEnvDir = RudeBuild.ProcessLauncher.GetDevEnvDir(highestVSVersion.Value);
            var vsixInstallerPath = System.IO.Path.Combine(devEnvDir, "VSIXInstaller.exe");
            var vsixInstallerCommandLine = "/quiet /uninstall:RudeBuild.0d68d53e-21ed-4ee6-8510-8c437e43b090";  // The VSIX ID comes from the ProductID in the source.extension.vsixmanifest file!

            Process process = Process.Start(vsixInstallerPath, vsixInstallerCommandLine);
            process.WaitForExit();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                "Couldn't successfully run the RudeBuild extension installer file '" + filePath + "'.\n" +
                "You might be able to manually uninstall the RudeBuild extension in Visual Studio.\n" +
                "Exception message: " + ex.Message,
                "RudeBuild",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
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

    [CustomAction]
    public static ActionResult OnInstall(Session session)
    {
        DebugBreak();

        try
        {
            string installationPath = session.Property("INSTALLDIR");
            string vsixFilePath = System.IO.Path.Combine(installationPath, VSIXFileName);
            InstallVSIX(vsixFilePath);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("RudeBuild install error trying to install the VSIX!\n" + ex.Message,
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
            string installationPath = session.Property("INSTALLDIR");
            string globalSettingsPath = System.IO.Path.Combine(installationPath, GlobalSettingsFileName);
            if (System.IO.File.Exists(globalSettingsPath))
            {
                System.IO.File.Delete(globalSettingsPath);
            }

            RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(RudeBuildSetupRegistryPath);
            if (null != registryKey)
                Registry.CurrentUser.DeleteSubKeyTree(RudeBuildSetupRegistryPath);

            string vsixFilePath = System.IO.Path.Combine(installationPath, VSIXFileName);
            UninstallVSIX(vsixFilePath);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("RudeBuild uninstall error!\n" + ex.Message,
                "RudeBuild", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }

        return ActionResult.Success;
    }
}

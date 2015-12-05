using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Security.Permissions;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using System.Xml;
using System.Windows;
using Microsoft.Win32;
using EnvDTE80;
using RudeBuild;

namespace RudeBuildAddIn
{
	/* This is a custom action used by the RudeBuildSetup setup project to patch and copy the RudeBuild.AddIn
	 * file to the correct folder for every version of Visual Studio installed.
	 * An example destination folder is <UserDocuments>\Visual Studio 2010\Addins.
	 * 
	 * To find where this custom action is hooked up in the RudeBuildSetup project, right-click on the project,
	 * then select View/Custom Actions. Note that RudeBuildSetup has a custom post build step that patches
	 * the generated .msi file to set the NoImpersonate flag to false (from its default value of true) for
	 * custom actions. Normally the code of this class would run on a computer account not bound to the
	 * user that started the installation. However, since we need to get the user's personal documents folder
	 * we want to run this code with the permissions of the user. Setting NoImpersonate to false achieves this.
	 * This also means this installer cannot access any machine-wide data, such as the HKLM registry key!
	 * 
	 * See http://msdn.microsoft.com/en-us/library/vstudio/2kt85ked(v=vs.100).aspx and
	 * http://blogs.msdn.com/b/astebner/archive/2006/10/23/mailbag-how-to-set-the-noimpersonate-flag-for-a-custom-action-in-visual-studio-2005.aspx
	 */
	[RunInstaller(true)]
    public partial class Installer : System.Configuration.Install.Installer
    {
        private const string AddInFileName = "RudeBuild.AddIn";
        private const string GlobalSettingsFileName = "RudeBuild.GlobalSettings.config";

        public Installer()
        {
            InitializeComponent();
        }

        private void PatchAddInFile(string filePath, string installationPath)
        {
            XDocument document = XDocument.Load(filePath);
            if (null == document || null == document.Root)
            {
                throw new InvalidDataException("Couldn't load required add-in file '" + filePath + "'.");
            }

            XNamespace ns = document.Root.Name.Namespace;
            XElement assemblyElement = document.Descendants(ns + "Assembly").Single();
            assemblyElement.Value = Path.Combine(installationPath, "RudeBuildAddIn.dll");
            document.Save(filePath);
        }

        private static bool IsVisualStudioInstalled(VisualStudioVersion version)
        {
            string registryPath = ProcessLauncher.GetDevEvnBaseRegistryKey(version);
            RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(registryPath);
            return registryKey != null;
        }

        private static string GetUserPersonalFolder(string versionString, VisualStudioVersion version)
        {
            string userPersonalFolder = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            if (!string.IsNullOrEmpty(userPersonalFolder))
                return userPersonalFolder;

            string registryPath = ProcessLauncher.GetDevEvnBaseRegistryKey(version);
            RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(registryPath);
            if (null == registryKey)
                throw new ArgumentException("Couldn't open Visual Studio registry key. Your version of Visual Studio is unsupported by this tool or Visual Studio is not installed properly.");

            userPersonalFolder = registryKey.GetValue("MyDocumentsLocation") as string;
            if (!string.IsNullOrEmpty(userPersonalFolder))
                return userPersonalFolder;

            throw new ApplicationException("Couldn't determine the user's Documents folder for Visual Studio " + versionString);
        }

        private void InstallAddInFile(IDictionary savedState, string filePath, string versionString, VisualStudioVersion version)
        {
            if (!IsVisualStudioInstalled(version))
                return;

            try
            {
                string userPersonalFolder = GetUserPersonalFolder(versionString, version);
                string installDirectory = Path.Combine(userPersonalFolder, "Visual Studio " + versionString);
                installDirectory = Path.Combine(installDirectory, "Addins");

                if (!Directory.Exists(installDirectory))
                    Directory.CreateDirectory(installDirectory);

                string installPath = Path.Combine(installDirectory, AddInFileName);
                File.Copy(filePath, installPath, true);
                savedState.Add("AddInInstallPath" + versionString, installPath);
            }
            catch (Exception ex)
            {
                string installDirectory = Path.Combine("Visual Studio " + versionString, "Addins");
                MessageBox.Show(
                    "Couldn't install RudeBuild.AddIn file to Visual Studio add-ins folder '" + installDirectory + "'.\n" +
                    "You might be able to simply copy the file manually to the add-ins folder and Visual Studio should pick it up.\n" +
                    "Alternatively, you can manually install the add-in in Visual Studio by going to Tools/Options/Add-in and adding the RudeBuild installation folder as add-in folder.\n\n" +
                    "Exception message: " + ex.Message,
                    "RudeBuild",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UninstallAddInFile(IDictionary savedState, string versionString, VisualStudioVersion version)
        {
            if (!IsVisualStudioInstalled(version))
                return;

            if (savedState.Contains("AddInInstallPath" + versionString))
            {
                var installPath = (string)savedState["AddInInstallPath" + versionString];
                if (!string.IsNullOrEmpty(installPath))
                {
                    if (File.Exists(installPath))
                    {
                        File.Delete(installPath);
                    }
                }
            }
        }

        private static string GetInstallationPath()
        {
            return Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        }

        public static object CreateComObjectFromProgId(string progId)
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

        private void DebugBreak()
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
            //MessageBox.Show("Attach debugger now!");
            //Debugger.Break();
        }

        [SecurityPermission(SecurityAction.Demand)]
        public override void Install(IDictionary savedState)
        {
            DebugBreak();

            base.Install(savedState);

            try
            {
                string installationPath = GetInstallationPath();
                string addInFilePath = Path.Combine(installationPath, AddInFileName);
                PatchAddInFile(addInFilePath, installationPath);
                InstallAddInFile(savedState, addInFilePath, "2008", VisualStudioVersion.VS2008);
                InstallAddInFile(savedState, addInFilePath, "2010", VisualStudioVersion.VS2010);
                InstallAddInFile(savedState, addInFilePath, "2012", VisualStudioVersion.VS2012);
                InstallAddInFile(savedState, addInFilePath, "2013", VisualStudioVersion.VS2013);
                InstallAddInFile(savedState, addInFilePath, "2015", VisualStudioVersion.VS2015);
            }
            catch (Exception ex)
            {
                MessageBox.Show("RudeBuild install error!\n" + ex.Message, "RudeBuild", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [SecurityPermission(SecurityAction.Demand)]
        public override void Commit(IDictionary savedState)
        {
            DebugBreak();
            base.Commit(savedState);
            // Nothing to do here for now.
        }

        private static string GetDevEnvProgId(VisualStudioVersion version)
        {
            switch (version)
            {
            case VisualStudioVersion.VS2005: return "VisualStudio.DTE.8.0";
            case VisualStudioVersion.VS2008: return "VisualStudio.DTE.9.0";
            case VisualStudioVersion.VS2010: return "VisualStudio.DTE.10.0";
            case VisualStudioVersion.VS2012: return "VisualStudio.DTE.11.0";
            case VisualStudioVersion.VS2013: return "VisualStudio.DTE.12.0";
            case VisualStudioVersion.VS2015: return "VisualStudio.DTE.14.0";
            default: return null;
            }
        }

        private void RemoveCommands(VisualStudioVersion version)
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

                var connect = new Connect();
                connect.OnUninstall(application);
            }
            catch (Exception ex)
            {
                MessageBox.Show("RudeBuild error while removing commands from Visual Studio!\n" + ex.Message, "RudeBuild", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (null != application)
                {
                    application.Quit();
                }
            }
        }

        private void UninstallOrRollback(IDictionary savedState)
        {
            try
            {
                RemoveCommands(VisualStudioVersion.VS2008);
                RemoveCommands(VisualStudioVersion.VS2010);
                RemoveCommands(VisualStudioVersion.VS2012);
                RemoveCommands(VisualStudioVersion.VS2013);
                RemoveCommands(VisualStudioVersion.VS2015);

                UninstallAddInFile(savedState, "2008", VisualStudioVersion.VS2008);
                UninstallAddInFile(savedState, "2010", VisualStudioVersion.VS2010);
                UninstallAddInFile(savedState, "2012", VisualStudioVersion.VS2012);
                UninstallAddInFile(savedState, "2013", VisualStudioVersion.VS2013);
                UninstallAddInFile(savedState, "2015", VisualStudioVersion.VS2015);

                string installationPath = GetInstallationPath();
                string globalSettingsPath = Path.Combine(installationPath, GlobalSettingsFileName);
                if (File.Exists(globalSettingsPath))
                {
                    File.Delete(globalSettingsPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("RudeBuild uninstall error!\n" + ex.Message, "RudeBuild", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [SecurityPermission(SecurityAction.Demand)]
        public override void Rollback(IDictionary savedState)
        {
            DebugBreak();
            base.Rollback(savedState);
            UninstallOrRollback(savedState);
        }

        private void ResetAddIn(VisualStudioVersion version)
        {
            try
            {
                var process = new Process();
                ProcessStartInfo info = process.StartInfo;
                info.CreateNoWindow = true;
                info.UseShellExecute = false;
                info.WindowStyle = ProcessWindowStyle.Hidden; 
                info.ErrorDialog = false;
                info.FileName = ProcessLauncher.GetDevEnvPath(version);
                info.Arguments = " /ResetAddIn RudeBuildAddIn.Connect /Command File.Exit";
                if (process.Start())
                {
                    process.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("RudeBuild uninstall error!\n" + ex.Message, "RudeBuild", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [SecurityPermission(SecurityAction.Demand)]
        public override void Uninstall(IDictionary savedState)
        {
            DebugBreak();
            base.Rollback(savedState);
            UninstallOrRollback(savedState);

//             ResetAddIn(VisualStudioVersion.VS2008);
//             ResetAddIn(VisualStudioVersion.VS2010);
//             ResetAddIn(VisualStudioVersion.VS2012);
//             ResetAddIn(VisualStudioVersion.VS2013);
//             ResetAddIn(VisualStudioVersion.VS2015);
        }
    }
}

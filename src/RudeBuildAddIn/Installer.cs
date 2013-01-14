using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using System.Xml;
using System.Windows;
using EnvDTE80;
using RudeBuild;

namespace RudeBuildAddIn
{
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

        private void InstallAddInFile(IDictionary savedState, string filePath, string vsVersion)
        {
            string userPersonalFolder = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            string installDirectory = Path.Combine(userPersonalFolder, "Visual Studio " + vsVersion);
            installDirectory = Path.Combine(installDirectory, "Addins");

            var directoryInfo = new DirectoryInfo(installDirectory);
            if (!directoryInfo.Exists)
            {
                directoryInfo.Create();
            }

            string installPath = Path.Combine(installDirectory, AddInFileName);
            File.Copy(filePath, installPath, true);
            savedState.Add("AddInInstallPath" + vsVersion, installPath);            
        }

        private void UninstallAddInFile(IDictionary savedState, string vsVersion)
        {
            if (savedState.Contains("AddInInstallPath" + vsVersion))
            {
                var installPath = (string)savedState["AddInInstallPath" + vsVersion];
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

        public override void Install(IDictionary savedState)
        {
            // Uncomment the following line, recompile, and run the built setup if you want to debug this installer.
            //Debugger.Break();

            base.Install(savedState);

            try
            {
                string installationPath = GetInstallationPath();
                string addInFilePath = Path.Combine(installationPath, AddInFileName);
                PatchAddInFile(addInFilePath, installationPath);
                InstallAddInFile(savedState, addInFilePath, "2008");
                InstallAddInFile(savedState, addInFilePath, "2010");
                InstallAddInFile(savedState, addInFilePath, "11");
            }
            catch (Exception ex)
            {
                MessageBox.Show("RudeBuild install error!\n" + ex.Message, "RudeBuild", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string GetDevEnvProgId(VisualStudioVersion version)
        {
            switch (version)
            {
            case VisualStudioVersion.VS2005: return "VisualStudio.DTE.8.0";
            case VisualStudioVersion.VS2008: return "VisualStudio.DTE.9.0";
            case VisualStudioVersion.VS2010: return "VisualStudio.DTE.10.0";
            case VisualStudioVersion.VS2012: return "VisualStudio.DTE.11.0";
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

                UninstallAddInFile(savedState, "2008");
                UninstallAddInFile(savedState, "2010");
                UninstallAddInFile(savedState, "2012");

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

        public override void Rollback(IDictionary savedState)
        {
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

        public override void Uninstall(IDictionary savedState)
        {
            base.Rollback(savedState);
            UninstallOrRollback(savedState);

//             ResetAddIn(VisualStudioVersion.VS2008);
//             ResetAddIn(VisualStudioVersion.VS2010);
//             ResetAddIn(VisualStudioVersion.VS2012);
        }
    }
}

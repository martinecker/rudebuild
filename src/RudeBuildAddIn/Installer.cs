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

namespace RudeBuildAddIn
{
    [RunInstaller(true)]
    public partial class Installer : System.Configuration.Install.Installer
    {
        private const string _addInFileName = "RudeBuild.AddIn";

        public Installer()
        {
            InitializeComponent();
        }

        private void PatchAddInFile(string filePath, string installationPath)
        {
            XDocument document = XDocument.Load(filePath);
            if (null == document)
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

            DirectoryInfo directoryInfo = new DirectoryInfo(installDirectory);
            if (!directoryInfo.Exists)
            {
                directoryInfo.Create();
            }

            string installPath = Path.Combine(installDirectory, _addInFileName);
            File.Copy(filePath, installPath, true);
            savedState.Add("AddInInstallPath" + vsVersion, installPath);            
        }

        private void UninstallAddInFile(IDictionary savedState, string vsVersion)
        {
            if (savedState.Contains("AddInInstallPath" + vsVersion))
            {
                string installPath = (string)savedState["AddInInstallPath" + vsVersion];
                if (!string.IsNullOrEmpty(installPath))
                {
                    if (File.Exists(installPath))
                    {
                        File.Delete(installPath);
                    }
                }
            }
        }

        public override void Install(IDictionary savedState)
        {
            // Uncomment the following line, recompile, and run the built setup if you want to debug this installer.
            //Debugger.Break();

            base.Install(savedState);

            try
            {
                string installationPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string addInFilePath = Path.Combine(installationPath, _addInFileName);
                PatchAddInFile(addInFilePath, installationPath);
                InstallAddInFile(savedState, addInFilePath, "2008");
                InstallAddInFile(savedState, addInFilePath, "2010");

            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private void UninstallOrRollback(IDictionary savedState)
        {
            try
            {
                UninstallAddInFile(savedState, "2008");
                UninstallAddInFile(savedState, "2010");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        public override void Rollback(IDictionary savedState)
        {
            base.Rollback(savedState);
            UninstallOrRollback(savedState);
        }

        public override void Uninstall(IDictionary savedState)
        {
            base.Rollback(savedState);
            UninstallOrRollback(savedState);
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace RudeBuild
{
    public sealed class ProcessLauncher
    {
        private readonly Settings _settings;
        private readonly object _processLock = new object();
        private Process _process;
        private bool _stopped;

        public ProcessLauncher(Settings settings)
        {
            _settings = settings;
        }

        public static string GetDevEvnBaseRegistryKey(VisualStudioVersion version)
        {
            //TODO: This hack gets us the right registry path for 32-bit software on 64 bit Windows.
            // The right fix is to either build an x86 target (rather than "Any CPU"), or to build
            // against .NET Framework 4 (or greater) and use RegistryKey.OpenBaseKey to specify that
            // we want a 32-bit view on the registry.
            bool is64Bit = IntPtr.Size == 8;
            string registrySoftwarePath = string.Format(@"SOFTWARE\{0}", is64Bit ? @"Wow6432Node" : "");

            switch (version)
            {
                case VisualStudioVersion.VS2005: return registrySoftwarePath + @"\Microsoft\VisualStudio\8.0\";
                case VisualStudioVersion.VS2008: return registrySoftwarePath + @"\Microsoft\VisualStudio\9.0\";
                case VisualStudioVersion.VS2010: return registrySoftwarePath + @"\Microsoft\VisualStudio\10.0\";
                case VisualStudioVersion.VS2012: return registrySoftwarePath + @"\Microsoft\VisualStudio\11.0\";
                case VisualStudioVersion.VS2013: return registrySoftwarePath + @"\Microsoft\VisualStudio\12.0\";
                case VisualStudioVersion.VS2015: return registrySoftwarePath + @"\Microsoft\VisualStudio\14.0\";
                default: throw new ArgumentException("Couldn't determine Visual Studio registry key. Your version of Visual Studio is unsupported by this tool.");
            }
        }

		public static string GetDevEnvDir(VisualStudioVersion version)
		{
			string registryPath = GetDevEvnBaseRegistryKey(version) + @"Setup\VS";
			RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(registryPath);
			if (null == registryKey)
				throw new ArgumentException("Couldn't open Visual Studio registry key. Your version of Visual Studio is unsupported by this tool or Visual Studio is not installed properly.");

			var devEnvDir = (string)registryKey.GetValue("EnvironmentDirectory");
			return devEnvDir;
		}

		public static string GetDevEnvPath(VisualStudioVersion version)
        {
            var devEnvPath = Path.Combine(GetDevEnvDir(version), "devenv.com");
            return devEnvPath;
        }

        public void RemoveSolutionFromDevEnvMRUList(SolutionInfo solutionInfo)
        {
            try
            {
                string registryPath = GetDevEvnBaseRegistryKey(solutionInfo.Version) + @"ProjectMRUList";
                RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(registryPath, true);
                if (null == registryKey)
                    throw new ArgumentException("Couldn't open Visual Studio registry key. Your version of Visual Studio is unsupported by this tool or Visual Studio is not installed properly.");

                string modifiedSolutionFilePath = _settings.ModifyFileName(solutionInfo.FilePath);
                foreach (var keyName in registryKey.GetValueNames())
                {
                    if (keyName.StartsWith("File"))
                    {
                        var keyValue = (string) registryKey.GetValue(keyName);
                        if (keyValue.StartsWith(modifiedSolutionFilePath, StringComparison.OrdinalIgnoreCase))
                        {
                            registryKey.DeleteValue(keyName);
                            break;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // ignore exception
            }
        }

        private void SetupDevEnvProcessObject(SolutionInfo solutionInfo, ref ProcessStartInfo info)
        {
            info.FileName = GetDevEnvPath(solutionInfo.Version);

            string buildCommand = "Build";
            if (_settings.BuildOptions.Clean)
                buildCommand = "Clean";
            else if (_settings.BuildOptions.Rebuild)
                buildCommand = "Rebuild";

            info.Arguments = string.Format(" \"{0}\" /{1} \"{2}\"", _settings.ModifyFileName(solutionInfo.FilePath), buildCommand, _settings.BuildOptions.Config);
            if (!string.IsNullOrEmpty(_settings.BuildOptions.Project))
            {
                string projectName = _settings.BuildOptions.Project;
                info.Arguments += string.Format(" /project \"{0}\"", projectName);
            }
        }

        private static string GetIncrediBuildPath()
        {
            const string registryPath = @"SOFTWARE\Xoreax\IncrediBuild\Builder";
            RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(registryPath);
            if (registryKey == null)
                return null;

            string resultPath = (string)registryKey.GetValue("Folder");
            if (string.IsNullOrEmpty(resultPath))
                return null;
            resultPath = Path.Combine(resultPath, "BuildConsole.exe");
            return resultPath;
        }

        private bool TryToSetupIncrediBuildProcessObject(SolutionInfo solutionInfo, ref ProcessStartInfo info)
        {
            try
            {
                info.FileName = GetIncrediBuildPath();
                if (string.IsNullOrEmpty(info.FileName) || !File.Exists(info.FileName))
                {
                    _settings.Output.WriteLine(
                        "Warning: RudeBuild is setup to use IncrediBuild, but IncrediBuild doesn't seem to be installed properly.\n" +
                        "Falling back to using a regular Visual Studio build.\n" +
                        "Error: Couldn't find IncrediBuild command-line tool: " + info.FileName);
                    return false;
                }

                string buildCommand = string.Empty;
                if (_settings.BuildOptions.Clean)
                    buildCommand = "/Clean";
                else if (_settings.BuildOptions.Rebuild)
                    buildCommand = "/Rebuild";

                info.Arguments = string.Format(" \"{0}\" {1} /cfg=\"{2}\"", _settings.ModifyFileName(solutionInfo.FilePath), buildCommand, _settings.BuildOptions.Config);
                if (!string.IsNullOrEmpty(_settings.BuildOptions.Project))
                {
                    string projectName = _settings.GlobalSettings.FileNamePrefix + _settings.BuildOptions.Project;
                    info.Arguments += string.Format(" /prj=\"{0}\"", projectName);
                }

                info.Arguments += " /UseIDEMonitor";
            }
            catch (Exception ex)
            {
                _settings.Output.WriteLine(
                    "Warning: RudeBuild is setup to use IncrediBuild, but IncrediBuild doesn't seem to be installed properly.\n" +
                    "Falling back to using a regular Visual Studio build.\n" +
                    "Error: " + ex.Message);
                return false;
            }
            return true;
        }

        private static string GetSnVsBuildPath()
        {
            string sceRootPath = Environment.GetEnvironmentVariable("SCE_ROOT_DIR");
            if (string.IsNullOrEmpty(sceRootPath))
                return null;

            // Try to find newest first.
            string result = Path.Combine(sceRootPath, "Common\\SceVSI-VS14\\bin\\vs14build.exe");
            if (File.Exists(result))
                return result;

            result = Path.Combine(sceRootPath, "Common\\SceVSI-VS11\\bin\\vs11build.exe");
            if (File.Exists(result))
                return result;

            result = Path.Combine(sceRootPath, "Common\\SceVSI\\bin\\vs10build.exe");
            if (File.Exists(result))
                return result;

            return null;
        }

        private bool TryToSetupSnVsBuildProcessObject(SolutionInfo solutionInfo, ref ProcessStartInfo info)
        {
            try
            {
                info.FileName = GetSnVsBuildPath();
                if (string.IsNullOrEmpty(info.FileName))
                {
                    _settings.Output.WriteLine(
                        "Warning: RudeBuild is setup to use SN-DBS, but SN-DBS or VSI doesn't seem to be installed properly.\n" +
                        "Falling back to using a regular Visual Studio build.\n" +
                        "Error: Couldn't find VSI command-line tool: " + info.FileName);
                    return false;
                }

                string buildCommand = "/build";
                if (_settings.BuildOptions.Clean)
                    buildCommand = "/clean";
                else if (_settings.BuildOptions.Rebuild)
                    buildCommand = "/rebuild";

                info.Arguments = string.Format(" \"{0}\" {1} \"{2}\"", _settings.ModifyFileName(solutionInfo.FilePath), buildCommand, _settings.BuildOptions.Config);
                if (!string.IsNullOrEmpty(_settings.BuildOptions.Project))
                {
                    ProjectInfo projectInfo = solutionInfo.GetProjectInfo(_settings.BuildOptions.Project);
                    if (projectInfo != null)
                    {
                        string projectConfigName = null;
                        SolutionConfigManager.ProjectConfig projectConfig = solutionInfo.ConfigManager.GetProjectByFileName(projectInfo.FileName);
                        if (projectConfig.SolutionToProjectConfigMap.TryGetValue(_settings.BuildOptions.Config, out projectConfigName))
                        {
                            info.Arguments += string.Format(" /project \"{0}\" /projectconfig \"{1}\"", _settings.ModifyFileName(projectInfo.FileName), projectConfigName);
                        }
                    }
                }
                info.Arguments += " /sn-dbs";
            }
            catch (Exception ex)
            {
                _settings.Output.WriteLine(
                    "Warning: RudeBuild is setup to use SN-DBS, but SN-DBS doesn't seem to be installed properly.\n" +
                    "Falling back to using a regular Visual Studio build.\n" +
                    "Error: " + ex.Message);
                return false;
            }
            return true;
        }

        private Process CreateProcessObject(SolutionInfo solutionInfo)
        {
            var process = new Process();
            ProcessStartInfo info = process.StartInfo;
            info.CreateNoWindow = true;
            info.UseShellExecute = false;
            info.ErrorDialog = false;
            info.RedirectStandardOutput = true;
            process.OutputDataReceived += delegate(object sendingProcess, DataReceivedEventArgs line)
            {
                _settings.Output.WriteLine(line.Data);
            };

            bool useDevEnvBuildTool = true;
            if (_settings.GlobalSettings.BuildTool == BuildTool.IncrediBuild && TryToSetupIncrediBuildProcessObject(solutionInfo, ref info))
                useDevEnvBuildTool = false;
            else if (_settings.GlobalSettings.BuildTool == BuildTool.SN_DBS && TryToSetupSnVsBuildProcessObject(solutionInfo, ref info))
                useDevEnvBuildTool = false;

            if (useDevEnvBuildTool)
                SetupDevEnvProcessObject(solutionInfo, ref info);

            _settings.Output.WriteLine("Launching: " + info.FileName + info.Arguments);

            return process;
        }

        private void ValidateBuildOptions(SolutionInfo solutionInfo)
        {
            string solutionConfig = _settings.BuildOptions.Config;
            if (string.IsNullOrEmpty(solutionConfig))
                throw new ArgumentException(string.Format("A solution configuration to build is required! None specified while trying to build solution {0}.", solutionInfo.Name));

            if (!solutionInfo.ConfigManager.SolutionConfigs.Contains(solutionConfig))
                throw new ArgumentException(string.Format("The specified solution configuration {0} does not exist in solution {1}.", solutionConfig, solutionInfo.Name));

            string projectName = _settings.BuildOptions.Project;
            if (!string.IsNullOrEmpty(projectName) && null == solutionInfo.GetProjectInfo(projectName))
                throw new ArgumentException(string.Format("Solution {0} doesn't contain a project called {1}!", solutionInfo.Name, projectName));
        }

        public int Run(SolutionInfo solutionInfo)
        {
            ValidateBuildOptions(solutionInfo);

            int exitCode = -1;

            try
            {
                bool processRunning = false;
                lock (_processLock)
                {
                    _stopped = false;
                    _process = CreateProcessObject(solutionInfo);
                    if (_process.Start())
                    {
                        _process.BeginOutputReadLine();
                        processRunning = !_process.WaitForExit(100);
                    }
                }

                while (processRunning)
                {
                    lock (_processLock)
                    {
                        if (_stopped)
                        {
                            processRunning = false;
                        }
                        else
                        {
                            processRunning = !_process.WaitForExit(100);
                            if (!processRunning)
                                exitCode = _process.ExitCode;
                        }
                    }
                }
            }
            finally
            {
                lock (_processLock)
                {
                    if (_process != null)
                    {
                        _process.Close();
                        _process = null;
                    }
                }

                RemoveSolutionFromDevEnvMRUList(solutionInfo);
            }
            return exitCode;
        }

        public void Stop()
        {
            lock (_processLock)
            {
                if (null == _process)
                    return;

                _stopped = false;

                using (var killProcess = new Process())
                {
                    ProcessStartInfo info = killProcess.StartInfo;
                    info.UseShellExecute = false;
                    info.CreateNoWindow = true;
                    info.FileName = "taskkill";
                    info.Arguments = "/pid " + _process.Id + " /f /t";
                    if (killProcess.Start())
                    {
                        killProcess.WaitForExit();
                        _stopped = true;
                    }
                }
            }
        }
    }
}

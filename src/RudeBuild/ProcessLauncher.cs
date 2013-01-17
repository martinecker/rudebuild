using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace RudeBuild
{
    public class ProcessLauncher
    {
        private readonly Settings _settings;
        private readonly object _processLock = new object();
        private Process _process;
        private bool _stopped;

        public ProcessLauncher(Settings settings)
        {
            _settings = settings;
        }

        public static string GetDevEnvPath(VisualStudioVersion version)
        {
            string registryPath = null;
            switch (version)
            {
                case VisualStudioVersion.VS2005: registryPath = @"SOFTWARE\Microsoft\VisualStudio\8.0\Setup\VS"; break;
                case VisualStudioVersion.VS2008: registryPath = @"SOFTWARE\Microsoft\VisualStudio\9.0\Setup\VS"; break;
                case VisualStudioVersion.VS2010: registryPath = @"SOFTWARE\Microsoft\VisualStudio\10.0\Setup\VS"; break;
                case VisualStudioVersion.VS2012: registryPath = @"SOFTWARE\Microsoft\VisualStudio\11.0\Setup\VS"; break;
                default: throw new System.ArgumentException("Couldn't find Visual Studio registry key. Your version of Visual Studio is either not properly installed, or it is unsupported by this tool.");
            }

            RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(registryPath);
            var devEnvPath = (string)registryKey.GetValue("EnvironmentDirectory");
            devEnvPath = Path.Combine(devEnvPath, "devenv.com");
            return devEnvPath;
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
            if (_settings.BuildOptions.Project != null)
            {
                string projectName = _settings.BuildOptions.Project;
                info.Arguments += string.Format(" /project \"{0}\"", projectName);
            }
        }

        private static string GetIncrediBuildPath()
        {
            const string registryPath = @"SOFTWARE\Xoreax\IncrediBuild\Builder";
            RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(registryPath);
            string incrediBuildPath = (string)registryKey.GetValue("Folder");
            incrediBuildPath = Path.Combine(incrediBuildPath, "BuildConsole.exe");
            return incrediBuildPath;
        }

        private void SetupIncrediBuildProcessObject(SolutionInfo solutionInfo, ref ProcessStartInfo info)
        {
            info.FileName = GetIncrediBuildPath();

            string buildCommand = string.Empty;
            if (_settings.BuildOptions.Clean)
                buildCommand = "/Clean";
            else if (_settings.BuildOptions.Rebuild)
                buildCommand = "/Rebuild";

            info.Arguments = string.Format(" \"{0}\" {1} /cfg=\"{2}\"", _settings.ModifyFileName(solutionInfo.FilePath), buildCommand, _settings.BuildOptions.Config);
            if (_settings.BuildOptions.Project != null)
            {
                string projectName = _settings.GlobalSettings.FileNamePrefix + _settings.BuildOptions.Project;
                info.Arguments += string.Format(" /prj=\"{0}\"", projectName);
            }
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
            if (_settings.GlobalSettings.BuildTool == BuildTool.IncrediBuild)
            {
                useDevEnvBuildTool = false;
                try
                {
                    SetupIncrediBuildProcessObject(solutionInfo, ref info);
                    if (!File.Exists(info.FileName))
                        useDevEnvBuildTool = true;
                }
                catch
                {
                    useDevEnvBuildTool = true;
                }

                if (useDevEnvBuildTool)
                {
                    _settings.Output.WriteLine("Warning: RudeBuild is setup to use IncrediBuild, but IncrediBuild doesn't seem to be installed properly. Falling back to using a regular Visual Studio build.");
                }
            }

            if (useDevEnvBuildTool)
            {
                SetupDevEnvProcessObject(solutionInfo, ref info);
            }

            _settings.Output.WriteLine("Launching: " + info.FileName + info.Arguments);

            return process;
        }

        public int Run(SolutionInfo solutionInfo)
        {
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
                    _process.Close();
                    _process = null;
                }
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

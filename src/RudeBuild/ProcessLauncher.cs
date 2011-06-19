using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace RudeBuild
{
    public class ProcessLauncher
    {
        private GlobalSettings _globalSettings;
        private object _processLock = new object();
        private Process _process = null;
        private bool _stopped = false;

        public ProcessLauncher(GlobalSettings globalSettings)
        {
            _globalSettings = globalSettings;
        }

        private string GetDevEnvPath(SolutionInfo solutionInfo)
        {
            string registryPath = null;
            switch (solutionInfo.Version)
            {
                case VisualStudioVersion.VS2005: registryPath = @"SOFTWARE\Microsoft\VisualStudio\8.0\Setup\VS"; break;
                case VisualStudioVersion.VS2008: registryPath = @"SOFTWARE\Microsoft\VisualStudio\9.0\Setup\VS"; break;
                case VisualStudioVersion.VS2010: registryPath = @"SOFTWARE\Microsoft\VisualStudio\10.0\Setup\VS"; break;
                default: throw new System.Exception("Couldn't find Visual Studio registry key. Your version of Visual Studio is either not properly installed, or it is unsupported by this tool.");
            }

            RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(registryPath);
            string devEnvPath = (string)registryKey.GetValue("EnvironmentDirectory");
            devEnvPath = Path.Combine(devEnvPath, "devenv.com");
            return devEnvPath;
        }

        private Process CreateProcessObject(SolutionInfo solutionInfo)
        {
            Process process = new Process();
            ProcessStartInfo info = process.StartInfo;
            info.CreateNoWindow = true;
            info.UseShellExecute = false;
            info.ErrorDialog = false;
            info.RedirectStandardOutput = true;
            process.OutputDataReceived += delegate(object sendingProcess, DataReceivedEventArgs line)
            {
                _globalSettings.Output.WriteLine(line.Data);
            };
            
            info.FileName = GetDevEnvPath(solutionInfo);

            string buildCommand = "Build";
            if (_globalSettings.RunOptions.Clean)
                buildCommand = "Clean";
            else if (_globalSettings.RunOptions.Rebuild)
                buildCommand = "Rebuild";

            info.Arguments = string.Format(" \"{0}\" /{1} \"{2}\"", _globalSettings.ModifyFileName(solutionInfo.FilePath), buildCommand, _globalSettings.RunOptions.Config);
            if (_globalSettings.RunOptions.Project != null)
            {
                string projectName = _globalSettings.RunOptions.Project;
                if (solutionInfo.Version == VisualStudioVersion.VS2010)     // VS2010 expects the project file name instead of the actual project name on the command line.
                    projectName = _globalSettings.FileNamePrefix + projectName;
                info.Arguments += string.Format(" /project \"{0}\"", projectName);
            }

            _globalSettings.Output.WriteLine("Launching: " + info.FileName + info.Arguments);

            return process;
        }

        public int Run(SolutionInfo solutionInfo)
        {
            bool processRunning = false;
            int exitCode = -1;

            lock (_processLock)
            {
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

            lock (_processLock)
            {
                _process.Close();
                _process = null;
            }
            return exitCode;
        }

        public void Stop()
        {
            lock (_processLock)
            {
                if (null == _process)
                    return;

                Process killProcess = new System.Diagnostics.Process();
                ProcessStartInfo info = killProcess.StartInfo;
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.FileName = "taskkill";
                info.Arguments = "/pid " + _process.Id + " /f /t";
                if (killProcess.Start())
                {
                    killProcess.WaitForExit();
                }

                _stopped = true;
            }
        }
    }
}

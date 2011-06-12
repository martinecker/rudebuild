using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace RudeBuild
{
    public class ProcessLauncher
    {
        private object _processLock = new object();
        private Process _process = null;
        private bool _stopped = false;

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

        private Process CreateProcessObject(SolutionInfo solutionInfo, GlobalSettings settings)
        {
            Process process = new Process();
            ProcessStartInfo info = process.StartInfo;
            info.CreateNoWindow = true;
            info.UseShellExecute = false;
            info.RedirectStandardOutput = true;
            info.FileName = GetDevEnvPath(solutionInfo);

            string buildCommand = "Build";
            if (settings.RunOptions.Clean)
                buildCommand = "Clean";
            else if (settings.RunOptions.Rebuild)
                buildCommand = "Rebuild";

            info.Arguments = string.Format(" \"{0}\" /{1} \"{2}\"", settings.ModifyFileName(solutionInfo.FilePath), buildCommand, settings.RunOptions.Config);
            if (settings.RunOptions.Project != null)
            {
                info.Arguments += string.Format(" /project \"{0}\"", settings.RunOptions.Project);
            }

            settings.Output.WriteLine("Launching: " + info.FileName + info.Arguments);

            return process;
        }

        public int Run(SolutionInfo solutionInfo, GlobalSettings settings)
        {
            bool processRunning = true;
            int exitCode = -1;

            lock (_processLock)
            {
                _process = CreateProcessObject(solutionInfo, settings);
                _process.Start();
                processRunning = !_process.WaitForExit(100);
            }

            while (processRunning)
            {
                lock (_processLock)
                {
                    if (_stopped)
                    {
                        _process = null;
                        return exitCode;
                    }
                    processRunning = !_process.WaitForExit(100);
                }
            }

            lock (_processLock)
            {
                exitCode = _process.ExitCode;
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

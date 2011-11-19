using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Diagnostics;
using RudeBuild;

namespace RudeBuildAddIn
{
    public class Builder
    {
        private GlobalSettings _globalSettings;

        private EnvDTE80.DTE2 _application;
        
        private IOutput _output;
        public IOutput Output
        {
            get { return _output; }
        }

        private Stopwatch _stopwatch;

        private object _lock = new object();
        private ProcessLauncher _processLauncher;
        private Thread _buildThread;

        private bool _isBeingStopped = false;
        public bool IsBeingStopped
        {
            get { lock (_lock) { return _isBeingStopped; } }
        }

        public bool IsBuilding
        {
            get { lock (_lock) { return _buildThread != null && _processLauncher != null || _isBeingStopped; } } 
        }

        private bool _lastBuildWasSuccessful = false;
        public bool LastBuildWasSuccessful
        {
            get { lock (_lock) { return _lastBuildWasSuccessful; } }
        }

        private bool _lastBuildWasStopped = false;
        public bool LastBuildWasStopped
        {
            get { lock (_lock) { return _lastBuildWasStopped; } }
        }

        public Builder(EnvDTE80.DTE2 application, IOutput output)
        {
            _application = application;
            _output = output;
            _stopwatch = new Stopwatch();

            try
            {
                _globalSettings = GlobalSettings.Load(_output);
                _globalSettings.Save();
            }
            catch (System.Exception ex)
            {
                _output.WriteLine("Error saving global settings: " + ex.Message);
            }
        }

        public void Build(BuildOptions options)
        {
            if (IsBuilding)
                return;

            _globalSettings = GlobalSettings.Load(_output);
            Settings settings = new Settings(_globalSettings, options, _output);

            if (options.CleanCache)
            {
                CacheCleaner cacheCleaner = new CacheCleaner();
                cacheCleaner.Run(settings);
                return;
            }

            lock (_lock)
            {
                _processLauncher = new ProcessLauncher(settings);
                _lastBuildWasStopped = false;
                _isBeingStopped = false;
                _buildThread = new Thread(delegate() { BuildThread(settings); });
                _buildThread.IsBackground = true;
                _buildThread.Start();
            }
        }

        private void BuildThread(Settings settings)
        {
            _output.Clear();
            _output.Activate();
            _output.WriteLine("RudeBuild building...");
            _output.WriteLine();

            _stopwatch.Reset();
            _stopwatch.Start();

            int exitCode = -1;
            try
            {
                SolutionReaderWriter solutionReaderWriter = new SolutionReaderWriter(settings);
                SolutionInfo solutionInfo = solutionReaderWriter.ReadWrite(settings.BuildOptions.Solution.FullName);
                settings.SolutionSettings = SolutionSettings.Load(settings, solutionInfo);
                ProjectReaderWriter projectReaderWriter = new ProjectReaderWriter(settings);
                projectReaderWriter.ReadWrite(solutionInfo);
                settings.SolutionSettings.UpdateAndSave(settings, solutionInfo);

                exitCode = _processLauncher.Run(solutionInfo);
            }
            catch (System.Exception ex)
            {
                _output.WriteLine("Build failed. An error occurred while building:");
                _output.WriteLine(ex.Message);
            }

            _stopwatch.Stop();
            TimeSpan ts = _stopwatch.Elapsed;
            string buildTimeText = string.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            _output.WriteLine("Build time: " + buildTimeText);

            lock (_lock)
            {
                _lastBuildWasSuccessful = _isBeingStopped ? false : exitCode == 0;
                _buildThread = null;
                _processLauncher = null;
            }
        }

        public void Stop()
        {
            if (IsBuilding)
            {
                lock (_lock)
                {
                    if (_isBeingStopped)
                        return;

                    _isBeingStopped = true;
                    _output.WriteLine("Stopping build...");
                    Thread stopThread = new Thread(delegate() { StopThread(); });
                    stopThread.Start();
                }
            }
        }

        private void StopThread()
        {
            ProcessLauncher processLauncher = null;
            Thread buildThread = null;
            lock (_lock)
            {
                processLauncher = _processLauncher;
                buildThread = _buildThread;
            }
            if (processLauncher != null && buildThread != null)
            {
                try
                {
                    processLauncher.Stop();
                    buildThread.Join();
                    _output.WriteLine("Build stopped.");
                }
                catch (System.Exception ex)
                {
                    _output.WriteLine("An error occurred trying to stop the build:");
                    _output.WriteLine(ex.Message);
                }
                lock (_lock)
                {
                    _lastBuildWasStopped = true;
                    _isBeingStopped = false;
                }
            }
        }
    }
}
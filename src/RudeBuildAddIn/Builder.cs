using System.IO;
using System.Reflection;
using System.Threading;
using RudeBuild;

namespace RudeBuildAddIn
{
    public class Builder
    {
        private EnvDTE80.DTE2 _application;
        private IOutput _output;

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
        }

        public void Build(RunOptions options)
        {
            if (IsBuilding)
                return;

            GlobalSettings globalSettings = new GlobalSettings(options, _output);

            lock (_lock)
            {
                _processLauncher = new ProcessLauncher(globalSettings);
                _lastBuildWasStopped = false;
                _isBeingStopped = false;
                _buildThread = new Thread(delegate() { BuildThread(globalSettings); });
                _buildThread.IsBackground = true;
                _buildThread.Start();
            }
        }

        private void BuildThread(GlobalSettings globalSettings)
        {
            SolutionReaderWriter solutionReaderWriter = new SolutionReaderWriter(globalSettings);
            SolutionInfo solutionInfo = solutionReaderWriter.ReadWrite(globalSettings.RunOptions.Solution.FullName);
            ProjectReaderWriter projectReaderWriter = new ProjectReaderWriter(globalSettings);
            projectReaderWriter.ReadWrite(solutionInfo);

            int exitCode = _processLauncher.Run(solutionInfo);
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
                    _isBeingStopped = true;
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
                processLauncher.Stop();
                buildThread.Join();
                lock (_lock)
                {
                    _lastBuildWasStopped = true;
                    _isBeingStopped = false;
                }
            }
        }
    }
}
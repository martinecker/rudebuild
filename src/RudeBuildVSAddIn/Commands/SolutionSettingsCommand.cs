using System.IO;
using RudeBuild;

namespace RudeBuildVSAddIn
{
    public class SolutionSettingsCommand : CommandBase
    {
        private readonly Builder _builder;
        private readonly OutputPane _outputPane;
        private SolutionInfo _cachedSolutionInfo;
        private Settings _cachedSettings;

        public SolutionSettingsCommand(Builder builder, OutputPane outputPane)
        {
            _builder = builder;
            _outputPane = outputPane;
        }

        private void CacheSolutionInfo(CommandManager commandManager)
        {
            string solutionPath = commandManager.Application.Solution.FullName;
            if (_cachedSolutionInfo != null && _cachedSolutionInfo.FilePath == solutionPath)
                return;
            if (!File.Exists(solutionPath))
            {
                _cachedSolutionInfo = null;
                _cachedSettings = null;
                return;
            }

            FileInfo solutionFileInfo = GetSolutionFileInfo(commandManager);
            if (null == solutionFileInfo || !solutionFileInfo.Exists)
                return;

            GlobalSettings globalSettings = GlobalSettings.Load(_outputPane);
            var buildOptions = new BuildOptions();
            buildOptions.Solution = solutionFileInfo;
            buildOptions.Config = GetActiveSolutionConfig(commandManager);
            _cachedSettings = new Settings(globalSettings, buildOptions, _outputPane);

            var solutionReaderWriter = new SolutionReaderWriter(_cachedSettings);
            _cachedSolutionInfo = solutionReaderWriter.Read(_cachedSettings.BuildOptions.Solution.FullName);
            _cachedSettings.SolutionSettings = SolutionSettings.Load(_cachedSettings, _cachedSolutionInfo);
            var projectReaderWriter = new ProjectReaderWriter(_cachedSettings);
            projectReaderWriter.Read(_cachedSolutionInfo);

            _cachedSettings.SolutionSettings.UpdateAndSave(_cachedSettings, _cachedSolutionInfo);
        }

        public override void Execute(CommandManager commandManager)
        {
            CacheSolutionInfo(commandManager);
            if (null == _cachedSolutionInfo || null == _cachedSettings)
                return;

            var dialog = new SolutionSettingsDialog(_cachedSettings, _cachedSolutionInfo);
            try
            {
                dialog.ShowDialog();
            }
            finally
            {
                dialog.Close();
            }
        }

        public override bool IsEnabled(CommandManager commandManager)
        {
            if (IsSolutionOpen(commandManager) && !_builder.IsBuilding)
            {
                CacheSolutionInfo(commandManager);
                return null != _cachedSolutionInfo && null != _cachedSettings && _cachedSolutionInfo.Projects.Count > 0;
            }
            return false;
        }
    }
}

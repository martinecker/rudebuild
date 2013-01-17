using System.IO;
using RudeBuild;

namespace RudeBuildAddIn
{
    public class SolutionSettingsCommand : CommandBase
    {
        private Builder _builder;
        private OutputPane _outputPane;

        public SolutionSettingsCommand(Builder builder, OutputPane outputPane)
        {
            _builder = builder;
            _outputPane = outputPane;
        }

        public override void Execute(CommandManager commandManager)
        {
            GlobalSettings globalSettings = GlobalSettings.Load(_outputPane);
            BuildOptions buildOptions = new BuildOptions();
            buildOptions.Solution = GetSolutionFileInfo(commandManager);
			buildOptions.Config = GetActiveSolutionConfig(commandManager);
            Settings settings = new Settings(globalSettings, buildOptions, _outputPane);

            SolutionReaderWriter solutionReaderWriter = new SolutionReaderWriter(settings);
            SolutionInfo solutionInfo = solutionReaderWriter.Read(settings.BuildOptions.Solution.FullName);
            settings.SolutionSettings = SolutionSettings.Load(settings, solutionInfo);
            ProjectReaderWriter projectReaderWriter = new ProjectReaderWriter(settings);
            projectReaderWriter.Read(solutionInfo);
            settings.SolutionSettings.UpdateAndSave(settings, solutionInfo);

            SolutionSettingsDialog dialog = new SolutionSettingsDialog(settings, solutionInfo);
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
                string solutionPath = commandManager.Application.Solution.FullName;
                if (!File.Exists(solutionPath))
                {
                    return false;
                }
                return true;
            }
            return false;
        }
    }
}

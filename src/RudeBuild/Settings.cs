namespace RudeBuild
{
    public class Settings
    {
        private GlobalSettings _globalSettings;
        public GlobalSettings GlobalSettings
        {
            get { return _globalSettings; }
        }

        private RunOptions _runOptions;
        public RunOptions RunOptions
        {
            get { return _runOptions; }
        }

        private IOutput _output;
        public IOutput Output
        {
            get { return _output; }
        }

        public Settings(GlobalSettings globalSettings, RunOptions runOptions, IOutput output)
        {
            _globalSettings = globalSettings;
            _runOptions = runOptions;
            _output = output;
        }
    }
}

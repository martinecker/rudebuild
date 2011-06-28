namespace RudeBuild
{
    public class Settings
    {
        private GlobalSettings _globalSettings;
        public GlobalSettings GlobalSettings
        {
            get { return _globalSettings; }
        }

        private BuildOptions _buildOptions;
        public BuildOptions BuildOptions
        {
            get { return _buildOptions; }
        }

        private IOutput _output;
        public IOutput Output
        {
            get { return _output; }
        }

        public Settings(GlobalSettings globalSettings, BuildOptions buildOptions, IOutput output)
        {
            _globalSettings = globalSettings;
            _buildOptions = buildOptions;
            _output = output;
        }
    }
}

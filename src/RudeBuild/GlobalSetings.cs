using System.IO;

namespace RudeBuild
{
    public class GlobalSettings
    {
        private IOutput _output;
        public IOutput Output
        {
            get { return _output; }
        }

        private RunOptions _runOptions;
        public RunOptions RunOptions
        {
            get { return _runOptions; }
        }

        public readonly string CachePath;
        public readonly string FileNamePrefix;
        public readonly long MaxUnityFileSize;

        public GlobalSettings(RunOptions runOptions, IOutput output)
        {
            _runOptions = runOptions;
            _output = output;
            CachePath = "C:\\RudeBuildCache";
            FileNamePrefix = "RudeBuild_";
            MaxUnityFileSize = 500 * 1024;
        }

        public string ModifyFileName(string fileName)
        {
            string modifiedFileName = Path.Combine(Path.GetDirectoryName(fileName), FileNamePrefix + Path.GetFileName(fileName));
            return modifiedFileName;
        }
    }
}

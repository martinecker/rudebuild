using System.IO;

namespace RudeBuildConsole
{
    public class GlobalSettings
    {
        public readonly string CachePath;
        public readonly string FilenamePrefix;

        public GlobalSettings()
        {
            CachePath = "C:\\RudeBuildCache";
            FilenamePrefix = "RudeBuild_";
        }

        public string ModifyFilename(string filename)
        {
            string modifiedFilename = Path.Combine(Path.GetDirectoryName(filename), FilenamePrefix + Path.GetFileName(filename));
            return modifiedFilename;
        }
    }
}

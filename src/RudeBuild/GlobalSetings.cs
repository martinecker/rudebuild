using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace RudeBuild
{
    public class GlobalSettings
    {
        public const string ConfigFileName = "RudeBuild.GlobalSettings.config";
        public string ConfigFilePath 
        {
            get
            {
                string installationPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string configFilePath = Path.Combine(installationPath, ConfigFileName);
                return configFilePath;
            }
        }

        public string CachePath { get; private set; }
        public string FileNamePrefix { get; private set; }

        private long _maxUnityFileSizeInBytes;
        public long MaxUnityFileSizeInBytes
        {
            get { return _maxUnityFileSizeInBytes; }
            private set { _maxUnityFileSizeInBytes = value; }
        }

        public GlobalSettings()
        {
            SetToDefaults();
            Read();
        }

        public void SetToDefaults()
        {
            CachePath = "C:\\RudeBuildCache";
            FileNamePrefix = "RudeBuild_";
            MaxUnityFileSizeInBytes = 500 * 1024;
        }

        public string ModifyFileName(string fileName)
        {
            string modifiedFileName = Path.Combine(Path.GetDirectoryName(fileName), FileNamePrefix + Path.GetFileName(fileName));
            return modifiedFileName;
        }

        public void Read()
        {
            if (!File.Exists(ConfigFilePath))
                return;

            XDocument document = XDocument.Load(ConfigFilePath);
            XElement element = document.Descendants("CachePath").SingleOrDefault();
            if (null != element)
                CachePath = (string)element.Value;
            element = document.Descendants("FileNamePrefix").SingleOrDefault();
            if (null != element)
                FileNamePrefix = (string)element.Value;
            element = document.Descendants("MaxUnityFileSizeInBytes").SingleOrDefault();
            if (null != element)
            {
                try
                {
                    MaxUnityFileSizeInBytes = System.Int64.Parse((string)element.Value);
                }
                catch (System.Exception)
                {
                    // Just ignore it and use the default value.
                }
            }
        }

        public void Write()
        {
            XDocument document = new XDocument(
                new XElement("RudeBuildGlobalSettings",
                    new XElement("CachePath", CachePath),
                    new XElement("FileNamePrefix", FileNamePrefix),
                    new XElement("MaxUnityFileSizeInBytes", MaxUnityFileSizeInBytes)
                    )
                );

            document.Save(ConfigFilePath);
        }
    }
}

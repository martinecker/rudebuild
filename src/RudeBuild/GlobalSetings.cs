using System;
using System.IO;
using System.ComponentModel;
using System.Xml.Serialization;

namespace RudeBuild
{
    public enum BuildTool
    {
        [DisplayValue("Visual Studio")]
        VisualStudio,
        [DisplayValue("IncrediBuild")]
        IncrediBuild
    }

    public class GlobalSettings
    {
        public const string ConfigFileName = "RudeBuild.GlobalSettings.config";
        public static string ConfigFilePath 
        {
            get
            {
                string installationPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string configFilePath = Path.Combine(installationPath, ConfigFileName);
                return configFilePath;
            }
        }

        private string _cachePath;
        [DefaultValue("C:\\RudeBuildCache")]
        public string CachePath
        {
            get { return _cachePath; }
            set
            {
                char[] invalidChars = Path.GetInvalidPathChars();
                if (string.IsNullOrEmpty(value))
                    throw new ArgumentException("You must specify a valid cache path.");
                if (-1 != value.IndexOfAny(invalidChars))
                    throw new ArgumentException("The cache path contains not valid characters for a path.");
                _cachePath = value;
            }
        }

        private string _fileNamePrefix;
        [DefaultValue("RudeBuild_")]
        public string FileNamePrefix
        {
            get { return _fileNamePrefix; }
            set
            {
                char[] invalidChars = Path.GetInvalidFileNameChars();
                if (string.IsNullOrEmpty(value))
                    throw new ArgumentException("You must specify a valid file name prefix.");
                if (-1 != value.IndexOfAny(invalidChars))
                    throw new ArgumentException("The file name prefix contains not valid characters for file names.");
                _fileNamePrefix = value;
            }
        }

        private long _maxUnityFileSizeInBytes;
        [DefaultValue(50 * 1024)]
        public long MaxUnityFileSizeInBytes
        {
            get { return _maxUnityFileSizeInBytes; }
            set
            {
                if (value <= 0)
                    throw new ArgumentException("The maximum unity file size needs to be positive.");
                _maxUnityFileSizeInBytes = value;
            }
        }

        [DefaultValue(BuildTool.VisualStudio)]
        public BuildTool BuildTool { get; set; }

        public GlobalSettings()
        {
            SetToDefaults();
        }

        public void SetToDefaults()
        {
            foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(this))
            {
                DefaultValueAttribute attribute = property.Attributes[typeof(DefaultValueAttribute)] as DefaultValueAttribute;
                if (null == attribute)
                    continue;
                property.SetValue(this, attribute.Value);
            }
        }

        public static GlobalSettings Load()
        {
            if (!File.Exists(ConfigFilePath))
            {
                return new GlobalSettings();
            }

            using (TextReader textReader = new StreamReader(ConfigFilePath))
            {
                XmlSerializer deserializer = new XmlSerializer(typeof(GlobalSettings));
                GlobalSettings globalSettings = (GlobalSettings)deserializer.Deserialize(textReader);
                return globalSettings;
            }
        }

        public void Save()
        {
            using (TextWriter textWriter = new StreamWriter(ConfigFilePath))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(GlobalSettings));
                serializer.Serialize(textWriter, this);
            }
        }
    }
}

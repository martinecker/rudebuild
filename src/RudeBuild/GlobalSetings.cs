using System;
using System.IO;
using System.Threading;
using System.ComponentModel;
using System.Xml.Serialization;

namespace RudeBuild
{
    public enum BuildTool
    {
        [DisplayValue("Visual Studio")]
        VisualStudio,
        [DisplayValue("IncrediBuild")]
        IncrediBuild,
        [DisplayValue("SN-DBS")]
        SN_DBS
    }

    public class GlobalSettings
    {
        public const string ConfigFileName = "RudeBuild.GlobalSettings.config";
        public static string ConfigFilePath 
        {
            get
            {
                string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RudeBuild");
                string configFilePath = Path.Combine(appDataPath, ConfigFileName);
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
                    throw new ArgumentException("The cache path contains invalid characters for a path.");
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
                    throw new ArgumentException("The file name prefix contains invalid characters for file names.");
                _fileNamePrefix = value;
            }
        }

        private long _maxUnityFileSizeInBytes;
        [DefaultValue(300 * 1024)]
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

        [DefaultValue(false)]
        public bool ExcludeWritableFilesFromUnityMerge { get; set; }

        [DefaultValue(false)]
        public bool RandomizeOrderOfUnityMergedFiles { get; set; }

        public GlobalSettings()
        {
            this.SetToDefaults();
        }

        public static GlobalSettings Load(IOutput output)
        {
            if (!File.Exists(ConfigFilePath))
            {
                return new GlobalSettings();
            }

            try
            {
                using (TextReader textReader = new StreamReader(ConfigFilePath))
                {
                    var deserializer = new XmlSerializer(typeof(GlobalSettings));
                    var globalSettings = (GlobalSettings)deserializer.Deserialize(textReader);
                    return globalSettings;
                }
            }
            catch (Exception e)
            {
                output.WriteLine("An exception occurred trying to read global settings file '" + ConfigFilePath + "':");
                output.WriteLine(e.Message);
                output.WriteLine("Using default global settings!");
                return new GlobalSettings();
            }
        }

        private void CreateConfigFileDirectoryIfNotExists()
        {
            if (!File.Exists(ConfigFilePath))
            {
                var directory = System.IO.Path.GetDirectoryName(ConfigFilePath);
                System.IO.Directory.CreateDirectory(directory);
            }
        }

        private void SaveInternal()
        {
            CreateConfigFileDirectoryIfNotExists();
            using (TextWriter textWriter = new StreamWriter(ConfigFilePath))
            {
                var serializer = new XmlSerializer(typeof(GlobalSettings));
                serializer.Serialize(textWriter, this);
            }
        }

        public void Save()
        {
            // Since multiple instances of RudeBuild could try to save the global
            // settings at the same time, retry a few times if it fails because we
            // couldn't get exclusive access to the file.
            int retryCount = 3;
            while (retryCount > 0)
            {
                try
                {
                    SaveInternal();
                    retryCount = 0;
                }
                catch (Exception)
                {
                    Thread.Sleep(300);
                    --retryCount;
                    if (retryCount == 0)
                    {
                        throw;
                    }
                }
            }
        }
    }
}

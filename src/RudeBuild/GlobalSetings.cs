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
        //[XmlIgnore]
        public const string ConfigFileName = "RudeBuild.GlobalSettings.config";
        //[XmlIgnore]
        public static string ConfigFilePath 
        {
            get
            {
                string installationPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string configFilePath = Path.Combine(installationPath, ConfigFileName);
                return configFilePath;
            }
        }

        [DefaultValue("C:\\RudeBuildCache")]
        public string CachePath { get; set; }
        [DefaultValue("RudeBuild_")]
        public string FileNamePrefix { get; set; }
        [DefaultValue(50 * 1024)]
        public long MaxUnityFileSizeInBytes { get; set; }
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

        public string ModifyFileName(string fileName)
        {
            string modifiedFileName = Path.Combine(Path.GetDirectoryName(fileName), FileNamePrefix + Path.GetFileName(fileName));
            return modifiedFileName;
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

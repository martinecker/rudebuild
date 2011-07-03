using System;
using System.Collections.Generic;
using System.IO;
using System.ComponentModel;
using System.Xml.Serialization;

namespace RudeBuild
{
    public class SolutionSettings
    {
        public const string ConfigFileNameSuffix = ".RudeBuild.config";

        [DefaultValue(false)]
        public bool DisablePrecompiledHeaders { get; set; }

        public SerializableDictionary<string, List<string>> ProjectNameToExcludedCppFileNameMap { get; set; }

        public SolutionSettings()
        {
            this.SetToDefaults();
        }

        public static string GetConfigFilePath(Settings settings, SolutionInfo solutionInfo)
        {
            string solutionFileName = Path.GetFileNameWithoutExtension(solutionInfo.FilePath);
            string configFilePath = Path.Combine(Path.GetDirectoryName(solutionInfo.FilePath), solutionFileName + ConfigFileNameSuffix);
            return configFilePath;
        }

        public void Update(SolutionInfo solutionInfo)
        {

        }

        public bool IsExcludedCppFileNameForProject(ProjectInfo projectInfo, string cppFileName)
        {
            if (null == ProjectNameToExcludedCppFileNameMap)
                return false;
            List<string> cppFileNames = null;
            ProjectNameToExcludedCppFileNameMap.TryGetValue(projectInfo.Name, out cppFileNames);
            if (null == cppFileNames)
                return false;
            return cppFileNames.Contains(cppFileName);
        }

        public static SolutionSettings Load(Settings settings, SolutionInfo solutionInfo)
        {
            string configFilePath = GetConfigFilePath(settings, solutionInfo);
            if (File.Exists(configFilePath))
            {
                using (TextReader textReader = new StreamReader(configFilePath))
                {
                    XmlSerializer deserializer = new XmlSerializer(typeof(SolutionSettings));
                    try
                    {
                        SolutionSettings solutionSettings = (SolutionSettings)deserializer.Deserialize(textReader);
                        return solutionSettings;
                    }
                    catch
                    {
                        // ignore any errors
                    }
                }
            }

            return new SolutionSettings();
        }

        public void Save(Settings settings, SolutionInfo solutionInfo)
        {
            string configFilePath = GetConfigFilePath(settings, solutionInfo);
            using (TextWriter textWriter = new StreamWriter(configFilePath))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(SolutionSettings));
                serializer.Serialize(textWriter, this);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.ComponentModel;
using System.Xml.Serialization;

namespace RudeBuild
{
    public sealed class SolutionSettings
    {
        public const string ConfigFileNameSuffix = ".RudeBuild.config";

        [DefaultValue(false)]
        public bool DisablePrecompiledHeaders { get; set; }
        [DefaultValue(false)]
        public bool SetBigObjCompilerFlag { get; set; }

        public SerializableDictionary<string, List<string>> ProjectNameToExcludedCppFileNameMap { get; set; }

        public SolutionSettings()
        {
            this.SetToDefaults();
        }

        public static string GetConfigFilePath(SolutionInfo solutionInfo)
        {
            string solutionFileName = Path.GetFileNameWithoutExtension(solutionInfo.FilePath);
            string configFilePath = Path.Combine(Path.GetDirectoryName(solutionInfo.FilePath), solutionFileName + ConfigFileNameSuffix);
            return configFilePath;
        }

        private bool RemoveNoLongerExistingProjects(SolutionInfo solutionInfo)
        {
            bool changed = false;
            IList<string> projectNames = new List<string>(ProjectNameToExcludedCppFileNameMap.Keys);
            foreach (string projectName in projectNames)
            {
                // Check if the projects we have stored settings for still exists. If they don't, remove them.
                if (solutionInfo.GetProjectInfo(projectName) == null)
                {
                    ProjectNameToExcludedCppFileNameMap.Remove(projectName);
                    changed = true;
                }
            }

            return changed;
        }

        private bool AddNewProjects(SolutionInfo solutionInfo)
        {
            bool changed = false;
            foreach (string projectName in solutionInfo.ProjectNames)
            {
                if (!ProjectNameToExcludedCppFileNameMap.ContainsKey(projectName))
                {
                    ProjectNameToExcludedCppFileNameMap.Add(projectName, new List<string>());
                    changed = true;
                }
            }
            return changed;
        }

        private bool RemoveNoLongerExistingCppFileNames(ProjectInfo projectInfo)
        {
            // Check if all the file names we have stored settings for still exist. If they don't, remove them.
            bool changed = false;
            List<string> cppFileNames = null;
            ProjectNameToExcludedCppFileNameMap.TryGetValue(projectInfo.Name, out cppFileNames);
            if (null != cppFileNames)
            {
                for (int i = 0; i < cppFileNames.Count; ++i)
                {
                    if (!projectInfo.AllCppFileNames.Contains(cppFileNames[i]))
                    {
                        cppFileNames.RemoveAt(i);
                        --i;
                        changed = true;
                    }
                }
            }
            return changed;
        }

        public bool Update(SolutionInfo solutionInfo)
        {
            if (null == ProjectNameToExcludedCppFileNameMap)
            {
                ProjectNameToExcludedCppFileNameMap = new SerializableDictionary<string, List<string>>();
            }

            bool changed = RemoveNoLongerExistingProjects(solutionInfo);
            changed = AddNewProjects(solutionInfo) || changed;

            foreach (string projectName in solutionInfo.ProjectNames)
            {
                ProjectInfo projectInfo = solutionInfo.GetProjectInfo(projectName);
                if (null == projectInfo)
                    throw new InvalidDataException("SolutionInfo does not contain ProjectInfo object for project called " + projectName);

                changed = RemoveNoLongerExistingCppFileNames(projectInfo) || changed;
            }

            return changed;
        }

        public bool UpdateAndSave(Settings settings, SolutionInfo solutionInfo)
        {
            bool changed = Update(solutionInfo);
            if (changed)
            {
                try
                {
                    Save(settings, solutionInfo);
                }
                catch (Exception ex)
                {
                    settings.Output.WriteLine("Couldn't save solution settings file: " + GetConfigFilePath(solutionInfo));
                    settings.Output.WriteLine("because of exception: " + ex.Message);
                }
            }
            return changed;
        }

        public void ExcludeCppFileNameForProject(ProjectInfo projectInfo, string cppFileName)
        {
            if (null == ProjectNameToExcludedCppFileNameMap)
            {
                ProjectNameToExcludedCppFileNameMap = new SerializableDictionary<string, List<string>>();
            }
            if (!projectInfo.AllCppFileNames.Contains(cppFileName))
            {
                throw new ArgumentException("The project " + projectInfo.Name + " does not contain the file '" + cppFileName + "' and so cannot be excluded.");
            }

            List<string> cppFileNames;
            ProjectNameToExcludedCppFileNameMap.TryGetValue(projectInfo.Name, out cppFileNames);
            if (null == cppFileNames)
            {
                cppFileNames = new List<string>();
                cppFileNames.Add(cppFileName);
                ProjectNameToExcludedCppFileNameMap.Add(projectInfo.Name, cppFileNames);
            }
            else
            {
                if (!cppFileNames.Contains(cppFileName))
                    cppFileNames.Add(cppFileName);
            }
        }

        public void RemoveExcludedCppFileNameForProject(ProjectInfo projectInfo, string cppFileName)
        {
            if (null == ProjectNameToExcludedCppFileNameMap)
                return;
            List<string> cppFileNames = null;
            ProjectNameToExcludedCppFileNameMap.TryGetValue(projectInfo.Name, out cppFileNames);
            if (null == cppFileNames)
                return;
            cppFileNames.Remove(cppFileName);
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

        public ReadOnlyCollection<string> GetExcludedCppFileNamesForProject(string projectName)
        {
            if (null == ProjectNameToExcludedCppFileNameMap)
                return null;

            List<string> cppFileNames = null;
            ProjectNameToExcludedCppFileNameMap.TryGetValue(projectName, out cppFileNames);
            if (null == cppFileNames)
                return null;
            return new ReadOnlyCollection<string>(cppFileNames);
        }

        public static SolutionSettings Load(Settings settings, SolutionInfo solutionInfo)
        {
            string configFilePath = GetConfigFilePath(solutionInfo);
            if (File.Exists(configFilePath))
            {
                using (TextReader textReader = new StreamReader(configFilePath))
                {
                    var deserializer = new XmlSerializer(typeof(SolutionSettings));
                    try
                    {
                        var solutionSettings = (SolutionSettings)deserializer.Deserialize(textReader);
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

        private void SaveInternal(Settings settings, SolutionInfo solutionInfo)
        {
            string configFilePath = GetConfigFilePath(solutionInfo);
            using (TextWriter textWriter = new StreamWriter(configFilePath))
            {
                var serializer = new XmlSerializer(typeof(SolutionSettings));
                serializer.Serialize(textWriter, this);
            }
        }

        public void Save(Settings settings, SolutionInfo solutionInfo)
        {
            // Since multiple instances of RudeBuild could try to save the solution
            // settings at the same time, retry a few times if it fails because we
            // couldn't get exclusive access to the file.
            int retryCount = 3;
            while (retryCount > 0)
            {
                try
                {
                    SaveInternal(settings, solutionInfo);
                    retryCount = 0;
                }
                catch (Exception e)
                {
                    Thread.Sleep(300);
                    --retryCount;
                    if (retryCount == 0)
                    {
                        throw e;
                    }
                }
            }
        }
    }
}

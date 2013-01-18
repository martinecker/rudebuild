using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.IO;
using System.Xml;

namespace RudeBuild
{
    internal abstract class SingleProjectReaderWriterBase
    {
        protected Settings _settings;

        protected SingleProjectReaderWriterBase(Settings settings)
        {
            _settings = settings;
        }

        public abstract ProjectInfo ReadWrite(string projectFileName, SolutionInfo solutionInfo, XDocument projectDocument, bool performReadOnly);

        protected string GetProjectConfigName(SolutionConfigManager.ProjectConfig projectConfig)
        {
            string solutionConfigName = _settings.BuildOptions.Config;
            return projectConfig.GetProjectConfig(solutionConfigName) ?? solutionConfigName;
        }

        protected static bool IsValidCppFileName(string fileName)
        {
            string extension = Path.GetExtension(fileName);
            return extension == ".cpp" || extension == ".cxx" || extension == ".c" || extension == ".cc";
        }

        protected bool IsValidCppFileElement(SolutionConfigManager.ProjectConfig projectConfig, XNamespace ns, XElement cppFileElement, string pathAttributeName)
        {
            XAttribute pathAttribute = cppFileElement.Attribute(pathAttributeName);
            if (pathAttribute == null || !IsValidCppFileName(pathAttribute.Value))
                return false;

            // If the file element has no child elements, then we accept this file for the unity merge.
            if (!cppFileElement.HasElements)
                return true;

            return IsValidCppFileElementInternal(projectConfig, ns, cppFileElement);
        }

        protected abstract bool IsValidCppFileElementInternal(SolutionConfigManager.ProjectConfig projectConfig, XNamespace ns, XElement cppFileElement);

        protected static bool IsValidIncludeFileName(string fileName)
        {
            string extension = Path.GetExtension(fileName);
            return extension == ".h" || extension == ".hpp" || extension == ".hxx";
        }

        protected bool IsValidIncludeFileElement(XNamespace ns, XElement includeFileElement, string pathAttributeName)
        {
            XAttribute pathAttribute = includeFileElement.Attribute(pathAttributeName);
            return pathAttribute != null && IsValidIncludeFileName(pathAttribute.Value);
        }
    }

    internal class SingleProjectReaderWriterPostVS2010 : SingleProjectReaderWriterBase
    {
        public SingleProjectReaderWriterPostVS2010(Settings settings)
            : base(settings)
        {
        }

        private bool IsConfigConditionTrue(SolutionConfigManager.ProjectConfig projectConfig, string condition)
        {
            string projectConfigName = GetProjectConfigName(projectConfig);
    
            if (condition == string.Format("'$(Configuration)|$(Platform)'=='{0}'", projectConfigName))
                return true;

            int platformStartIndex = projectConfigName.IndexOf('|');
            if (platformStartIndex > 0)
            {
                string platformName = projectConfigName.Substring(platformStartIndex + 1);
                if (condition == string.Format("'$(Platform)'=='{0}'", platformName))
                    return true;
            }

            return false;
        }

        protected override bool IsValidCppFileElementInternal(SolutionConfigManager.ProjectConfig projectConfig, XNamespace ns, XElement cppFileElement)
        {
            // Get all elements with a Condition attribute. If others exist, we don't handle those currently,
            // so don't include the file in the unity merge.
            var conditionalBuildElements = (from element in cppFileElement.Elements()
                                            where element.Attribute("Condition") != null
                                            select element).ToList();
            if (cppFileElement.Elements().Count() != conditionalBuildElements.Count)
                return false;

            XName excludedFromBuildName = ns + "ExcludedFromBuild";
            foreach (XElement conditionalBuildElement in conditionalBuildElements)
            {
                XAttribute conditionAttribute = conditionalBuildElement.Attribute("Condition");
                if (!IsConfigConditionTrue(projectConfig, conditionAttribute.Value))
                    continue;
                if (conditionalBuildElement.Name != excludedFromBuildName)
                    return false;
                if (conditionalBuildElement.Value == "true")
                    return false;
            }

            return true;
        }

        private XElement GetConfigurationElement(SolutionConfigManager.ProjectConfig projectConfig, XDocument projectDocument, XNamespace ns)
        {
            var configElements =
                from configElement in projectDocument.Descendants(ns + "ItemDefinitionGroup")
                let configConditionElement = configElement.Attribute("Condition")
                where configConditionElement != null && IsConfigConditionTrue(projectConfig, configConditionElement.Value)
                select configElement;
            return configElements.SingleOrDefault();
        }

        private string GetPrecompiledHeader(SolutionConfigManager.ProjectConfig projectConfig, XDocument projectDocument, XNamespace ns)
        {
            XElement configElement = GetConfigurationElement(projectConfig, projectDocument, ns);
            if (null == configElement)
                return string.Empty;

            XElement precompiledHeaderElement = configElement.Descendants(ns + "PrecompiledHeader").SingleOrDefault();
            XElement precompiledHeaderFileElement = configElement.Descendants(ns + "PrecompiledHeaderFile").SingleOrDefault();
            if (null == precompiledHeaderElement || precompiledHeaderElement.Value != "Use")
                return string.Empty;

            return null != precompiledHeaderFileElement ? precompiledHeaderFileElement.Value : "StdAfx.h";
        }

        private static void DisablePrecompiledHeaders(XDocument projectDocument, XNamespace ns)
        {
            var pchElements =
                from pchElement in projectDocument.Descendants(ns + "PrecompiledHeader")
                select pchElement;
            foreach (XElement pchElement in pchElements)
            {
                pchElement.Value = "NotUsing";
            }
        }

        private static void SetBigObjCompilerFlag(XDocument projectDocument, XNamespace ns)
        {
            XElement projectElement = projectDocument.Element(ns + "Project");
            if (null == projectElement)
                return;
            foreach (XElement itemDefGroupElement in projectElement.Elements(ns + "ItemDefinitionGroup"))
            {
                if (null == itemDefGroupElement.Attribute("Condition"))
                    continue;
                XElement compileElement = itemDefGroupElement.Element(ns + "ClCompile");
                if (null == compileElement)
                    continue;

                XElement additionalOptionsElement = compileElement.Element(ns + "AdditionalOptions");
                string options = "/bigobj %(AdditionalOptions)";
                if (null != additionalOptionsElement)
                {
                    options = "/bigobj " + additionalOptionsElement.Value;
                }
                else
                {
                    additionalOptionsElement = new XElement(ns + "AdditionalOptions");
                    compileElement.Add(additionalOptionsElement);
                }
                additionalOptionsElement.Value = options;
            }
        }

        private XElement GetProjectElement(string projectFileName, XNamespace ns, XDocument projectDocument)
        {
            XElement projectElement = projectDocument.Element(ns + "Project");
            if (null == projectElement)
            {
                throw new InvalidDataException("Project file '" + projectFileName + "' is corrupt. Couldn't find Project XML element.");
            }
            return projectElement;
        }

        private XElement GetGlobalPropertyGroupElement(string projectFileName, XNamespace ns, XElement projectElement)
        {
            XElement globalPropertyGroupElement = (from element in projectElement.Elements()
                                                   let labelAttribute = element.Attribute("Label")
                                                   where labelAttribute != null && labelAttribute.Value == "Globals"
                                                   select element).SingleOrDefault();
            if (null == globalPropertyGroupElement)
            {
                throw new InvalidDataException("Project file '" + projectFileName + "' is corrupt. Couldn't find PropertyGroup XML element with Label=\"Globals\".");
            }
            return globalPropertyGroupElement;
        }

        private XElement GetCompileItemGroupElement(SolutionConfigManager.ProjectConfig projectConfig, XNamespace ns, XElement projectElement)
        {
            var compileItemGroupElement = from itemGroupElement in projectElement.Elements(ns + "ItemGroup")
                                          let compileElements = itemGroupElement.Elements(ns + "ClCompile")
                                          where compileElements.Any(cppFileElement => IsValidCppFileElement(projectConfig, ns, cppFileElement, "Include"))
                                          select itemGroupElement;
            XElement result = compileItemGroupElement.SingleOrDefault();
            return result;
        }

        private IList<string> GetIncludeFileNames(XNamespace ns, XElement projectElement)
        {
            var includeItemGroupElement = from itemGroupElement in projectElement.Elements(ns + "ItemGroup")
                                          let includeElements = itemGroupElement.Elements(ns + "ClInclude")
                                          where includeElements.Any()
                                          select itemGroupElement;
            return (from includeElement in includeItemGroupElement.Elements(ns + "ClInclude")
                    where IsValidIncludeFileElement(ns, includeElement, "Include")
                    select includeElement.Attribute("Include").Value).ToList();
        }

        private static void AddExcludedFromBuild(XNamespace ns, XElement element)
        {
            XName excludedName = ns + "ExcludedFromBuild";
            XElement existingExlude = element.Element(excludedName);
            if (existingExlude != null)
            {
                existingExlude.Remove();
            }
            element.Add(new XElement(excludedName, "true"));
        }

        private void ReadWriteFilters(string projectFileName, SolutionConfigManager.ProjectConfig projectConfig, UnityFileMerger merger)
        {
            string projectFiltersFileName = projectFileName + ".filters";
            if (!File.Exists(projectFiltersFileName))
                return;

            XDocument projectFiltersDocument = XDocument.Load(projectFiltersFileName);
            if (null == projectFiltersDocument)
            {
                throw new InvalidDataException("Couldn't load project filters file '" + projectFiltersFileName + "'.");
            }

            XNamespace ns = projectFiltersDocument.Root.Name.Namespace;
            XElement projectElement = GetProjectElement(projectFileName, ns, projectFiltersDocument);
            XElement compileItemGroupElement = GetCompileItemGroupElement(projectConfig, ns, projectElement);

            if (compileItemGroupElement != null)
            {
                compileItemGroupElement.Add(
                    from unityFileName in merger.UnityFilePaths
                    select new XElement(ns + "ClCompile", new XAttribute("Include", unityFileName)));
            }

            string destProjectFiltersFileName = _settings.ModifyFileName(projectFiltersFileName);
            var writer = new ModifiedTextFileWriter(destProjectFiltersFileName, _settings.BuildOptions.ShouldForceWriteCachedFiles());
            if (writer.Write(projectFiltersDocument.ToString()))
            {
                _settings.Output.WriteLine("Creating project filters file " + destProjectFiltersFileName);
            }
        }

        public override ProjectInfo ReadWrite(string projectFileName, SolutionInfo solutionInfo, XDocument projectDocument, bool performReadOnly)
        {
            SolutionConfigManager.ProjectConfig projectConfig = solutionInfo.ConfigManager.GetProjectByFileName(projectFileName);
            if (null == projectConfig)
                throw new InvalidDataException("Couldn't find project " + projectFileName + " in solution " + solutionInfo.Name);

            XNamespace ns = projectDocument.Root.Name.Namespace;
            XElement projectElement = GetProjectElement(projectFileName, ns, projectDocument);
            
            // Determine the project name and ensure the generated RudeBuild project has a ProjectName element.
            string projectName = Path.GetFileNameWithoutExtension(projectFileName);
            XElement globalPropertyGroupElement = GetGlobalPropertyGroupElement(projectFileName, ns, projectElement);
            XElement projectNameElement = globalPropertyGroupElement.Element(ns + "ProjectName");
            if (projectNameElement == null)
                globalPropertyGroupElement.Add(new XElement(ns + "ProjectName", projectName));
            else
                projectName = projectNameElement.Value;

            XElement compileItemGroupElement = GetCompileItemGroupElement(projectConfig, ns, projectElement);
            IList<string> cppFileNames = null;
            IList<XElement> cppFileNameElements = null;
            if (compileItemGroupElement != null)
            {
                cppFileNameElements = (
                    from compileElement in compileItemGroupElement.Elements(ns + "ClCompile")
                    where IsValidCppFileElement(projectConfig, ns, compileElement, "Include")
                    select compileElement).ToList();
                cppFileNames = (
                    from compileElement in cppFileNameElements
                    select compileElement.Attribute("Include").Value).ToList();
            }
            else
            {
                cppFileNames = new List<string>();
            }

            IList<string> includeFileNames = GetIncludeFileNames(ns, projectElement);
            string precompiledHeaderName = GetPrecompiledHeader(projectConfig, projectDocument, ns);
            var projectInfo = new ProjectInfo(solutionInfo, projectName, projectFileName, cppFileNames, includeFileNames, precompiledHeaderName);

            if (!performReadOnly)
            {
                var merger = new UnityFileMerger(_settings);
                merger.Process(projectInfo);

                if (cppFileNameElements != null)
                {
                    foreach (XElement cppFileNameElement in cppFileNameElements)
                    {
                        string cppFileName = cppFileNameElement.Attribute("Include").Value;
                        if (merger.MergedCppFileNames.Contains(cppFileName))
                            AddExcludedFromBuild(ns, cppFileNameElement);
                    }
                }

                if (compileItemGroupElement != null)
                {
                    compileItemGroupElement.Add(
                        from unityFileName in merger.UnityFilePaths
                        select new XElement(ns + "ClCompile", new XAttribute("Include", unityFileName)));
                }

                if (_settings.SolutionSettings.DisablePrecompiledHeaders)
                {
                    DisablePrecompiledHeaders(projectDocument, ns);
                }
                if (_settings.SolutionSettings.SetBigObjCompilerFlag)
                {
                    SetBigObjCompilerFlag(projectDocument, ns);
                }

                ReadWriteFilters(projectFileName, projectConfig, merger);
            }

            return projectInfo;
        }
    }

    internal class SingleProjectReaderWriterPreVS2010 : SingleProjectReaderWriterBase
    {
        public SingleProjectReaderWriterPreVS2010(Settings settings)
            : base(settings)
        {
        }

        private bool IsValidFileConfigElement(XNamespace ns, XElement element)
        {
            if (element.Name != ns + "FileConfiguration")
                return false;

            if (!element.Attributes().All(attribute => attribute.Name == "Name" || attribute.Name == "ExcludedFromBuild"))
                return false;

            int childElementCount = element.Elements().Count();
            if (childElementCount == 0)
                return true;
            if (childElementCount > 1)
                return false;

            XElement toolElement = element.Element(ns + "Tool");
            if (null == toolElement)
                return false;
            if (toolElement.Attributes().Any(attribute => attribute.Name != "Name"))
                return false;
            XAttribute nameAttribute = toolElement.Attribute("Name");
            if (null == nameAttribute || nameAttribute.Value != "VCCLCompilerTool")
                return false;
            return true;
        }

        protected override bool IsValidCppFileElementInternal(SolutionConfigManager.ProjectConfig projectConfig, XNamespace ns, XElement cppFileElement)
        {
            // Get all child elements called FileConfiguration with a Name and possibly ExcludedFromBuild attribute.
            // If others exist, we don't handle those currently, so don't include the file in the unity merge.
            var configBuildElements = (from element in cppFileElement.Elements()
                                       where IsValidFileConfigElement(ns, element)
                                       select element).ToList();
            if (cppFileElement.Elements().Count() != configBuildElements.Count)
                return false;

            string currentConfigCondition = GetProjectConfigName(projectConfig);
            foreach (XElement configBuildElement in configBuildElements)
            {
                XAttribute nameAttribute = configBuildElement.Attribute("Name");
                if (nameAttribute.Value != currentConfigCondition)
                    continue;
                XAttribute excludedFromBuildAttribute = configBuildElement.Attribute("ExcludedFromBuild");
                if (null != excludedFromBuildAttribute && excludedFromBuildAttribute.Value == "true")
                    return false;
            }

            return true;
        }

        private XElement GetConfigurationElement(SolutionConfigManager.ProjectConfig projectConfig, XDocument projectDocument, XNamespace ns)
        {
            string projectConfigName = GetProjectConfigName(projectConfig);
            var configElements =
                from configElement in projectDocument.Descendants(ns + "Configuration")
                where configElement.Attribute(ns + "Name") != null && configElement.Attribute(ns + "Name").Value == projectConfigName
                select configElement;
            return configElements.SingleOrDefault();
        }

        private string GetPrecompiledHeader(SolutionConfigManager.ProjectConfig projectConfig, XDocument projectDocument, XNamespace ns)
        {
            XElement configElement = GetConfigurationElement(projectConfig, projectDocument, ns);
            if (null == configElement)
                return string.Empty;

            XElement precompiledHeaderElement =
                (from toolElement in configElement.Elements(ns + "Tool")
                where toolElement.Attribute(ns + "UsePrecompiledHeader") != null && toolElement.Attribute(ns + "UsePrecompiledHeader").Value == "2"
                select toolElement).SingleOrDefault();
            if (null == precompiledHeaderElement)
                return string.Empty;

            XAttribute precompiledHeader = precompiledHeaderElement.Attribute(ns + "PrecompiledHeaderThrough");
            return null != precompiledHeader ? precompiledHeader.Value : "StdAfx.h";
        }

        private static void DisablePrecompiledHeaders(XDocument projectDocument, XNamespace ns)
        {
            var pchAttributes =
                from pchAttribute in projectDocument.Descendants().Attributes(ns + "UsePrecompiledHeader")
                select pchAttribute;
            foreach (XAttribute pchAttribute in pchAttributes.ToList())
            {
                pchAttribute.Value = "0";
            }
        }

        private static void SetBigObjCompilerFlag(XDocument projectDocument, XNamespace ns)
        {
            XElement projectElement = projectDocument.Element(ns + "VisualStudioProject");
            if (null == projectElement)
                return;
            XElement configurationsElement = projectElement.Element(ns + "Configurations");
            if (null == configurationsElement)
                return;
            foreach (XElement configElement in configurationsElement.Elements(ns + "Configuration"))
            {
                XElement compilerToolElement = (from element in configElement.Elements()
                                                let nameAttribute = element.Attribute("Name")
                                                where element.Name == ns + "Tool" && nameAttribute != null && nameAttribute.Value == "VCCLCompilerTool"
                                                select element).SingleOrDefault();
                if (null != compilerToolElement)
                {
                    XAttribute additionalOptionsAttribute = compilerToolElement.Attribute("AdditionalOptions");
                    string options = "/bigobj ";
                    if (null != additionalOptionsAttribute)
                        options += additionalOptionsAttribute.Value;
                    compilerToolElement.SetAttributeValue("AdditionalOptions", options);
                }
            }
        }

        public override ProjectInfo ReadWrite(string projectFileName, SolutionInfo solutionInfo, XDocument projectDocument, bool performReadOnly)
        {
            SolutionConfigManager.ProjectConfig projectConfig = solutionInfo.ConfigManager.GetProjectByFileName(projectFileName);
            if (null == projectConfig)
                throw new InvalidDataException("Couldn't find project " + projectFileName + " in solution " + solutionInfo.Name);

            XNamespace ns = projectDocument.Root.Name.Namespace;

            var cppFileNameElements =
                (from cppFileElement in projectDocument.Descendants(ns + "File")
                 where IsValidCppFileElement(projectConfig, ns, cppFileElement, "RelativePath")
                 select cppFileElement).ToList();
            var cppFileNames =
                from cppFileElement in cppFileNameElements
                select cppFileElement.Attribute("RelativePath").Value;
            var includeFileNames =
                from includeFileElement in projectDocument.Descendants(ns + "File")
                where IsValidIncludeFileElement(ns, includeFileElement, "RelativePath")
                select includeFileElement.Attribute("RelativePath").Value;

            string projectName = Path.GetFileNameWithoutExtension(projectFileName);
            string precompiledHeaderName = GetPrecompiledHeader(projectConfig, projectDocument, ns);
            var projectInfo = new ProjectInfo(solutionInfo, projectName, projectFileName, cppFileNames.ToList(), includeFileNames.ToList(), precompiledHeaderName);

            if (!performReadOnly)
            {
                var merger = new UnityFileMerger(_settings);
                merger.Process(projectInfo);

                foreach (XElement cppFileNameElement in cppFileNameElements)
                {
                    string cppFileName = cppFileNameElement.Attribute("RelativePath").Value;
                    if (merger.MergedCppFileNames.Contains(cppFileName))
                        cppFileNameElement.Remove();
                }

                XElement filesElement = projectDocument.Descendants(ns + "Files").Single();
                filesElement.Add(
                    from unityFileName in merger.UnityFilePaths
                    select new XElement(ns + "File", new XAttribute("RelativePath", unityFileName)));

                if (_settings.SolutionSettings.DisablePrecompiledHeaders)
                {
                    DisablePrecompiledHeaders(projectDocument, ns);
                }
                if (_settings.SolutionSettings.SetBigObjCompilerFlag)
                {
                    SetBigObjCompilerFlag(projectDocument, ns);
                }
            }

            return projectInfo;
        }
    }

    public class ProjectReaderWriter
    {
        private readonly Settings _settings;

        public ProjectReaderWriter(Settings settings)
        {
            _settings = settings;
        }

        private SingleProjectReaderWriterBase CreateSingleProjectReaderWriter(SolutionInfo solutionInfo)
        {
            if (solutionInfo.Version >= VisualStudioVersion.VS2010)
                return new SingleProjectReaderWriterPostVS2010(_settings);
            else
                return new SingleProjectReaderWriterPreVS2010(_settings);
        }

        public void Read(SolutionInfo solutionInfo)
        {
            SingleProjectReaderWriterBase singleProjectReaderWriter = CreateSingleProjectReaderWriter(solutionInfo);
            foreach (string projectFileName in solutionInfo.ProjectFileNames)
            {
                ReadWrite(projectFileName, solutionInfo, singleProjectReaderWriter, performReadOnly: true);
            }
        }

        public void ReadWrite(SolutionInfo solutionInfo)
        {
            SingleProjectReaderWriterBase singleProjectReaderWriter = CreateSingleProjectReaderWriter(solutionInfo);
            foreach (string projectFileName in solutionInfo.ProjectFileNames)
            {
                ReadWrite(projectFileName, solutionInfo, singleProjectReaderWriter, performReadOnly: false);
            }
        }

        private void ReadWrite(string projectFileName, SolutionInfo solutionInfo, SingleProjectReaderWriterBase singleProjectReaderWriter, bool performReadOnly)
        {
            XDocument projectDocument = XDocument.Load(projectFileName);
            if (null == projectDocument)
            {
                throw new InvalidDataException("Couldn't load project file '" + projectFileName + "'.");
            }

            ProjectInfo projectInfo = singleProjectReaderWriter.ReadWrite(projectFileName, solutionInfo, projectDocument, performReadOnly);
            solutionInfo.AddProject(projectInfo);

            if (!performReadOnly)
            {
                string destProjectFileName = _settings.ModifyFileName(projectFileName);
                var writer = new ModifiedTextFileWriter(destProjectFileName, _settings.BuildOptions.ShouldForceWriteCachedFiles());
                if (writer.Write(projectDocument.ToString()))
                {
                    _settings.Output.WriteLine("Creating project file " + destProjectFileName);
                }
            }
        }
    }
}

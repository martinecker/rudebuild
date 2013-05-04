using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.IO;

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

        protected bool IsValidCppFileElement(XNamespace ns, XElement cppFileElement, string pathAttributeName)
        {
            XAttribute pathAttribute = cppFileElement.Attribute(pathAttributeName);
            return pathAttribute != null && IsValidCppFileName(pathAttribute.Value);
        }

        protected bool IsMergableCppFileElement(SolutionConfigManager.ProjectConfig projectConfig, XNamespace ns, XElement cppFileElement, string pathAttributeName)
        {
            if (!IsValidCppFileElement(ns, cppFileElement, pathAttributeName))
                return false;

            // If the file element has no child elements, then we accept this file for the unity merge.
            if (!cppFileElement.HasElements)
                return true;

            return IsMergableCppFileElementInternal(projectConfig, ns, cppFileElement);
        }

        protected abstract bool IsMergableCppFileElementInternal(SolutionConfigManager.ProjectConfig projectConfig, XNamespace ns, XElement cppFileElement);

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

        protected bool ShouldDisablePrecompiledHeaders(ProjectInfo projectInfo)
        {
            return _settings.SolutionSettings.DisablePrecompiledHeaders || string.IsNullOrEmpty(projectInfo.PrecompiledHeaderName);
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

        protected override bool IsMergableCppFileElementInternal(SolutionConfigManager.ProjectConfig projectConfig, XNamespace ns, XElement cppFileElement)
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

        private IEnumerable<XElement> GetConfigurationElements(SolutionConfigManager.ProjectConfig projectConfig, XDocument projectDocument, XNamespace ns)
        {
            var configElements =
                from configElement in projectDocument.Descendants(ns + "ItemDefinitionGroup")
                let configConditionElement = configElement.Attribute("Condition")
                where configConditionElement == null || IsConfigConditionTrue(projectConfig, configConditionElement.Value)
                select configElement;
            return configElements;
        }

        private string GetPrecompiledHeader(SolutionConfigManager.ProjectConfig projectConfig, XDocument projectDocument, XNamespace ns)
        {
            IEnumerable<XElement> configElements = GetConfigurationElements(projectConfig, projectDocument, ns);
            int precompiledHeaderUseCount = (from precompiledHeaderElement in configElements.Descendants(ns + "PrecompiledHeader")
                                             where precompiledHeaderElement.Value == "Use"
                                             select precompiledHeaderElement).Count();
            if (0 == precompiledHeaderUseCount)
                return string.Empty;

            XElement precompiledHeaderFileElement = (from element in configElements.Descendants(ns + "PrecompiledHeaderFile")
                                                     select element).SingleOrDefault();
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

        private XElement GetGlobalProjectPropertyGroupElement(string projectFileName, XNamespace ns, XElement projectElement)
        {
            var globalPropertyGroupElements = from element in projectElement.Elements()
                                              let labelAttribute = element.Attribute("Label")
                                              where labelAttribute != null && labelAttribute.Value == "Globals" && element.Element(ns + "ProjectGuid") != null
                                              select element;
            if (globalPropertyGroupElements.Count() != 1)
            {
                throw new InvalidDataException("Project file '" + projectFileName + "' is corrupt. Couldn't find PropertyGroup XML element with Label=\"Globals\" and a ProjectGuid child element.");
            }
            return globalPropertyGroupElements.Single();
        }

        private IEnumerable<XElement> GetCompileItemGroupElements(XNamespace ns, XElement projectElement)
        {
            return from itemGroupElement in projectElement.Elements(ns + "ItemGroup")
                   let compileElements = itemGroupElement.Elements(ns + "ClCompile")
                   where compileElements.Any(cppFileElement => IsValidCppFileElement(ns, cppFileElement, "Include"))
                   select itemGroupElement;
        }

        private IList<string> GetAllIncludeFileNames(XNamespace ns, XElement projectElement)
        {
            XElement includeItemGroupElement = (from itemGroupElement in projectElement.Elements(ns + "ItemGroup")
                                                let includeElements = itemGroupElement.Elements(ns + "ClInclude")
                                                where includeElements.Any(includeElement => IsValidIncludeFileElement(ns, includeElement, "Include"))
                                                select itemGroupElement).SingleOrDefault();
            if (null == includeItemGroupElement)
                return new List<string>();

            return (from includeElement in includeItemGroupElement.Elements(ns + "ClInclude")
                    where IsValidIncludeFileElement(ns, includeElement, "Include")
                    select includeElement.Attribute("Include").Value).ToList();
        }

        private IList<string> GetAllCppFileNames(XNamespace ns, XElement projectElement)
        {
            var compileItemGroupElements = GetCompileItemGroupElements(ns, projectElement);
            var result = new List<string>();
            foreach (var compileItemGroupElement in compileItemGroupElements)
            {
                result.AddRange(from compileElement in compileItemGroupElement.Elements(ns + "ClCompile")
                                where IsValidCppFileElement(ns, compileElement, "Include")
                                select compileElement.Attribute("Include").Value);
            }
            return result;
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
            var compileItemGroupElements = GetCompileItemGroupElements(ns, projectElement);
            if (compileItemGroupElements.Count() > 0)
            {
                compileItemGroupElements.First().Add(
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
            XElement globalPropertyGroupElement = GetGlobalProjectPropertyGroupElement(projectFileName, ns, projectElement);
            XElement projectNameElement = globalPropertyGroupElement.Element(ns + "ProjectName");
            if (projectNameElement == null)
                globalPropertyGroupElement.Add(new XElement(ns + "ProjectName", projectName));
            else
                projectName = projectNameElement.Value;

            var compileItemGroupElements = GetCompileItemGroupElements(ns, projectElement);
            var mergableCppFileNames = new List<string>();
            var mergableCppFileNameElements = new List<XElement>();
            foreach (var compileItemGroupElement in compileItemGroupElements)
            {
                mergableCppFileNameElements.AddRange(
                    from compileElement in compileItemGroupElement.Elements(ns + "ClCompile")
                    where IsMergableCppFileElement(projectConfig, ns, compileElement, "Include")
                    select compileElement);
            }
            mergableCppFileNames.AddRange(
                from compileElement in mergableCppFileNameElements
                select compileElement.Attribute("Include").Value);

            IList<string> allCppFileNames = GetAllCppFileNames(ns, projectElement);
            IList<string> allIncludeFileNames = GetAllIncludeFileNames(ns, projectElement);
            string precompiledHeaderName = GetPrecompiledHeader(projectConfig, projectDocument, ns);
            var projectInfo = new ProjectInfo(solutionInfo, projectName, projectFileName, mergableCppFileNames, allCppFileNames, allIncludeFileNames, precompiledHeaderName);

            if (!performReadOnly)
            {
                var merger = new UnityFileMerger(_settings);
                merger.Process(projectInfo);

                foreach (XElement cppFileNameElement in mergableCppFileNameElements)
                {
                    string cppFileName = cppFileNameElement.Attribute("Include").Value;
                    if (merger.MergedCppFileNames.Contains(cppFileName))
                        AddExcludedFromBuild(ns, cppFileNameElement);
                }

                if (compileItemGroupElements.Count() > 0)
                {
                    compileItemGroupElements.First().Add(
                        from unityFileName in merger.UnityFilePaths
                        select new XElement(ns + "ClCompile", new XAttribute("Include", unityFileName)));
                }

                if (ShouldDisablePrecompiledHeaders(projectInfo))
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

        protected override bool IsMergableCppFileElementInternal(SolutionConfigManager.ProjectConfig projectConfig, XNamespace ns, XElement cppFileElement)
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

            var fileElements = projectDocument.Descendants(ns + "File").ToList();
            var mergableCppFileElements =
                (from element in fileElements
                 where IsMergableCppFileElement(projectConfig, ns, element, "RelativePath")
                 select element).ToList();
            var mergableCppFileNames =
                from cppFileElement in mergableCppFileElements
                select cppFileElement.Attribute("RelativePath").Value;
            
            var allCppFileNames =
                from element in fileElements
                where IsValidCppFileElement(ns, element, "RelativePath")
                select element.Attribute("RelativePath").Value;
            var allIncludeFileNames =
                from element in fileElements
                where IsValidIncludeFileElement(ns, element, "RelativePath")
                select element.Attribute("RelativePath").Value;

            string projectName = Path.GetFileNameWithoutExtension(projectFileName);
            string precompiledHeaderName = GetPrecompiledHeader(projectConfig, projectDocument, ns);
            var projectInfo = new ProjectInfo(solutionInfo, projectName, projectFileName, mergableCppFileNames.ToList(), allCppFileNames.ToList(), allIncludeFileNames.ToList(), precompiledHeaderName);

            if (!performReadOnly)
            {
                var merger = new UnityFileMerger(_settings);
                merger.Process(projectInfo);

                foreach (XElement cppFileNameElement in mergableCppFileElements)
                {
                    string cppFileName = cppFileNameElement.Attribute("RelativePath").Value;
                    if (merger.MergedCppFileNames.Contains(cppFileName))
                        cppFileNameElement.Remove();
                }

                XElement filesElement = projectDocument.Descendants(ns + "Files").Single();
                filesElement.Add(
                    from unityFileName in merger.UnityFilePaths
                    select new XElement(ns + "File", new XAttribute("RelativePath", unityFileName)));

                if (ShouldDisablePrecompiledHeaders(projectInfo))
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

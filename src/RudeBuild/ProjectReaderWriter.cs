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

		protected static bool IsValidCppFileName(string fileName)
		{
			string extension = Path.GetExtension(fileName);
			return extension == ".cpp" || extension == ".cxx" || extension == ".c" || extension == ".cc";
		}

		protected bool IsValidCppFileElement(XNamespace ns, XElement cppFileElement, string pathAttributeName)
		{
			// Exclude any files that have special handling, precompiled headers, except for files that are excluded from build, etc.
			XAttribute pathAttribute = cppFileElement.Attribute(pathAttributeName);
			if (pathAttribute == null || !IsValidCppFileName(pathAttribute.Value))
				return false;

			if (!cppFileElement.HasElements)
				return true;

			// Make sure the only elements we have are ExcludedFromBuild elements because those are the only ones we handle correctly.
			// If there are other elements, don't include this file in the unity build and just build it on its own.
			var excludedFromBuildElements = (from element in cppFileElement.Elements()
											 where element.Name == ns + "ExcludedFromBuild"
											 select element).ToList();
			if (cppFileElement.Elements().Count() != excludedFromBuildElements.Count)
				return false;

			string currentConfigCondition = string.Format("'$(Configuration)|$(Platform)'=='{0}'", _settings.BuildOptions.Config);
			foreach (XElement excludedFromBuildElement in excludedFromBuildElements)
			{
				XAttribute conditionAttribute = excludedFromBuildElement.Attribute("Condition");
				if (conditionAttribute == null)
					return false;
				if (conditionAttribute.Value != currentConfigCondition)
					continue;
				if (excludedFromBuildElement.Value == "true")
					return false;
			}

			return true;
		}

		protected bool HasValidCppFileElements(XNamespace ns, IEnumerable<XElement> cppFileElements, string pathAttributeName)
		{
		    return cppFileElements.Any(cppFileElement => IsValidCppFileElement(ns, cppFileElement, pathAttributeName));
		}
    }

    internal class SingleProjectReaderWriterPostVS2010 : SingleProjectReaderWriterBase
    {
        public SingleProjectReaderWriterPostVS2010(Settings settings)
            : base(settings)
        {
        }

        private XElement GetConfigurationElement(XDocument projectDocument, XNamespace ns)
        {
            string configCondition = "'$(Configuration)|$(Platform)'=='" + _settings.BuildOptions.Config + "'";

            var configElements =
                from configElement in projectDocument.Descendants(ns + "ItemDefinitionGroup")
                where configElement.Attribute("Condition") != null && configElement.Attribute("Condition").Value == configCondition
                select configElement;
            return configElements.SingleOrDefault();
        }

        private string GetPrecompiledHeader(XDocument projectDocument, XNamespace ns)
        {
            XElement configElement = GetConfigurationElement(projectDocument, ns);
            if (null != configElement)
            {
                XElement precompiledHeaderElement = configElement.Descendants(ns + "PrecompiledHeader").SingleOrDefault();
                XElement precompiledHeaderFileElement = configElement.Descendants(ns + "PrecompiledHeaderFile").SingleOrDefault();
                if (null != precompiledHeaderElement && precompiledHeaderElement.Value == "Use")
                {
                    if (null != precompiledHeaderFileElement)
                        return precompiledHeaderFileElement.Value;
                    else
                        return "StdAfx.h";
                }
            }

            return string.Empty;
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

        private XElement GetCompileItemGroupElement(string projectFileName, XNamespace ns, XDocument projectDocument)
        {
            XElement projectElement = projectDocument.Element(ns + "Project");
            if (null == projectElement)
            {
                throw new InvalidDataException("Project file '" + projectFileName + "' is corrupt. Couldn't find Project XML element.");
            }

            var compileItemGroupElement = from itemGroupElement in projectElement.Elements(ns + "ItemGroup")
										  let compileElements = itemGroupElement.Elements(ns + "ClCompile")
                                          where HasValidCppFileElements(ns, compileElements, "Include")
                                          select itemGroupElement;
            XElement result = compileItemGroupElement.SingleOrDefault();
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

        private void ReadWriteFilters(string projectFileName, UnityFileMerger merger)
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
            XElement compileItemGroupElement = GetCompileItemGroupElement(projectFileName, ns, projectFiltersDocument);

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
            XNamespace ns = projectDocument.Root.Name.Namespace;
            XElement compileItemGroupElement = GetCompileItemGroupElement(projectFileName, ns, projectDocument);

			IList<string> cppFileNames = null;
			IList<XElement> cppFileNameElements = null;
			if (compileItemGroupElement != null)
			{
				cppFileNameElements = (
					from compileElement in compileItemGroupElement.Elements(ns + "ClCompile")
					where IsValidCppFileElement(ns, compileElement, "Include")
					select compileElement).ToList();
				cppFileNames = (
					from compileElement in cppFileNameElements
					select compileElement.Attribute("Include").Value).ToList();
			}
			else
			{
				cppFileNames = new List<string>();
			}

            string precompiledHeaderFileName = GetPrecompiledHeader(projectDocument, ns);
            var projectInfo = new ProjectInfo(solutionInfo, projectFileName, cppFileNames, precompiledHeaderFileName);

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

                ReadWriteFilters(projectFileName, merger);
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

        private XElement GetConfigurationElement(XDocument projectDocument, XNamespace ns)
        {
            var configElements =
                from configElement in projectDocument.Descendants(ns + "Configuration")
                where configElement.Attribute(ns + "Name") != null && configElement.Attribute(ns + "Name").Value == _settings.BuildOptions.Config
                select configElement;
            return configElements.SingleOrDefault();
        }

        private string GetPrecompiledHeader(XDocument projectDocument, XNamespace ns)
        {
            XElement configElement = GetConfigurationElement(projectDocument, ns);
            if (null == configElement)
                return string.Empty;

            XElement precompiledHeaderElement =
                (from toolElement in configElement.Elements(ns + "Tool")
                where toolElement.Attribute(ns + "UsePrecompiledHeader") != null && toolElement.Attribute(ns + "UsePrecompiledHeader").Value == "2"
                select toolElement).SingleOrDefault();
            if (null == precompiledHeaderElement)
                return string.Empty;

            XAttribute precompiledHeader = precompiledHeaderElement.Attribute(ns + "PrecompiledHeaderThrough");
            if (null != precompiledHeader)
                return precompiledHeader.Value;
            else
                return "StdAfx.h";
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

        public override ProjectInfo ReadWrite(string projectFileName, SolutionInfo solutionInfo, XDocument projectDocument, bool performReadOnly)
        {
            XNamespace ns = projectDocument.Root.Name.Namespace;

            var cppFileNameElements =
                from cppFileElement in projectDocument.Descendants(ns + "File")
                where IsValidCppFileElement(ns, cppFileElement, "RelativePath")
                select cppFileElement;
            var cppFileNames =
                from cppFileElement in cppFileNameElements
                select cppFileElement.Attribute("RelativePath").Value;

            string precompiledHeaderFileName = GetPrecompiledHeader(projectDocument, ns);
            var projectInfo = new ProjectInfo(solutionInfo, projectFileName, cppFileNames.ToList(), precompiledHeaderFileName);

            if (!performReadOnly)
            {
                var merger = new UnityFileMerger(_settings);
                merger.Process(projectInfo);

                foreach (XElement cppFileNameElement in cppFileNameElements.ToList())
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

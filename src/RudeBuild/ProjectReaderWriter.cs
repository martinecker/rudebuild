using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.IO;
using System.Xml;

namespace RudeBuild
{
    public class ProjectReaderWriter
    {
        private GlobalSettings _globalSettings;

        public ProjectReaderWriter(GlobalSettings globalSettings)
        {
            _globalSettings = globalSettings;
        }

        public void ReadWrite(SolutionInfo solutionInfo)
        {
            foreach (string projectFileName in solutionInfo.ProjectFileNames)
            {
                ReadWrite(projectFileName, solutionInfo);
            }
        }

        private XElement FindCompileItemGroupElement(string projectFileName, XNamespace ns, XDocument projectDocument)
        {
            XElement projectElement = projectDocument.Element(ns + "Project");
            if (null == projectElement)
            {
                throw new InvalidDataException("Project file '" + projectFileName + "' is corrupt. Couldn't find Project XML element.");
            }

            var compileItemGroupElement = from itemGroupElement in projectElement.Elements(ns + "ItemGroup")
                                          where itemGroupElement.Elements(ns + "ClCompile").Count() > 0
                                          select itemGroupElement;
            XElement result = compileItemGroupElement.SingleOrDefault();
            if (null == result)
            {
                throw new InvalidDataException("Project file '" + projectFileName + "' is corrupt. Couldn't find ItemGroup XML element with the source files to compile.");
            }
            return result;
        }

        private static XElement AddExcludedFromBuild(XNamespace ns, XElement element)
        {
            XName excludedName = ns + "ExcludedFromBuild";
            XElement existingExlude = element.Element(excludedName);
            if (existingExlude != null)
            {
                existingExlude.Remove();
            }
            element.Add(new XElement(excludedName, "true"));
            return element;
        }

        private UnityFileMerger ReadWriteVS2010(string projectFileName, SolutionInfo solutionInfo, XDocument projectDocument, XNamespace ns)
        {
            XElement compileItemGroupElement = FindCompileItemGroupElement(projectFileName, ns, projectDocument);

            var cppFileNames = from compileElement in compileItemGroupElement.Elements(ns + "ClCompile")
                               where !compileElement.HasElements
                               select compileElement.Attribute("Include").Value;

            ProjectInfo projectInfo = new ProjectInfo(solutionInfo, projectFileName, cppFileNames.ToList());

            UnityFileMerger merger = new UnityFileMerger(_globalSettings);
            merger.Process(projectInfo);

            compileItemGroupElement.ReplaceAll(
                from compileElement in compileItemGroupElement.Elements(ns + "ClCompile")
                select AddExcludedFromBuild(ns, compileElement));
            compileItemGroupElement.Add(
                from unityFileName in merger.UnityFilePaths
                select new XElement(ns + "ClCompile", new XAttribute("Include", unityFileName)));

            return merger;
        }

        private void ReadWriteVS2010Filters(string projectFileName, UnityFileMerger merger)
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
            XElement compileItemGroupElement = FindCompileItemGroupElement(projectFileName, ns, projectFiltersDocument);

            compileItemGroupElement.Add(
                from unityFileName in merger.UnityFilePaths
                select new XElement(ns + "ClCompile", new XAttribute("Include", unityFileName)));

            string destProjectFiltersFileName = _globalSettings.ModifyFileName(projectFiltersFileName);
            ModifiedTextFileWriter writer = new ModifiedTextFileWriter(destProjectFiltersFileName);
            if (writer.Write(projectFiltersDocument.ToString()))
            {
                _globalSettings.Output.WriteLine("Creating project filters file " + destProjectFiltersFileName);
            }
        }

        private static bool IsValidCppFileName(string fileName)
        {
            string extension = Path.GetExtension(fileName);
            return extension == ".cpp" || extension == ".cxx" || extension == ".c" || extension == ".cc";
        }

        private void ReadWritePreVS2010(string projectFileName, SolutionInfo solutionInfo, XDocument projectDocument, XNamespace ns)
        {
            var cppFileNameElements = 
                from cppFileElement in projectDocument.Descendants(ns + "File")
                where IsValidCppFileName(cppFileElement.Attribute("RelativePath").Value) && !cppFileElement.HasElements
                select cppFileElement;
            var cppFileNames = 
                from cppFileElement in cppFileNameElements
                select cppFileElement.Attribute("RelativePath").Value;

            ProjectInfo projectInfo = new ProjectInfo(solutionInfo, projectFileName, cppFileNames.ToList());

            UnityFileMerger merger = new UnityFileMerger(_globalSettings);
            merger.Process(projectInfo);

            cppFileNameElements.Remove();
            
            XElement filesElement = projectDocument.Descendants(ns + "Files").Single();
            filesElement.Add(
                from unityFileName in merger.UnityFilePaths
                select new XElement(ns + "File", new XAttribute("RelativePath", unityFileName)));
        }

        private void ReadWrite(string projectFileName, SolutionInfo solutionInfo)
        {
            XDocument projectDocument = XDocument.Load(projectFileName);
            if (null == projectDocument)
            {
                throw new InvalidDataException("Couldn't load project file '" + projectFileName + "'.");
            }

            XNamespace ns = projectDocument.Root.Name.Namespace;

            if (solutionInfo.Version == VisualStudioVersion.VS2010)
            {
                UnityFileMerger merger = ReadWriteVS2010(projectFileName, solutionInfo, projectDocument, ns);
                ReadWriteVS2010Filters(projectFileName, merger);
            }
            else
            {
                ReadWritePreVS2010(projectFileName, solutionInfo, projectDocument, ns);
            }

            string destProjectFileName = _globalSettings.ModifyFileName(projectFileName);
            ModifiedTextFileWriter writer = new ModifiedTextFileWriter(destProjectFileName);
            if (writer.Write(projectDocument.ToString()))
            {
                _globalSettings.Output.WriteLine("Creating project file " + destProjectFileName);
            }
        }
    }
}

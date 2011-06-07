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

        private XElement FindCompileItemGroupElement(XNamespace ns, XElement projectElement)
        {
            var compileItemGroupElement = from itemGroupElement in projectElement.Elements(ns + "ItemGroup")
                                          where itemGroupElement.Elements(ns + "ClCompile").Count() > 0
                                          select itemGroupElement;
            return compileItemGroupElement.SingleOrDefault();
        }

        private static XElement AddExcludedFromBuild(XNamespace ns, XElement element)
        {
            element.Add(new XElement(ns + "ExcludedFromBuild", "true"));
            return element;
        }

        private void ReadWrite(string projectFileName, SolutionInfo solutionInfo)
        {
            XDocument projectDocument = XDocument.Load(projectFileName);
            if (null == projectDocument)
            {
                throw new InvalidDataException("Couldn't load project file '" + projectFileName + "'.");
            }

            XNamespace ns = projectDocument.Root.Name.Namespace;
            XElement projectElement = projectDocument.Element(ns + "Project");
            if (null == projectElement)
            {
                throw new InvalidDataException("Project file '" + projectFileName + "' is corrupt. Couldn't find Project XML element.");
            }
            XElement compileItemGroupElement = FindCompileItemGroupElement(ns, projectElement);
            if (null == compileItemGroupElement)
            {
                throw new InvalidDataException("Project file '" + projectFileName + "' is corrupt. Couldn't find ItemGroup XML element with the source files to compile.");
            }

            var cppFileNames = from compileElement in compileItemGroupElement.Elements(ns + "ClCompile")
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

            string destProjectFileName = _globalSettings.ModifyFileName(projectFileName);
            ModifiedTextFileWriter writer = new ModifiedTextFileWriter(destProjectFileName);
            writer.Write(projectDocument.ToString());
        }
    }
}

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
            foreach (string projectFilename in solutionInfo.ProjectFilenames)
            {
                ReadWrite(projectFilename, solutionInfo);
            }
        }

        private XElement FindCompileItemGroupElement(XNamespace ns, XElement projectElement)
        {
            var compileItemGroupElement = from itemGroupElement in projectElement.Elements(ns + "ItemGroup")
                                          where itemGroupElement.Elements(ns + "ClCompile").Count() > 0
                                          select itemGroupElement;
            return compileItemGroupElement.SingleOrDefault();
        }

        private void ReadWrite(string projectFilename, SolutionInfo solutionInfo)
        {
            XDocument projectDocument = XDocument.Load(projectFilename);
            if (null == projectDocument)
            {
                throw new InvalidDataException("Couldn't load project file '" + projectFilename + "'.");
            }

            XNamespace ns = projectDocument.Root.Name.Namespace;
            XElement projectElement = projectDocument.Element(ns + "Project");
            if (null == projectElement)
            {
                throw new InvalidDataException("Project file '" + projectFilename + "' is corrupt. Couldn't find Project XML element.");
            }
            XElement compileItemGroupElement = FindCompileItemGroupElement(ns, projectElement);
            if (null == compileItemGroupElement)
            {
                throw new InvalidDataException("Project file '" + projectFilename + "' is corrupt. Couldn't find ItemGroup XML element with the source files to compile.");
            }

            var cppFilenames = from compileElement in compileItemGroupElement.Elements(ns + "ClCompile")
                               select compileElement.Attribute("Include").Value;

            ProjectInfo projectInfo = new ProjectInfo(projectFilename, cppFilenames.ToList());
            
            string destProjectFilename = _globalSettings.ModifyFilename(projectFilename);
            projectDocument.Save(destProjectFilename);
        }
    }
}

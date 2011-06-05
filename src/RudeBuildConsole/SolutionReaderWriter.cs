using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RudeBuildConsole
{
    public class SolutionReaderWriter
    {
        private GlobalSettings _globalSettings;

        public SolutionReaderWriter(GlobalSettings globalSettings)
        {
            _globalSettings = globalSettings;
        }

        private bool ParseVisualStudioVersion(string line, string solutionFormatVersionString, VisualStudioVersion versionToSet, ref VisualStudioVersion versionToChange)
        {
            if (line.StartsWith("Microsoft Visual Studio Solution File, Format Version " + solutionFormatVersionString))
            {
                if (versionToChange != VisualStudioVersion.VSUnknown)
                {
                    throw new InvalidDataException("Solution file is corrupt. It contains two lines declaring the solution file format version.");
                }
                versionToChange = versionToSet;
                return true;
            }
            return false;
        }

        private string ParseCppProject(ref string line, VisualStudioVersion version)
        {
            if (!line.StartsWith("Project(\"{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}\")"))        // Guid for C++ projects
            {
                return null;
            }
            if (version == VisualStudioVersion.VSUnknown)
            {
                throw new InvalidDataException("Solution file is corrupt. Found C++ project section before solution file format information.");
            }

            string extension = version == VisualStudioVersion.VS2010 ? ".vcxproj" : ".vcproj";
            int extensionIndex = line.IndexOf(extension);
            if (extensionIndex <= 0)
            {
                throw new InvalidDataException("Solution file is corrupt. Found C++ project without project filename.");
            }
            int projectFilenameIndex = line.LastIndexOf('"', extensionIndex) + 1;
            if (projectFilenameIndex <= 0)
            {
                throw new InvalidDataException("Solution file is corrupt. Found C++ project without project filename.");
            }

            string projectFilename = line.Substring(projectFilenameIndex, extensionIndex - projectFilenameIndex) + extension;
            string destProjectFilename = _globalSettings.ModifyFilename(projectFilename);
            line = line.Substring(0, projectFilenameIndex) + destProjectFilename + line.Substring(extensionIndex + extension.Length);
            return projectFilename;
        }

        public SolutionInfo ReadWrite(string srcFilename)
        {
            VisualStudioVersion version = VisualStudioVersion.VSUnknown;
            List<string> projectFilenames = new List<string>();
            StringBuilder destSolutionText = new StringBuilder();

            using (StreamReader reader = new StreamReader(srcFilename))
            {
                string line;
                bool isInSourceControlSection = false;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!ParseVisualStudioVersion(line, "9.00", VisualStudioVersion.VS2005, ref version) &&
                        !ParseVisualStudioVersion(line, "10.00", VisualStudioVersion.VS2008, ref version) &&
                        !ParseVisualStudioVersion(line, "11.00", VisualStudioVersion.VS2010, ref version))
                    {
                        string projectFilename = ParseCppProject(ref line, version);
                        if (null != projectFilename)
                        {
                            if (projectFilenames.Contains(projectFilename))
                            {
                                throw new InvalidDataException("Solution file is corrupt. It contains two sections for project '" + projectFilename + "'.");
                            }
                            projectFilenames.Add(projectFilename);
                        }
                        else if (line.Contains("GlobalSection(SourceCodeControl)"))
                        {
                            if (isInSourceControlSection)
                            {
                                throw new InvalidDataException("Solution file is corrupt. Found a source code control section inside another source code control section.");
                            }
                            isInSourceControlSection = true;
                        }
                        else if (isInSourceControlSection)
                        {
                            if (line.Contains("EndGlobalSection"))
                            {
                                isInSourceControlSection = false;
                            }
                            line = null;
                        }
                    }

                    if (line != null)
                    {
                        destSolutionText.AppendLine(line);
                    }
                }
            }

            if (version == VisualStudioVersion.VSUnknown)
            {
                throw new InvalidDataException("Solution file '" + srcFilename + "' is corrupt. It does not contain a Visual Studio version.");
            }

            string destFilename = _globalSettings.ModifyFilename(srcFilename);
            ModifiedTextFileWriter writer = new ModifiedTextFileWriter(destFilename);
            writer.Write(destSolutionText.ToString());

            return new SolutionInfo(version, projectFilenames);
        }
    }
}

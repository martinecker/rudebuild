using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RudeBuild
{
    public class SolutionReaderWriter
    {
        private Settings _settings;

        public SolutionReaderWriter(Settings settings)
        {
            _settings = settings;
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
                throw new InvalidDataException("Solution file is corrupt. Found C++ project without project fileName.");
            }
            int projectFileNameIndex = line.LastIndexOf('"', extensionIndex) + 1;
            if (projectFileNameIndex <= 0)
            {
                throw new InvalidDataException("Solution file is corrupt. Found C++ project without project fileName.");
            }

            string projectFileName = line.Substring(projectFileNameIndex, extensionIndex - projectFileNameIndex) + extension;
            string destProjectFileName = _settings.GlobalSettings.ModifyFileName(projectFileName);
            line = line.Substring(0, projectFileNameIndex) + destProjectFileName + line.Substring(extensionIndex + extension.Length);
            return projectFileName;
        }

        public SolutionInfo ReadWrite(string srcFileName)
        {
            VisualStudioVersion version = VisualStudioVersion.VSUnknown;
            List<string> projectFileNames = new List<string>();
            StringBuilder destSolutionText = new StringBuilder();
            string solutionDirectory = Path.GetDirectoryName(srcFileName);

            using (StreamReader reader = new StreamReader(srcFileName))
            {
                string line;
                bool isInSourceControlSection = false;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line == string.Empty)
                    {
                        line = null;
                    }
                    else if (!ParseVisualStudioVersion(line, "9.00", VisualStudioVersion.VS2005, ref version) &&
                             !ParseVisualStudioVersion(line, "10.00", VisualStudioVersion.VS2008, ref version) &&
                             !ParseVisualStudioVersion(line, "11.00", VisualStudioVersion.VS2010, ref version))
                    {
                        string projectFileName = ParseCppProject(ref line, version);
                        if (null != projectFileName)
                        {
                            projectFileName = Path.Combine(solutionDirectory, projectFileName);
                            if (projectFileNames.Contains(projectFileName))
                            {
                                throw new InvalidDataException("Solution file is corrupt. It contains two sections for project '" + projectFileName + "'.");
                            }
                            projectFileNames.Add(projectFileName);
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
                throw new InvalidDataException("Solution file '" + srcFileName + "' is corrupt. It does not contain a Visual Studio version.");
            }

            string destFileName = _settings.GlobalSettings.ModifyFileName(srcFileName);
            ModifiedTextFileWriter writer = new ModifiedTextFileWriter(destFileName, _settings.BuildOptions.ShouldForceWriteCachedFiles());
            if (writer.Write(destSolutionText.ToString()))
            {
                _settings.Output.WriteLine("Creating solution file " + destFileName);
            }

            return new SolutionInfo(srcFileName, version, projectFileNames);
        }
    }
}

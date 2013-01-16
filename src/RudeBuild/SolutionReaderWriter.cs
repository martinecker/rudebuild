using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RudeBuild
{
    public class SolutionReaderWriter
    {
        private readonly Settings _settings;

        public SolutionReaderWriter(Settings settings)
        {
            _settings = settings;
        }

        private static bool ParseVisualStudioVersion(string line, string solutionFormatVersionString, VisualStudioVersion versionToSet, ref VisualStudioVersion versionToChange)
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

        private bool ParseCppProject(ref string line, VisualStudioVersion version, string solutionDirectory, SolutionConfigManager configManager)
        {
            if (!line.StartsWith("Project(\"{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}\")"))        // Guid for C++ projects
            {
                return false;
            }
            if (version == VisualStudioVersion.VSUnknown)
            {
                throw new InvalidDataException("Solution file is corrupt. Found C++ project section before solution file format information.");
            }

            string extension = version >= VisualStudioVersion.VS2010 ? ".vcxproj" : ".vcproj";
            int extensionIndex = line.IndexOf(extension, System.StringComparison.Ordinal);
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
            const string guidPlaceholder = "{00000000-0000-0000-0000-000000000000}";
            string projectGuid = line.Substring(extensionIndex + extension.Length + "\", \"".Length, guidPlaceholder.Length);
            if (projectGuid.Length != guidPlaceholder.Length)
            {
                throw new InvalidDataException("Solution file is corrupt. Couldn't correctly parse C++ project GUID.");
            }

            string destProjectFileName = _settings.ModifyFileName(projectFileName);
            line = line.Substring(0, projectFileNameIndex) + destProjectFileName + line.Substring(extensionIndex + extension.Length);

            configManager.AddProject(Path.Combine(solutionDirectory, projectFileName), projectGuid);

            return true;
        }

        public SolutionInfo Read(string fileName)
        {
            var version = VisualStudioVersion.VSUnknown;
            var configManager = new SolutionConfigManager();
            var destSolutionText = new StringBuilder();
            string solutionDirectory = Path.GetDirectoryName(fileName);
            
            string currentGlobalSection = string.Empty;
            const string globalSectionStart = "GlobalSection(";
            const string globalSectionEnd = "EndGlobalSection";

            using (var reader = new StreamReader(fileName))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrEmpty(line))
                    {
                        line = null;
                    }
                    else if (ParseVisualStudioVersion(line, "9.00", VisualStudioVersion.VS2005, ref version) ||
                             ParseVisualStudioVersion(line, "10.00", VisualStudioVersion.VS2008, ref version) ||
                             ParseVisualStudioVersion(line, "11.00", VisualStudioVersion.VS2010, ref version) ||
                             ParseVisualStudioVersion(line, "12.00", VisualStudioVersion.VS2012, ref version))
                    {
                    }
                    else if (ParseCppProject(ref line, version, solutionDirectory, configManager))
                    {
                    }
                    else if (line.Contains(globalSectionStart))
                    {
                        if (!string.IsNullOrEmpty(currentGlobalSection))
                            throw new InvalidDataException("Solution file is corrupt. Found a source code control section inside another source code control section " + currentGlobalSection);

                        int globalSectionNameStartIndex = line.IndexOf(globalSectionStart, StringComparison.Ordinal);
                        globalSectionNameStartIndex += globalSectionStart.Length;
                        int globalSectionNameEndIndex = line.IndexOf(')', globalSectionNameStartIndex);
                        if (globalSectionNameEndIndex <= 0)
                            throw new InvalidDataException("Solution file is corrupt. Couldn't find global section name end in line: " + line);
                        currentGlobalSection = line.Substring(globalSectionNameStartIndex, globalSectionNameEndIndex - globalSectionNameStartIndex);
                    }
                    else if (!string.IsNullOrEmpty(currentGlobalSection))
                    {
                        if (line.Contains(globalSectionEnd))
                            currentGlobalSection = string.Empty;
                        else if (currentGlobalSection == "SourceCodeControl")       // Remove anything inside a SourceCodeControl section in the RudeBuild-generated solution file.
                            line = null;
                        else if (currentGlobalSection == "SolutionConfigurationPlatforms")
                        {
                            string solutionConfig = line.Trim();
                            int index = solutionConfig.IndexOf(" = ", StringComparison.Ordinal);
                            if (index > 0)
                            {
                                solutionConfig = solutionConfig.Substring(0, index);
                                configManager.AddSolutionConfig(solutionConfig);
                            }
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
                throw new InvalidDataException("Solution file '" + fileName + "' is corrupt. It does not contain a Visual Studio version.");
            }

            return new SolutionInfo(fileName, version, configManager, destSolutionText.ToString());
        }

        public void Write(SolutionInfo solutionInfo)
        {
            string destFileName = _settings.ModifyFileName(solutionInfo.FilePath);
            var writer = new ModifiedTextFileWriter(destFileName, _settings.BuildOptions.ShouldForceWriteCachedFiles());
            if (writer.Write(solutionInfo.Contents))
            {
                _settings.Output.WriteLine("Creating solution file " + destFileName);
            }
        }

        public SolutionInfo ReadWrite(string fileName)
        {
            SolutionInfo solutionInfo = Read(fileName);
            Write(solutionInfo);
            return solutionInfo;
        }
    }
}

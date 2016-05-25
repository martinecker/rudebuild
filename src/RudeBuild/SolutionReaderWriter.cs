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

        private static bool ParseSolutionFormatVersion(string line, string solutionFormatVersionString, VisualStudioVersion versionToSet, ref VisualStudioVersion versionToChange)
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

        private static bool ParseVisualStudioVersion(string line, string visualStudioVersionString, VisualStudioVersion versionToSet, ref VisualStudioVersion versionToChange)
        {
            if (line.StartsWith("VisualStudioVersion = " + visualStudioVersionString))
            {
                if (versionToChange != VisualStudioVersion.VSUnknown)
                {
                    throw new InvalidDataException("Solution file is corrupt. It contains two lines declaring the Visual Studio version.");
                }
                versionToChange = versionToSet;
                return true;
            }
            return false;
        }

        private const string GuidPlaceholder = "{00000000-0000-0000-0000-000000000000}";
        private const string GlobalSectionStart = "GlobalSection(";
        private const string GlobalSectionEnd = "EndGlobalSection";

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
            string projectGuid = line.Substring(extensionIndex + extension.Length + "\", \"".Length, GuidPlaceholder.Length);
            if (projectGuid.Length != GuidPlaceholder.Length)
            {
                throw new InvalidDataException("Solution file is corrupt. Couldn't correctly parse C++ project GUID.");
            }

            string destProjectFileName = _settings.ModifyFileName(projectFileName);
            line = line.Substring(0, projectFileNameIndex) + destProjectFileName + line.Substring(extensionIndex + extension.Length);

            configManager.AddProject(Path.GetFullPath(Path.Combine(solutionDirectory, projectFileName)), projectGuid);

            return true;
        }

        private static bool ParseGlobalSectionStart(string line, ref string currentGlobalSection)
        {
            if (!line.Contains(GlobalSectionStart))
                return false;

            if (!string.IsNullOrEmpty(currentGlobalSection))
                throw new InvalidDataException("Solution file is corrupt. Found a source code control section inside another source code control section " + currentGlobalSection);

            int globalSectionNameStartIndex = line.IndexOf(GlobalSectionStart, StringComparison.Ordinal);
            globalSectionNameStartIndex += GlobalSectionStart.Length;
            int globalSectionNameEndIndex = line.IndexOf(')', globalSectionNameStartIndex);
            if (globalSectionNameEndIndex <= 0)
                throw new InvalidDataException("Solution file is corrupt. Couldn't find global section name end in line: " + line);
            currentGlobalSection = line.Substring(globalSectionNameStartIndex, globalSectionNameEndIndex - globalSectionNameStartIndex);

            return true;
        }

        private static bool ParseGlobalSectionEnd(string line, ref string currentGlobalSection)
        {
            if (!line.Contains(GlobalSectionEnd))
                return false;
            if (string.IsNullOrEmpty(currentGlobalSection))
                throw new InvalidDataException("Solution file is corrupt. Found EndGlobalSection even though no global section has been started.");
            currentGlobalSection = string.Empty;
            return true;
        }

        public SolutionInfo Read(string fileName)
        {
            var solutionFormatVersion = VisualStudioVersion.VSUnknown;
            var visualStudioVersion = VisualStudioVersion.VSUnknown;
            var version = VisualStudioVersion.VSUnknown;
            var configManager = new SolutionConfigManager();
            var destSolutionText = new StringBuilder();
            string solutionDirectory = Path.GetDirectoryName(fileName);
            
            string currentGlobalSection = string.Empty;

            using (var reader = new StreamReader(fileName))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrEmpty(line))
                    {
                        line = null;
                    }
                    else if (ParseSolutionFormatVersion(line, "9.00", VisualStudioVersion.VS2005, ref solutionFormatVersion) ||
                             ParseSolutionFormatVersion(line, "10.00", VisualStudioVersion.VS2008, ref solutionFormatVersion) ||
                             ParseSolutionFormatVersion(line, "11.00", VisualStudioVersion.VS2010, ref solutionFormatVersion) ||
                             ParseSolutionFormatVersion(line, "12.00", VisualStudioVersion.VS2012, ref solutionFormatVersion))  // Note that VS 2013 and 2015 retain solution formation version 12.00!
                    {
                        version = solutionFormatVersion;
                    }
                    else if (ParseVisualStudioVersion(line, "8.0", VisualStudioVersion.VS2005, ref visualStudioVersion) ||
                             ParseVisualStudioVersion(line, "9.0", VisualStudioVersion.VS2008, ref visualStudioVersion) ||
                             ParseVisualStudioVersion(line, "10.0", VisualStudioVersion.VS2010, ref visualStudioVersion) ||
                             ParseVisualStudioVersion(line, "11.0", VisualStudioVersion.VS2012, ref visualStudioVersion) ||
                             ParseVisualStudioVersion(line, "12.0", VisualStudioVersion.VS2013, ref visualStudioVersion) ||
                             ParseVisualStudioVersion(line, "14.0", VisualStudioVersion.VS2015, ref visualStudioVersion))
                    {
                        version = visualStudioVersion;
                    }
                    else if (ParseCppProject(ref line, version, solutionDirectory, configManager))
                    {
                    }
                    else if (ParseGlobalSectionStart(line, ref currentGlobalSection))
                    {
                    }
                    else if (ParseGlobalSectionEnd(line, ref currentGlobalSection)) 
                    {
                    }
                    else if (!string.IsNullOrEmpty(currentGlobalSection))
                    {
                        if (currentGlobalSection == "SourceCodeControl")
                        {
                            // Remove anything inside a SourceCodeControl section in the RudeBuild-generated solution file.
                            line = null;
                        }
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
                        else if (currentGlobalSection == "ProjectConfigurationPlatforms")
                        {
                            string trimmedLine = line.Trim();
                            string projectGuid = trimmedLine.Substring(0, GuidPlaceholder.Length);
                            if (projectGuid.Length != GuidPlaceholder.Length)
                                throw new InvalidDataException("Solution file is corrupt. Expected project GUID at beginning of line: " + trimmedLine);

                            int solutionConfigStartIndex = GuidPlaceholder.Length + 1;
                            int solutionConfigEndIndex = trimmedLine.IndexOf('.', solutionConfigStartIndex);
                            if (solutionConfigEndIndex <= 0)
                                throw new InvalidDataException("Solution file is corrupt. Expected solution config after project GUID in line: " + trimmedLine);
                            string solutionConfig = trimmedLine.Substring(solutionConfigStartIndex, solutionConfigEndIndex - solutionConfigStartIndex);

                            int projectConfigStartIndex = trimmedLine.IndexOf(" = ", solutionConfigEndIndex, StringComparison.Ordinal);
                            if (projectConfigStartIndex <= 0)
                                throw new InvalidDataException("Solution file is corrupt. Expected project config after = in line: " + trimmedLine);
                            string projectConfig = trimmedLine.Substring(projectConfigStartIndex + 3);

                            string activeCfg = trimmedLine.Substring(solutionConfigEndIndex + 1, projectConfigStartIndex - solutionConfigEndIndex - 1);
                            if (activeCfg == "ActiveCfg")
                                configManager.SetProjectConfig(projectGuid, solutionConfig, projectConfig);
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

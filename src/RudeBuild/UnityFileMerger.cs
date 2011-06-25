using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Security.Cryptography;

namespace RudeBuild
{
    public class UnityFileMerger
    {
        private Settings _settings;
        private string _cachePath;
        private IList<string> _unityFilePaths;
        public IList<string> UnityFilePaths
        {
            get { return _unityFilePaths; }
        }

        public UnityFileMerger(Settings settings)
        {
            _settings = settings;
        }

        private static string GetMD5Hash(string input)
        {
            MD5 md5Hasher = MD5.Create();

            // Convert the input string to a byte array and compute the hash.
            byte[] data = md5Hasher.ComputeHash(Encoding.Default.GetBytes(input));

            // Convert to a 32 character hexadecimal output string.
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < data.Length; i++)
            {
                result.Append(data[i].ToString("x2"));
            }
            return result.ToString();
        }

        private void CreateCachePath(ProjectInfo projectInfo)
        {
            string solutionDirectory = projectInfo.Solution.Name + "_" + GetMD5Hash(projectInfo.Solution.FilePath);
            string config = _settings.RunOptions.Config.Replace('|', '-');

            _cachePath = Path.Combine(_settings.GlobalSettings.CachePath, solutionDirectory);
            _cachePath = Path.Combine(_cachePath, config);
            Directory.CreateDirectory(_cachePath);
        }

        private void WritePrefix(ProjectInfo projectInfo, StringBuilder text)
        {
            if (!_settings.RunOptions.DisablePrecompiledHeaders && !string.IsNullOrEmpty(projectInfo.PrecompiledHeaderFileName))
            {
                text.AppendLine("#include \"" + projectInfo.PrecompiledHeaderFileName + "\"");
                text.AppendLine();
            }
            text.AppendLine("#ifdef _MSC_VER");
            text.AppendLine("#define RUDE_BUILD_SUPPORTS_PRAGMA_MESSAGE");
            text.AppendLine("#endif");
            text.AppendLine();
        }

        private void WritePostfix(StringBuilder text)
        {
            text.AppendLine();
            text.AppendLine("#ifdef RUDE_BUILD_SUPPORTS_PRAGMA_MESSAGE");
            text.AppendLine("#undef RUDE_BUILD_SUPPORTS_PRAGMA_MESSAGE");
            text.AppendLine("#endif");
        }

        private void WriteUnityFile(ProjectInfo projectInfo, StringBuilder text, int fileIndex)
        {
            WritePostfix(text);

            string destFileName = Path.Combine(_cachePath, projectInfo.Name + fileIndex + ".cpp");
            ModifiedTextFileWriter writer = new ModifiedTextFileWriter(destFileName, _settings.RunOptions.ShouldForceWriteCachedFiles());
            if (writer.Write(text.ToString()))
            {
                _settings.Output.WriteLine("Creating unity file " + projectInfo.Name + fileIndex);
            }

            _unityFilePaths.Add(destFileName);
        }

        public void Process(ProjectInfo projectInfo)
        {
            CreateCachePath(projectInfo);

            _unityFilePaths = new List<string>();

            StringBuilder currentUnityFileContents = new StringBuilder();
            WritePrefix(projectInfo, currentUnityFileContents);

            int currentUnityFileIndex = 1;
            long currentUnityFileSize = 0;

            foreach (string cppFileName in projectInfo.CppFileNames)
            {
                string cppFilePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectInfo.FileName), cppFileName));
                if (!File.Exists(cppFilePath))
                {
                    _settings.Output.WriteLine("Input file '" + cppFileName + "' does not exist. Skipping.");
                    continue;
                }

                FileInfo fileInfo = new FileInfo(cppFilePath);
                currentUnityFileSize += fileInfo.Length;
                if (currentUnityFileSize > _settings.GlobalSettings.MaxUnityFileSizeInBytes)
                {
                    WriteUnityFile(projectInfo, currentUnityFileContents, currentUnityFileIndex);

                    currentUnityFileSize = 0;
                    currentUnityFileContents = new StringBuilder();
                    WritePrefix(projectInfo, currentUnityFileContents);
                    ++currentUnityFileIndex;
                }

                currentUnityFileContents.AppendLine("#ifdef RUDE_BUILD_SUPPORTS_PRAGMA_MESSAGE");
                currentUnityFileContents.AppendLine("#pragma message(\"" + Path.GetFileName(cppFileName) + "\")");
                currentUnityFileContents.AppendLine("#endif");
                currentUnityFileContents.AppendLine("#include \"" + cppFilePath + "\"");
            }

            if (currentUnityFileSize > 0)
            {
                WriteUnityFile(projectInfo, currentUnityFileContents, currentUnityFileIndex);
            }
        }
    }
}

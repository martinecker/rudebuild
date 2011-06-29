using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace RudeBuild
{
    public static class ListShuffleExtension
    {
        private static Random random = new System.Random();

        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            for (int i = 0; i < n; ++i)
            {
                int r = i + (int)(random.Next(n - i));
                T t = list[r];
                list[r] = list[i];
                list[i] = t;
            }
        }
    }

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

        private void CreateCachePath(SolutionInfo solutionInfo)
        {
            _cachePath = _settings.GetCachePath(solutionInfo);
            Directory.CreateDirectory(_cachePath);
        }

        private void WritePrefix(ProjectInfo projectInfo, StringBuilder text)
        {
            if (!_settings.BuildOptions.DisablePrecompiledHeaders && !string.IsNullOrEmpty(projectInfo.PrecompiledHeaderFileName))
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
            ModifiedTextFileWriter writer = new ModifiedTextFileWriter(destFileName, _settings.BuildOptions.ShouldForceWriteCachedFiles());
            if (writer.Write(text.ToString()))
            {
                _settings.Output.WriteLine("Creating unity file " + projectInfo.Name + fileIndex);
            }

            _unityFilePaths.Add(destFileName);
        }

        public void Process(ProjectInfo projectInfo)
        {
            CreateCachePath(projectInfo.Solution);

            _unityFilePaths = new List<string>();

            StringBuilder currentUnityFileContents = new StringBuilder();
            WritePrefix(projectInfo, currentUnityFileContents);

            int currentUnityFileIndex = 1;
            long currentUnityFileSize = 0;

            IList<string> cppFileNames = projectInfo.CppFileNames;
            if (_settings.GlobalSettings.RandomizeOrderOfUnityMergedFiles)
            {
                cppFileNames.Shuffle();
            }

            foreach (string cppFileName in cppFileNames)
            {
                string cppFilePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectInfo.FileName), cppFileName));
                if (!File.Exists(cppFilePath))
                {
                    _settings.Output.WriteLine("Input file '" + cppFilePath + "' does not exist. Skipping.");
                    continue;
                }

                FileInfo fileInfo = new FileInfo(cppFilePath);
                if (_settings.GlobalSettings.ExcludeWritableFilesFromUnityMerge && !fileInfo.IsReadOnly)
                    continue;

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

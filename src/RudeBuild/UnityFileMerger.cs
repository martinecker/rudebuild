using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace RudeBuild
{
    public static class ListShuffleExtension
    {
        private static readonly Random random = new Random();

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
        private readonly Settings _settings;
        private string _cachePath;

        public IList<string> UnityFilePaths { get; private set; }
        public IList<string> MergedCppFileNames { get; private set; }

		private class UnityFile
		{
			public readonly StringBuilder Contents = new StringBuilder();
			public long TotalMergedSizeInBytes = 0;
		}
		private Dictionary<string, UnityFile> _unityFiles;

        public UnityFileMerger(Settings settings)
        {
            _settings = settings;
        }

        private void CreateCachePath(ProjectInfo projectInfo)
        {
            _cachePath = Path.Combine(_settings.GetCachePath(projectInfo.Solution), projectInfo.Name);
            Directory.CreateDirectory(_cachePath);
        }

        private void WritePrefix(ProjectInfo projectInfo, StringBuilder text)
        {
            text.AppendLine("// This file is autogenerated by RudeBuild. Do not modify.");
            text.AppendLine();
            if (!string.IsNullOrEmpty(projectInfo.PrecompiledHeaderName))
            {
                text.AppendLine("#include \"" + projectInfo.PrecompiledHeaderName + "\"");
                text.AppendLine();
            }
            text.AppendLine("#define RUDE_BUILD_UNITY_BUILD");
            text.AppendLine("#ifdef __GNUC__");
            text.AppendLine("  #if __GNUC__ >= 4 && __GNUC_MINOR__ >= 4");
            text.AppendLine("  #define RUDE_BUILD_SUPPORTS_PRAGMA_MESSAGE");
            text.AppendLine("  #endif");
            text.AppendLine("#endif");
            text.AppendLine("#ifdef _MSC_VER");
            text.AppendLine("  #define RUDE_BUILD_SUPPORTS_PRAGMA_MESSAGE");
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

        private void WriteUnityFile(ProjectInfo projectInfo, StringBuilder text, int fileIndex, string fileExtension)
        {
            WritePostfix(text);

			string destFileName = Path.Combine(_cachePath, projectInfo.Name + fileIndex + fileExtension);
            var writer = new ModifiedTextFileWriter(destFileName, _settings.BuildOptions.ShouldForceWriteCachedFiles());
            if (writer.Write(text.ToString()))
            {
                _settings.Output.WriteLine("Creating unity file " + projectInfo.Name + fileIndex);
            }

            UnityFilePaths.Add(destFileName);
        }

        private void WriteEmptyPrecompiledHeader(ProjectInfo projectInfo)
        {
            // For precompiled headers to work for projects that have .cpp files in multiple
            // folders, we need to write out a precompiled header file that includes the original precompiled header.
            if (string.IsNullOrEmpty(projectInfo.PrecompiledHeaderName) || string.IsNullOrEmpty(projectInfo.PrecompiledHeaderFileName))
                return;

            string precompiledHeaderFilePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectInfo.FileName), projectInfo.PrecompiledHeaderFileName));
            if (!File.Exists(precompiledHeaderFilePath))
            {
                _settings.Output.WriteLine("Precompiled header file '" + precompiledHeaderFilePath + "' does not exist.");
                return;
            }

            string destFileName = Path.Combine(_cachePath, projectInfo.PrecompiledHeaderName);

            var text = new StringBuilder();
            text.AppendLine("// This file is autogenerated by RudeBuild. Do not modify.");
            text.AppendLine();
            text.AppendLine("#include \"" + precompiledHeaderFilePath + "\"");

            var writer = new ModifiedTextFileWriter(destFileName, _settings.BuildOptions.ShouldForceWriteCachedFiles());
            writer.Write(text.ToString());
        }

        public void Process(ProjectInfo projectInfo)
        {
            CreateCachePath(projectInfo);

            UnityFilePaths = new List<string>();
            MergedCppFileNames = new List<string>();

            IList<string> cppFileNames = projectInfo.MergableCppFileNames;
            if (_settings.GlobalSettings.RandomizeOrderOfUnityMergedFiles)
            {
                cppFileNames.Shuffle();
            }

			_unityFiles = new Dictionary<string, UnityFile>();

			int currentUnityFileIndex = 1;
			foreach (string cppFileName in cppFileNames)
            {
                string cppFilePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectInfo.FileName), cppFileName));
                if (!File.Exists(cppFilePath))
                {
                    _settings.Output.WriteLine("Input file '" + cppFilePath + "' does not exist. Skipping.");
                    continue;
                }

                var fileInfo = new FileInfo(cppFilePath);
                if (_settings.GlobalSettings.ExcludeWritableFilesFromUnityMerge && !fileInfo.IsReadOnly)
                    continue;
                if (_settings.SolutionSettings.IsExcludedCppFileNameForProject(projectInfo, cppFileName))
                    continue;

				// Get the Unity file for the file extension. We separate .c and .cpp files because this is what makes
				// Visual Studio choose if it should do a C-only or a C++ compile. There are subtle compiler differences.
				string fileExtension = Path.GetExtension(cppFileName);
				UnityFile unityFile;
				if (!_unityFiles.TryGetValue(fileExtension, out unityFile))
				{
					unityFile = new UnityFile();
					_unityFiles.Add(fileExtension, unityFile);
					WritePrefix(projectInfo, unityFile.Contents);
				}

				if (unityFile.TotalMergedSizeInBytes > _settings.GlobalSettings.MaxUnityFileSizeInBytes)
                {
					WriteUnityFile(projectInfo, unityFile.Contents, currentUnityFileIndex, fileExtension);
					++currentUnityFileIndex;

					unityFile = new UnityFile();
					_unityFiles[fileExtension] = unityFile;
					WritePrefix(projectInfo, unityFile.Contents);
                }

				unityFile.TotalMergedSizeInBytes += fileInfo.Length;
				unityFile.Contents.AppendLine("#ifdef RUDE_BUILD_SUPPORTS_PRAGMA_MESSAGE");
				unityFile.Contents.AppendLine("#pragma message(\"" + Path.GetFileName(cppFileName) + "\")");
				unityFile.Contents.AppendLine("#endif");
				unityFile.Contents.AppendLine("#include \"" + cppFilePath + "\"");

                MergedCppFileNames.Add(cppFileName);
            }

			foreach (var keyValue in _unityFiles)
			{
				string fileExtension = keyValue.Key;
				UnityFile unityFile = keyValue.Value;
				if (unityFile.Contents.Length > 0)
				{
					WriteUnityFile(projectInfo, unityFile.Contents, currentUnityFileIndex, fileExtension);
					++currentUnityFileIndex;
				}
			}

            WriteEmptyPrecompiledHeader(projectInfo);
        }
    }
}

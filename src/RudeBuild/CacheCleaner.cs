using System.IO;
using System.Collections.Generic;

namespace RudeBuild
{
    public class CacheCleaner
    {
        private static void SafeDeleteDirectory(string path)
        {
            try
            {
                Directory.Delete(path, true);
            }
            catch
            {
                // ignore any errors
            }
        }

        private static void SafeDeleteFile(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // ignore any errors
            }
        }

        private static void SafeDeleteFileWithWildcards(string path)
        {
            string fileNameWithWildcards = Path.GetFileName(path);
            if (fileNameWithWildcards.IndexOf("*") == -1 && fileNameWithWildcards.IndexOf("?") == -1)
            {
                SafeDeleteFile(path);
                return;
            }

            string directory = Path.GetDirectoryName(path);
            string[] files = Directory.GetFiles(directory, fileNameWithWildcards);
            foreach (string file in files)
            {
                SafeDeleteFile(file);
            }
        }

        private static void DeleteCachedUnityFiles(Settings settings, SolutionInfo solutionInfo)
        {
            string cachePath = settings.GetCachePath(solutionInfo);
            if (Directory.Exists(cachePath))
            {
                SafeDeleteDirectory(cachePath);
            }
        }

        private static void DeleteCachedProjectFiles(Settings settings, SolutionInfo solutionInfo)
        {
            IList<string> filesToDelete = new List<string>();

            string solutionFilePath = settings.ModifyFileName(solutionInfo.FilePath);
            filesToDelete.Add(solutionFilePath);

            solutionFilePath = Path.Combine(Path.GetDirectoryName(solutionFilePath), Path.GetFileNameWithoutExtension(solutionFilePath));
            filesToDelete.Add(solutionFilePath + ".suo");
            filesToDelete.Add(solutionFilePath + ".sdf");
            filesToDelete.Add(solutionFilePath + ".ncb");

            foreach (string projectFileName in solutionInfo.ProjectFileNames)
            {
                string projectFilePath = settings.ModifyFileName(projectFileName);
                filesToDelete.Add(projectFilePath);
                projectFilePath = Path.Combine(Path.GetDirectoryName(projectFilePath), Path.GetFileNameWithoutExtension(projectFilePath));
                filesToDelete.Add(projectFilePath + ".vcxproj.*");
                filesToDelete.Add(projectFilePath + ".vcproj.*");
            }

            foreach (string path in filesToDelete)
            {
                SafeDeleteFileWithWildcards(path);
            }
        }

        public static void Run(Settings settings)
        {
            SolutionReaderWriter solutionReaderWriter = new SolutionReaderWriter(settings);
            SolutionInfo solutionInfo = solutionReaderWriter.Read(settings.BuildOptions.Solution.FullName);
            DeleteCachedUnityFiles(settings, solutionInfo);
            DeleteCachedProjectFiles(settings, solutionInfo);
        }
    }
}

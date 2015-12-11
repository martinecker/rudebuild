using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace RudeBuild
{
	internal static class PathHelpers
	{
		private const int FILE_ATTRIBUTE_DIRECTORY = 0x10;
		private const int FILE_ATTRIBUTE_NORMAL = 0x80;
		private const int MAX_PATH = 260;

		[DllImport("shlwapi.dll", SetLastError = true)]
		private static extern int PathRelativePathTo(StringBuilder pszPath, string pszFrom, int dwAttrFrom, string pszTo, int dwAttrTo);

		public static string GetRelativePath(string fromPath, string toPath)
		{
			int fromAttr = GetPathAttribute(fromPath);
			int toAttr = GetPathAttribute(toPath);

			StringBuilder path = new StringBuilder(MAX_PATH);
			if (PathRelativePathTo(path, fromPath, fromAttr, toPath, toAttr) == 0)
			{
				throw new ArgumentException("Paths must have a common prefix");
			}
			return path.ToString();
		}

		private static int GetPathAttribute(string path)
		{
			DirectoryInfo di = new DirectoryInfo(path);
			if (di.Exists)
			{
				return FILE_ATTRIBUTE_DIRECTORY;
			}

			FileInfo fi = new FileInfo(path);
			if (fi.Exists)
			{
				return FILE_ATTRIBUTE_NORMAL;
			}

			throw new FileNotFoundException();
		}
	}

	public class ProjectInfo
    {
        public SolutionInfo Solution { get; private set; }
        public string FileName { get; private set; }
        public string Name { get; private set; }
        public IList<string> MergableCppFileNames { get; private set; }
        public IList<string> AllCppFileNames { get; private set; }
        public IList<string> IncludeFileNames { get; private set; }
        public string PrecompiledHeaderName { get; private set; }
        public string PrecompiledHeaderProjectRelativePath { get; private set; }
        public string PrecompiledHeaderAbsolutePath { get; private set; }

        public ProjectInfo(SolutionInfo solution, string name, string fileName, IList<string> mergableCppFileNames, IList<string> allCppFileNames, IList<string> allIncludeFileNames, string precompiledHeaderName)
        {
            Solution = solution;
            FileName = fileName;
            MergableCppFileNames = mergableCppFileNames;
            AllCppFileNames = allCppFileNames;
            IncludeFileNames = allIncludeFileNames;
            Name = name;
            SetupPrecompiledHeader(precompiledHeaderName);
        }

        private static string GetPrecompiledHeaderProjectRelativePath(string precompiledHeaderName, IEnumerable<string> includeFileNames)
        {
            var result = from includeFileName in includeFileNames
                         let nameWithoutPath = Path.GetFileName(includeFileName)
                         where string.Compare(includeFileName, precompiledHeaderName, StringComparison.OrdinalIgnoreCase) == 0 ||
                            (!string.IsNullOrEmpty(nameWithoutPath) && string.Compare(nameWithoutPath, precompiledHeaderName, StringComparison.OrdinalIgnoreCase) == 0)
                         select includeFileName;
            string resultRelativePath = result.SingleOrDefault();
            if (string.IsNullOrEmpty(resultRelativePath))
                resultRelativePath = precompiledHeaderName;
            return resultRelativePath;
        }

        private void SetupPrecompiledHeader(string precompiledHeaderName)
        {
            precompiledHeaderName = ExpandMacros(precompiledHeaderName);
            string precompiledHeaderProjectRelativePath = ExpandMacros(GetPrecompiledHeaderProjectRelativePath(precompiledHeaderName, IncludeFileNames));

            if (string.IsNullOrEmpty(precompiledHeaderName) || string.IsNullOrEmpty(precompiledHeaderProjectRelativePath))
                return;

            string precompiledHeaderAbsolutePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(FileName), precompiledHeaderProjectRelativePath));
            if (File.Exists(precompiledHeaderAbsolutePath))
            {
                PrecompiledHeaderName = precompiledHeaderName;
                PrecompiledHeaderProjectRelativePath = precompiledHeaderProjectRelativePath;
                PrecompiledHeaderAbsolutePath = precompiledHeaderAbsolutePath;
            }
        }

        public string ExpandMacros(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            string solutionPath = Solution.FilePath;
            string projectPath = Path.GetFullPath(FileName);

            value = value.Replace("$(SolutionName)", Solution.Name);
            value = value.Replace("$(SolutionPath)", solutionPath);
            value = value.Replace("$(SolutionFileName)", Path.GetFileName(solutionPath));
            value = value.Replace("$(SolutionDir)", Path.GetDirectoryName(solutionPath) + Path.DirectorySeparatorChar);
            value = value.Replace("$(SolutionExt)", Path.GetExtension(solutionPath));

            value = value.Replace("$(ProjectName)", Name);
            value = value.Replace("$(ProjectPath)", projectPath);
            value = value.Replace("$(ProjectFileName)", Path.GetFileName(projectPath));
            value = value.Replace("$(ProjectDir)", Path.GetDirectoryName(projectPath) + Path.DirectorySeparatorChar);
            value = value.Replace("$(ProjectExt)", Path.GetExtension(projectPath));

            return value;
        }

		public string GetProjectRelativePathFromAbsolutePath(string absolutePath)
		{
			string relativePath = PathHelpers.GetRelativePath(FileName, absolutePath);
			if (relativePath.StartsWith(".\\"))
				relativePath = relativePath.Substring(2);
			return relativePath;
		}
    }
}

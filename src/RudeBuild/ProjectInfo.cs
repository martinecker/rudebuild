using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace RudeBuild
{
    internal static class StringExtensions
    {
        public static string Replace(this string str, string oldValue, string @newValue, StringComparison comparisonType)
        {
            @newValue = @newValue ?? string.Empty;
            if (string.IsNullOrEmpty(str) || string.IsNullOrEmpty(oldValue) || oldValue.Equals(@newValue, comparisonType))
            {
                return str;
            }

            int foundAt;
            while ((foundAt = str.IndexOf(oldValue, 0, comparisonType)) != -1)
            {
                str = str.Remove(foundAt, oldValue.Length).Insert(foundAt, @newValue);
            }

            return str;
        }
    }

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
		public static string ExpandEnvironmentVariables(string value)
		{
			if (string.IsNullOrEmpty(value))
				return value;

			foreach (DictionaryEntry environmentVariable in Environment.GetEnvironmentVariables())
			{
				value = value.Replace("%" + (string)environmentVariable.Key + "%", (string)environmentVariable.Value, StringComparison.OrdinalIgnoreCase);
			}

			return value;
		}
	}

	public sealed class ProjectInfo
    {
        public SolutionInfo Solution { get; private set; }
        public string FileName { get; private set; }
        public string Name { get; private set; }
        public IList<string> MergableCppFileNames { get; private set; } // All C/C++ file names in the project that can be merged into unity files
        public IList<string> AllCppFileNames { get; private set; } // All C/C++ file names in the project (including those that can't be merged)
        public IList<string> IncludeFileNames { get; private set; } // All C/C++ include/header file names in the project
        public string PrecompiledHeaderName { get; private set; }
        public string PrecompiledHeaderProjectRelativePath { get; private set; }
        public string PrecompiledHeaderAbsolutePath { get; private set; }

        public ProjectInfo(SolutionInfo solution, string name, string fileName, IList<string> mergableCppFileNames, IList<string> allCppFileNames, IList<string> allIncludeFileNames, string precompiledHeaderName)
        {
            Solution = solution;
            Name = name;
            FileName = fileName;
            MergableCppFileNames = ExpandMacros(mergableCppFileNames);
            AllCppFileNames = ExpandMacros(allCppFileNames);
            IncludeFileNames = ExpandMacros(allIncludeFileNames);
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
            precompiledHeaderName = ExpandEnvironmentVariables(ExpandMacros(precompiledHeaderName));
            string precompiledHeaderProjectRelativePath = ExpandEnvironmentVariables(ExpandMacros(GetPrecompiledHeaderProjectRelativePath(precompiledHeaderName, IncludeFileNames)));

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

            value = value.Replace("$(SolutionName)", Solution.Name, StringComparison.OrdinalIgnoreCase);
            value = value.Replace("$(SolutionPath)", solutionPath, StringComparison.OrdinalIgnoreCase);
            value = value.Replace("$(SolutionFileName)", Path.GetFileName(solutionPath), StringComparison.OrdinalIgnoreCase);
            value = value.Replace("$(SolutionDir)", Path.GetDirectoryName(solutionPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            value = value.Replace("$(SolutionExt)", Path.GetExtension(solutionPath), StringComparison.OrdinalIgnoreCase);

            value = value.Replace("$(ProjectName)", Name, StringComparison.OrdinalIgnoreCase);
            value = value.Replace("$(ProjectPath)", projectPath, StringComparison.OrdinalIgnoreCase);
            value = value.Replace("$(ProjectFileName)", Path.GetFileName(projectPath), StringComparison.OrdinalIgnoreCase);
            value = value.Replace("$(ProjectDir)", Path.GetDirectoryName(projectPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            value = value.Replace("$(ProjectExt)", Path.GetExtension(projectPath), StringComparison.OrdinalIgnoreCase);

            return value;
        }

        public IList<string> ExpandMacros(IList<string> strings)
        {
            for (int i = 0; i < strings.Count(); ++i)
            {
                strings[i] = ExpandMacros(strings[i]);
            }
            return strings;
        }

        public static string ExpandEnvironmentVariables(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            foreach (DictionaryEntry environmentVariable in Environment.GetEnvironmentVariables())
            {
                value = value.Replace("$(" + (string)environmentVariable.Key + ")", (string)environmentVariable.Value, StringComparison.OrdinalIgnoreCase);
            }

            return value;
        }

        public string GetProjectRelativePathFromAbsolutePath(string absolutePath)
		{
			string relativePath = PathHelpers.GetRelativePath(FileName, Path.GetFullPath(absolutePath));
			if (relativePath.StartsWith(".\\"))
				relativePath = relativePath.Substring(2);
			return relativePath;
		}
    }
}

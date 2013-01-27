using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RudeBuild
{
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
    }
}

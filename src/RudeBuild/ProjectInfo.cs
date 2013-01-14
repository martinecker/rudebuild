using System.Collections.Generic;
using System.IO;

namespace RudeBuild
{
    public class ProjectInfo
    {
        public SolutionInfo Solution { get; private set; }
        public string FileName { get; private set; }
        public string Name { get; private set; }
        public IList<string> CppFileNames { get; private set; }
        public string PrecompiledHeaderFileName { get; private set; }

        public ProjectInfo(SolutionInfo solution, string fileName, IList<string> cppFileNames, string precompiledHeaderFileName)
        {
            Solution = solution;
            FileName = fileName;
            CppFileNames = cppFileNames;
            Name = Path.GetFileNameWithoutExtension(fileName);
            PrecompiledHeaderFileName = ExpandMacros(precompiledHeaderFileName);
        }

        public string ExpandMacros(string value)
        {
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

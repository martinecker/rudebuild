using System.Collections.Generic;
using System.IO;

namespace RudeBuild
{
    public class ProjectInfo
    {
        private SolutionInfo _solution;
        public SolutionInfo Solution
        {
            get { return _solution; }
        }

        private string _fileName;
        public string FileName
        {
            get { return _fileName; }
        }

        private string _name;
        public string Name
        {
            get { return _name; }
        }

        private IList<string> _cppFileNames;
        public IList<string> CppFileNames
        {
            get { return _cppFileNames; }
        }

        private string _precompiledHeaderFileName;
        public string PrecompiledHeaderFileName
        {
            get { return _precompiledHeaderFileName; }
        }

        public ProjectInfo(SolutionInfo solution, string fileName, IList<string> cppFileNames, string precompiledHeaderFileName)
        {
            _solution = solution;
            _fileName = fileName;
            _cppFileNames = cppFileNames;
            _name = Path.GetFileNameWithoutExtension(fileName);
            _precompiledHeaderFileName = ExpandMacros(precompiledHeaderFileName);
        }

        public string ExpandMacros(string value)
        {
            string solutionPath = _solution.FilePath;
            string projectPath = Path.GetFullPath(_fileName);

            value = value.Replace("$(SolutionName)", _solution.Name);
            value = value.Replace("$(SolutionPath)", solutionPath);
            value = value.Replace("$(SolutionFileName)", Path.GetFileName(solutionPath));
            value = value.Replace("$(SolutionDir)", Path.GetDirectoryName(solutionPath) + Path.DirectorySeparatorChar);
            value = value.Replace("$(SolutionExt)", Path.GetExtension(solutionPath));
            
            value = value.Replace("$(ProjectName)", _name);
            value = value.Replace("$(ProjectPath)", projectPath);
            value = value.Replace("$(ProjectFileName)", Path.GetFileName(projectPath));
            value = value.Replace("$(ProjectDir)", Path.GetDirectoryName(projectPath) + Path.DirectorySeparatorChar);
            value = value.Replace("$(ProjectExt)", Path.GetExtension(projectPath));

            return value;
        }
    }
}

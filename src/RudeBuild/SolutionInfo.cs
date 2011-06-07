using System.Collections.Generic;
using System.IO;

namespace RudeBuild
{
    public enum VisualStudioVersion
    {
        VSUnknown,
        VS2005,
        VS2008,
        VS2010
    }

    public class SolutionInfo
    {
        private string _filePath;
        public string FilePath
        {
            get { return _filePath; }
        }

        private string _name;
        public string Name
        {
            get { return _name; }
        }

        private VisualStudioVersion _version;
        public VisualStudioVersion Version
        {
            get { return _version; }
        }

        private IList<string> _projectNames;
        public IList<string> ProjectNames
        {
            get { return _projectNames; }
        }

        private IList<string> _projectFileNames;
        public IList<string> ProjectFileNames
        {
            get { return _projectFileNames; }
        }

        public SolutionInfo(string filePath, VisualStudioVersion version, IList<string> projectFileNames)
        {
            _filePath = Path.GetFullPath(filePath);
            _name = Path.GetFileNameWithoutExtension(filePath);
            _projectFileNames = projectFileNames;
            _version = version;
            _projectNames = new List<string>();
            foreach (string projectFileName in projectFileNames)
            {
                _projectNames.Add(Path.GetFileNameWithoutExtension(projectFileName));
            }
        }
    }
}

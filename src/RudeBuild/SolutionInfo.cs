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

        private IList<string> _projectFilenames;
        public IList<string> ProjectFilenames
        {
            get { return _projectFilenames; }
        }

        public SolutionInfo(VisualStudioVersion version, IList<string> projectFilenames)
        {
            _projectFilenames = projectFilenames;
            _version = version;
            _projectNames = new List<string>();
            foreach (string projectFilename in projectFilenames)
            {
                _projectNames.Add(Path.GetFileNameWithoutExtension(projectFilename));
            }
        }
    }
}

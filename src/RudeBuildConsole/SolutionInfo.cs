using System.Collections.Generic;

namespace RudeBuildConsole
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
        public int ProjectCount
        {
            get { return _projectNames.Count; }
        }

        public SolutionInfo(VisualStudioVersion version, IList<string> projectFilenames)
        {
            _projectNames = projectFilenames;
            _version = version;
        }
    }
}

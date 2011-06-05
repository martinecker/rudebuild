using System.Collections.Generic;
using System.IO;

namespace RudeBuild
{
    public class ProjectInfo
    {
        private string _filename;
        public string Filename
        {
            get { return _filename; }
        }

        private string _name;
        public string Name
        {
            get { return _name; }
        }

        private IList<string> _cppFilenames;
        public IList<string> CppFilenames
        {
            get { return _cppFilenames; }
        }

        public ProjectInfo(string filename, IList<string> cppFilenames)
        {
            _filename = filename;
            _cppFilenames = cppFilenames;
            _name = Path.GetFileNameWithoutExtension(filename);
        }
    }
}

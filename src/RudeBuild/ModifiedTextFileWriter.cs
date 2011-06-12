using System.IO;

namespace RudeBuild
{
    public class ModifiedTextFileWriter
    {
        private string _fileName;
        private bool _forceWrite;

        public ModifiedTextFileWriter(string fileName, bool forceWrite)
        {
            _fileName = fileName;
            _forceWrite = forceWrite;
        }

        public bool Write(string text)
        {
            if (File.Exists(_fileName))
            {
                using (StreamReader reader = new StreamReader(_fileName))
                {
                    string existingText = reader.ReadToEnd();
                    if (!_forceWrite && existingText == text)
                        return false;
                }
            }

            using (StreamWriter writer = File.CreateText(_fileName))
            {
                writer.Write(text);
            }
            return true;
        }
    }
}

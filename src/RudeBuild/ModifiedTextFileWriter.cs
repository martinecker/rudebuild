using System.IO;

namespace RudeBuild
{
    public sealed class ModifiedTextFileWriter
    {
        private readonly string _fileName;
        private readonly bool _forceWrite;

        public ModifiedTextFileWriter(string fileName, bool forceWrite)
        {
            _fileName = fileName;
            _forceWrite = forceWrite;
        }

        public bool Write(string text)
        {
            if (!_forceWrite && File.Exists(_fileName))
            {
                using (var reader = new StreamReader(_fileName))
                {
                    string existingText = reader.ReadToEnd();
                    if (existingText == text)
                        return false;
                }
            }

            // Create the directory if it doesn't exist.
            string directory = Path.GetDirectoryName(_fileName);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using (StreamWriter writer = File.CreateText(_fileName))
            {
                writer.Write(text);
            }
            return true;
        }
    }
}

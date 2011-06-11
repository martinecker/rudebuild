using System.IO;

namespace RudeBuild
{
    public class ModifiedTextFileWriter
    {
        private string _fileName;

        public ModifiedTextFileWriter(string fileName)
        {
            _fileName = fileName;
        }

        public bool Write(string text)
        {
            if (File.Exists(_fileName))
            {
                using (StreamReader reader = new StreamReader(_fileName))
                {
                    string existingText = reader.ReadToEnd();
                    if (existingText == text)
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

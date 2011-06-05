using System.IO;

namespace RudeBuildConsole
{
    public class ModifiedTextFileWriter
    {
        private string _filename;

        public ModifiedTextFileWriter(string filename)
        {
            _filename = filename;
        }

        public void Write(string text)
        {
            if (File.Exists(_filename))
            {
                using (StreamReader reader = new StreamReader(_filename))
                {
                    string existingText = reader.ReadToEnd();
                    if (existingText == text)
                        return;
                }
            }

            using (StreamWriter writer = File.CreateText(_filename))
            {
                writer.Write(text);
            }
        }
    }
}

using System;
using System.Text;
using System.IO;
using System.ComponentModel;
using System.Security.Cryptography;

namespace RudeBuild
{
    public static class ObjectDefaultSetterExtension
    {
        public static object DefaultForType(Type targetType)
        {
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }
        
        public static void SetToDefaults(this object obj)
        {
            foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(obj))
            {
                if (property.IsReadOnly)
                    continue;
                DefaultValueAttribute attribute = property.Attributes[typeof(DefaultValueAttribute)] as DefaultValueAttribute;
                if (null == attribute)
                    property.SetValue(obj, DefaultForType(property.PropertyType));
                else
                    property.SetValue(obj, attribute.Value);
            }
        }
    }

    public class Settings
    {
        private GlobalSettings _globalSettings;
        public GlobalSettings GlobalSettings
        {
            get { return _globalSettings; }
        }

        private BuildOptions _buildOptions;
        public BuildOptions BuildOptions
        {
            get { return _buildOptions; }
        }

        public SolutionSettings SolutionSettings { get; set; }

        private IOutput _output;
        public IOutput Output
        {
            get { return _output; }
        }

        public Settings(GlobalSettings globalSettings, BuildOptions buildOptions, IOutput output)
        {
            _globalSettings = globalSettings;
            _buildOptions = buildOptions;
            _output = output;
        }

        private static string GetMD5Hash(string input)
        {
            MD5 md5Hasher = MD5.Create();

            // Convert the input string to a byte array and compute the hash.
            byte[] data = md5Hasher.ComputeHash(Encoding.Default.GetBytes(input));

            // Convert to a 32 character hexadecimal output string.
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < data.Length; i++)
            {
                result.Append(data[i].ToString("x2"));
            }
            return result.ToString();
        }

        public string GetCachePath(SolutionInfo solutionInfo)
        {
            string solutionDirectory = solutionInfo.Name + "_" + GetMD5Hash(solutionInfo.FilePath);
            string config = BuildOptions.Config.Replace('|', '-');

            string cachePath = Path.Combine(GlobalSettings.CachePath, solutionDirectory);
            cachePath = Path.Combine(cachePath, config);
            return cachePath;
        }

        public string ModifyFileName(string fileName)
        {
            string modifiedFileName = Path.Combine(Path.GetDirectoryName(fileName), _globalSettings.FileNamePrefix + Path.GetFileName(fileName));
            return modifiedFileName;
        }
    }
}

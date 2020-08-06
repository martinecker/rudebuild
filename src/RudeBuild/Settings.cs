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
                var attribute = property.Attributes[typeof(DefaultValueAttribute)] as DefaultValueAttribute;
                if (null == attribute)
                    property.SetValue(obj, DefaultForType(property.PropertyType));
                else
                    property.SetValue(obj, attribute.Value);
            }
        }
    }

    public sealed class Settings
    {
        public GlobalSettings GlobalSettings { get; private set; }
        public BuildOptions BuildOptions { get; private set; }
        public SolutionSettings SolutionSettings { get; set; }
        public IOutput Output { get; private set; }

        public Settings(GlobalSettings globalSettings, BuildOptions buildOptions, IOutput output)
        {
            GlobalSettings = globalSettings;
            BuildOptions = buildOptions;
            Output = output;
        }

        private static string GetMD5Hash(string input)
        {
            var result = new StringBuilder();
            using (MD5 md5Hasher = MD5.Create())
            {
                // Convert the input string to a byte array and compute the hash.
                byte[] data = md5Hasher.ComputeHash(Encoding.Default.GetBytes(input));

                // Convert to a 32 character hexadecimal output string.
                for (int i = 0; i < data.Length; i++)
                {
                    result.Append(data[i].ToString("x2"));
                }
            }
            return result.ToString();
        }

        public string GetCachePath(SolutionInfo solutionInfo)
        {
            string solutionDirectory = solutionInfo.Name + "_" + GetMD5Hash(solutionInfo.FilePath);
            string config = BuildOptions.Config.Replace('|', '-');

            string cachePath = Path.Combine(PathHelpers.ExpandEnvironmentVariables(GlobalSettings.CachePath), solutionDirectory);
            cachePath = Path.Combine(cachePath, config);
            return cachePath;
        }

        public string ModifyFileName(string fileName)
        {
            if (string.IsNullOrEmpty(GlobalSettings.FileNamePrefix) && string.IsNullOrEmpty(GlobalSettings.FileNameSuffix))
                throw new ArgumentException("Either a prefix or suffix for file names needs to be specified in the global settings.");

            string modifiedFileName = GlobalSettings.FileNamePrefix + Path.GetFileNameWithoutExtension(fileName) + GlobalSettings.FileNameSuffix + Path.GetExtension(fileName);
            return Path.Combine(Path.GetDirectoryName(fileName), modifiedFileName);
        }

		public bool IsValidCppFileName(string fileName)
		{
			string extension = Path.GetExtension(fileName).ToLower();
			return extension == ".cpp" || extension == ".cxx" || extension == ".c" || extension == ".cc";
		}
	}
}

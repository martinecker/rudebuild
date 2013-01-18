using System;
using System.IO;
using System.Reflection;

namespace RudeBuildConsole
{
    public static class ApplicationInfo
    {
        public static Version Version
        {
            get { return Assembly.GetCallingAssembly().GetName().Version; }
        }

        private static T GetCallingAssemblyAttribute<T>() where T : Attribute
        {
            object[] attributes = Assembly.GetCallingAssembly().GetCustomAttributes(typeof(T), false);
            if (attributes.Length == 0)
                return null;
            return ((T)attributes[0]);
        }

        public static string Title
        {
            get
            {
                var titleAttribute = GetCallingAssemblyAttribute<AssemblyTitleAttribute>();
                if (null != titleAttribute)
                    return titleAttribute.Title;
                return Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().CodeBase);
            }
        }

        public static string ProductName
        {
            get
            {
                var attribute = GetCallingAssemblyAttribute<AssemblyProductAttribute>();
                return null != attribute ? attribute.Product : string.Empty;
            }
        }

        public static string Description
        {
            get
            {
                var attribute = GetCallingAssemblyAttribute<AssemblyDescriptionAttribute>();
                return null != attribute ? attribute.Description : string.Empty;
            }
        }

        public static string CopyrightHolder
        {
            get
            {
                var attribute = GetCallingAssemblyAttribute<AssemblyCopyrightAttribute>();
                return null != attribute ? attribute.Copyright : string.Empty;
            }
        }

        public static string CompanyName
        {
            get
            {
                var attribute = GetCallingAssemblyAttribute<AssemblyCompanyAttribute>();
                return null != attribute ? attribute.Company : string.Empty;
            }
        }
    }
}

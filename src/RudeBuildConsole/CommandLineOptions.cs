using System.IO;
using CommandLineParser.Arguments;
using CommandLineParser.Validation;

namespace RudeBuildConsole
{
    [ArgumentGroupCertification("rebuild,clean", EArgumentGroupCondition.ExactlyOneUsed)]
    public class CommandLineOptions
    {
        [FileArgument('s', "solution", FileMustExist = true, Description = "Filename of the solution to build.", Optional = false)]
        public FileInfo Solution;
        [ValueArgument(typeof(string), 'c', "config", Description = "Name of the configuration to build including platform, e.g. Debug|Win32", Optional = false)]
        public string Config;
        [ValueArgument(typeof(string), 'p', "project", Description = "Name of the project inside the solution to build. If not specified, a solution build is performed.")]
        public string Project;
        [SwitchArgument('r', "rebuild", false, Description = "Does a full rebuild of the build target")]
        public bool Rebuild;
        [SwitchArgument('l', "clean", false, Description = "Cleans the solution. Also deletes all RudeBuild-generated intermediate files.")]
        public bool Clean;
    }
}

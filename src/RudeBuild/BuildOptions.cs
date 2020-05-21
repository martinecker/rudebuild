using System.IO;
using CommandLineParser.Arguments;
using CommandLineParser.Validation;

namespace RudeBuild
{
    [ArgumentGroupCertification("rebuild,generateOnly,clean", EArgumentGroupCondition.OneOreNoneUsed)]
    public sealed class BuildOptions
    {
        [FileArgument('s', "solution", FileMustExist = true, Description = "FileName of the solution to build.", Optional = false)]
        public FileInfo Solution;
        [ValueArgument(typeof(string), 'c', "config", Description = "Name of the solution configuration (NOT the project configuration!) to build including platform, e.g. \"Debug|Win32\". Be sure to use quotes around this because | is interpreted as a pipe on the command line otherwise.", Optional = false)]
        public string Config;
        [ValueArgument(typeof(string), 'p', "project", Description = "Name of the project inside the solution to build. If not specified, a solution build is performed.")]
        public string Project;
        [SwitchArgument('r', "rebuild", false, Description = "Does a full rebuild of the build target")]
        public bool Rebuild;
        [SwitchArgument('g', "generateOnly", false, Description = "Generates the RudeBuild solution, project and intermediate cache files for the build target, but does not proceed to build the target.")]
        public bool GenerateOnly;
        [SwitchArgument('l', "clean", false, Description = "Cleans the solution. Also deletes all RudeBuild-generated intermediate cache files.")]
        public bool Clean;
        [SwitchArgument('h', "cleanCache", false, Description = "Deletes all RudeBuild-generated intermediate cache files for the given solution, i.e. the cached unity .cpp files and the generated solution and project files.")]
        public bool CleanCache;

        public bool ShouldForceWriteCachedFiles()
        {
            return Rebuild || GenerateOnly || Clean || CleanCache;
        }
    }
}

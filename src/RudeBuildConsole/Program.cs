using System;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using RudeBuild;

namespace RudeBuildConsole
{
    public class ConsoleOutput : IOutput
    {
        public void WriteLine(string line)
        {
            Console.WriteLine(line);
            Debug.WriteLine(line);
        }

        public void WriteLine()
        {
            Console.WriteLine();
            Debug.WriteLine("");
        }

        public void Activate()
        {
        }

        public void Clear()
        {
            Console.Clear();
        }
    }

    public static class ApplicationInfo
    {
        public static Version Version { get { return Assembly.GetCallingAssembly().GetName().Version; } }

        public static string Title
        {
            get
            {
                object[] attributes = Assembly.GetCallingAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
                if (attributes.Length > 0)
                {
                    var titleAttribute = (AssemblyTitleAttribute)attributes[0];
                    if (titleAttribute.Title.Length > 0)
                        return titleAttribute.Title;
                }
                return Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().CodeBase);
            }
        }

        public static string ProductName
        {
            get
            {
                object[] attributes = Assembly.GetCallingAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute), false);
                return attributes.Length == 0 ? "" : ((AssemblyProductAttribute)attributes[0]).Product;
            }
        }

        public static string Description
        {
            get
            {
                object[] attributes = Assembly.GetCallingAssembly().GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false);
                return attributes.Length == 0 ? "" : ((AssemblyDescriptionAttribute)attributes[0]).Description;
            }
        }

        public static string CopyrightHolder
        {
            get
            {
                object[] attributes = Assembly.GetCallingAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
                return attributes.Length == 0 ? "" : ((AssemblyCopyrightAttribute)attributes[0]).Copyright;
            }
        }

        public static string CompanyName
        {
            get
            {
                object[] attributes = Assembly.GetCallingAssembly().GetCustomAttributes(typeof(AssemblyCompanyAttribute), false);
                return attributes.Length == 0 ? "" : ((AssemblyCompanyAttribute)attributes[0]).Company;
            }
        }
    }

    public class Program
    {
        private IOutput _output;

        private BuildOptions ParseBuildOptions(string[] args)
        {
            _output.WriteLine("RudeBuild, Version " + ApplicationInfo.Version);
            _output.WriteLine("A unity C++ build tool for Visual Studio developed by Martin Ecker.");
            _output.WriteLine("This is free, open source software under the zlib license.");
            _output.WriteLine(ApplicationInfo.CopyrightHolder);
            _output.WriteLine();
            _output.WriteLine("For more information and latest updates please visit:");
            _output.WriteLine("http://rudebuild.sourceforge.net");

            _output.WriteLine("Arguments: " + string.Join(" ", args));
            _output.WriteLine();

            var parser = new CommandLineParser.CommandLineParser { ShowUsageOnEmptyCommandline = true };
            try
            {
                var options = new BuildOptions();
                parser.ExtractArgumentAttributes(options);
                parser.ParseCommandLine(args);
                return options;
            }
            catch (CommandLineParser.Exceptions.CommandLineException e)
            {
                parser.ShowUsage();
                _output.WriteLine(e.Message);
            }
            catch (System.IO.FileNotFoundException e)
            {
                parser.ShowUsage();
                _output.WriteLine(e.Message);
            }
            return null;
        }

        private int Run(string[] args)
        {
            _output = new ConsoleOutput();

            try
            {
                BuildOptions options = ParseBuildOptions(args);
                if (options == null || options.Solution == null)
                    return 1;

                GlobalSettings globalSettings = GlobalSettings.Load(_output);
                globalSettings.Save();
                var settings = new Settings(globalSettings, options, _output);

                var stopwatch = new Stopwatch();
                stopwatch.Start();

                int exitCode = 0;
                if (options.CleanCache)
                {
                    CacheCleaner.Run(settings);
                }
                else
                {
                    var solutionReaderWriter = new SolutionReaderWriter(settings);
                    SolutionInfo solutionInfo = solutionReaderWriter.ReadWrite(options.Solution.FullName);
                    settings.SolutionSettings = SolutionSettings.Load(settings, solutionInfo);
                    var projectReaderWriter = new ProjectReaderWriter(settings);
                    projectReaderWriter.ReadWrite(solutionInfo);
                    settings.SolutionSettings.UpdateAndSave(settings, solutionInfo);

                    var processLauncher = new ProcessLauncher(settings);
                    Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs cancelArgs)
                    {
                        _output.WriteLine("Stopping build...");
                        processLauncher.Stop();
                        cancelArgs.Cancel = true;
                    };

                    exitCode = processLauncher.Run(solutionInfo);
                }

                stopwatch.Stop();
                TimeSpan ts = stopwatch.Elapsed;
                string buildTimeText = string.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
                _output.WriteLine("Build time: " + buildTimeText);

                return exitCode;
            }
            catch (Exception e)
            {
                _output.WriteLine(e.Message);
                return -1;
            }
        }

        static int Main(string[] args)
        {
            var program = new Program();
            return program.Run(args);
        }
    }
}

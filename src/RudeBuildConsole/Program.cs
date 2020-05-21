using System;
using System.Diagnostics;
using RudeBuild;

namespace RudeBuildConsole
{
    public sealed class ConsoleOutput : IOutput
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

    public sealed class Program
    {
        private IOutput _output;

        private BuildOptions ParseBuildOptions(string[] args)
        {
            _output.WriteLine("RudeBuild, Version " + ApplicationInfo.Version);
            _output.WriteLine(ApplicationInfo.Description);
            _output.WriteLine("This is free, open source software under the zlib license.");
            _output.WriteLine(ApplicationInfo.CopyrightHolder);
            _output.WriteLine();
            _output.WriteLine("For more information and latest updates please visit:");
            _output.WriteLine("http://www.martinecker.com/rudebuild/");

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

                    if (!options.GenerateOnly)
                    {
                        var processLauncher = new ProcessLauncher(settings);
                        Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs cancelArgs)
                        {
                            _output.WriteLine("Stopping build...");
                            processLauncher.Stop();
                            cancelArgs.Cancel = true;
                        };

                        exitCode = processLauncher.Run(solutionInfo);
                    }
                }

                stopwatch.Stop();
                TimeSpan ts = stopwatch.Elapsed;
                string buildTimeText = string.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
                _output.WriteLine("Build time: " + buildTimeText);

                return exitCode;
            }
            catch (Exception e)
            {
                _output.WriteLine("ERROR: " + e.Message);
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

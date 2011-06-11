using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RudeBuild;

namespace RudeBuildConsole
{
    public class ConsoleOutput : IOutput
    {
        public void WriteLine(string line)
        {
            Console.WriteLine(line);
        }
    }

    public class Program
    {
        private RunOptions ParseRunOptions(string[] args)
        {
            Console.WriteLine("RudeBuild, Version 1.0");
            Console.WriteLine("A unity C++ build tool for Visual Studio developed by Martin Ecker. This is free, open source software.");
            Console.WriteLine("Arguments: " + string.Join(" ", args));

            CommandLineParser.CommandLineParser parser = new CommandLineParser.CommandLineParser();
            parser.ShowUsageOnEmptyCommandline = true;
            try
            {
                RunOptions options = new RunOptions();
                parser.ExtractArgumentAttributes(options);
                parser.ParseCommandLine(args);
                return options;
            }
            catch (CommandLineParser.Exceptions.CommandLineException e)
            {
                parser.ShowUsage();
                Console.WriteLine(e.Message);
            }
            catch (System.IO.FileNotFoundException e)
            {
                parser.ShowUsage();
                Console.WriteLine(e.Message);
            }
            return null;
        }

        private int Run(string[] args)
        {
            try
            {
                RunOptions options = ParseRunOptions(args);
                if (options == null)
                    return 1;

                GlobalSettings globalSettings = new GlobalSettings(options, new ConsoleOutput());
                SolutionReaderWriter solutionReaderWriter = new SolutionReaderWriter(globalSettings);
                SolutionInfo solutionInfo = solutionReaderWriter.ReadWrite(options.Solution.FullName);
                ProjectReaderWriter projectReaderWriter = new ProjectReaderWriter(globalSettings);
                projectReaderWriter.ReadWrite(solutionInfo);

                ProcessLauncher processLauncher = new ProcessLauncher();
                Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs cancelArgs)
                {
                    Console.WriteLine("Stopping build...");
                    processLauncher.Stop();
                    cancelArgs.Cancel = true;
                };
                return processLauncher.Run(solutionInfo, globalSettings);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return -1;
            }
        }

        static int Main(string[] args)
        {
            Program program = new Program();
            return program.Run(args);
        }
    }
}

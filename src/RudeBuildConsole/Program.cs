using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
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

    public class Program
    {
        private IOutput _output;

        private RunOptions ParseRunOptions(string[] args)
        {
            _output.WriteLine("RudeBuild, Version 1.0");
            _output.WriteLine("A unity C++ build tool for Visual Studio developed by Martin Ecker.");
            _output.WriteLine("This is free, open source software under the zlib license.");
            _output.WriteLine();
            _output.WriteLine("For more information and latest updates please visit:");
            _output.WriteLine("http://rudebuild.sourceforge.net");

            _output.WriteLine("Arguments: " + string.Join(" ", args));
            _output.WriteLine();

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
                RunOptions options = ParseRunOptions(args);
                if (options == null)
                    return 1;

                GlobalSettings globalSettings = new GlobalSettings(options, _output);
                SolutionReaderWriter solutionReaderWriter = new SolutionReaderWriter(globalSettings);
                SolutionInfo solutionInfo = solutionReaderWriter.ReadWrite(options.Solution.FullName);
                ProjectReaderWriter projectReaderWriter = new ProjectReaderWriter(globalSettings);
                projectReaderWriter.ReadWrite(solutionInfo);

                ProcessLauncher processLauncher = new ProcessLauncher(globalSettings);
                Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs cancelArgs)
                {
                    _output.WriteLine("Stopping build...");
                    processLauncher.Stop();
                    cancelArgs.Cancel = true;
                };
                return processLauncher.Run(solutionInfo);
            }
            catch (Exception e)
            {
                _output.WriteLine(e.Message);
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

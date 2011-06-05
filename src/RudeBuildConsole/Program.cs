using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RudeBuildConsole
{
    public class Program
    {
        private CommandLineOptions ParseCommandLineOptions(string[] args)
        {
            Console.WriteLine("RudeBuild");
            Console.WriteLine("Arguments: " + string.Join(" ", args));

            CommandLineParser.CommandLineParser parser = new CommandLineParser.CommandLineParser();
            parser.ShowUsageOnEmptyCommandline = true;
            try
            {
                CommandLineOptions options = new CommandLineOptions();
                parser.ExtractArgumentAttributes(options);
                parser.ParseCommandLine(args);
                parser.ShowParsedArguments();
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
                CommandLineOptions options = ParseCommandLineOptions(args);
                if (options == null)
                    return 1;

                GlobalSettings globalSettings = new GlobalSettings();
                SolutionReaderWriter solutionReaderWriter = new SolutionReaderWriter(globalSettings);
                SolutionInfo solutionInfo = solutionReaderWriter.ReadWrite(options.Solution.FullName);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return -1;
            }
            return 0;
        }

        static int Main(string[] args)
        {
            Program program = new Program();
            return program.Run(args);
        }
    }
}

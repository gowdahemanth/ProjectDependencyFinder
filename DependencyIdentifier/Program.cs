using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace DependencyIdentifier
{
    public class Program
    {
        private string inputPath;

        public string InputPath
        {
            get { return inputPath; }
            set { inputPath = value; }
        }

        static void Main(string[] args)
        {
            Program p = new Program();
            ProjectAndSolutionParser parser = new ProjectAndSolutionParser();

            switch(args.Length)
            {
                case 1:
                    p.InputPath = args[0];
                    if (p.ValidateInputPath(p))
                    {
                        Console.WriteLine("DependencyIdentifier in progress..");
                        parser.StartParsing(p);
                    }
                    break;
                default:
                    Console.WriteLine("Invalid arguments...");
                    Console.WriteLine("Proper format: DependencyIdentifier 'folder path of full_build.proj'");
                    break;
            };

        }

        private bool ValidateInputPath(Program p)
        {
            if (p.InputPath.EndsWith(".proj"))
            {
                p.InputPath = p.InputPath.Substring(0, p.InputPath.Length - 15);
            }
            p.InputPath = p.InputPath;

            try
            {
                XDocument doc_test = XDocument.Load(string.Format("{0}full_build.proj", p.InputPath));
            }
            catch (Exception)
            {
                Console.WriteLine("Not a valid path - {0}", p.InputPath);
                return false;
            }

            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CCompilerNet.CodeGen;
using CCompilerNet.Lex;
using CCompilerNet.Parser;

namespace CCompilerNet
{
    class Program
    {
        static void Main(string[] args)
        {
            string output = "";
            string path = "";

            if (args.Length == 0)
            {
                Console.Error.WriteLine("Error: No arguments were passed.");
                Environment.Exit(-1);
            }

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Contains("-output="))
                {
                    output = args[i].Replace("-output=", "");
                }

                if (args[i].Contains(".cm"))
                {
                    path = args[i];
                }
            }

            if (string.IsNullOrEmpty(path))
            {
                Console.Error.WriteLine("Error: No input files provided (input files must have .cm extension).");
                Environment.Exit(-1);
            }

            output = string.IsNullOrEmpty(output) ? path.Replace(".cm", ".exe") : output;   //if output is empty save the .exe file at the same path as the .c file

            Parser.Parser parser = new Parser.Parser(args[0], Path.GetFileName(output));
            parser.CompileProgram();

            parser.VM.Save(Path.GetFileName(output));

            if (File.Exists(output) && Path.GetFileName(output) != output)
            {
                File.Delete(output);
            }

            File.Move(Path.GetFileName(output), output);
            Console.WriteLine("File Compiled Succesfuly and saved at {0}", Path.GetFullPath(output));
        }
    }
}

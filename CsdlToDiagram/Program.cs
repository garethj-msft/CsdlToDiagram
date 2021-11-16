using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using CommandLine;
using CsdlToPlant;

namespace CsdlToDiagram
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            ParserResult<ProgramOptions> result = (await Parser.Default.ParseArguments<ProgramOptions>(args).WithParsedAsync(RunCommandAsync));
            if (Debugger.IsAttached)
            {
                Console.ReadLine();
            }
            return result.Tag == ParserResultType.NotParsed ? 1 : 0;
        }

        private static async Task<int> RunCommandAsync(ProgramOptions args)
        {
            var csdlFile = args.CsdlFile;
            string csdl = File.ReadAllText(csdlFile);
            var root = XElement.Parse(csdl);
            if (root.Name.LocalName.Equals("schema", StringComparison.OrdinalIgnoreCase))
            {
                // This is an unwrapped CSDL file - user needs to top and tail it with standard EDMX nodes for the CSDL reader.
                Console.WriteLine("CSDL file is missing standard Edmx and Edmx:DataServices wrapper nodes.");
                return 1;
            }

            var convertor = new PlantConverter();
            string output = convertor.EmitPlantDiagram(csdl, csdlFile);
            if (!args.SvgModel)
            {
                Console.WriteLine(output);
            }
            else
            {
                output = await RenderSvg.RenderSvgDiagram(output);
                Console.WriteLine(output);
            }

            if (Debugger.IsAttached)
            {
                Console.ReadLine();
            
            }
            return 0;
        }
    }
}

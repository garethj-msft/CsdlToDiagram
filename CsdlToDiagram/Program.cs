using System;
using System.Diagnostics;
using System.IO;
using System.Xml.Linq;
using CsdlToPlant;

namespace CsdlToDiagram
{
    class Program
    {
        static void Main(string[] args)
        {
            var usage = "CsdlToDiagram <csdlfile>";

            if (args.Length != 1 || string.IsNullOrEmpty(args[0]))
            {
                Console.WriteLine(usage);
                return;
            }

            var csdlFile = args[0];
            string csdl = File.ReadAllText(csdlFile);
            var root = XElement.Parse(csdl);
            if (root.Name.LocalName.Equals("schema", StringComparison.OrdinalIgnoreCase))
            {
                // This is an unwrapped CSDL file - user needs to top and tail it with standard EDMX nodes for the CSDL reader.
                Console.WriteLine("CSDL file is missing standard Edmx and Edmx:DataServices wrapper nodes.");
                return;
            }

            var convertor = new PlantConverter();
            string plant = convertor.EmitPlantDiagram(csdl, csdlFile);
            Console.WriteLine(plant);
            if (Debugger.IsAttached)
            {
                Console.ReadLine();
            }
        }
    }
}

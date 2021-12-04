// <copyright file="ProgramOptions.cs" company="Microsoft">
// © Microsoft. All rights reserved.
// </copyright>

using System.Collections.Generic;
using CommandLine;

namespace CsdlToDiagram
{
    public class ProgramOptions
    {
        [Option('s', "svgMode", Required = false, HelpText = "Flag to emit svg files rather than raw PlantUML.")]
        public bool SvgModel { get; set; }
        
        [Option('o', "out", Required = false, HelpText = "Option to specify a filename to output.")]
        public string? Output { get; set; }

        [Option('u', "serverUrl", Required = false, HelpText = "Option to specify a PlantUML rendering server to use.")]
        public string? ServerUrl { get; set; }

        [Value(0, HelpText = "The CSDL file to render.", Required = true)]
        public string? CsdlFile { get; set; }
    }
}
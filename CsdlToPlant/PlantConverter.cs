using System;
using System.Linq;

namespace CsdlToPlant
{
    public class PlantConverter
    {
        private const string GenerationErrorsMessage = "There were errors generating the PlantUML file.";
        private readonly Generator generator = new Generator();

        public string EmitPlantDiagram(string csdlContent, string csdlFilename, GeneratorOptions options = null)
        {
            options = options ?? GeneratorOptions.DefaultGeneratorOptions;

            this.generator.EmitPlantDiagram(csdlContent, csdlFilename, options);
            if (this.generator.Errors.Any(e => !e.IsWarning))
            {
                return GenerationErrorsMessage;
            }
            else
            {
                return this.generator.GetText();
            }
        }

        private class Generator : CsdlToPlantGenerator
        {
            public string GetText()
            {
                return this.GenerationEnvironment.ToString();
            }
        }
    }
}
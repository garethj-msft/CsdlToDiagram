namespace CsdlToPlant
{
    public class PlantConverter
    {
        private readonly Generator generator = new Generator();

        public string EmitPlantDiagram(string csdlContent, string csdlFilename)
        {
            this.generator.EmitPlantDiagram(csdlContent, csdlFilename);
            if (this.generator.Errors.HasErrors)
            {
                return "There were errors generating the PlantUML file.";
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
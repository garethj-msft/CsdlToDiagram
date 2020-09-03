namespace CsdlToDiagram
{
    internal class PlantConverter : CsdlToPlant
    {
        public string GetText()
        {
            return this.GenerationEnvironment.ToString();
        }
    }
}
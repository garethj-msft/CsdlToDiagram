# CsdlToDiagram
Simple console app to generate a PlantUml diagram from an OData CSDL file.
Available on nuget as a tool: `CsdlToDiagram`

## Generate a PlantUML diagram to the console.
```
CsdlToDiagram <csdlFile>
```

## Library CsdlToPlant
.Net Standard 2.1 library to create PlantUML text from a CSDL file.
Available on nuget as CsdlDiagrams.Net.

Usage:
```cs
    var csdlFile = "<somefilename>";
    var csdl = File.ReadAllText(csdlFile);
    var converter = new PlantConverter();
    var plantUml = converter.EmitPlantDiagram(generator.csdl, csdlFilename);
```
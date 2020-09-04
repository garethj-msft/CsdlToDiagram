# CsdlToDiagram
Simple T4-powered console app to generate a PlantUml diagram from an OData CSDL file.

## Generate a PlantUML diagram to the console.
```
CsdlToDiagram.exe <csdlFile>
```

## Library CsdlToPlant
.Net Standard 2.0 library to create PlantUML text from a CSDL file.
Available on nuget as CsdlDiagrams.Net.

Usage:
```cs
    var csdlFile = "<somefilename>";
    var csdl = File.ReadAllText(csdlFile);
    var converter = new PlantConverter();
    var plantUml = converter.EmitPlantDiagram(generator.csdl, csdlFilename);
```
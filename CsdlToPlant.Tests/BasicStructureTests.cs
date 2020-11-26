using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsdlToPlant.Tests
{
    [TestClass]
    public class BasicStructureTests
    {
        private const string CsdlHeader = @"<?xml version=""1.0"" encoding=""utf-8""?>
            <edmx:Edmx Version=""4.0""
                xmlns:edmx=""http://docs.oasis-open.org/odata/ns/edmx""
                xmlns:edm=""http://docs.oasis-open.org/odata/ns/edm""
                xmlns:ags=""http://aggregator.microsoft.com/internal""
                xmlns=""http://docs.oasis-open.org/odata/ns/edm"">
                <edmx:Reference Uri=""https://oasis-tcs.github.io/odata-vocabularies/vocabularies/Org.OData.Core.V1.xml"">
                    <edmx:Include Alias=""Core"" Namespace=""Org.OData.Core.V1"" />
                </edmx:Reference>
                <edmx:DataServices>";

        private const string CsdlFooter = @"</edmx:DataServices></edmx:Edmx>";

        private const string CsdlStartSchema = @"<edm:Schema Namespace=""{0}"">";

        private const string CsdlEndSchema = @"</edm:Schema>";

        [TestMethod]
        public void EntityProjectsClass()
        {
            string csdl = this.FormCsdl("myNamespace", CreateEntity(@"entityName"));

            var convertor = new PlantConverter();
            string plant = convertor.EmitPlantDiagram(csdl, @"c:\model.csdl");

            StringAssert.Contains(plant, $@"class entityName");
        }

        private static string CreateEntity(string entityName, string content = null)
        {
            content ??= string.Empty;

            return $@"<EntityType Name=""{entityName}"">{content}</EntityType>";
        }

        private string FormCsdl(string theNamespace, string content)
        {
            return $"{CsdlHeader}{String.Format(CsdlStartSchema, theNamespace)}{content}{CsdlEndSchema}{CsdlFooter}";
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static CsdlToPlant.Tests.CsdlTestHelper;

namespace CsdlToPlant.Tests
{
    [TestClass]
    public class BasicStructureTests
    {
        [TestMethod]
        public void EntityProjectsClass()
        {
            string csdl = FormCsdl("myNamespace", CreateEntity(@"entityName"));
            var convertor = new PlantConverter();

            string plant = convertor.EmitPlantDiagram(csdl, @"c:\model.csdl");

            StringAssert.Matches(plant, new Regex(@"^class entityName", RegexOptions.Multiline));
        }

        [TestMethod]
        public void AbstractEntityProjectsAbstractClass()
        {
            string csdl = FormCsdl("myNamespace", CreateEntity(@"entityName", new[] {new KeyValuePair<string, string>("Abstract", "true")}));
            var convertor = new PlantConverter();

            string plant = convertor.EmitPlantDiagram(csdl, @"c:\model.csdl");

            StringAssert.Matches(plant, new Regex(@"^abstract class entityName", RegexOptions.Multiline));
        }

        [TestMethod]
        public void AnnotatedEntityProjectsClassAndNote()
        {
            string csdl = FormCsdl("myNamespace", CreateEntity(@"entityName", @"<!-- Note: First note. -->"));
            var convertor = new PlantConverter();

            string plant = convertor.EmitPlantDiagram(csdl, @"c:\model.csdl");

            StringAssert.Matches(plant, new Regex(@"^class entityName", RegexOptions.Multiline));
            StringAssert.That.MatchesLines(plant, @"^note top of entityName", @"Note: First note.", @"end note\r\n$");
            StringAssert.That.ContainsCountOf(plant, 1, "note top of");
            StringAssert.That.ContainsCountOf(plant, 1, "First note.");
        }

        [TestMethod]
        public void RootCommentProjectsFloatingNote()
        {
            string csdl = FormCsdl("myNamespace", 
                CreateEntity(@"entityName") +
                @"<!-- Note: Root note. -->");
            var convertor = new PlantConverter();

            string plant = convertor.EmitPlantDiagram(csdl, @"c:\model.csdl");

            StringAssert.Contains(plant, @"class entityName");
            StringAssert.That.MatchesLines(plant, @"^note as RootNoteR1", @"Note: Root note.", @"end note\r\n$");
        }

    }
}

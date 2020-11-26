using System;
using System.Collections.Generic;
using System.Linq;

namespace CsdlToPlant.Tests
{
    public class CsdlTestHelper
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

        public static string CreateEntity(string entityName, IEnumerable<KeyValuePair<string, string>> attributes, string content = null)
        {
            content ??= String.Empty;
            string attributeString = String.Join(' ', attributes.Select(a => $@"{a.Key}=""{a.Value}"""));
            return $@"<EntityType Name=""{entityName}"" {attributeString}>{content}</EntityType>";
        }

        public static string CreateEntity(string entityName, string content = null)
        {
            return CreateEntity(entityName, new KeyValuePair<string, string>[0], content);
        }

        public static string FormCsdl(string theNamespace, string content)
        {
            return $"{CsdlHeader}{string.Format(CsdlStartSchema, theNamespace)}{content}{CsdlEndSchema}{CsdlFooter}";
        }
    }
}
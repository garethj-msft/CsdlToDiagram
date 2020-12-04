namespace CsdlToPlant
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml.Linq;
    using Microsoft.OData.Edm;
    using Microsoft.OData.Edm.Csdl;

    internal class CsdlToPlantGenerator : CodeGeneratorBase
    {
        private const string CollectionPrefix = "Collection(";
        private IEdmModel model;
        private string theFilename;
        private bool usesNamespaces;

        /// <summary>
        /// Dictionary of (namespace, name) to list of notes, sorted on the namespace.
        /// </summary>
        private readonly SortedDictionary<(string theNamespace, string theName), IEnumerable<string>>
            noteMap = new SortedDictionary<(string theNamespace, string theName), IEnumerable<string>>();
                // Comparer<(string theNamespace, string theName)>.Create((o1, o2) =>
                    //StringComparer.OrdinalIgnoreCase.Compare(o1.theNamespace, o2.theNamespace)));

        private readonly Dictionary<IEdmStructuredType, IList<IEdmOperation>> boundOperations =
            new Dictionary<IEdmStructuredType, IList<IEdmOperation>>();

        private GeneratorOptions options;

        public void EmitPlantDiagram(string csdl, string filename, GeneratorOptions options)
        {
            this.options = options;
            var parsed = XElement.Parse(csdl);
            this.ConstructNotesLookaside(parsed);
            this.model = CsdlReader.Parse(parsed.CreateReader());
            if (this.model == null)
            {
                this.WriteLine("Failed to parse the CSDL file.");
            }

            this.CalculateNamespaceUsage();
            this.theFilename = filename;
            this.WriteLine(@"@startuml");
            this.WriteLine(@"skinparam classAttributeIconSize 0");
            this.WriteLine($@"title API Entity Diagram for {this.theFilename}");
            this.WriteLine("");
            this.EmitEntityContainer();
            this.WriteLine("");
            this.CollateBoundOperations();

            foreach (var entity in this.FilterSkipped(this.model.SchemaElements.OfType<IEdmEntityType>()))
            {
                this.EmitStructuralType(entity, "entity");
                this.EmitNavigationProperties(entity);
                this.WriteLine("");
            }

            foreach (var complex in this.FilterSkipped(this.model.SchemaElements.OfType<IEdmComplexType>()))
            {
                this.EmitStructuralType(complex, "complexType");
                this.WriteLine("");
            }

            foreach (var enumeration in this.FilterSkipped(this.model.SchemaElements.OfType<IEdmEnumType>()))
            {
                this.EmitEnumType(enumeration);
                this.WriteLine("");
            }

            this.EmitNotes();
        }

        private void CalculateNamespaceUsage()
        {
            int count = this.model.DeclaredNamespaces.Count();
            string firstNamespace = this.model.DeclaredNamespaces.First();
            this.usesNamespaces = count > 1 ||
                                  !firstNamespace.Equals("microsoft.graph", StringComparison.OrdinalIgnoreCase) &&
                                  firstNamespace.StartsWith("microsoft.graph.", StringComparison.OrdinalIgnoreCase);
        }

        private void CalculateNamespaceUsage()
        {
            int count = this.model.DeclaredNamespaces.Count();
            string firstNamespace = this.model.DeclaredNamespaces.First();
            this.usesNamespaces = count > 1 ||
                                  !firstNamespace.Equals("microsoft.graph", StringComparison.OrdinalIgnoreCase) &&
                                  firstNamespace.StartsWith("microsoft.graph.", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Return elements that are not in the options SkipList.
        /// </summary>
        private IEnumerable<T> FilterSkipped<T>(IEnumerable<T> unfilteredList)
            where T: IEdmNamedElement
        {
            return unfilteredList.Where(t => !this.options.SkipList.Contains(t.Name, StringComparer.OrdinalIgnoreCase));
        }

        private void EmitStructuralType(
            IEdmStructuredType theType,
            string prototype)
        {
            var props = new List<string>();
            var complexUsages = new List<string>();
            if (this.options.SkipList.Contains("entity", StringComparer.OrdinalIgnoreCase))
            {
                if (theType is IEdmEntityType && 
                        (theType.BaseType == null ||
                         ((IEdmNamedElement)(theType.BaseType)).Name.Equals("entity", StringComparison.OrdinalIgnoreCase)))
                {
                    // Add fake id property because everything is originally derived from Graph's 'Entity' base type which would clutter the diagram.
                    props.Add("+id: String");
                }
            }

            foreach (var property in theType.DeclaredProperties)
            {
                var typeName = this.GetTypeName(property.Type);
                var fullTypeName = typeName;
                if (GetNamespace(typeName).Equals(GetNamespace(this.GetTypeName(theType)),
                    StringComparison.OrdinalIgnoreCase))
                {
                    typeName = GetSimpleName(typeName);
                }

                // Prefix properties with parentheses in them to avoid them being interpreted as methods.
                var isCollection = property.Type.Definition is IEdmCollectionType;
                var prefix = isCollection ? "{field} " : string.Empty;

                var optionality = CalculatePropertyCardinalityText(property, isCollection, out var cardinalityMin, out var cardinalityMax);
                if (!string.IsNullOrWhiteSpace(optionality))
                {
                    optionality = $" [{optionality}]";
                }

                var navType = string.Empty;
                var exposure = "+";
                if (property is IEdmNavigationProperty navProp)
                {
                    navType = navProp.ContainsTarget ? "*" : navType;
                    // Nav props don't show up by default.
                    exposure = "-";
                }

                props.Add($"{prefix}{exposure}{property.Name}: {typeName}{optionality}{navType}");

                IEdmType propFundamental = GetFundamentalType(property.Type.Definition);
                if (propFundamental.TypeKind == EdmTypeKind.Complex ||
                    propFundamental.TypeKind == EdmTypeKind.Enum)
                {
                    string basePropType = StripCollection(fullTypeName);
                    complexUsages.Add(
                        $@"{this.GetTypeName(theType)} +--> ""[{cardinalityMin}..{cardinalityMax}]"" {basePropType}: {property.Name}");
                }
            }

            var isAbstract = theType.IsAbstract ? "abstract " : string.Empty;
            if (prototype.Equals("entity") && !theType.IsAbstract)
            {
                prototype = $"(N,white){prototype}";
            }

            var extends = string.Empty;
            if (theType.BaseType != null && !this.options.SkipList.Contains(((IEdmNamedElement)theType.BaseType).Name, StringComparer.OrdinalIgnoreCase))
            {
                extends = $" extends {this.GetTypeName(theType.BaseType)}";
            }

            this.WriteLine(
                $"{isAbstract}class {this.GetTypeName(theType)} <<{prototype}>> {GetTypeColor(theType)}{extends} {{");

            foreach (var prop in props)
            {
                this.WriteLine(prop);
            }

            if (this.boundOperations.TryGetValue(theType, out IList<IEdmOperation> list))
            {
                foreach (var boundOperation in list)
                {
                    string returnType = string.Empty;
                    if (boundOperation is IEdmFunction function)
                    {
                        returnType = $": {this.GetTypeName(function.ReturnType)}";
                    }

                    this.WriteLine($"+{boundOperation.Name}(){returnType}");
                }
            }

            this.WriteLine("}");

            foreach (var usage in complexUsages)
            {
                this.WriteLine(usage);
            }
        }

        private static string CalculatePropertyCardinalityText(
            IEdmProperty property,
            bool isCollection,
            out string cardinalityMin,
            out string cardinalityMax)
        {
            var optionality = string.Empty;
            cardinalityMin = "1";
            cardinalityMax = isCollection ? "*" : "1";
            if (property.Type.IsNullable)
            {
                // Optionality only specified on property names if they are optional.
                cardinalityMin = "0";
                optionality = $"{cardinalityMin}..{cardinalityMax}";
            }

            return optionality;
        }

        private void ConstructNotesLookaside(XElement root)
        {
            IEnumerable<string> extractComments(IEnumerable<XNode> childNodes)
            {
                return from c in childNodes.OfType<XComment>()
                    where c.Value.Trim().StartsWith("Note:", StringComparison.OrdinalIgnoreCase)
                    select c.Value.Trim();
            }

            var commentedEntities = from s in Enumerable.Repeat(root, 1).DescendantsAndSelf()
                where s.Name.LocalName == "Schema"
                let n = s.Attributes().First(a => a.Name == "Namespace").Value
                from e in Enumerable.Repeat(s, 1).DescendantsAndSelf()
                where e.Name.LocalName == "EntityType" ||
                      e.Name.LocalName == "ComplexType" ||
                      e.Name.LocalName == "EnumType"
                let comments = extractComments(e.DescendantNodes())
                where comments.Any()
                select new {Namespace = n, Entity = e, Comments = comments};

            foreach (var commentedEntity in commentedEntities)
            {
                this.noteMap[(commentedEntity.Namespace, commentedEntity.Entity.Attributes().First(a => a.Name == "Name").Value)] =
                    commentedEntity.Comments;
            }

            var rootComments = from s in Enumerable.Repeat(root, 1).DescendantsAndSelf()
                where s.Name.LocalName == "Schema"
                let n = s.Attributes().First(a => a.Name == "Namespace").Value
                let comments = extractComments(s.Nodes())
                from comment in comments
                select new {Namespace = n, Comments = comments};

            foreach (var rootComment in rootComments)
            {
                this.noteMap[(rootComment.Namespace, string.Empty)] = rootComment.Comments;
            }
        }

        private void EmitEnumType(IEdmEnumType theType)
        {
            this.WriteLine($"enum {this.GetTypeName(theType)} <<enum>> #GoldenRod {{");
            foreach (IEdmEnumMember member in theType.Members)
            {
                this.WriteLine($"{member.Name}");
            }
            this.WriteLine("}");
        }

        private void EmitNotes()
        {
            string theNamespace = string.Empty;
            foreach (var note in this.noteMap)
            {
                if (this.usesNamespaces && !theNamespace.Equals(note.Key.theNamespace))
                {
                    if (theNamespace != string.Empty)
                    {
                        this.WriteLine("}");
                    }

                    theNamespace = note.Key.theNamespace;
                    this.WriteLine($"namespace {theNamespace} {{");
                }

                this.EmitNote(note.Key.theName, note.Value);
            }

            if (this.usesNamespaces)
            {
                this.WriteLine("}");
            }
        }

        private void EmitNote(string noteTarget, IEnumerable<string> notes)
        {
            this.WriteLine(string.IsNullOrWhiteSpace(noteTarget) ? "note as RootNoteR1" : $"note top of {noteTarget}");

            foreach (string note in notes)
            {
                this.WriteLine(note);
            }
            this.WriteLine("end note");
        }

        private void EmitNavigationProperties(IEdmEntityType entity)
        {
            foreach (IEdmNavigationProperty navProp in entity.DeclaredProperties.OfType<IEdmNavigationProperty>())
            {
                
                var target = navProp.Type.Definition;
                if (target is IEdmNamedElement namedTarget &&
                    this.options.SkipList.Contains(namedTarget.Name, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                var entityTarget = target as IEdmEntityType;
                bool isCollection = false;
                if (target is IEdmCollectionType collectionTarget)
                {
                    isCollection = true;
                    entityTarget = collectionTarget.ElementType.Definition as IEdmEntityType;
                }

                var navType = navProp.ContainsTarget ? "*" : string.Empty;
                CalculatePropertyCardinalityText(navProp, isCollection, out var cardinalityMin, out var cardinalityMax);
                this.WriteLine(
                    $@"{this.GetTypeName(entity)} {navType}--> ""{cardinalityMin}..{cardinalityMax}"" {this.GetTypeName(entityTarget)}: {navProp.Name}");
            }
        }

        private void EmitEntityContainer()
        {
            if (this.model.EntityContainer == null)
            {
                return;
            }

            var members = new List<string>();
            string ecName = this.StripNamespace(this.model.EntityContainer.FullName());
            foreach (var singleton in this.model.EntityContainer.Elements.OfType<IEdmSingleton>())
            {
                var singletonTypeName = this.GetTypeName(singleton.Type);
                members.Add($"+{singleton.Name}: {singletonTypeName}");
                this.WriteLine($@"{ecName} .. ""1..1"" {singletonTypeName}: {singleton.Name}");
            }

            foreach (var entitySet in this.model.EntityContainer.Elements.OfType<IEdmEntitySet>())
            {
                var entitySetTypeName = this.GetTypeName(entitySet.EntityType());
                members.Add($"+{entitySet.Name}: {entitySetTypeName}");
                this.WriteLine(
                    $@"{ecName} .. ""0..*"" {StripCollection(entitySetTypeName)}: {entitySet.Name}");
            }

            this.WriteLine($"class {ecName} <<(S,white)entityContainer>> #LightPink {{");
            foreach (var member in members)
            {
                this.WriteLine(member);
            }
            this.WriteLine("}");
        }

        private void CollateBoundOperations()
        {
            // Collate the bound actions and functions against their bound elements.
            foreach (var operation in this.model.SchemaElements.OfType<IEdmOperation>().Where(o => o.IsBound))
            {
                // By spec definition, first parameter is the binding parameter.
                if ((operation?.Parameters?.FirstOrDefault()?.Type?.Definition) is IEdmStructuredType
                    bindingParameterType)
                {
                    if (!this.boundOperations.TryGetValue(bindingParameterType, out var list))
                    {
                        list = new List<IEdmOperation>();
                        this.boundOperations[bindingParameterType] = list;
                    }

                    list.Add(operation);
                }
            }
        }

        private static string StripCollection(string name)
        {
            if (name.Contains(CollectionPrefix))
            {
                name = name.Replace(CollectionPrefix, string.Empty);
                name = name.Substring(0, name.Length - 1);
            }

        private string StripNamespace(string name)
        {
            if (name == null) return null;

            return this.usesNamespaces ? name : GetSimpleName(name);
        }

        private static bool IsCollection(string name)
        {
            return name.Contains(CollectionPrefix);
        }

        private string StripNamespace(string name)
        {
            if (name == null) return null;

            return this.usesNamespaces ? name : GetSimpleName(name);
        }

        private static string GetNamespace(string name)
        {
            if (name == null)
            {
                return null;
            }
            else
            {
                name = StripCollection(name);
                return name.Contains('.') ? string.Join(".", name.Split('.')[..^2]) : string.Empty;
            }
        }

        private static string GetSimpleName(string name)
        {
            if (name == null)
            {
                return null;
            }
            else
            {
                bool isColl = IsCollection(name);
                name = StripCollection(name);
                name = name.Contains('.') ? name.Split('.')[^1] : name;
                name = isColl ? $"{CollectionPrefix}{name})" : name;
                return name;
            }
        }

        private string GetTypeName(IEdmTypeReference theType)
        {
            var name = theType.ShortQualifiedName() ?? theType.FullName();

            name = this.StripNamespace(name);
            return name;
        }

        private string GetTypeName(IEdmType theType)
        {
            var typeName = string.Empty;
            switch (theType)
            {
                case IEdmComplexType complex:
                    typeName = complex.FullTypeName();
                    break;
                case IEdmEntityType entity:
                    typeName = entity.FullTypeName();
                    break;
                case IEdmCollectionType collection:
                    typeName = this.GetTypeName(collection.ElementType);
                    break;
                case IEdmEnumType enumeration:
                    typeName = enumeration.FullTypeName();
                    break;
            }

            return this.StripNamespace(typeName);
        }

        private static string GetTypeColor(IEdmType theType)
        {
            switch (theType)
            {
                case IEdmComplexType _:
                    return "#Skyblue";
                case IEdmEntityType _:
                    return "#PaleGreen";
                default:
                    return string.Empty;
            }
        }

        private static IEdmType GetFundamentalType(IEdmType theType) => theType is IEdmCollectionType collection ? collection.ElementType.Definition : theType;
    }
}

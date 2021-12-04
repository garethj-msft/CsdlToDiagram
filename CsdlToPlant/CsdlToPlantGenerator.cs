using System.IO;
using System.Xml;

namespace CsdlToPlant
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml.Linq;
    using Microsoft.OData.Edm;
    using Microsoft.OData.Edm.Csdl;
    using Microsoft.OData.Edm.Validation;

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

        private readonly Dictionary<IEdmStructuredType, IList<IEdmOperation>> boundOperations =
            new Dictionary<IEdmStructuredType, IList<IEdmOperation>>();

        private GeneratorOptions options;

        private readonly HashSet<IEdmEntityType> entitiesToEmit = new HashSet<IEdmEntityType>();
        private readonly HashSet<IEdmComplexType> complexToEmit = new HashSet<IEdmComplexType>();
        private readonly HashSet<IEdmEnumType> enumsToEmit = new HashSet<IEdmEnumType>();

        private readonly HashSet<IEdmEntityType> emittedEntities = new HashSet<IEdmEntityType>();
        private readonly HashSet<IEdmComplexType> emittedComplex = new HashSet<IEdmComplexType>();
        private readonly HashSet<IEdmEnumType> emittedEnums = new HashSet<IEdmEnumType>();

        public void EmitPlantDiagram(string csdl, string filename, GeneratorOptions options)
        {
            this.options = options;
            var parsed = XElement.Parse(csdl);
            this.ConstructNotesLookaside(parsed);
            this.model = this.LoadModel(filename, parsed);
            if (this.model == null)
            {
                return;
            }

            this.CalculateNamespaceUsage();
            this.theFilename = Path.GetFileName(filename);
            this.WriteLine(@"@startuml");
            this.WriteLine(@"skinparam classAttributeIconSize 0");
            this.WriteLine($@"title API Entity Diagram for {this.theFilename}");
            this.WriteLine("");

            // This may populate the sets with types referenced in the container.
            this.EmitEntityContainer();

            this.WriteLine("");
            this.CollateBoundOperations();

            // Put top-level elements onto the processing sets.
            this.entitiesToEmit.UnionWith(this.FilterSkipped(this.model.SchemaElements.OfType<IEdmEntityType>()));
            this.complexToEmit.UnionWith(this.FilterSkipped(this.model.SchemaElements.OfType<IEdmComplexType>()));
            this.enumsToEmit.UnionWith(this.FilterSkipped(this.model.SchemaElements.OfType<IEdmEnumType>()));

            // Keep spitting out types until nothing new has been introduced.
            bool anyEmitted;
            do
            {
                // Any types emitted on this iteration.
                anyEmitted = false;

                // Now walk the sets - these could dynamically get more added during processing so use a RemoveFirst rather than an enumerator.
                for (var entity = this.RemoveFirst(this.entitiesToEmit);
                    entity != null;
                    entity = this.RemoveFirst(this.entitiesToEmit))
                {
                    this.EmitStructuralType(entity, "entity");
                    this.EmitNavigationProperties(entity);
                    this.WriteLine("");
                    this.emittedEntities.Add(entity);
                    anyEmitted = true;

                    // Also need to emit any types derived from this type.
                    this.AddDerivedStructuredTypes(entity);
                }

                for (var complex = this.RemoveFirst(this.complexToEmit);
                    complex != null;
                    complex = this.RemoveFirst(this.complexToEmit))
                {
                    this.EmitStructuralType(complex, "complexType");
                    this.WriteLine("");
                    this.emittedComplex.Add(complex);
                    anyEmitted = true;

                    // Also need to emit any types derived from this type.
                    this.AddDerivedStructuredTypes(complex);
                }

                for (var enumeration = this.RemoveFirst(this.enumsToEmit);
                    enumeration != null;
                    enumeration = this.RemoveFirst(this.enumsToEmit))
                {
                    this.EmitEnumType(enumeration);
                    this.WriteLine("");
                    this.emittedEnums.Add(enumeration);
                    anyEmitted = true;
                }

            } while (anyEmitted);

            this.EmitNotes();
            this.WriteLine(@"@enduml");
        }

        private IEdmModel LoadModel(string filename, XElement parsed)
        {
            IEdmModel theModel = null;
            try
            {
                var directory = Path.GetDirectoryName(filename);
                using XmlReader mainReader = parsed.CreateReader();
                theModel = CsdlReader.Parse(mainReader, u =>
                {
                    if (string.IsNullOrEmpty(directory))
                    {
                        this.Error($"No directory to resolve referenced model.");
                        return null;
                    }

                    // Currently only support relative paths
                    if (u.IsAbsoluteUri)
                    {
                        this.Error($"Referenced model must use relative URIs.");
                        return null;
                    }

                    var file = Path.Combine(directory, u.OriginalString);
                    string referenceText = File.ReadAllText(file);
                    var referenceParsed = XElement.Parse(referenceText);
                    this.ConstructNotesLookaside(referenceParsed);
                    XmlReader referenceReader = referenceParsed.CreateReader();
                    return referenceReader;
                });
            }
            catch (EdmParseException parseException)
            {
                this.Error("Failed to parse the CSDL file.");
                this.Error(string.Join(Environment.NewLine, parseException.Errors.Select(e => e.ToString())));
                return null;
            }

            if (theModel == null)
            {
                this.Error("Failed to load the CSDL file.");
            }

            return theModel;
        }

        private void AddDerivedStructuredTypes(IEdmStructuredType structured)
        {
            foreach (var derived in this.model.FindAllDerivedTypes(structured))
            {
                if (derived is IEdmEntityType derivedEntity)
                {
                    this.AddEntityToEmit(derivedEntity);
                }
                else
                {
                    this.AddComplexToEmit(derived as IEdmComplexType);
                }
            }
        }

        private void AddEntityToEmit(IEdmEntityType type)
        {
            this.AddTypeToEmit(type, this.entitiesToEmit, this.emittedEntities);
        }

        private void AddComplexToEmit(IEdmComplexType type)
        {
            this.AddTypeToEmit(type, this.complexToEmit, this.emittedComplex);
        }

        private void AddEnumToEmit(IEdmEnumType type)
        {
            this.AddTypeToEmit(type, this.enumsToEmit, this.emittedEnums);
        }

        private void AddTypeToEmit<T>(T type, HashSet<T> toEmitCollection, HashSet<T> emittedCollection)
            where T : IEdmSchemaType
        {
            var positive = this.FilterSkipped(Enumerable.Repeat(type, 1));
            type = positive.SingleOrDefault();
            if (type != null
                && !emittedCollection.Contains(type)
                && !IsUnresolved(type))
            {
                toEmitCollection.UnionWith(positive);
            }
        }

        /// <summary>
        /// Take the first item out of a HashSet
        /// </summary>
        private T RemoveFirst<T>(HashSet<T> collection) where T : IEdmType
        {
            var first = collection.FirstOrDefault();
            if (first != null)
            {
                collection.Remove(first);
            }
            return first;
        }

        private void CalculateNamespaceUsage()
        {
            // If this model has non-normative references that will be chased down,
            // we have to use namespaces, as we can't calculate in advance.
            if (this.model.GetEdmReferences().Any(r => !r.Uri.IsAbsoluteUri))
            {
                this.usesNamespaces = true;
                return;
            }

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
            IEdmStructuredType baseType = theType.BaseType;
            if (this.options.SkipList.Contains("entity", StringComparer.OrdinalIgnoreCase))
            {
                if (theType is IEdmEntityType && 
                        (baseType == null ||
                         ((IEdmNamedElement)baseType).Name.Equals("entity", StringComparison.OrdinalIgnoreCase)))
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

                IEdmType propFundamental = property.Type.Definition.AsElementType();
                if (propFundamental.TypeKind == EdmTypeKind.Complex ||
                    propFundamental.TypeKind == EdmTypeKind.Enum)
                {
                    if (propFundamental.TypeKind == EdmTypeKind.Complex)
                    {
                        this.AddComplexToEmit(propFundamental as IEdmComplexType);
                    }
                    else
                    {
                        this.AddEnumToEmit(propFundamental as IEdmEnumType);
                    }

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
            if (baseType != null && !this.options.SkipList.Contains(((IEdmNamedElement)baseType).Name, StringComparer.OrdinalIgnoreCase))
            {
                extends = $" extends {this.GetTypeName(baseType)}";
                if (baseType is IEdmEntityType baseEntity)
                {
                    this.AddEntityToEmit(baseEntity);
                }
                else
                {
                    this.AddComplexToEmit(baseType as IEdmComplexType);
                }
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
            static IEnumerable<string> extractComments(IEnumerable<XNode> childNodes)
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

            if (this.usesNamespaces && this.noteMap.Any())
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

                if (IsUnresolved(navProp.Type.Definition))
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
                this.AddEntityToEmit(entityTarget);

                var navType = navProp.ContainsTarget ? "*" : string.Empty;
                CalculatePropertyCardinalityText(navProp, isCollection, out var cardinalityMin, out var cardinalityMax);
                this.WriteLine(
                    $@"{this.GetTypeName(entity)} {navType}--> ""{cardinalityMin}..{cardinalityMax}"" {this.GetTypeName(entityTarget)}: {navProp.Name}");
            }

            // TODO: Emit a (custom?) nav line for types refered to in operation parameters.
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
                IEdmEntityType entityType = singleton.EntityType();
                var singletonTypeName = this.GetTypeName(entityType);
                members.Add($"+{singleton.Name}: {singletonTypeName}");
                this.WriteLine($@"{ecName} .. ""1..1"" {singletonTypeName}: {singleton.Name}");
                this.AddEntityToEmit(entityType);
            }

            foreach (var entitySet in this.model.EntityContainer.Elements.OfType<IEdmEntitySet>())
            {
                IEdmEntityType entityType = entitySet.EntityType();
                var entitySetTypeName = this.GetTypeName(entityType);
                members.Add($"+{entitySet.Name}: {entitySetTypeName}");
                this.WriteLine(
                    $@"{ecName} .. ""0..*"" {StripCollection(entitySetTypeName)}: {entitySet.Name}");
                this.AddEntityToEmit(entityType);
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
            List<IEdmOperation> allOperations = new List<IEdmOperation>();
            allOperations.AddRange(this.model.SchemaElements.OfType<IEdmOperation>().Where(o => o.IsBound));
            foreach (IEdmModel refModel in this.model.ReferencedModels)
            {
                allOperations.AddRange(refModel.SchemaElements.OfType<IEdmOperation>().Where(o => o.IsBound));
            }

            // Collate the bound actions and functions against their bound elements.
            foreach (var operation in allOperations)
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
        private static bool IsUnresolved(IEdmType type)
        {
            type = type.AsElementType();
            if (!(type is IEdmNamedElement named))
            {
                return true;
            }

            string name = named.Name;
            return type.Errors().Any(e =>
                (e.ErrorCode == EdmErrorCode.BadUnresolvedEntityType
                || e.ErrorCode == EdmErrorCode.BadUnresolvedComplexType
                || e.ErrorCode == EdmErrorCode.BadUnresolvedEnumType) && 
                e.ErrorMessage.Contains(name));
        }

        private static string StripCollection(string name)
        {
            if (name.Contains(CollectionPrefix))
            {
                name = name.Replace(CollectionPrefix, string.Empty);
                name = name[0..^1];
            }

            return name;
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
            return theType switch
            {
                IEdmComplexType _ => "#Skyblue",
                IEdmEntityType _ => "#PaleGreen",
                _ => string.Empty,
            };
        }
    }
}

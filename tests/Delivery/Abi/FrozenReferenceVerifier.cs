using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AgentBridge.Delivery.Abi.Tests;

/// <summary>Разрешает ссылки неперекомпилированного consumer через symbols новой runtime closure.</summary>
internal class FrozenReferenceVerifier
{
    private readonly IAssemblySymbol _consumer;
    private readonly Dictionary<string, IAssemblySymbol> _assemblies;
    private readonly AbiSignatureProvider _types;
    internal CSharpCompilation Compilation { get; }

    /// <summary>Читает только PE metadata; все переданные references принадлежат одному проверенному kit.</summary>
    internal FrozenReferenceVerifier(byte[] consumer, IEnumerable<MetadataReference> references)
    {
        PortableExecutableReference frozen = MetadataReference.CreateFromImage(consumer);
        Compilation = CSharpCompilation.Create("AbiVerifier", references: references.Append(frozen),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                metadataImportOptions: MetadataImportOptions.All));
        _consumer = (IAssemblySymbol)Compilation.GetAssemblyOrModuleSymbol(frozen)!;
        _assemblies = Compilation.References.Select(reference => Compilation.GetAssemblyOrModuleSymbol(reference))
            .OfType<IAssemblySymbol>().ToDictionary(assembly => assembly.Identity.Name, StringComparer.OrdinalIgnoreCase);
        _types = new(this);
    }

    /// <summary>Проверяет каждый type/member/spec handle, а не только public поверхность consumer.</summary>
    internal (int Types, int Members, int Specifications) Verify(byte[] consumer)
    {
        using MemoryStream stream = new(consumer, writable: false);
        using PEReader pe = new(stream);
        MetadataReader reader = pe.GetMetadataReader();
        Dictionary<EntityHandle, HashSet<MethodDefinitionHandle>> sites = ConsumerReferenceSites.Read(pe, reader);

        foreach (AssemblyReferenceHandle handle in reader.AssemblyReferences)
        {
            VerifyAssembly(reader, handle);
        }

        foreach (TypeReferenceHandle handle in reader.TypeReferences)
        {
            RequireAccessible(ResolveReference(reader, handle));
        }

        foreach (MemberReferenceHandle handle in reader.MemberReferences)
        {
            foreach ((INamedTypeSymbol? owner, IMethodSymbol? method) in Contexts(reader, sites, handle))
            {
                ResolveMember(reader, handle, (owner, method));
            }
        }

        int specifications = reader.GetTableRowCount(System.Reflection.Metadata.Ecma335.TableIndex.MethodSpec);
        for (int row = 1; row <= specifications; row++)
        {
            MethodSpecification specification = reader.GetMethodSpecification(
                System.Reflection.Metadata.Ecma335.MetadataTokens.MethodSpecificationHandle(row));
            EntityHandle specificationHandle = MetadataTokens.MethodSpecificationHandle(row);
            foreach ((INamedTypeSymbol? owner, IMethodSymbol? caller) in Contexts(reader, sites, specificationHandle))
            {
                IMethodSymbol method = ResolveMethod(reader, specification.Method, (owner, caller));
                ITypeSymbol[] arguments = specification.DecodeSignature(_types, (owner, caller))
                    .Select(argument => argument.Symbol).ToArray();
                if (arguments.Length != method.Arity)
                {
                    throw new InvalidOperationException("Generic instantiation arity mismatch.");
                }

                IMethodSymbol constructed = method.Construct(arguments);
                AbiGenericConstraints.Verify(Compilation, constructed);
            }
        }

        return (reader.TypeReferences.Count, reader.MemberReferences.Count, specifications);
    }

    /// <summary>Ищет declaring assembly и поддерживает настоящий type forwarding, без глобального поиска одноимённого типа.</summary>
    internal INamedTypeSymbol ResolveReference(MetadataReader reader, TypeReferenceHandle handle)
    {
        TypeReference type = reader.GetTypeReference(handle);
        if (type.ResolutionScope.Kind == HandleKind.TypeReference)
        {
            INamedTypeSymbol owner = ResolveReference(reader, (TypeReferenceHandle)type.ResolutionScope);
            return owner.GetTypeMembers().SingleOrDefault(child => child.MetadataName == reader.GetString(type.Name))
                ?? throw new InvalidOperationException("Unresolved nested type: " + reader.GetString(type.Name));
        }

        IAssemblySymbol assembly = _consumer;
        if (type.ResolutionScope.Kind == HandleKind.AssemblyReference)
        {
            string name = reader.GetString(reader.GetAssemblyReference((AssemblyReferenceHandle)type.ResolutionScope).Name);
            assembly = GetAssembly(name);
        }
        else if (type.ResolutionScope.Kind is not (HandleKind.ModuleDefinition or HandleKind.ModuleReference))
        {
            throw new NotSupportedException("Unsupported TypeRef resolution scope.");
        }

        string fullName = reader.GetString(type.Namespace) + "." + reader.GetString(type.Name);
        fullName = fullName.TrimStart('.');
        INamedTypeSymbol? resolved = assembly.GetTypeByMetadataName(fullName);
        if (resolved is null)
        {
            resolved = assembly.ResolveForwardedType(fullName);
        }

        return resolved ?? throw new InvalidOperationException($"Unresolved TypeRef: {assembly.Identity.Name}:{fullName}");
    }

    /// <summary>Разрешает собственные compiler-generated типы frozen consumer через его metadata.</summary>
    internal INamedTypeSymbol ResolveDefinition(MetadataReader reader, TypeDefinitionHandle handle)
    {
        TypeDefinition type = reader.GetTypeDefinition(handle);
        if (!type.GetDeclaringType().IsNil)
        {
            return ResolveDefinition(reader, type.GetDeclaringType()).GetTypeMembers()
                .Single(child => child.MetadataName == reader.GetString(type.Name));
        }

        string name = (reader.GetString(type.Namespace) + "." + reader.GetString(type.Name)).TrimStart('.');
        return _consumer.GetTypeByMetadataName(name) ?? throw new InvalidOperationException("Unresolved consumer type: " + name);
    }

    /// <summary>Сохраняет identity и проверяет допустимую версию зависимости; новый API не заменяет сборку с другим ключом.</summary>
    private void VerifyAssembly(MetadataReader reader, AssemblyReferenceHandle handle)
    {
        AssemblyReference reference = reader.GetAssemblyReference(handle);
        IAssemblySymbol resolved = GetAssembly(reader.GetString(reference.Name));
        byte[] expectedToken = reader.GetBlobBytes(reference.PublicKeyOrToken);
        if ((reference.Flags & AssemblyFlags.PublicKey) != 0)
        {
            expectedToken = SHA1.HashData(expectedToken)[^8..].Reverse().ToArray();
        }

        if (resolved.Identity.Version < reference.Version || resolved.Identity.CultureName != reader.GetString(reference.Culture) ||
            !resolved.Identity.PublicKeyToken.SequenceEqual(expectedToken))
        {
            throw new InvalidOperationException("Assembly identity mismatch: " + reader.GetString(reference.Name));
        }
    }

    /// <summary>Не использует assemblies test runner как запасной источник зависимостей.</summary>
    private IAssemblySymbol GetAssembly(string name)
    {
        if (!_assemblies.TryGetValue(name, out IAssemblySymbol? assembly))
        {
            throw new InvalidOperationException("Unresolved assembly: " + name);
        }

        return assembly;
    }

    /// <summary>Проверяет точный owner, static/instance, arity, return, параметры и modifiers.</summary>
    private ISymbol ResolveMember(MetadataReader reader, MemberReferenceHandle handle,
        (INamedTypeSymbol? Owner, IMethodSymbol? Method) context = default)
    {
        MemberReference reference = reader.GetMemberReference(handle);
        string name = reader.GetString(reference.Name);
        INamedTypeSymbol owner = ResolveOwner(reader, reference.Parent, context);
        foreach (INamedTypeSymbol declaring in Owners(owner, name))
        {
            if (reference.GetKind() == MemberReferenceKind.Field)
            {
                AbiTypeSignature expected = reference.DecodeFieldSignature(_types, (declaring, (IMethodSymbol?)null));
                foreach (IFieldSymbol field in declaring.GetMembers(name).OfType<IFieldSymbol>())
                {
                    if (expected.Matches(field.Type, false, AbiSignatureProvider.Modifiers(field.CustomModifiers)))
                    {
                        RequireAccessible(field);
                        return field;
                    }
                }
            }
            else
            {
                foreach (IMethodSymbol method in Methods(declaring, name))
                {
                    BlobReader signatureReader = reader.GetBlobReader(reference.Signature);
                    SignatureHeader header = signatureReader.ReadSignatureHeader();
                    int arity = 0;
                    if (header.IsGeneric)
                    {
                        arity = signatureReader.ReadCompressedInteger();
                    }

                    if (method.Arity != arity)
                    {
                        continue;
                    }

                    MethodSignature<AbiTypeSignature> expected = reference.DecodeMethodSignature(_types, (declaring, method));
                    if (Matches(expected, method))
                    {
                        RequireAccessible(method);
                        return method;
                    }
                }
            }
        }

        throw new InvalidOperationException($"Unresolved MemberRef: {owner.ToDisplayString()}.{name}");
    }

    /// <summary>MethodSpec может ссылаться на external MemberRef либо собственный MethodDef consumer.</summary>
    private IMethodSymbol ResolveMethod(MetadataReader reader, EntityHandle handle,
        (INamedTypeSymbol? Owner, IMethodSymbol? Method) context = default)
    {
        if (handle.Kind == HandleKind.MemberReference)
        {
            return (IMethodSymbol)ResolveMember(reader, (MemberReferenceHandle)handle, context);
        }

        if (handle.Kind != HandleKind.MethodDefinition)
        {
            throw new NotSupportedException("Unsupported MethodSpec target.");
        }

        MethodDefinition definition = reader.GetMethodDefinition((MethodDefinitionHandle)handle);
        INamedTypeSymbol owner = ResolveDefinition(reader, definition.GetDeclaringType());
        return Methods(owner, reader.GetString(definition.Name)).Single(method =>
            Matches(definition.DecodeSignature(_types, (owner, method)), method));
    }

    /// <summary>Подставляет concrete generic owner из TypeSpec до проверки его членов.</summary>
    private INamedTypeSymbol ResolveOwner(MetadataReader reader, EntityHandle handle,
        (INamedTypeSymbol? Owner, IMethodSymbol? Method) context)
    {
        return handle.Kind switch
        {
            HandleKind.TypeReference => ResolveReference(reader, (TypeReferenceHandle)handle),
            HandleKind.TypeDefinition => ResolveDefinition(reader, (TypeDefinitionHandle)handle),
            HandleKind.TypeSpecification => (INamedTypeSymbol)reader.GetTypeSpecification((TypeSpecificationHandle)handle)
                .DecodeSignature(_types, context).Symbol,
            _ => throw new NotSupportedException("Unsupported MemberRef parent.")
        };
    }

    /// <summary>Использует каждый реальный IL call site, а не guessed generic parameters target.</summary>
    private IEnumerable<(INamedTypeSymbol? Owner, IMethodSymbol? Method)> Contexts(MetadataReader reader,
        Dictionary<EntityHandle, HashSet<MethodDefinitionHandle>> sites, EntityHandle handle)
    {
        if (!sites.TryGetValue(handle, out HashSet<MethodDefinitionHandle>? callers))
        {
            yield return default;
            yield break;
        }

        foreach (MethodDefinitionHandle caller in callers)
        {
            IMethodSymbol method = ResolveMethod(reader, caller);
            yield return (method.ContainingType, method);
        }
    }

    /// <summary>Разрешает наследуемые члены с подстановкой generic base; конструкторы не наследуются.</summary>
    private static IEnumerable<INamedTypeSymbol> Owners(INamedTypeSymbol owner, string name)
    {
        yield return owner;
        if (name is ".ctor" or ".cctor")
        {
            yield break;
        }

        for (INamedTypeSymbol? current = owner.BaseType; current is not null; current = current.BaseType)
        {
            yield return current;
        }

        if (owner.TypeKind == TypeKind.Interface)
        {
            foreach (INamedTypeSymbol contract in owner.AllInterfaces)
            {
                yield return contract;
            }
        }
    }

    /// <summary>Учитывает accessors properties/events, которые Roslyn представляет отдельными symbols.</summary>
    private static IEnumerable<IMethodSymbol> Methods(INamedTypeSymbol owner, string name)
    {
        HashSet<IMethodSymbol> seen = new(SymbolEqualityComparer.Default);
        foreach (ISymbol symbol in owner.GetMembers())
        {
            IEnumerable<IMethodSymbol?> candidates = symbol switch
            {
                IMethodSymbol method => [method],
                IPropertySymbol property => [property.GetMethod, property.SetMethod],
                IEventSymbol eventSymbol => [eventSymbol.AddMethod, eventSymbol.RemoveMethod, eventSymbol.RaiseMethod],
                _ => []
            };
            foreach (IMethodSymbol? method in candidates)
            {
                if (method?.MetadataName == name && seen.Add(method))
                {
                    yield return method;
                }
            }
        }
    }

    /// <summary>Сравнивает CLI подпись без исходных имён параметров и nullable annotations.</summary>
    private static bool Matches(MethodSignature<AbiTypeSignature> signature, IMethodSymbol method)
    {
        if (signature.GenericParameterCount != method.Arity || signature.Header.IsInstance == method.IsStatic ||
            signature.ParameterTypes.Length != method.Parameters.Length)
        {
            return false;
        }

        if (signature.Header.HasExplicitThis || method.IsVararg || signature.Header.CallingConvention != SignatureCallingConvention.Default)
        {
            throw new NotSupportedException("Non-default member calling convention is unsupported.");
        }

        string returnModifiers = AbiSignatureProvider.Modifiers(method.ReturnTypeCustomModifiers) +
            AbiSignatureProvider.Modifiers(method.RefCustomModifiers);
        if (!signature.ReturnType.Matches(method.ReturnType, method.ReturnsByRef || method.ReturnsByRefReadonly, returnModifiers))
        {
            return false;
        }

        for (int index = 0; index < method.Parameters.Length; index++)
        {
            IParameterSymbol parameter = method.Parameters[index];
            string modifiers = AbiSignatureProvider.Modifiers(parameter.CustomModifiers) + AbiSignatureProvider.Modifiers(parameter.RefCustomModifiers);
            if (!signature.ParameterTypes[index].Matches(parameter.Type, parameter.RefKind != RefKind.None, modifiers))
            {
                return false;
            }
        }

        return true;
    }


    /// <summary>Собственные private metadata доступны consumer; внешние ссылки требуют видимого контракта.</summary>
    private void RequireAccessible(ISymbol symbol)
    {
        if (SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, _consumer))
        {
            return;
        }

        if (symbol.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal))
        {
            throw new InvalidOperationException("Inaccessible external metadata: " + symbol.ToDisplayString());
        }

        if (symbol.ContainingType is { } owner)
        {
            RequireAccessible(owner);
        }
    }
}

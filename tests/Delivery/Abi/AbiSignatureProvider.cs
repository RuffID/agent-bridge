using System.Collections.Immutable;
using System.Reflection.Metadata;
using Microsoft.CodeAnalysis;

namespace AgentBridge.Delivery.Abi.Tests;

/// <summary>Декодирует CLI типы в symbols новой closure без загрузки целевых DLL.</summary>
internal class AbiSignatureProvider(FrozenReferenceVerifier resolver) :
    ISignatureTypeProvider<AbiTypeSignature, (INamedTypeSymbol? Owner, IMethodSymbol? Method)>
{
    /// <inheritdoc/>
    public AbiTypeSignature GetArrayType(AbiTypeSignature elementType, ArrayShape shape)
    {
        if (!shape.Sizes.IsEmpty || shape.LowerBounds.Any(bound => bound != 0))
        {
            throw new NotSupportedException("Explicit array bounds are not supported by this ABI verifier.");
        }

        return new(resolver.Compilation.CreateArrayTypeSymbol(elementType.Symbol, shape.Rank));
    }

    /// <inheritdoc/>
    public AbiTypeSignature GetByReferenceType(AbiTypeSignature elementType)
    {
        return elementType with { ByReference = true };
    }

    /// <inheritdoc/>
    public AbiTypeSignature GetFunctionPointerType(MethodSignature<AbiTypeSignature> signature)
    {
        throw new NotSupportedException("Function pointers require an explicit ABI verifier extension.");
    }

    /// <inheritdoc/>
    public AbiTypeSignature GetGenericInstantiation(AbiTypeSignature genericType, ImmutableArray<AbiTypeSignature> typeArguments)
    {
        INamedTypeSymbol definition = (INamedTypeSymbol)genericType.Symbol;
        if (typeArguments.Any(argument => argument.ByReference || argument.Modifiers.Length != 0))
        {
            throw new NotSupportedException("Modified generic arguments are unsupported.");
        }

        return new(ConstructNested(definition, typeArguments.Select(argument => argument.Symbol).ToArray()));
    }

    /// <inheritdoc/>
    public AbiTypeSignature GetGenericMethodParameter((INamedTypeSymbol? Owner, IMethodSymbol? Method) context, int index)
    {
        IMethodSymbol method = context.Method ?? throw new InvalidOperationException("Missing generic method context.");
        return new(method.TypeParameters[index]);
    }

    /// <inheritdoc/>
    public AbiTypeSignature GetGenericTypeParameter((INamedTypeSymbol? Owner, IMethodSymbol? Method) context, int index)
    {
        INamedTypeSymbol owner = context.Owner ?? throw new InvalidOperationException("Missing generic type context.");
        return new(TypeArguments(owner)[index]);
    }

    /// <inheritdoc/>
    public AbiTypeSignature GetModifiedType(AbiTypeSignature modifier, AbiTypeSignature unmodifiedType, bool isRequired)
    {
        return unmodifiedType with { Modifiers = unmodifiedType.Modifiers + Modifier(modifier.Symbol, isRequired) };
    }

    /// <inheritdoc/>
    public AbiTypeSignature GetPinnedType(AbiTypeSignature elementType)
    {
        throw new NotSupportedException("Pinned member signatures are unsupported.");
    }

    /// <inheritdoc/>
    public AbiTypeSignature GetPointerType(AbiTypeSignature elementType)
    {
        return new(resolver.Compilation.CreatePointerTypeSymbol(elementType.Symbol));
    }

    /// <inheritdoc/>
    public AbiTypeSignature GetPrimitiveType(PrimitiveTypeCode typeCode)
    {
        SpecialType special = typeCode switch
        {
            PrimitiveTypeCode.Void => SpecialType.System_Void,
            PrimitiveTypeCode.Boolean => SpecialType.System_Boolean,
            PrimitiveTypeCode.Char => SpecialType.System_Char,
            PrimitiveTypeCode.SByte => SpecialType.System_SByte,
            PrimitiveTypeCode.Byte => SpecialType.System_Byte,
            PrimitiveTypeCode.Int16 => SpecialType.System_Int16,
            PrimitiveTypeCode.UInt16 => SpecialType.System_UInt16,
            PrimitiveTypeCode.Int32 => SpecialType.System_Int32,
            PrimitiveTypeCode.UInt32 => SpecialType.System_UInt32,
            PrimitiveTypeCode.Int64 => SpecialType.System_Int64,
            PrimitiveTypeCode.UInt64 => SpecialType.System_UInt64,
            PrimitiveTypeCode.Single => SpecialType.System_Single,
            PrimitiveTypeCode.Double => SpecialType.System_Double,
            PrimitiveTypeCode.String => SpecialType.System_String,
            PrimitiveTypeCode.Object => SpecialType.System_Object,
            PrimitiveTypeCode.IntPtr => SpecialType.System_IntPtr,
            PrimitiveTypeCode.UIntPtr => SpecialType.System_UIntPtr,
            PrimitiveTypeCode.TypedReference => SpecialType.System_TypedReference,
            _ => throw new NotSupportedException("Unknown CLI primitive type.")
        };

        return new(resolver.Compilation.GetSpecialType(special));
    }

    /// <inheritdoc/>
    public AbiTypeSignature GetSZArrayType(AbiTypeSignature elementType)
    {
        return new(resolver.Compilation.CreateArrayTypeSymbol(elementType.Symbol));
    }

    /// <inheritdoc/>
    public AbiTypeSignature GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        return new(resolver.ResolveDefinition(reader, handle));
    }

    /// <inheritdoc/>
    public AbiTypeSignature GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        return new(resolver.ResolveReference(reader, handle));
    }

    /// <inheritdoc/>
    public AbiTypeSignature GetTypeFromSpecification(MetadataReader reader,
        (INamedTypeSymbol? Owner, IMethodSymbol? Method) context, TypeSpecificationHandle handle, byte rawTypeKind)
    {
        return reader.GetTypeSpecification(handle).DecodeSignature(this, context);
    }

    /// <summary>Сохраняет required/optional модификаторы в порядке CLI metadata.</summary>
    internal static string Modifiers(ImmutableArray<CustomModifier> modifiers)
    {
        return string.Concat(modifiers.Select(modifier => Modifier(modifier.Modifier, !modifier.IsOptional)));
    }

    /// <summary>Идентифицирует modifier вместе с фактически разрешённой сборкой.</summary>
    private static string Modifier(ITypeSymbol type, bool required)
    {
        return $"|{required}:{type.ContainingAssembly.Identity}:{type.ToDisplayString()}";
    }

    /// <summary>CLI нумерует outer и nested generic parameters общим списком.</summary>
    internal static ITypeSymbol[] TypeArguments(INamedTypeSymbol type)
    {
        if (type.ContainingType is { } owner)
        {
            return [..TypeArguments(owner), ..type.TypeArguments];
        }

        return type.TypeArguments.ToArray();
    }

    /// <summary>Roslyn конструирует outer отдельно, затем собственные arguments nested типа.</summary>
    private static INamedTypeSymbol ConstructNested(INamedTypeSymbol definition, ITypeSymbol[] arguments)
    {
        int outerCount = 0;
        if (definition.ContainingType is { } owner)
        {
            outerCount = TypeArguments(owner).Length;
            INamedTypeSymbol constructedOwner = ConstructNested(owner, arguments[..outerCount]);
            definition = constructedOwner.GetTypeMembers().Single(type => type.MetadataName == definition.MetadataName);
        }

        if (definition.Arity == 0)
        {
            return definition;
        }

        return definition.Construct(arguments[outerCount..]);
    }
}

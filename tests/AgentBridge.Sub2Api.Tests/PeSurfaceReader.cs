using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace AgentBridge.Sub2Api.Tests;

/// <summary>Семантическое чтение public/protected PE API без загрузки или выполнения целевых сборок.</summary>
internal static class PeSurfaceReader
{
    /// <summary>Декодирует типы сигнатур в assembly-qualified имена вместо сравнения локальных metadata tokens.</summary>
    internal static HashSet<string> Read(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using PEReader pe = new(stream);
        MetadataReader reader = pe.GetMetadataReader();
        SignatureNames names = new();
        HashSet<string> result = new(StringComparer.Ordinal)
        {
            "assembly:" + Identity(reader, reader.GetAssemblyDefinition())
        };
        foreach (TypeDefinitionHandle handle in reader.TypeDefinitions)
        {
            if (!Visible(reader, handle))
            {
                continue;
            }

            TypeDefinition type = reader.GetTypeDefinition(handle);
            string owner = names.GetTypeFromDefinition(reader, handle, 0);
            result.Add($"type:{owner}:{type.Attributes}:{EntityName(reader, type.BaseType, names)}");
            AddGenerics(reader, type.GetGenericParameters(), owner, names, result);
            foreach (InterfaceImplementationHandle implemented in type.GetInterfaceImplementations())
            {
                result.Add($"interface:{owner}:{EntityName(reader, reader.GetInterfaceImplementation(implemented).Interface, names)}");
            }

            foreach (MethodDefinitionHandle methodHandle in type.GetMethods())
            {
                MethodDefinition method = reader.GetMethodDefinition(methodHandle);
                MethodAttributes access = method.Attributes & MethodAttributes.MemberAccessMask;
                if (access is not (MethodAttributes.Public or MethodAttributes.Family or MethodAttributes.FamORAssem))
                {
                    continue;
                }

                string signature = $"method:{owner}:{reader.GetString(method.Name)}:{method.Attributes}:{Format(method.DecodeSignature(names, null))}";
                result.Add(signature);
                AddGenerics(reader, method.GetGenericParameters(), signature, names, result);
                foreach (ParameterHandle parameterHandle in method.GetParameters())
                {
                    Parameter parameter = reader.GetParameter(parameterHandle);
                    result.Add($"parameter:{signature}:{parameter.SequenceNumber}:{reader.GetString(parameter.Name)}:{parameter.Attributes}:{Constant(reader, parameter.GetDefaultValue())}");
                }
            }

            foreach (FieldDefinitionHandle fieldHandle in type.GetFields())
            {
                FieldDefinition field = reader.GetFieldDefinition(fieldHandle);
                FieldAttributes access = field.Attributes & FieldAttributes.FieldAccessMask;
                if (access is FieldAttributes.Public or FieldAttributes.Family or FieldAttributes.FamORAssem)
                {
                    result.Add($"field:{owner}:{reader.GetString(field.Name)}:{field.Attributes}:{field.DecodeSignature(names, null)}:{Constant(reader, field.GetDefaultValue())}");
                }
            }
        }

        return result;
    }

    /// <summary>Видимость вложенного типа ограничивается видимостью владельца.</summary>
    private static bool Visible(MetadataReader reader, TypeDefinitionHandle handle)
    {
        TypeDefinition type = reader.GetTypeDefinition(handle);
        TypeAttributes access = type.Attributes & TypeAttributes.VisibilityMask;
        if (access == TypeAttributes.Public)
        {
            return true;
        }

        if (access is TypeAttributes.NestedPublic or TypeAttributes.NestedFamily or TypeAttributes.NestedFamORAssem)
        {
            return Visible(reader, type.GetDeclaringType());
        }

        return false;
    }

    /// <summary>Сохраняет generic constraints и variance без зависимости от порядка токенов.</summary>
    private static void AddGenerics(MetadataReader reader, GenericParameterHandleCollection handles, string owner,
        SignatureNames names, HashSet<string> result)
    {
        foreach (GenericParameterHandle handle in handles)
        {
            GenericParameter parameter = reader.GetGenericParameter(handle);
            string key = $"generic:{owner}:{parameter.Index}:{parameter.Attributes}:{reader.GetString(parameter.Name)}";
            result.Add(key);
            foreach (GenericParameterConstraintHandle constraint in parameter.GetConstraints())
            {
                result.Add(key + ":" + EntityName(reader, reader.GetGenericParameterConstraint(constraint).Type, names));
            }
        }
    }

    /// <summary>Читает декларативное default/enum значение, не выполняя static constructors.</summary>
    private static string Constant(MetadataReader reader, ConstantHandle handle)
    {
        if (handle.IsNil)
        {
            return "absent";
        }

        Constant value = reader.GetConstant(handle);
        return value.TypeCode + ":" + Convert.ToHexString(reader.GetBlobBytes(value.Value));
    }

    /// <summary>Разрешает definition/reference/specification независимо от номера строки metadata.</summary>
    private static string EntityName(MetadataReader reader, EntityHandle handle, SignatureNames names)
    {
        if (handle.IsNil)
        {
            return "none";
        }

        return handle.Kind switch
        {
            HandleKind.TypeDefinition => names.GetTypeFromDefinition(reader, (TypeDefinitionHandle)handle, 0),
            HandleKind.TypeReference => names.GetTypeFromReference(reader, (TypeReferenceHandle)handle, 0),
            HandleKind.TypeSpecification => names.GetTypeFromSpecification(reader, null, (TypeSpecificationHandle)handle, 0),
            _ => throw new InvalidOperationException("Unexpected type handle")
        };
    }

    /// <summary>Фиксирует instance/calling convention, generic arity, return и все параметры.</summary>
    private static string Format(MethodSignature<string> signature)
    {
        return $"{signature.Header.RawValue}:{signature.GenericParameterCount}:{signature.RequiredParameterCount}:{signature.ReturnType}({string.Join(",", signature.ParameterTypes)})";
    }

    /// <summary>Возвращает полную собственную identity, включая ключ и culture.</summary>
    private static string Identity(MetadataReader reader, AssemblyDefinition assembly)
    {
        return $"{reader.GetString(assembly.Name)}/{assembly.Version}/{reader.GetString(assembly.Culture)}/{Convert.ToHexString(reader.GetBlobBytes(assembly.PublicKey))}";
    }

    /// <summary>Декодирует формы CLI сигнатур, не загружая referenced assemblies.</summary>
    private class SignatureNames : ISignatureTypeProvider<string, object?>
    {
        /// <inheritdoc/>
        public string GetArrayType(string elementType, ArrayShape shape)
        {
            return $"{elementType}[{shape.Rank};{string.Join(",", shape.Sizes)};{string.Join(",", shape.LowerBounds)}]";
        }

        /// <inheritdoc/>
        public string GetByReferenceType(string elementType)
        {
            return elementType + "&";
        }

        /// <inheritdoc/>
        public string GetFunctionPointerType(MethodSignature<string> signature)
        {
            return "fn:" + Format(signature);
        }

        /// <inheritdoc/>
        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
        {
            return genericType + "<" + string.Join(",", typeArguments) + ">";
        }

        /// <inheritdoc/>
        public string GetGenericMethodParameter(object? genericContext, int index)
        {
            return "!!" + index;
        }

        /// <inheritdoc/>
        public string GetGenericTypeParameter(object? genericContext, int index)
        {
            return "!" + index;
        }

        /// <inheritdoc/>
        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired)
        {
            return $"{unmodifiedType} mod({isRequired},{modifier})";
        }

        /// <inheritdoc/>
        public string GetPinnedType(string elementType)
        {
            return elementType + " pinned";
        }

        /// <inheritdoc/>
        public string GetPointerType(string elementType)
        {
            return elementType + "*";
        }

        /// <inheritdoc/>
        public string GetPrimitiveType(PrimitiveTypeCode typeCode)
        {
            return typeCode.ToString();
        }

        /// <inheritdoc/>
        public string GetSZArrayType(string elementType)
        {
            return elementType + "[]";
        }

        /// <inheritdoc/>
        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            TypeDefinition type = reader.GetTypeDefinition(handle);
            if (!type.GetDeclaringType().IsNil)
            {
                return GetTypeFromDefinition(reader, type.GetDeclaringType(), 0) + "+" + reader.GetString(type.Name);
            }

            return $"[{reader.GetString(reader.GetAssemblyDefinition().Name)}]{reader.GetString(type.Namespace)}.{reader.GetString(type.Name)}";
        }

        /// <inheritdoc/>
        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            TypeReference type = reader.GetTypeReference(handle);
            if (type.ResolutionScope.Kind == HandleKind.TypeReference)
            {
                return GetTypeFromReference(reader, (TypeReferenceHandle)type.ResolutionScope, 0) + "+" + reader.GetString(type.Name);
            }

            string assembly = reader.GetString(reader.GetAssemblyDefinition().Name);
            if (type.ResolutionScope.Kind == HandleKind.AssemblyReference)
            {
                AssemblyReference reference = reader.GetAssemblyReference((AssemblyReferenceHandle)type.ResolutionScope);
                assembly = reader.GetString(reference.Name);
            }

            return $"[{assembly}]{reader.GetString(type.Namespace)}.{reader.GetString(type.Name)}";
        }

        /// <inheritdoc/>
        public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
        {
            return reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
        }
    }
}

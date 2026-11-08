using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AgentBridge.Delivery.Abi.Tests;

/// <summary>Проверяет ограничения конкретных generic arguments после разрешения CLI ссылки.</summary>
internal static class AbiGenericConstraints
{
    /// <summary>Проверяет generic constraints конкретного MethodSpec после разрешения метода.</summary>
    internal static void Verify(CSharpCompilation compilation, IMethodSymbol method)
    {
        for (int index = 0; index < method.Arity; index++)
        {
            ITypeParameterSymbol parameter = method.TypeParameters[index];
            ITypeSymbol argument = method.TypeArguments[index];
            if (argument.TypeKind == TypeKind.Error)
            {
                throw new InvalidOperationException("Unresolved MethodSpec argument.");
            }

            if (parameter.HasReferenceTypeConstraint && !argument.IsReferenceType ||
                parameter.HasValueTypeConstraint && !argument.IsValueType ||
                parameter.HasUnmanagedTypeConstraint && !argument.IsUnmanagedType)
            {
                throw new InvalidOperationException("Generic type category constraint mismatch.");
            }

            if (parameter.HasConstructorConstraint && !argument.IsValueType)
            {
                bool valid = argument is ITypeParameterSymbol { HasConstructorConstraint: true };
                if (argument is INamedTypeSymbol named)
                {
                    valid = !named.IsAbstract && named.InstanceConstructors.Any(constructor =>
                        constructor.Parameters.IsEmpty && constructor.DeclaredAccessibility == Accessibility.Public);
                }

                if (!valid)
                {
                    throw new InvalidOperationException("Generic constructor constraint mismatch.");
                }
            }

            foreach (ITypeSymbol constraint in parameter.ConstraintTypes)
            {
                ITypeSymbol substituted = Substitute(constraint, method);
                Microsoft.CodeAnalysis.CSharp.Conversion conversion = compilation.ClassifyConversion(argument, substituted);
                if (!conversion.IsIdentity && !conversion.IsReference && !conversion.IsBoxing)
                {
                    throw new InvalidOperationException("Generic type constraint mismatch.");
                }
            }
        }
    }

    /// <summary>Подставляет concrete method arguments в зависимые generic constraints.</summary>
    private static ITypeSymbol Substitute(ITypeSymbol type, IMethodSymbol method)
    {
        if (type is ITypeParameterSymbol parameter)
        {
            if (parameter.TypeParameterKind == TypeParameterKind.Method)
            {
                return method.TypeArguments[parameter.Ordinal];
            }

            return AbiSignatureProvider.TypeArguments(method.ContainingType)[parameter.Ordinal];
        }

        if (type is INamedTypeSymbol { IsGenericType: true } named)
        {
            return named.OriginalDefinition.Construct(named.TypeArguments.Select(argument => Substitute(argument, method)).ToArray());
        }

        return type;
    }

}

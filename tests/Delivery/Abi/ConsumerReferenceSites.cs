using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace AgentBridge.Delivery.Abi.Tests;

/// <summary>Находит declaring method каждого IL reference для корректного generic caller context.</summary>
internal static class ConsumerReferenceSites
{
    /// <summary>Читает IL operands без выполнения кода, включая MemberRef под MethodSpec.</summary>
    internal static Dictionary<EntityHandle, HashSet<MethodDefinitionHandle>> Read(PEReader pe, MetadataReader reader)
    {
        Dictionary<ushort, OpCode> opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(OpCode)).Select(field => (OpCode)field.GetValue(null)!)
            .ToDictionary(opcode => unchecked((ushort)opcode.Value));
        Dictionary<EntityHandle, HashSet<MethodDefinitionHandle>> sites = [];
        foreach (TypeDefinitionHandle typeHandle in reader.TypeDefinitions)
        {
            foreach (MethodImplementationHandle implementationHandle in reader.GetTypeDefinition(typeHandle).GetMethodImplementations())
            {
                MethodImplementation implementation = reader.GetMethodImplementation(implementationHandle);
                if (implementation.MethodBody.Kind != HandleKind.MethodDefinition)
                {
                    throw new NotSupportedException("External MethodImpl body is unsupported.");
                }

                Add(sites, implementation.MethodDeclaration, (MethodDefinitionHandle)implementation.MethodBody);
            }
        }

        foreach (MethodDefinitionHandle methodHandle in reader.MethodDefinitions)
        {
            MethodDefinition method = reader.GetMethodDefinition(methodHandle);
            if (method.RelativeVirtualAddress == 0)
            {
                continue;
            }

            byte[] il = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()
                ?? throw new BadImageFormatException("Missing method body.");
            int offset = 0;
            while (offset < il.Length)
            {
                ushort value = il[offset++];
                if (value == 0xfe)
                {
                    value = (ushort)(0xfe00 | il[offset++]);
                }

                OpCode opcode = opcodes[value];
                if (opcode.OperandType is OperandType.InlineMethod or OperandType.InlineField or OperandType.InlineType or OperandType.InlineTok)
                {
                    EntityHandle reference = MetadataTokens.EntityHandle(BitConverter.ToInt32(il, offset));
                    Add(sites, reference, methodHandle);
                    if (reference.Kind == HandleKind.MethodSpecification)
                    {
                        Add(sites, reader.GetMethodSpecification((MethodSpecificationHandle)reference).Method, methodHandle);
                    }
                }

                int size = opcode.OperandType switch
                {
                    OperandType.InlineNone => 0,
                    OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                    OperandType.InlineVar => 2,
                    OperandType.InlineI8 or OperandType.InlineR => 8,
                    OperandType.InlineSwitch => checked(4 + 4 * BitConverter.ToInt32(il, offset)),
                    OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineI or OperandType.InlineMethod or
                        OperandType.InlineSig or OperandType.InlineString or OperandType.InlineTok or OperandType.InlineType or OperandType.ShortInlineR => 4,
                    _ => throw new NotSupportedException("Unsupported IL operand kind.")
                };
                offset = checked(offset + size);
                if (offset > il.Length)
                {
                    throw new BadImageFormatException("Truncated IL operand.");
                }
            }
        }

        return sites;
    }

    /// <summary>Одна metadata ссылка может использоваться несколькими generic methods.</summary>
    private static void Add(Dictionary<EntityHandle, HashSet<MethodDefinitionHandle>> sites, EntityHandle reference, MethodDefinitionHandle method)
    {
        if (!sites.TryGetValue(reference, out HashSet<MethodDefinitionHandle>? methods))
        {
            methods = [];
            sites.Add(reference, methods);
        }

        methods.Add(method);
    }
}

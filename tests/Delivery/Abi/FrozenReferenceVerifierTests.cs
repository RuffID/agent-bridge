using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace AgentBridge.Delivery.Abi.Tests;

/// <summary>Независимые synthetic PE controls: verifier должен замечать реально сломанные references.</summary>
public class FrozenReferenceVerifierTests
{
    private const string API = """
        public class Port {
            public int Count;
            public string Read(int value) => value.ToString();
            public static T Echo<T>(T value) where T : class => value;
        }
        """;
    private const string CONSUMER = """
        public class Consumer {
            public string Run() {
                Port port = new Port();
                port.Count = 1;
                return Port.Echo<string>(port.Read(port.Count));
            }
        }
        """;

    /// <summary>Положительный контроль включает поле, instance member и concrete generic MethodSpec.</summary>
    [Fact]
    public void CompatibleReferencesResolve()
    {
        // Arrange
        IReadOnlyList<MetadataReference> framework = AbiEvidence.References();
        byte[] api = Emit("SyntheticApi", API, framework);
        MetadataReference reference = MetadataReference.CreateFromImage(api);
        byte[] consumer = Emit("SyntheticConsumer", CONSUMER, framework.Append(reference));
        FrozenReferenceVerifier verifier = new(consumer, framework.Append(reference));

        // Act
        (int types, int members, int specifications) = verifier.Verify(consumer);

        // Assert
        Assert.True(types > 0);
        Assert.True(members > 0);
        Assert.Equal(1, specifications);
    }

    /// <summary>Generic caller определяет !0/!!0 в IL references; их нельзя угадывать по вызываемому методу.</summary>
    [Fact]
    public void GenericCallerContextResolves()
    {
        // Arrange
        IReadOnlyList<MetadataReference> framework = AbiEvidence.References();
        byte[] api = Emit("SyntheticApi", API + "public class Holder<T> { public T Value; }", framework);
        MetadataReference reference = MetadataReference.CreateFromImage(api);
        const string SOURCE = """
            public class Consumer<T> where T : class {
                public T Run(Holder<T> holder) => Port.Echo<T>(holder.Value);
            }
            """;
        byte[] consumer = Emit("SyntheticConsumer", SOURCE, framework.Append(reference));
        FrozenReferenceVerifier verifier = new(consumer, framework.Append(reference));

        // Act
        (int types, int members, int specifications) = verifier.Verify(consumer);

        // Assert
        Assert.True(types > 0);
        Assert.True(members > 0);
        Assert.Equal(1, specifications);
    }

    /// <summary>Удаление, недоступность и несовместимые сигнатуры не принимаются как успешное разрешение ABI.</summary>
    [Theory]
    [InlineData("type")]
    [InlineData("member")]
    [InlineData("parameter")]
    [InlineData("return")]
    [InlineData("static")]
    [InlineData("access")]
    [InlineData("arity")]
    [InlineData("constraint")]
    [InlineData("field")]
    [InlineData("field-type")]
    public void IncompatibleReferencesFail(string change)
    {
        // Arrange
        IReadOnlyList<MetadataReference> framework = AbiEvidence.References();
        byte[] oldApi = Emit("SyntheticApi", API, framework);
        byte[] consumer = Emit("SyntheticConsumer", CONSUMER, framework.Append(MetadataReference.CreateFromImage(oldApi)));
        string changed = change switch
        {
            "type" => API.Replace("class Port", "class RemovedPort", StringComparison.Ordinal),
            "member" => API.Replace("Read(int", "RemovedRead(int", StringComparison.Ordinal),
            "parameter" => API.Replace("Read(int", "Read(long", StringComparison.Ordinal),
            "return" => API.Replace("string Read", "object Read", StringComparison.Ordinal),
            "static" => API.Replace("public string Read", "public static string Read", StringComparison.Ordinal),
            "access" => API.Replace("public string Read", "private string Read", StringComparison.Ordinal),
            "arity" => API.Replace("Echo<T>", "Echo<T, TExtra>", StringComparison.Ordinal),
            "constraint" => API.Replace("where T : class", "where T : struct", StringComparison.Ordinal),
            "field" => API.Replace("int Count", "int RemovedCount", StringComparison.Ordinal),
            "field-type" => API.Replace("int Count", "long Count", StringComparison.Ordinal),
            _ => throw new InvalidOperationException("Unknown negative control.")
        };
        byte[] newApi = Emit("SyntheticApi", changed, framework);
        FrozenReferenceVerifier verifier = new(consumer, framework.Append(MetadataReference.CreateFromImage(newApi)));

        // Act
        Exception? error = Record.Exception(() => verifier.Verify(consumer));

        // Assert
        Assert.IsType<InvalidOperationException>(error);
    }

    /// <summary>Компилирует synthetic DLL только в памяти, без выполнения сгенерированного кода.</summary>
    private static byte[] Emit(string name, string source, IEnumerable<MetadataReference> references)
    {
        CSharpCompilation compilation = CSharpCompilation.Create(name, [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using MemoryStream stream = new();
        Microsoft.CodeAnalysis.Emit.EmitResult result = compilation.Emit(stream);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));

        return stream.ToArray();
    }
}

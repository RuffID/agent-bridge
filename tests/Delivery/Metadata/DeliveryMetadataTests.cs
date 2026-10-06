using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace AgentBridge.Delivery.Metadata.Tests;

/// <summary>Читает только PE/XML поставки; код AgentBridge и native библиотеки не загружаются и не исполняются.</summary>
public class DeliveryMetadataTests
{
    /// <summary>Все managed assembly references удовлетворяются комплектом либо .NET runtime; лишний tooling не поставляется.</summary>
    [Theory]
    [InlineData("SqlServer", "win-x64")]
    [InlineData("SqlServer", "linux-x64")]
    [InlineData("SqlServer", "linux-arm64")]
    [InlineData("Sqlite", "win-x64")]
    [InlineData("PostgreSql", "win-x64")]
    public void ManagedClosureIsComplete(string provider, string rid)
    {
        string directory = Path.Combine(GetRoot(), provider, rid, "lib");
        Dictionary<string, string> files = Directory.GetFiles(directory, "*.dll")
            .ToDictionary(path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> framework = GetFrameworkFiles();
        string resourceDirectory = Path.Combine(GetRoot(), provider, rid, "resources");
        foreach (string file in files.Values.Concat(Directory.GetFiles(resourceDirectory, "*.dll", SearchOption.AllDirectories)))
        {
            using FileStream stream = File.OpenRead(file);
            using PEReader pe = new(stream);
            MetadataReader metadata = pe.GetMetadataReader();
            foreach (AssemblyReferenceHandle handle in metadata.AssemblyReferences)
            {
                AssemblyReference reference = metadata.GetAssemblyReference(handle);
                string name = metadata.GetString(reference.Name);
                Assert.True(files.ContainsKey(name) || framework.ContainsKey(name), $"{file}: missing {name}");
                string resolved = files.TryGetValue(name, out string? delivered) ? delivered : framework[name];
                using FileStream dependencyStream = File.OpenRead(resolved);
                using PEReader dependencyPe = new(dependencyStream);
                Assert.True(dependencyPe.GetMetadataReader().GetAssemblyDefinition().Version >= reference.Version,
                    $"{file}: lower assembly version for {name}");
            }
        }
        Assert.Contains($"AgentBridge.Persistence.Migrations.{provider}", files.Keys);
        Assert.Single(files.Keys, name => name.StartsWith("AgentBridge.Persistence.Migrations.", StringComparison.Ordinal));
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore.Design", files.Keys);
        Assert.DoesNotContain("Microsoft.CodeAnalysis", files.Keys);
        Assert.DoesNotContain("AgentBridge.Delivery", files.Keys);
        Assert.Contains("AgentBridge.Integration", files.Keys);
        Assert.Contains("EFCoreLibrary.Maintenance.Sqlite", files.Keys);
        Assert.Contains("EFCoreLibrary.Maintenance.PostgreSql", files.Keys);
        Assert.Contains("EFCoreLibrary.Maintenance.SqlServer", files.Keys);
        Assert.Contains("Microsoft.Data.SqlClient", files.Keys);
        foreach (string name in new[] { "O200kBase", "Cl100kBase" })
        {
            using FileStream stream = File.OpenRead(files[$"Microsoft.ML.Tokenizers.Data.{name}"]);
            using PEReader pe = new(stream);
            Assert.NotEmpty(pe.GetMetadataReader().ManifestResources);
        }
        string nativeDirectory = Path.Combine(GetRoot(), provider, rid, "native", rid);
        string sqliteFile = rid == "win-x64" ? "e_sqlite3.dll" : "libe_sqlite3.so";
        Assert.True(File.Exists(Path.Combine(nativeDirectory, sqliteFile)));
        foreach (string nativeFile in Directory.GetFiles(nativeDirectory))
        {
            NativeAssetMetadata.Validate(File.ReadAllBytes(nativeFile), rid);
        }
    }

    /// <summary>Потребитель находит русский summary интерфейса и inheritdoc реализации по metadata без исходников.</summary>
    [Theory]
    [InlineData("SqlServer", "win-x64")]
    [InlineData("SqlServer", "linux-x64")]
    [InlineData("SqlServer", "linux-arm64")]
    [InlineData("Sqlite", "win-x64")]
    [InlineData("PostgreSql", "win-x64")]
    public void InheritdocContractIsAvailableFromBinaryMetadata(string provider, string rid)
    {
        string directory = Path.Combine(GetRoot(), provider, rid, "lib");
        Dictionary<string, string> references = GetFrameworkFiles();
        foreach (string file in Directory.GetFiles(directory, "*.dll"))
        {
            references[Path.GetFileNameWithoutExtension(file)] = file;
        }
        CSharpCompilation compilation = CSharpCompilation.Create("DocumentationConsumer", references: references.Values.Select(file =>
            MetadataReference.CreateFromFile(file, documentation: File.Exists(Path.ChangeExtension(file, ".xml"))
                ? XmlDocumentationProvider.CreateFromFile(Path.ChangeExtension(file, ".xml")) : null)));
        foreach ((string implementationName, string contractName) in new[]
        {
            ("AgentBridge.Tokenization.ContextTokenCounter", "AgentBridge.Application.Ports.IContextTokenCounter"),
            ("AgentBridge.CodexLb.Models.CodexLbModelCatalog", "AgentBridge.Application.Ports.IModelCatalog"),
            ("AgentBridge.Persistence.EfCore.Reading.DialogReader", "AgentBridge.Application.Ports.IDialogReader")
        })
        {
            INamedTypeSymbol implementation = Assert.IsAssignableFrom<INamedTypeSymbol>(compilation.GetTypeByMetadataName(implementationName));
            INamedTypeSymbol contract = Assert.IsAssignableFrom<INamedTypeSymbol>(compilation.GetTypeByMetadataName(contractName));
            Assert.Contains(implementation.AllInterfaces, item => SymbolEqualityComparer.Default.Equals(item, contract));
            AssertRussianSummary(contract);
            Assert.Contains("inheritdoc", implementation.GetDocumentationCommentXml());
            foreach (IMethodSymbol method in contract.GetMembers().OfType<IMethodSymbol>())
            {
                AssertRussianSummary(method);
                ISymbol target = Assert.IsAssignableFrom<ISymbol>(implementation.FindImplementationForInterfaceMember(method));
                Assert.Contains("inheritdoc", target.GetDocumentationCommentXml());
            }
        }
    }

    /// <summary>Требует непустое описание с кириллицей в XML, доступном Roslyn consumer.</summary>
    private static void AssertRussianSummary(ISymbol symbol)
    {
        string xml = Assert.IsType<string>(symbol.GetDocumentationCommentXml());
        XElement member = XElement.Parse(xml);
        string summary = Assert.IsType<XElement>(member.Element("summary")).Value;
        Assert.Matches("[\u0400-\u04ff]", summary);
    }

    /// <summary>Принимает только явный существующий каталог поставки, без поиска соседних исходников.</summary>
    private static string GetRoot()
    {
        string? root = Environment.GetEnvironmentVariable("AGENTBRIDGE_DELIVERY_ROOT");
        Assert.False(string.IsNullOrWhiteSpace(root));
        Assert.True(Path.IsPathFullyQualified(root));
        Assert.True(Directory.Exists(root));
        return root;
    }

    /// <summary>Выбирает только DLL установленного .NET runtime, исключая пакеты самого test runner.</summary>
    private static Dictionary<string, string> GetFrameworkFiles() =>
        Directory.GetFiles(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "*.dll")
            .ToDictionary(path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase);
}

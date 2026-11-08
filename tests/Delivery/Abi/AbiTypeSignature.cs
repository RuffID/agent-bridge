using Microsoft.CodeAnalysis;

namespace AgentBridge.Delivery.Abi.Tests;

/// <summary>Тип CLI сигнатуры с отдельной by-ref формой и custom modifiers.</summary>
internal record AbiTypeSignature(ITypeSymbol Symbol, bool ByReference = false, string Modifiers = "")
{
    /// <summary>Сравнивает CLI форму, не включая nullable source annotations.</summary>
    internal bool Matches(ITypeSymbol symbol, bool byReference, string modifiers)
    {
        return ByReference == byReference && Modifiers == modifiers &&
            SymbolEqualityComparer.Default.Equals(Symbol, symbol);
    }
}

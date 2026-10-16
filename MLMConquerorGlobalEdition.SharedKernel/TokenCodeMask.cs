using System.Text.RegularExpressions;

namespace MLMConquerorGlobalEdition.SharedKernel;

/// <summary>
/// Tapa los códigos de token para quien no debe poder canjearlos. Los códigos son al portador (ver
/// <see cref="TokenCodeGenerator"/>): leer uno basta para usarlo. Se dejan los tres primeros
/// caracteres, que bastan para hablar de un código con el miembro sin poder canjearlo.
/// </summary>
public static partial class TokenCodeMask
{
    public const int    VisibleChars = 3;
    public const string Hidden       = "****";

    /// <summary><c>K7Q4X29</c> → <c>K7Q****</c>. Un código de tres caracteres o menos sale entero tapado.</summary>
    public static string? Mask(string? code) =>
        code is null ? null
        : code.Length <= VisibleChars ? Hidden
        : code[..VisibleChars] + Hidden;

    /// <summary>
    /// Tapa, dentro de un texto libre (la nota de una distribución o de una transferencia), cada palabra
    /// con la forma de un código: siete caracteres con exactamente tres letras y cuatro dígitos. Los ids
    /// de miembro y los números de pedido no tienen esa forma y quedan como están.
    /// </summary>
    public static string? MaskCodesIn(string? text) =>
        string.IsNullOrEmpty(text) ? text
        : CodeShaped().Replace(text, m => IsCodeShaped(m.Value) ? Mask(m.Value)! : m.Value);

    private static bool IsCodeShaped(string s) =>
        s.Count(char.IsAsciiDigit) == 4 && s.Count(char.IsAsciiLetterUpper) == TokenCodeGenerator.LetterCount;

    [GeneratedRegex(@"\b[A-Z0-9]{7}\b")]
    private static partial Regex CodeShaped();
}

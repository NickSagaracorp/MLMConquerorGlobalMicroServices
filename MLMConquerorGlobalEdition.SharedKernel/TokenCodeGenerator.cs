using System.Security.Cryptography;

namespace MLMConquerorGlobalEdition.SharedKernel;

/// <summary>
/// Código canjeable de un token: 3 letras y <c>maximumNumbers</c> dígitos (4 por defecto) en orden
/// aleatorio, p. ej. <c>4K7QX29</c>. Es el formato del legado y el que ya llevan los códigos emitidos.
/// </summary>
/// <remarks>
/// Los códigos son al portador: quien adivina uno lo canjea. Por eso sale de
/// <see cref="RandomNumberGenerator"/> (impredecible y seguro entre hilos) y no de un
/// <see cref="Random"/> estático, que no es thread-safe (con llamadas concurrentes puede quedarse
/// devolviendo ceros) y cuya secuencia se puede reconstruir viendo unos cuantos códigos.
/// El espacio sigue siendo pequeño, así que quien emite debe seguir descartando los repetidos
/// (TokenIssuer lo hace contra la base).
/// </remarks>
public static class TokenCodeGenerator
{
    /// <summary>Sin I, L ni O, que se confunden con 1 y 0 al leer el código.</summary>
    public const string Letters = "ABCDEFGHJKMNPQRSTUVWXYZ";
    public const string Digits  = "0123456789";
    public const int    LetterCount = 3;

    public static string Generate(int maximumNumbers = 4)
    {
        if (maximumNumbers < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumNumbers));

        var chars = new char[LetterCount + maximumNumbers];

        for (var i = 0; i < LetterCount; i++)
            chars[i] = Letters[RandomNumberGenerator.GetInt32(Letters.Length)];

        for (var i = LetterCount; i < chars.Length; i++)
            chars[i] = Digits[RandomNumberGenerator.GetInt32(Digits.Length)];

        // Fisher–Yates: cada posición de letras y dígitos es igual de probable.
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }
}

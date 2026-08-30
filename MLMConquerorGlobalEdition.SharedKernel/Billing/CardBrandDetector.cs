using System.Text.RegularExpressions;

namespace MLMConquerorGlobalEdition.SharedKernel.Billing;

/// <summary>
/// La marca de una tarjeta a partir de sus primeros dígitos (el BIN). UNA sola función, para
/// todo el producto.
/// </summary>
/// <remarks>
/// POR QUÉ ESTO EXISTE COMO PIEZA APARTE. Esta función estaba escrita TRES veces: una en
/// <c>SimulatedCardTokenizationService</c> (servidor, la que acaba en la columna
/// <c>MemberCreditCards.CardBrand</c>) y dos más, copiadas entre sí, en los asistentes de alta
/// —<c>Signup.razor</c> y <c>MemberJoin.razor</c>—. Y no coincidían: las del asistente devolvían
/// "MC" y "Disc", clasificaban como Mastercard cualquier número que empezara por 2 (los rangos
/// reales son 51-55 y 22-27), ponían Discover en todo lo que empezara por 6, y no conocían ni JCB
/// ni Diners. Es decir: la marca que veía la persona al teclear y la marca que se guardaba en la
/// base podían ser distintas para la misma tarjeta.
///
/// La ordenación de las comprobaciones IMPORTA y se conserva tal cual estaba en el servidor, que
/// era la versión correcta: de lo más específico a lo más general. Mastercard mira 22-27 antes de
/// que JCB mire 2131, y por eso 2131 no cae en Mastercard.
///
/// El nombre que sale de aquí es el que viaja a la API y el que se persiste, así que tiene que
/// pasar el patrón de <c>CreditCardInfoDtoValidator.CardBrand</c> —letras y espacios, hasta 30—.
/// Cualquier marca nueva que se añada debe respetarlo.
/// </remarks>
public static class CardBrandDetector
{
    /// <summary>Lo que se devuelve cuando el BIN no corresponde a ninguna red conocida.</summary>
    /// <remarks>
    /// No es un fallo: hay redes locales que no clasificamos y la pasarela es quien tiene la última
    /// palabra. Se guarda tal cual para que el equipo de fraude vea que no se supo, en vez de una
    /// marca inventada.
    /// </remarks>
    public const string Unknown = "Unknown";

    /// <summary>
    /// Devuelve la marca del número dado. Tolera espacios, guiones y cualquier otro separador:
    /// el asistente llama a esto en cada pulsación, con el número a medio escribir.
    /// </summary>
    public static string Detect(string? cardNumber)
    {
        var digits = OnlyDigits(cardNumber);
        if (digits.Length < 1) return Unknown;

        // El orden importa: los patrones más específicos primero.
        if (Regex.IsMatch(digits, "^4"))                 return "Visa";
        if (Regex.IsMatch(digits, "^3[47]"))             return "Amex";
        if (Regex.IsMatch(digits, "^(5[1-5]|2[2-7])"))   return "Mastercard";
        if (Regex.IsMatch(digits, "^6(?:011|5|4[4-9])")) return "Discover";
        if (Regex.IsMatch(digits, "^(?:2131|1800|35)"))  return "JCB";
        if (Regex.IsMatch(digits, "^3(?:0[0-5]|[68])"))  return "Diners";
        return Unknown;
    }

    /// <summary>
    /// Se queda solo con los dígitos. Es lo primero que hace todo el que toca un PAN aquí: el
    /// asistente lo recibe con espacios porque así lo teclea la gente.
    /// </summary>
    public static string OnlyDigits(string? value) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : new string(value.Where(char.IsDigit).ToArray());
}

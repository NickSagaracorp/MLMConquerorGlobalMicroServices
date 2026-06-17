namespace MLMConquerorGlobalEdition.SharedKernel;

public static class OrderNumberHelper
{
    // Order number format: {INITIALS}{MMdd}-{HHmmssfff}-{XXXX}
    //   • INITIALS    — capital letters from each word of orderName (e.g. "Elite" → "E")
    //   • MMdd        — month/day of order (legacy / human-readable)
    //   • HHmmssfff   — hour/min/sec/ms (24h, UTC) to avoid same-millisecond collisions in bursts
    //   • XXXX        — 4 random uppercase letters (26^4 = 456,976) tail for safety
    //
    // Total entropy per millisecond per (level): 456,976 — practically collision-free even
    // under heavy concurrent signup load. Previous format (2 letters / day) was only 676
    // combinations and saturated quickly under load, causing the duplicate-check loop in
    // SignupAmbassadorHandler to spin forever and the API to hang.
    public static string Generate(string orderName, DateTime date)
    {
        var initials = string.Concat(
            orderName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                     .Select(w => char.ToUpper(w[0])));

        var datePart = date.ToString("MMdd");
        var timePart = date.ToString("HHmmssfff");

        Span<char> letters = stackalloc char[4];
        for (var i = 0; i < letters.Length; i++)
        {
            letters[i] = (char)('A' + Random.Shared.Next(26));
        }

        return $"{initials}{datePart}-{timePart}-{new string(letters)}";
    }
}

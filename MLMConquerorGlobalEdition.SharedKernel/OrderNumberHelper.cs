namespace MLMConquerorGlobalEdition.SharedKernel;

public static class OrderNumberHelper
{
    public static string Generate(string orderName, DateTime date)
    {
        var initials = string.Concat(
            orderName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                     .Select(w => char.ToUpper(w[0])));

        var datePart = date.ToString("MMdd");

        Span<char> letters = stackalloc char[2];
        letters[0] = (char)('A' + Random.Shared.Next(26));
        letters[1] = (char)('A' + Random.Shared.Next(26));

        return $"{initials}{datePart}{new string(letters)}";
    }
}

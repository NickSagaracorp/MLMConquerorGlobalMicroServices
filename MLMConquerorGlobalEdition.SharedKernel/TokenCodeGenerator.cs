namespace MLMConquerorGlobalEdition.SharedKernel;

public static class TokenCodeGenerator
{
    private static readonly Random R = new();

    public static string Generate(int maximumNumbers = 4)
    {
        const string passwordChars = "ABCDEFGHJKMNPQRSTUVWXYZ";
        const string passwordNumbers = "0123456789";
        var code = string.Empty;
        var number = string.Empty;

        for (var i = 1; i <= 3; i++)
        {
            var pos = R.Next(0, 22);
            code += passwordChars[pos];
        }

        for (var i = 1; i <= maximumNumbers; i++)
        {
            var pos = R.Next(0, 9);
            number += passwordNumbers[pos];
        }

        var random = new Random();
        var fullCode = code + number;
        var resultstring = string.Empty;
        var randLists = new string[fullCode.Length];
        for (var i = 0; i < fullCode.Length; i++)
        {
            randLists[i] = new string(fullCode.ToCharArray().OrderBy(s => random.Next(0, 25)).ToArray());
            resultstring = randLists[i];
        }
        return resultstring;
    }
}

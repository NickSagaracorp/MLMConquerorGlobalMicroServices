using System.Reflection;
using FluentAssertions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Tokens;

/// <summary>
/// El código de token: mismo formato que el legado (3 letras del alfabeto sin I/L/O y 4 dígitos,
/// mezclados) y generado con un RNG criptográfico, sin un Random estático compartido entre hilos.
/// </summary>
public class TokenCodeGeneratorTests
{
    [Fact]
    public void Tres_letras_y_cuatro_digitos_del_alfabeto_permitido()
    {
        for (var i = 0; i < 2000; i++)
        {
            var code = TokenCodeGenerator.Generate();

            code.Should().HaveLength(7);
            code.Count(char.IsAsciiDigit).Should().Be(4);
            code.Where(char.IsLetter).Should().HaveCount(3).And.OnlyContain(c => TokenCodeGenerator.Letters.Contains(c));
        }
    }

    [Fact]
    public void Respeta_la_cantidad_de_digitos_pedida()
        => TokenCodeGenerator.Generate(6).Count(char.IsAsciiDigit).Should().Be(6);

    [Fact]
    public void Usa_todo_el_alfabeto_y_todos_los_digitos()
    {
        // El generador del legado nunca sacaba la Z ni el 9 (Next(0, 22) y Next(0, 9)).
        var vistos = new HashSet<char>();
        for (var i = 0; i < 5000; i++)
            foreach (var c in TokenCodeGenerator.Generate()) vistos.Add(c);

        vistos.Should().Contain('Z').And.Contain('9');
    }

    [Fact]
    public void No_guarda_un_Random_estatico()
        => typeof(TokenCodeGenerator)
            .GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Should().NotContain(f => f.FieldType == typeof(Random));

    [Fact]
    public async Task En_paralelo_no_se_degenera()
    {
        // Un Random compartido usado desde varios hilos puede corromperse y devolver siempre lo mismo.
        var codes = await Task.WhenAll(Enumerable.Range(0, 64)
            .Select(_ => Task.Run(() => Enumerable.Range(0, 200).Select(_ => TokenCodeGenerator.Generate()).ToList())));

        codes.SelectMany(c => c).Distinct().Count().Should().BeGreaterThan(12000);
    }
}

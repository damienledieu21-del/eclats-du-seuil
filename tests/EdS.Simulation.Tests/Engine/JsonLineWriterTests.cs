using System.Text.Json;
using EdS.Domain.Numerics;
using EdS.Simulation.Engine;
using FsCheck.Xunit;

namespace EdS.Simulation.Tests.Engine;

[Trait("Category", "Engine")]
public class JsonLineWriterTests
{
    [Fact]
    public void Champs_dans_l_ordre_d_appel_sans_espace()
    {
        string line = new JsonLineWriter().BeginObject()
            .Field("b", 2).Field("a", "x").Field("f", Fixed.Parse("1.25")).Field("ok", true).Field("rien", (string?)null)
            .Name("liste").BeginArray().Value(1).Value("deux").BeginObject().Field("trois", 3).EndObject().EndArray()
            .EndObject().ToString();
        Assert.Equal("{\"b\":2,\"a\":\"x\",\"f\":1.25,\"ok\":true,\"rien\":null,\"liste\":[1,\"deux\",{\"trois\":3}]}", line);
    }

    [Fact]
    public void Objet_inacheve_refuse() =>
        Assert.Throws<InvalidOperationException>(() => new JsonLineWriter().BeginObject().Field("a", 1).ToString());

    [Property(MaxTest = 500)]
    public bool Texte_quelconque_relu_a_l_identique(string? text)
    {
        text ??= string.Empty;
        // Les demi-caractères UTF-16 isolés ne sont pas du texte valide : on ne les écrit pas dans un journal.
        if (text.Any(char.IsSurrogate)) return true;
        string line = new JsonLineWriter().BeginObject().Field("t", text).EndObject().ToString();
        return !line.Contains('\n') && JsonDocument.Parse(line).RootElement.GetProperty("t").GetString() == text;
    }

    [Property(MaxTest = 500)]
    public bool Entiers_et_decimaux_exacts(long integer, long raw)
    {
        string line = new JsonLineWriter().BeginObject().Field("i", integer).Field("f", Fixed.FromRaw(raw)).EndObject().ToString();
        JsonElement root = JsonDocument.Parse(line).RootElement;
        return root.GetProperty("i").GetInt64() == integer && Fixed.Parse(root.GetProperty("f").GetRawText()).Raw == raw;
    }
}

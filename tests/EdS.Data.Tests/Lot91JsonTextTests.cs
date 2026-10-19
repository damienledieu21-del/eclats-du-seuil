using System.Globalization;
using System.Reflection;
using System.Text;
using EdS.Data.Json;
using EdS.Data.Validation;
using FsCheck.Xunit;
using Portable = EdS.Data.Json.Text;
using Stj = System.Text.Json;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 9.1 : analyseur JSON strict sans dépendance (<c>EdS.Data.Json.Text</c>), qui remplace System.Text.Json dans la lecture des
/// données pour que Unity et le futur serveur lisent exactement comme les outils. Il est comparé à System.Text.Json (même arbre, mêmes
/// nombres bruts, même ordre) sur tous les fichiers livrés et figés, sur des valeurs aléatoires et sur des textes altérés ; ses refus
/// propres (paires de substitution incomplètes, UTF-8 invalide) sont plus stricts, jamais plus laxistes.
/// </summary>
[Trait("Category", "Data")]
public class Lot91JsonTextTests
{
    private static readonly Stj.JsonDocumentOptions StjOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = Stj.JsonCommentHandling.Disallow,
        MaxDepth = InputLimits.MaxJsonDepth,
    };

    private static readonly Portable.JsonDocumentOptions Options = new() { MaxDepth = InputLimits.MaxJsonDepth };

    /// <summary>Forme canonique d'un arbre System.Text.Json : nature, textes, nombres bruts, propriétés dans l'ordre (doublons compris).</summary>
    private static string Shape(Stj.JsonElement e) => e.ValueKind switch
    {
        Stj.JsonValueKind.Object => "{" + string.Join(",", e.EnumerateObject().Select(p => Quote(p.Name) + ":" + Shape(p.Value))) + "}",
        Stj.JsonValueKind.Array => "[" + string.Join(",", e.EnumerateArray().Select(Shape)) + "]",
        Stj.JsonValueKind.String => Quote(e.GetString()!),
        Stj.JsonValueKind.Number => "n" + e.GetRawText(),
        Stj.JsonValueKind.True => "true",
        Stj.JsonValueKind.False => "false",
        Stj.JsonValueKind.Null => "null",
        _ => "?",
    };

    private static string Shape(Portable.JsonElement e) => e.ValueKind switch
    {
        Portable.JsonValueKind.Object => "{" + string.Join(",", e.EnumerateObject().Select(p => Quote(p.Name) + ":" + Shape(p.Value))) + "}",
        Portable.JsonValueKind.Array => "[" + string.Join(",", e.EnumerateArray().Select(Shape)) + "]",
        Portable.JsonValueKind.String => Quote(e.GetString()!),
        Portable.JsonValueKind.Number => "n" + e.GetRawText(),
        Portable.JsonValueKind.True => "true",
        Portable.JsonValueKind.False => "false",
        Portable.JsonValueKind.Null => "null",
        _ => "?",
    };

    private static string Quote(string s) => "\"" + string.Concat(s.Select(c => c < 0x20 || c > 0x7E || c == '"' || c == '\\' ? $"\\u{(int)c:X4}" : c.ToString())) + "\"";

    /// <summary>Forme selon System.Text.Json, ou null s'il refuse le texte (ou ne sait pas en lire une chaîne).</summary>
    private static string? StjShape(byte[] utf8)
    {
        try
        {
            using var document = Stj.JsonDocument.Parse(utf8, StjOptions);
            return Shape(document.RootElement);
        }
        catch (Exception e) when (e is Stj.JsonException or InvalidOperationException) { return null; }
    }

    private static string? PortableShape(byte[] utf8)
    {
        try
        {
            using var document = Portable.JsonDocument.Parse(utf8, Options);
            return Shape(document.RootElement);
        }
        catch (Portable.JsonException) { return null; }
    }

    private static IEnumerable<string> Corpus()
    {
        string root = AppContext.BaseDirectory;
        foreach (string file in Directory.GetFiles(root, "*.json", SearchOption.AllDirectories).Where(f => !f.EndsWith(".deps.json") && !f.EndsWith(".runtimeconfig.json")))
            yield return file;
    }

    [Fact]
    public void Meme_arbre_que_System_Text_Json_pour_tous_les_fichiers_livres_et_figes()
    {
        int files = 0, lines = 0;
        foreach (string file in Corpus())
        {
            byte[] bytes = File.ReadAllBytes(file);
            string? expected = StjShape(bytes);
            Assert.NotNull(expected);
            Assert.Equal(expected, PortableShape(bytes));
            files++;
        }
        foreach (string log in Directory.GetFiles(AppContext.BaseDirectory, "*.jsonl", SearchOption.AllDirectories))
            foreach (string line in File.ReadAllLines(log))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(line);
                Assert.Equal(StjShape(bytes), PortableShape(bytes));
                lines++;
            }
        Assert.True(files >= 20, $"{files} fichiers");
        Assert.True(lines > 1000, $"{lines} lignes de journal");
    }

    [Theory]
    [InlineData("{ \"a\": 1, }")]                 // virgule finale
    [InlineData("[1, 2,]")]
    [InlineData("{ // commentaire\n }")]
    [InlineData("{ /* commentaire */ }")]
    [InlineData("{ 'a': 1 }")]                     // apostrophes
    [InlineData("{ a: 1 }")]                       // nom sans guillemets
    [InlineData("[NaN]")]
    [InlineData("[Infinity]")]
    [InlineData("[-Infinity]")]
    [InlineData("[01]")]                           // zéro initial
    [InlineData("[-01]")]
    [InlineData("[1.]")]
    [InlineData("[.5]")]
    [InlineData("[+1]")]
    [InlineData("[-]")]
    [InlineData("[1e]")]
    [InlineData("[1e+]")]
    [InlineData("[0x10]")]
    [InlineData("[tru]")]
    [InlineData("[True]")]
    [InlineData("[nul]")]
    [InlineData("[\"a\tb\"]")]                     // tabulation brute dans un texte
    [InlineData("[\"a\nb\"]")]
    [InlineData("[\"\\x41\"]")]                    // échappement inconnu
    [InlineData("[\"\\u12\"]")]
    [InlineData("[\"\\u12G4\"]")]
    [InlineData("[\"abc]")]                        // texte non terminé
    [InlineData("{\"a\" 1}")]
    [InlineData("{\"a\":}")]
    [InlineData("{,}")]
    [InlineData("[,1]")]
    [InlineData("[1 2]")]
    [InlineData("{} {}")]                          // deux documents
    [InlineData("1 2")]
    [InlineData("")]
    [InlineData("   \n\t ")]
    [InlineData("[")]
    [InlineData("{")]
    [InlineData("{\"a\":1")]
    [InlineData("]")]
    public void Textes_refuses_comme_par_System_Text_Json(string json)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(json);
        Assert.Null(StjShape(utf8));
        Assert.Throws<Portable.JsonException>(() => Portable.JsonDocument.Parse(utf8, Options));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0")]
    [InlineData("-0.0e-0")]
    [InlineData("1E+10")]
    [InlineData("123456789012345678901234567890")]
    [InlineData("\"\"")]
    [InlineData("\"\\\" \\\\ \\/ \\b \\f \\n \\r \\t \\u00e9 \\ud83d\\ude00\"")]
    [InlineData("\"é😀\"")]
    [InlineData("{\"a\":1,\"a\":2}")]              // doublons conservés, dans l'ordre
    [InlineData(" \r\n [ true , false , null ] \r\n ")]
    [InlineData("[[],{},[{}],{\"\":[]}]")]
    public void Textes_admis_avec_le_meme_arbre(string json)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(json);
        string? expected = StjShape(utf8);
        Assert.NotNull(expected);
        Assert.Equal(expected, PortableShape(utf8));
    }

    [Theory]
    [InlineData(new byte[] { (byte)'"', 0xC0, 0x80, (byte)'"' })]              // forme trop longue
    [InlineData(new byte[] { (byte)'"', 0xED, 0xA0, 0x80, (byte)'"' })]        // substitution encodée en UTF-8
    [InlineData(new byte[] { (byte)'"', 0xF4, 0x90, 0x80, 0x80, (byte)'"' })]  // au-delà de U+10FFFF
    [InlineData(new byte[] { (byte)'"', 0xE9, (byte)'"' })]                    // séquence tronquée
    [InlineData(new byte[] { (byte)'"', 0x80, (byte)'"' })]                    // octet de continuation isolé
    [InlineData(new byte[] { (byte)'[', 0xFF, (byte)']' })]
    public void UTF8_invalide_refuse(byte[] utf8) =>
        Assert.Throws<Portable.JsonException>(() => Portable.JsonDocument.Parse(utf8, Options));

    [Theory]
    [InlineData("[\"\\ud83d\"]")]                  // première moitié seule
    [InlineData("[\"\\ud83dx\"]")]
    [InlineData("[\"\\ud83d\\u0041\"]")]           // suivie d'autre chose qu'une seconde moitié
    [InlineData("[\"\\ude00\"]")]                  // seconde moitié seule
    public void Paire_de_substitution_incomplete_refusee_a_l_analyse(string json)
    {
        // Plus strict que System.Text.Json, qui l'admet à l'analyse et ne refuse qu'à la lecture du texte (InvalidOperationException).
        Assert.Throws<Portable.JsonException>(() => Portable.JsonDocument.Parse(Encoding.UTF8.GetBytes(json), Options));
    }

    [Theory]
    [InlineData(32, true)]
    [InlineData(33, false)]
    public void Profondeur_bornee_comme_System_Text_Json(int containers, bool accepted)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(new string('[', containers) + new string(']', containers));
        Assert.Equal(accepted, StjShape(utf8) != null);
        Assert.Equal(accepted, PortableShape(utf8) != null);
    }

    [Fact]
    public void Marque_d_ordre_UTF8_refusee_dans_le_texte_et_retiree_d_un_fichier()
    {
        // Comme System.Text.Json : refusée dans un texte ; JsonInput.ReadFile la retire d'un fichier avant analyse.
        byte[] utf8 = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes("{\"a\":1}")).ToArray();
        Assert.Null(StjShape(utf8));
        Assert.Null(PortableShape(utf8));
        string file = Path.Combine(Path.GetTempPath(), "eds-bom-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllBytes(file, utf8);
            Assert.Equal("{\"a\":n1}", PortableShape(JsonInput.ReadFile(file, new ValidationReport())!));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Lignes_des_proprietes_et_des_erreurs()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("{\r\n  \"a\": 1,\n  \"b\": {\n    \"c\": [\n      { \"d\": true }\n    ]\n  },\n  \"a\": 2\n}");
        using var document = Portable.JsonDocument.Parse(utf8, Options);
        var lines = Portable.JsonLineIndex.Build(document.RootElement);
        Assert.Equal(2, lines.Of("$.a"));
        Assert.Equal(8, lines.Of("$.a", occurrence: 1));
        Assert.Equal(3, lines.Of("$.b"));
        Assert.Equal(4, lines.Of("$.b.c"));
        Assert.Equal(5, lines.Of("$.b.c[0].d"));
        Assert.Null(lines.Of("$.z"));
        Assert.Equal(4, Portable.JsonLineIndex.FindFirst(document.RootElement, "$.b.c"));

        var error = Assert.Throws<Portable.JsonException>(() => Portable.JsonDocument.Parse(Encoding.UTF8.GetBytes("{\n  \"a\": 1,\n}"), Options));
        Assert.Equal(2, error.LineNumber);   // à partir de 0, comme System.Text.Json
        Assert.Equal(0, error.BytePositionInLine);
    }

    [Fact]
    public void Erreur_de_syntaxe_rapportee_avec_sa_ligne_et_un_message_en_francais()
    {
        var report = new ValidationReport();
        JsonInput.ParseDocument("{\n  \"a\": [1, 2,]\n}", report, (_, _) => throw new InvalidOperationException("non lu"));
        ValidationError error = Assert.Single(report.Errors);
        Assert.Equal((ValidationCode.InvalidJson, 2L), (error.Code, error.Line));
        Assert.Equal("JSON invalide : « ] » inattendu : une valeur (objet, tableau, texte, nombre, true, false ou null) attendu.", error.Message);
    }

    // --- Nombre de valeurs (revue Codex de la PR #12) ------------------------------------------------------------------------

    [Theory]
    [InlineData("0", 1)]
    [InlineData("\"x\"", 1)]
    [InlineData("[]", 1)]
    [InlineData("{}", 1)]
    [InlineData("{\"a\":1}", 2)]                     // le nom d'une propriété ne compte pas : il accompagne sa valeur
    [InlineData("[true,false,null]", 4)]
    [InlineData("[1,[2,{\"b\":null}],{}]", 7)]
    public void Valeurs_comptees_racine_et_conteneurs_compris(string json, int values)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(json);
        Assert.Equal(StjShape(utf8), Shape(Portable.JsonDocument.Parse(utf8, new Portable.JsonDocumentOptions { MaxValues = values }).RootElement));
        var error = Assert.Throws<Portable.JsonException>(() => Portable.JsonDocument.Parse(utf8, new Portable.JsonDocumentOptions { MaxValues = values - 1 }));
        Assert.True(error.LimitExceeded);
    }

    [Fact]
    public void Erreur_de_syntaxe_distincte_d_un_depassement_de_valeurs()
    {
        Assert.False(Assert.Throws<Portable.JsonException>(() => Portable.JsonDocument.Parse(Encoding.UTF8.GetBytes("[1,]"), Options)).LimitExceeded);
        // Au-delà de la limite, une valeur mal écrite n'est pas analysée : le refus pour volume vient d'abord.
        var error = Assert.Throws<Portable.JsonException>(() =>
            Portable.JsonDocument.Parse(Encoding.UTF8.GetBytes("[1,tru]"), new Portable.JsonDocumentOptions { MaxValues = 2 }));
        Assert.True(error.LimitExceeded);
        Assert.Equal(3, error.BytePositionInLine);
    }

    /// <summary>Tableau de <paramref name="zeros"/> zéros sur la deuxième ligne : <c>zeros + 1</c> valeurs.</summary>
    private static string Zeros(int zeros) => "[\n" + string.Join(",", Enumerable.Repeat("0", zeros)) + "]";

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void Limite_de_valeurs_d_un_document_a_la_frontiere(int extra, bool accepted)
    {
        var report = new ValidationReport();
        bool read = false;
        JsonInput.ParseDocument(Zeros(InputLimits.MaxJsonValues - 1 + extra), report, (_, root) => read = root.GetArrayLength() == InputLimits.MaxJsonValues - 1);
        Assert.Equal(accepted, read);
        if (accepted)
        {
            Assert.True(report.IsValid, report.ToString());
            return;
        }
        ValidationError error = Assert.Single(report.Errors);
        Assert.Equal(("$", ValidationCode.LimitExceeded, 2L), (error.Path, error.Code, error.Line));
        Assert.Equal("document de plus de 1048576 valeurs JSON (objets, tableaux, textes, nombres, true, false, null) : refusé pendant l'analyse, avant d'être construit en entier.", error.Message);
    }

    [Fact]
    public void Texte_valide_mais_demesure_refuse_sans_construire_ses_valeurs()
    {
        // Le cas de la revue : près de 16 Mio de « 0, », valide et sous la limite de taille, soit environ 8 millions de valeurs.
        int zeros = (int)(InputLimits.MaxDataFileBytes / 2) - 2;
        byte[] utf8 = Encoding.UTF8.GetBytes(Zeros(zeros));
        Assert.True(utf8.Length <= InputLimits.MaxDataFileBytes);

        long before = GC.GetAllocatedBytesForCurrentThread();
        var error = Assert.Throws<Portable.JsonException>(() => Portable.JsonDocument.Parse(utf8, Options));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Refus à la première valeur de trop, après environ 2 Mio lus sur 16 : le reste du texte n'est jamais analysé.
        Assert.True(error.LimitExceeded);
        Assert.Equal(1, error.LineNumber);
        Assert.Equal(2L * InputLimits.MaxJsonValues - 2, error.BytePositionInLine);
        // Mémoire bornée par la limite de valeurs, pas par la taille du texte (environ 32 Mio de pile de travail, aucun texte par zéro).
        Assert.True(allocated < 48L * 1024 * 1024, $"{allocated} octets alloués");

        var report = new ValidationReport();
        JsonInput.ParseDocument(utf8, report, (_, _) => throw new InvalidOperationException("non lu"));
        Assert.Equal(ValidationCode.LimitExceeded, Assert.Single(report.Errors).Code);
    }

    [Fact]
    public void Nombres_d_un_chiffre_et_litteraux_sans_allocation_par_valeur()
    {
        // Arbre compact : 16 octets par élément de tableau, rien de plus pour « 0 » à « 9 », true, false et null.
        const int count = 100_000;
        byte[] digits = Encoding.UTF8.GetBytes("[" + string.Join(",", Enumerable.Range(0, count).Select(i => (i % 10).ToString(CultureInfo.InvariantCulture))) + "]");
        byte[] literals = Encoding.UTF8.GetBytes("[" + string.Join(",", Enumerable.Range(0, count).Select(i => (i % 3) switch { 0 => "true", 1 => "false", _ => "null" })) + "]");
        foreach (byte[] utf8 in new[] { digits, literals })
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            using var document = Portable.JsonDocument.Parse(utf8, Options);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(count, document.RootElement.GetArrayLength());
            // Seuls restent les 16 octets de chaque élément du tableau final ; la pile de travail, aux capacités doublées, en alloue au
            // plus 64 de plus en tout (environ 42 ici). Un nœud et un texte par valeur en coûteraient plus de 70 de plus.
            Assert.True(allocated < 80L * count, $"{allocated} octets alloués pour {count} valeurs");
        }
    }

    // --- Propriétés : valeurs aléatoires et textes altérés ------------------------------------------------------------------

    private static string RandomJson(System.Random random, int depth)
    {
        int kind = depth >= 4 ? random.Next(4) : random.Next(6);
        switch (kind)
        {
            case 0: return random.Next(3) switch { 0 => "true", 1 => "false", _ => "null" };
            case 1:
                string number = (random.Next(2) == 0 ? "-" : "") + random.Next(0, 100000).ToString(CultureInfo.InvariantCulture);
                if (random.Next(2) == 0) number += "." + random.Next(0, 10000).ToString(CultureInfo.InvariantCulture);
                if (random.Next(4) == 0) number += "e" + (random.Next(2) == 0 ? "-" : "+") + random.Next(0, 30).ToString(CultureInfo.InvariantCulture);
                return number;
            case 2:
            case 3:
                var text = new StringBuilder("\"");
                int length = random.Next(0, 8);
                for (int i = 0; i < length; i++)
                {
                    int c = random.Next(8);
                    text.Append(c switch
                    {
                        0 => "\\n",
                        1 => "\\u00e9",
                        2 => "é",
                        3 => "\\\"",
                        4 => "😀",
                        _ => ((char)random.Next('a', 'z' + 1)).ToString(),
                    });
                }
                return text.Append('"').ToString();
            case 4:
                return "[" + string.Join(",", Enumerable.Range(0, random.Next(0, 4)).Select(_ => RandomJson(random, depth + 1))) + "]";
            default:
                return "{" + string.Join(",", Enumerable.Range(0, random.Next(0, 4)).Select(i => $"\"k{random.Next(3)}\" : " + RandomJson(random, depth + 1))) + "}";
        }
    }

    [Property(MaxTest = 300)]
    public bool Valeurs_aleatoires_meme_arbre_que_System_Text_Json(int seed)
    {
        var random = new System.Random(seed);
        byte[] utf8 = Encoding.UTF8.GetBytes(RandomJson(random, 0));
        string? expected = StjShape(utf8);
        return expected != null && expected == PortableShape(utf8);
    }

    /// <summary>
    /// Un octet remplacé, inséré ou supprimé : si l'analyseur admet le texte, System.Text.Json l'admet aussi, avec le même arbre. Le
    /// contraire n'est pas exigé : il refuse en plus les paires de substitution incomplètes que System.Text.Json admet à l'analyse.
    /// </summary>
    [Property(MaxTest = 2000)]
    public bool Textes_alteres_jamais_plus_laxiste_que_System_Text_Json(int seed, byte replacement, int position)
    {
        var random = new System.Random(seed);
        var bytes = Encoding.UTF8.GetBytes(RandomJson(random, 0)).ToList();
        int at = bytes.Count == 0 ? 0 : (int)((uint)position % (uint)bytes.Count);
        switch ((uint)seed % 3)
        {
            case 0: if (bytes.Count > 0) bytes[at] = replacement; break;
            case 1: bytes.Insert(at, replacement); break;
            default: if (bytes.Count > 0) bytes.RemoveAt(at); break;
        }
        byte[] utf8 = bytes.ToArray();
        string? mine = PortableShape(utf8);
        return mine is null || mine == StjShape(utf8);
    }

    // --- Contrat de portabilité --------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("EdS.Domain")]
    [InlineData("EdS.Simulation")]
    [InlineData("EdS.Data")]
    [InlineData("EdS.Data.Json")]
    [InlineData("EdS.Runtime")]   // sous-lot 9.2
    [InlineData("EdS.Net")]       // lot 10
    public void Projets_portables_netstandard21_sans_System_Text_Json(string name)
    {
        Assembly assembly = Assembly.Load(name);
        var framework = assembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>();
        Assert.Equal(".NETStandard,Version=v2.1", framework?.FrameworkName);
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), r => r.Name is "System.Text.Json" or "Newtonsoft.Json");
        Assert.All(assembly.GetReferencedAssemblies(), r => Assert.True(r.Name == "netstandard" || r.Name!.StartsWith("EdS.", StringComparison.Ordinal), r.Name));
    }
}

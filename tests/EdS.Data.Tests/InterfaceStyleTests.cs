using System.Text.Json;

namespace EdS.Data.Tests;

/// <summary>
/// Essai réaliste 5 (demande de Damien du 27 octobre 2026 : « améliorer complètement l'esthétisme de l'interface », style Diablo 4 avec
/// des icônes au lieu de texte ; police et icônes gratuites acceptées) : fichiers de l'interface présents et branchés.
/// </summary>
[Trait("Category", "Runtime")]
public class InterfaceStyleTests
{
    private static string Root => Lot93UnityPackageTests.Root;

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    private static readonly string Folder = Path.Combine("unity", "Assets", "EdS", "Interface");

    [Fact]
    public void Police_et_icones_presentes_avec_leurs_licences()
    {
        string resources = Path.Combine(Root, Folder, "Resources", "EdsInterface");
        byte[] font = File.ReadAllBytes(Path.Combine(resources, "Cinzel.ttf"));
        Assert.InRange(font.Length, 50_000, 400_000);
        Assert.Equal(new byte[] { 0, 1, 0, 0 }, font.Take(4).ToArray());   // police TrueType
        Assert.Contains("SIL Open Font License", Read(Folder.Split(Path.DirectorySeparatorChar).Append("Cinzel-OFL.txt").ToArray()), StringComparison.Ordinal);
        string licences = Read(Folder.Split(Path.DirectorySeparatorChar).Append("LICENCES.md").ToArray());
        Assert.Contains("CC BY 3.0", licences, StringComparison.Ordinal);
        Assert.Contains("**Lorc**", licences, StringComparison.Ordinal);
        Assert.Contains("**Delapouite**", licences, StringComparison.Ordinal);
        string credits = Read("docs", "CREDITS.md");
        Assert.Contains("game-icons.net", credits, StringComparison.Ordinal);
        Assert.Contains("Cinzel", credits, StringComparison.Ordinal);

        string[] icons = Directory.GetFiles(Path.Combine(resources, "Icones"), "*.png").Select(Path.GetFileNameWithoutExtension).OrderBy(n => n, StringComparer.Ordinal).ToArray()!;
        Assert.Equal(new[] { "barriere_d_acier", "bastion_absolu", "competence", "contrecoup", "frappe_du_rempart", "garde", "heurt_de_bouclier", "onde_de_rupture",
            "parade", "provocation" }, icons);
        foreach (string icon in icons)
        {
            byte[] png = File.ReadAllBytes(Path.Combine(resources, "Icones", icon + ".png"));
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png.Take(4).ToArray());
            // En-tête IHDR : 128 × 128, couleurs avec transparence (type 6).
            Assert.Equal(128, (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19]);
            Assert.Equal(128, (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23]);
            Assert.Equal(6, png[25]);
            Assert.InRange(png.Length, 500, 20_000);
        }

        // Chaque compétence active ou ultime du Bastion a son icône (nom = fin de son identifiant).
        using JsonDocument bastion = JsonDocument.Parse(Read("data", "v0", "classes", "bastion.json"));
        foreach (JsonElement skill in bastion.RootElement.GetProperty("data").GetProperty("skills").EnumerateArray())
        {
            string kind = skill.GetProperty("kind").GetString()!;
            if (kind != "active" && kind != "ultimate") continue;
            string id = skill.GetProperty("id").GetString()!;
            Assert.Contains(id.Substring(id.LastIndexOf('.') + 1), icons);
        }
    }

    [Fact]
    public void Interface_facon_Diablo_branchee()
    {
        string art = Read("unity", "Assets", "EdS", "Game", "HudArt.cs");
        string hud = Read("unity", "Assets", "EdS", "Game", "Hud.cs");
        string game = Read("unity", "Assets", "EdS", "Game", "EdsGame.cs");
        Assert.Contains("_font = Resources.Load<Font>(\"EdsInterface/Cinzel\");", art, StringComparison.Ordinal);
        Assert.Contains("Texture2D icon = Resources.Load<Texture2D>(\"EdsInterface/Icones/\" + name);", art, StringComparison.Ordinal);
        // Globes de vie et d'endurance, compétences en icônes avec recharge en arc, Résonance et Charges.
        Assert.Contains("HudArt.Orb(left, radius, health / (float)System.Math.Max(1, maxHealth), HealthColor,", hud, StringComparison.Ordinal);
        Assert.Contains("HudArt.Orb(right, radius, me.Endurance / (float)System.Math.Max(1, me.MaxEndurance), EnduranceColor,", hud, StringComparison.Ordinal);
        Assert.Contains("HudArt.Slot(rect, HudArt.Icon(slot.SkillId), key, slot.RemainingSeconds, slot.CooldownSeconds,", hud, StringComparison.Ordinal);
        Assert.Contains("GUI.DrawTexture(rect, _wipes[index]);", art, StringComparison.Ordinal);
        Assert.Contains("HudArt.Diamond(", hud, StringComparison.Ordinal);
        Assert.Contains("HudArt.Icon(me.ParryOpen ? \"parade\" : \"garde\")", hud, StringComparison.Ordinal);
        // Plus serré sur téléphone (commandes tactiles dans les coins) ; boutons et champs habillés partout.
        Assert.Contains("Hud.Player(player, _touch.Active);", game, StringComparison.Ordinal);
        Assert.Contains("HudArt.StyleButton(_button);", hud, StringComparison.Ordinal);
        Assert.Contains("HudArt.StyleField(_field);", hud, StringComparison.Ordinal);
        // Les grands panneaux des écrans ont leur cadre d'or.
        foreach (string screen in new[] { "AdventureScreens.cs", "NetworkScreen.cs", "TalentScreen.cs", "MiniMap.cs", "TouchControls.cs" })
            Assert.Contains("Hud.Panel(", Read("unity", "Assets", "EdS", "Game", screen), StringComparison.Ordinal);
        // Icône d'une compétence inconnue : l'icône générique.
        Assert.Contains("return Icons.TryGetValue(\"competence\", out Texture2D any) ? any : null;", art, StringComparison.Ordinal);
    }
}

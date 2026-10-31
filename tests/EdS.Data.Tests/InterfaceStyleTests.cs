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

    private static readonly string Hud = Path.Combine(Folder, "Resources", "EdsInterface", "Hud");
    private static readonly string Painted = Path.Combine(Folder, "Resources", "EdsInterface", "Peintes");

    private static (int Width, int Height) PngSize(string path)
    {
        byte[] png = File.ReadAllBytes(path);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png.Take(4).ToArray());
        Assert.Equal(6, png[25]);   // couleurs avec transparence
        return ((png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19], (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23]);
    }

    /// <summary>
    /// Essai réaliste 6 (choix de Damien du 27 octobre 2026 : interface « Fragments du Seuil », « je veux vraiment le meilleur ») : polices,
    /// dessins de l'interface et icônes peintes présents, avec leurs licences et leurs auteurs crédités.
    /// </summary>
    [Fact]
    public void Fragments_du_Seuil_presents_avec_leurs_licences()
    {
        string resources = Path.Combine(Root, Folder, "Resources", "EdsInterface");
        foreach (string font in new[] { "CinzelDecorative-Bold.ttf", "BarlowCondensed-SemiBold.ttf", "BarlowCondensed-Bold.ttf" })
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(resources, font));
            Assert.Equal(new byte[] { 0, 1, 0, 0 }, bytes.Take(4).ToArray());
            Assert.InRange(bytes.Length, 20_000, 600_000);
        }
        foreach (string licence in new[] { "CinzelDecorative-OFL.txt", "BarlowCondensed-OFL.txt" })
            Assert.Contains("SIL Open Font License", Read(Folder.Split(Path.DirectorySeparatorChar).Append(licence).ToArray()), StringComparison.Ordinal);

        // Chaque dessin demandé par l'interface existe, exporté au double de sa taille d'affichage (dimensions paires).
        string code = Read("unity", "Assets", "EdS", "Game", "HudView.cs");
        string[] sprites = System.Text.RegularExpressions.Regex.Matches(code, "UiKit\\.Sprite\\(\"([a-z_0-9]+)\"")
            .Select(m => m.Groups[1].Value).Concat(new[] { "eclat_1_masque", "eclat_5_cadre", "repere_ennemi", "repere_rond", "repere_objectif", "vague" })
            .Distinct().ToArray();
        Assert.True(sprites.Length >= 25, string.Join(", ", sprites));
        foreach (string sprite in sprites)
        {
            (int w, int h) = PngSize(Path.Combine(Root, Hud, sprite + ".png"));
            Assert.True(w % 2 == 0 && h % 2 == 0, sprite);
        }
        for (int i = 1; i <= 5; i++)
            foreach (string part in new[] { "_masque", "_cadre" })
                Assert.True(File.Exists(Path.Combine(Root, Hud, "eclat_" + i + part + ".png")), "eclat_" + i + part);

        // Icônes peintes : chaque compétence active ou ultime, chaque objet et chaque talent du Bastion (nom = fin de l'identifiant ; « t_ »
        // devant un talent), en 128 × 128.
        var icons = Directory.GetFiles(Path.Combine(Root, Painted), "*.png").Select(f => Path.GetFileNameWithoutExtension(f)!).ToHashSet(StringComparer.Ordinal);
        foreach (string icon in icons) Assert.Equal((128, 128), PngSize(Path.Combine(Root, Painted, icon + ".png")));
        using JsonDocument bastion = JsonDocument.Parse(Read("data", "v0", "classes", "bastion.json"));
        JsonElement data = bastion.RootElement.GetProperty("data");
        foreach (JsonElement skill in data.GetProperty("skills").EnumerateArray())
        {
            string kind = skill.GetProperty("kind").GetString()!;
            string id = skill.GetProperty("id").GetString()!;
            if (kind == "active" || kind == "ultimate") Assert.Contains(id.Substring(id.LastIndexOf('.') + 1), icons);
        }
        foreach (JsonElement tree in data.GetProperty("trees").EnumerateArray())
            foreach (JsonElement talent in tree.GetProperty("talents").EnumerateArray())
            {
                string id = talent.GetProperty("id").GetString()!;
                Assert.Contains("t_" + id.Substring(id.LastIndexOf('.') + 1), icons);
            }
        using JsonDocument items = JsonDocument.Parse(Read("data", "v0", "world", "items.json"));
        int itemCount = 0;
        foreach (JsonElement item in items.RootElement.GetProperty("data").GetProperty("items").EnumerateArray())
        {
            string id = item.GetProperty("id").GetString()!;
            // Sous-lot V1.2 : une icône peut être donnée (provisoire : celle d'un objet voisin), sinon la fin de l'identifiant.
            Assert.Contains(item.TryGetProperty("icon", out JsonElement icon) ? icon.GetString()! : id.Substring(id.LastIndexOf('.') + 1), icons);
            itemCount++;
        }
        Assert.True(itemCount >= 20);
        Assert.Contains("garde", icons);
        Assert.Contains("parade", icons);

        // Licences et crédits : chaque source citée (CC0, CC BY 3.0, OFL).
        string licences = Read(Folder.Split(Path.DirectorySeparatorChar).Append("LICENCES.md").ToArray());
        string credits = Read("docs", "CREDITS.md");
        foreach (string source in new[] { "Drummyfish", "LudicArts", "J. W. Bjerk", "boar-tusks", "metal-bar", "animal-hide", "Cinzel Decorative", "Barlow Condensed" })
        {
            Assert.Contains(source, licences, StringComparison.Ordinal);
            Assert.Contains(source, credits, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Essai réaliste 6 : l'interface en jeu est en uGUI (canevas mis à l'échelle sur 1080 de haut, TextMeshPro si ses ressources sont
    /// là, texte ordinaire sinon), mise à jour après la caméra, cachée sous les écrans dessinés en IMGUI ; l'ancienne interface en jeu est
    /// retirée ; le disque du bouclier et les annonces au sol sont refaits (essai de Damien du 27 octobre 2026).
    /// </summary>
    [Fact]
    public void Interface_ugui_branchee()
    {
        string game = Read("unity", "Assets", "EdS", "Game", "EdsGame.cs");
        string hud = Read("unity", "Assets", "EdS", "Game", "Hud.cs");
        string kit = Read("unity", "Assets", "EdS", "Game", "UiKit.cs");
        string text = Read("unity", "Assets", "EdS", "Game", "UiText.cs");
        string view = Read("unity", "Assets", "EdS", "Game", "HudView.cs");
        Assert.Contains("_hud = HudView.Create();", game, StringComparison.Ordinal);
        Assert.Contains("private void LateUpdate()", game, StringComparison.Ordinal);
        Assert.Contains("!_menuOpen && !LobbyShown && _talents == null && _combat != null", game, StringComparison.Ordinal);
        Assert.Contains("&& !TalentsOpen;", game, StringComparison.Ordinal);
        Assert.Contains("_hud.Player(player, _touch.Active, deltaTime);", game, StringComparison.Ordinal);
        Assert.Contains("_hud.Help(_help && open, a.PlaceName,", game, StringComparison.Ordinal);
        Assert.DoesNotContain("Hud.Player(", game, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawMini", game, StringComparison.Ordinal);
        Assert.DoesNotContain("public static void Player(", hud, StringComparison.Ordinal);
        Assert.Contains("scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);", kit, StringComparison.Ordinal);
        Assert.Contains("scaler.matchWidthOrHeight = 1f;", kit, StringComparison.Ordinal);
        Assert.Contains("ready = Resources.Load<TMP_Settings>(\"TMP Settings\") != null && Shader.Find(TmpShader) != null;", text, StringComparison.Ordinal);
        Assert.Contains("tmp.richText = false;", text, StringComparison.Ordinal);
        // Noms montrés (plus de « SANGLIER_V2 »), aide en touches venues des textes du monde, carte peinte.
        Assert.Contains("_targetName.Value = DisplayNames.Of(target.Id);", view, StringComparison.Ordinal);
        Assert.Contains("_adventureHelp = HelpKeys.Adventure(a.Text(\"text.ui.help\"));", game, StringComparison.Ordinal);
        Assert.Contains("Texture2D painted = PaintedMap.For(map);", view, StringComparison.Ordinal);
        Assert.Contains("_mapImage.uvRect = PaintedMap.Window(map, cx, cz, MapRadius);", view, StringComparison.Ordinal);
        Assert.Contains("_work = Task.Run(() => MapPainter.Paint(map, w, h));", Read("unity", "Assets", "EdS", "Game", "PaintedMap.cs"), StringComparison.Ordinal);

        // Projet : paquet uGUI, ressources de TextMeshPro importées par l'éditeur (non suivies), images importées à leur taille exacte.
        string setup = Read("unity", "Assets", "EdS", "Editor", "EdsInterfaceSetup.cs");
        Assert.Contains("AssetDatabase.ImportPackage(package, false);", setup, StringComparison.Ordinal);
        // Revue Codex de la PR #62 : dossier physique du paquet demandé au gestionnaire de paquets, puis cache des paquets.
        Assert.Contains("UnityEditor.PackageManager.PackageInfo.FindForAssetPath(\"Packages/\" + name)?.resolvedPath", setup, StringComparison.Ordinal);
        Assert.Contains("Directory.GetDirectories(cache, name + \"@*\")", setup, StringComparison.Ordinal);
        Assert.Contains("importer.npotScale = TextureImporterNPOTScale.None;", setup, StringComparison.Ordinal);
        Assert.Contains("importer.mipmapEnabled = false;", setup, StringComparison.Ordinal);
        Assert.Contains("unity/Assets/TextMesh Pro/", File.ReadAllLines(Path.Combine(Root, ".gitignore")));
        string build = Read("unity", "Assets", "EdS", "Editor", "EdsBuildShaders.cs");
        Assert.Contains("Shader text = Shader.Find(TextShader);", build, StringComparison.Ordinal);
        Assert.Contains("Shader annonce = Shader.Find(\"EdS/Annonce\");", build, StringComparison.Ordinal);

        // Bouclier : plus de disque sur le chevalier ; sa garde se voit à son clip et dans l'interface.
        string arena = Read("unity", "Assets", "EdS", "Game", "ArenaView.cs");
        Assert.Contains("_shield.SetActive(!styled && me.IsPresent && (me.Guarding || me.ParryOpen || broken));", arena, StringComparison.Ordinal);
        Assert.Contains("mine.Update(_player.transform.position, me.IsDead, Time.deltaTime, me.Guarding || me.ParryOpen);", arena, StringComparison.Ordinal);
        Assert.Contains("StyleRules.Motion(_speed, dead, false, guarding)", Read("unity", "Assets", "EdS", "Game", "StyleLook.cs"), StringComparison.Ordinal);

        // Annonces au sol : shader dédié, réglé par annonce ; imparable signalé.
        string shader = Read("unity", "Assets", "EdS", "Game", "Annonce.shader");
        string space = Read("unity", "Assets", "EdS", "Game", "SpaceView.cs");
        Assert.Contains("Shader \"EdS/Annonce\"", shader, StringComparison.Ordinal);
        foreach (string property in new[] { "_Progress", "_Kind", "_Unblockable", "_Linger" })
        {
            Assert.Contains(property, shader, StringComparison.Ordinal);
            Assert.Contains("_block.SetFloat(\"" + property + "\"", space, StringComparison.Ordinal);
        }
        Assert.Contains("if (other.View.NextAttack is { } next && next.Unblockable) unblockable.Add(other.Id + \"/\" + next.Id);", arena, StringComparison.Ordinal);
    }

    /// <summary>
    /// Essai réaliste 6, étape 2 (essai de Damien du 27 octobre 2026 : « l'arbre des compétences […] je ne peux pas l'ouvrir ») : T ouvre
    /// l'arbre des Éclats dans l'aventure (uGUI, clics et survol), sur les talents du personnage ; les points passent par l'éditeur (le
    /// validateur unique) et se gardent par l'aventure (hors combat, effet à la halte).
    /// </summary>
    [Fact]
    public void Arbre_des_eclats_ouvrable_dans_l_aventure()
    {
        string game = Read("unity", "Assets", "EdS", "Game", "EdsGame.cs");
        string view = Read("unity", "Assets", "EdS", "Game", "TalentTreeView.cs");
        string kit = Read("unity", "Assets", "EdS", "Game", "UiKit.cs");
        Assert.Contains("_talentView.Open(a.CreateTalentEditor(), a.Text, a.SetTalents);", game, StringComparison.Ordinal);
        Assert.Contains("if (!talking && !a.MustContinue && (Input.GetKeyDown(KeyCode.T) || _touch.TalentsPressed))", game, StringComparison.Ordinal);
        Assert.Contains("if (Input.GetKeyDown(KeyCode.T) || Input.GetKeyDown(KeyCode.Escape)) _talentView.Cancel();", game, StringComparison.Ordinal);
        Assert.Contains("|| a.MustContinue || TalentsOpen;", game, StringComparison.Ordinal);
        Assert.Contains("BuildViolation why = delta > 0 ? _editor.Add(node.Talent.Id) : _editor.Remove(node.Talent.Id);", view, StringComparison.Ordinal);
        Assert.Contains("string refusal = _apply(_editor.Build);", view, StringComparison.Ordinal);
        Assert.Contains("go.AddComponent<StandaloneInputModule>();", kit, StringComparison.Ordinal);
        Assert.Contains("ButtonId.Talents", Read("unity", "Assets", "EdS", "Game", "TouchControls.cs"), StringComparison.Ordinal);
        foreach (string sprite in new[] { "noeud_masque", "noeud_fond", "noeud_cadre", "noeud_cadre_sobre", "majeur_masque", "majeur_cadre", "anneau_progression",
                     "graine", "pip_on", "pip_off" })
            PngSize(Path.Combine(Root, Hud, sprite + ".png"));
        // Chaque talent du Bastion a sa description dans les textes du monde, et chaque arbre sa devise.
        using JsonDocument texts = JsonDocument.Parse(Read("data", "v0", "world", "texts", "fr.json"));
        JsonElement entries = texts.RootElement.GetProperty("data").GetProperty("entries");
        using JsonDocument bastion = JsonDocument.Parse(Read("data", "v0", "classes", "bastion.json"));
        foreach (JsonElement tree in bastion.RootElement.GetProperty("data").GetProperty("trees").EnumerateArray())
        {
            string treeId = tree.GetProperty("id").GetString()!;
            Assert.True(entries.TryGetProperty("text.tree." + treeId.Substring(treeId.LastIndexOf('.') + 1), out _), treeId);
            foreach (JsonElement talent in tree.GetProperty("talents").EnumerateArray())
            {
                string id = talent.GetProperty("id").GetString()!;
                Assert.True(entries.TryGetProperty("text.talent." + id.Substring(id.LastIndexOf('.') + 1), out JsonElement text), id);
                Assert.True(text.GetString()!.Length > 10, id);
            }
        }
    }

    /// <summary>
    /// Essai réaliste 6, étape 2 (« pareil pour l'inventaire, l'aide tout ces petits détails ») : les écrans de l'aventure (Personnage,
    /// Journal, Atelier, Carte) et le dialogue sont en uGUI, dans un panneau à onglets dont l'un ouvre l'arbre des Éclats ; les actions
    /// passent par l'aventure (donc par le cœur) ; l'ancienne carte en IMGUI est retirée.
    /// </summary>
    [Fact]
    public void Ecrans_de_l_aventure_en_ugui()
    {
        string game = Read("unity", "Assets", "EdS", "Game", "EdsGame.cs");
        // Sous-lot V1.2 : les places, les filtres, le rangement et la comparaison sont dans AdventureBook.Equipment.cs.
        string book = Read("unity", "Assets", "EdS", "Game", "AdventureBook.cs") + Read("unity", "Assets", "EdS", "Game", "AdventureBook.Equipment.cs");
        string screens = Read("unity", "Assets", "EdS", "Game", "AdventureScreens.cs");
        Assert.Contains("_book = AdventureBook.Create();", game, StringComparison.Ordinal);
        Assert.Contains("AdventureBook.Request request = _book.Update(_adventure, _screens, AdventureEnemies(), deltaTime);", game, StringComparison.Ordinal);
        Assert.Contains("else if (request == AdventureBook.Request.Talents) OpenAdventureTalents(_adventure);", game, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawDialogue", game, StringComparison.Ordinal);
        Assert.DoesNotContain("Hud.", screens, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(Root, "unity", "Assets", "EdS", "Game", "MiniMap.cs")));
        foreach (string action in new[] { "_session.Unequip(s);", "_session.Equip(place);", "_session.Craft(_recipeData[index].Id);", "_session?.Choose(index)",
                     "_session?.CloseDialogue()", "_session.Swap(slot, other.Id);", "_session.SortInventory(_sort);", "a.Compare(item, wornPlace)",
                     "_visible = a.InventoryPlaces(_filter);" })
            Assert.Contains(action, book, StringComparison.Ordinal);
        Assert.Contains("_map.uvRect = PaintedMap.Area(map);", book, StringComparison.Ordinal);
        using JsonDocument texts = JsonDocument.Parse(Read("data", "v0", "world", "texts", "fr.json"));
        JsonElement entries = texts.RootElement.GetProperty("data").GetProperty("entries");
        foreach (string key in new[] { "text.ui.tab.character", "text.ui.tab.skills", "text.ui.tab.talents", "text.ui.tab.journal", "text.ui.tab.crafting", "text.ui.tab.map" })
            Assert.True(entries.TryGetProperty(key, out _), key);
    }

    /// <summary>
    /// Sous-lot V1.5 : l'écran des compétences est un onglet du panneau de l'aventure, ouvert par B (K reste l'atelier, aucune autre
    /// touche ne prend B) ; on y glisse une compétence sur une place (souris ou doigt) ou on la touche puis la place, et tout passe par
    /// l'aventure (donc par le juge du cœur). Les API de glissé de Unity ont leurs bouchons.
    /// </summary>
    [Fact]
    public void Ecran_des_competences_branche()
    {
        string game = Read("unity", "Assets", "EdS", "Game", "EdsGame.cs");
        string book = Read("unity", "Assets", "EdS", "Game", "AdventureBook.cs");
        string skills = Read("unity", "Assets", "EdS", "Game", "AdventureBook.Skills.cs");
        string kit = Read("unity", "Assets", "EdS", "Game", "UiKit.cs");
        string stubs = Read("tests", "EdS.Unity.CompileCheck", "UnityApiStubs.cs");
        Assert.Contains("if (Input.GetKeyDown(KeyCode.B)) _screens.Toggle(AdventureScreens.Panel.Skills);", game, StringComparison.Ordinal);
        Assert.Contains("if (Input.GetKeyDown(KeyCode.K) || _touch.WorkshopPressed) _screens.Toggle(AdventureScreens.Panel.Crafting);", game, StringComparison.Ordinal);
        int bKeys = Directory.GetFiles(Path.Combine(Root, "unity", "Assets", "EdS"), "*.cs", SearchOption.AllDirectories)
            .Sum(f => System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(f), @"KeyCode\.B\b").Count);
        Assert.Equal(1, bKeys);
        Assert.Contains("public sealed partial class AdventureBook", book, StringComparison.Ordinal);
        Assert.Contains("(AdventureScreens.Panel.Skills, \"text.ui.tab.skills\", 165f)", book, StringComparison.Ordinal);
        Assert.Contains("case AdventureScreens.Panel.Skills: RefreshSkills(a); break;", book, StringComparison.Ordinal);
        Assert.Contains("case AdventureScreens.Panel.Skills: return \"B\";", book, StringComparison.Ordinal);
        // Écran quitté : plus de glissé ni de choix, et le dernier message (un refus en plein combat) ne reste pas à la réouverture.
        Assert.Contains("if (!book || screens.Open != AdventureScreens.Panel.Skills) LeaveSkills();", book, StringComparison.Ordinal);
        Assert.Contains("_skillMessage = null;", skills, StringComparison.Ordinal);
        // Relecture indépendante de la PR #71 : la fin d'un glissé n'est pas un clic ; un lâcher sur la place de départ ne fait rien ; une
        // action sans effet (le personnage n'a pas changé) n'affiche ni ne sonne rien ; le motif d'un refus vient du cœur (un par cas).
        Assert.Contains("if (eventData.dragging) return;", kit, StringComparison.Ordinal);
        Assert.Contains("bool home = ReferenceEquals(_dragSource, cell);", skills, StringComparison.Ordinal);
        Assert.Contains("if (refusal == null && ReferenceEquals(before, _session.World.Character)) return;", skills, StringComparison.Ordinal);
        Assert.Contains("_skillsStatus.Value = book.Locked ?? \"\";", skills, StringComparison.Ordinal);
        Assert.DoesNotContain("text.ui.skills.in_combat", skills, StringComparison.Ordinal);
        Assert.DoesNotContain("clic droit : vider", skills, StringComparison.Ordinal);
        Assert.Contains("pointer.Enter = () => _hoveredSkill = _upcomingSkills[at];", skills, StringComparison.Ordinal);
        Assert.Contains("book.HasReserve(cell.Kind) ? \"Libre\" : \"Rien à placer pour l'instant\"", skills, StringComparison.Ordinal);
        foreach (string action in new[] { "Apply(() => _session.PlaceSkill(cell.Kind, cell.Index, skill));", "Apply(() => _session.RemoveSkill(cell.Kind, cell.Index));",
                     "UiKit.Drop(cell.Hit).Dropped = () => DropOn(c);", "UiDrag drag = UiKit.Drag(cell.Hit);", "UiDrag drag = UiKit.Drag(row.Slab);",
                     "RectTransformUtility.ScreenPointToLocalPointInRectangle(_panel, screen, null, out Vector2 local)" })
            Assert.Contains(action, skills, StringComparison.Ordinal);
        Assert.Contains("public sealed class UiDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler", kit, StringComparison.Ordinal);
        Assert.Contains("public sealed class UiDrop : MonoBehaviour, IDropHandler", kit, StringComparison.Ordinal);
        foreach (string stub in new[] { "public interface IBeginDragHandler", "public interface IDragHandler", "public interface IEndDragHandler",
                     "public interface IDropHandler", "public Vector2 position { get; set; }", "public bool dragging { get; set; }", "ScreenPointToLocalPointInRectangle(RectTransform rect, Vector2 screenPoint, Camera cam, out Vector2 localPoint)", " A, B, C," })
            Assert.Contains(stub, stubs, StringComparison.Ordinal);
        // Dessins de l'écran : ceux des places (éclats de la barre de combat, nœuds de l'arbre) et l'icône des compétences sans peinture.
        foreach (string sprite in System.Text.RegularExpressions.Regex.Matches(skills, "UiKit\\.Sprite\\(\"([a-z_0-9]+)\"").Select(m => m.Groups[1].Value)
                     .Concat(new[] { "noeud_fond", "noeud_masque", "noeud_cadre", "eclat_4_masque", "eclat_5_cadre" }).Distinct())
            Assert.True(File.Exists(Path.Combine(Root, Hud, sprite + ".png")), sprite);
        Assert.True(File.Exists(Path.Combine(Root, Painted, "eclat_de_seuil.png")));
        string unity = Read("docs", "UNITY.md");
        Assert.Contains("| B | écran des compétences", unity, StringComparison.Ordinal);
    }

    /// <summary>Essai réaliste 6, étape 3 : finitions animées (niveau gagné, vie basse, coup reçu par la cible, fondus).</summary>
    [Fact]
    public void Finitions_animees()
    {
        string hud = Read("unity", "Assets", "EdS", "Game", "HudView.cs");
        string game = Read("unity", "Assets", "EdS", "Game", "EdsGame.cs");
        Assert.Contains("if (_shownLevel > 0 && level > _shownLevel)", game, StringComparison.Ordinal);
        Assert.Contains("_hud.LevelUp(level, level > a.World.EnteredLevel", game, StringComparison.Ordinal);
        Assert.Contains("_danger = me.IsDead || me.Health == null ? 0f : Mathf.Clamp01((0.3f - healthFraction) / 0.3f);", hud, StringComparison.Ordinal);
        Assert.Contains("if (_lastTargetFraction >= 0f && fraction < _lastTargetFraction - 0.0005f) _targetFlashAlpha = 0.65f;", hud, StringComparison.Ordinal);
        Assert.Contains("_fade.alpha = Mathf.MoveTowards(_fade.alpha, 1f, deltaTime * 6f);", Read("unity", "Assets", "EdS", "Game", "TalentTreeView.cs"), StringComparison.Ordinal);
        Assert.Contains("_fade.alpha = Mathf.MoveTowards(_fade.alpha, 1f, deltaTime * 6f);", Read("unity", "Assets", "EdS", "Game", "AdventureBook.cs"), StringComparison.Ordinal);
    }

    /// <summary>
    /// Relecture indépendante de l'essai 6 : icônes du sac visibles (bord sans centre), bords rouges de la vie basse sur une image blanche,
    /// survol visible, ombre et contour par matériau partagé (variantes emportées par la construction), pas de menu par-dessus l'arbre,
    /// annonces refaites seulement quand leur forme change, et aucun symbole absent des polices de l'interface (TextMeshPro les
    /// afficherait en carrés).
    /// </summary>
    [Fact]
    public void Corrections_de_la_relecture()
    {
        string book = Read("unity", "Assets", "EdS", "Game", "AdventureBook.cs");
        string hud = Read("unity", "Assets", "EdS", "Game", "HudView.cs");
        string text = Read("unity", "Assets", "EdS", "Game", "UiText.cs");
        Assert.Contains("cell.Edge.fillCenter = false;", book, StringComparison.Ordinal);
        Assert.Contains("UiKit.Sprite(\"bord_haut\")", hud, StringComparison.Ordinal);
        Assert.Contains("UiKit.Sprite(\"bord_bas\")", hud, StringComparison.Ordinal);
        Assert.Contains("material.EnableKeyword(\"OUTLINE_ON\");", text, StringComparison.Ordinal);
        Assert.Contains("if (material != null) _tmp.fontSharedMaterial = material;", text, StringComparison.Ordinal);
        Assert.DoesNotContain("fontMaterial", text.Replace("fontSharedMaterial", ""), StringComparison.Ordinal);
        Assert.Contains("outlined.EnableKeyword(\"OUTLINE_ON\");", Read("unity", "Assets", "EdS", "Editor", "EdsBuildShaders.cs"), StringComparison.Ordinal);
        Assert.Contains("if (!TalentsOpen && ((Keys.MenuPressed", Read("unity", "Assets", "EdS", "Game", "EdsGame.cs"), StringComparison.Ordinal);
        Assert.Contains("if (key != _shape)", Read("unity", "Assets", "EdS", "Game", "SpaceView.cs"), StringComparison.Ordinal);
        foreach (string sprite in new[] { "bord_haut", "bord_bas" }) PngSize(Path.Combine(Root, Hud, sprite + ".png"));
        // Symboles absents de Cinzel et de Barlow Condensed : interdits dans les textes de l'interface uGUI.
        foreach (string file in new[] { "HudView.cs", "AdventureBook.cs", "AdventureBook.Skills.cs", "TalentTreeView.cs", "UiKit.cs", "EdsGame.cs" })
        {
            string code = Read("unity", "Assets", "EdS", "Game", file);
            foreach (char symbol in "◆◇●○✓→★■▲►")
                Assert.False(code.Contains(symbol), $"{file} : « {symbol} »");
        }
    }

    /// <summary>
    /// Essai réaliste 7 (essai de Damien du 28 octobre 2026 : « les icônes sont juste trop gros, faut tous les rétrécir », « c'est quoi les
    /// petits trucs juste au-dessus de la barre de vie ? », puis sons, effets, animations et musique) : éclats réduits, Charges cachées sans
    /// Charge, sons de l'interface et textures des particules livrés (CC0) avec leurs licences, effets et musique branchés.
    /// </summary>
    [Fact]
    public void Essai_7_branche()
    {
        string hud = Read("unity", "Assets", "EdS", "Game", "HudView.cs");
        Assert.Contains("public const float ShardScale = 0.74f;", hud, StringComparison.Ordinal);
        Assert.Contains("shard.Node.localScale = new Vector3(ShardScale, ShardScale, 1f);", hud, StringComparison.Ordinal);
        Assert.Contains("ChargesShown = total > 0 && me.RampartCharges > 0;", hud, StringComparison.Ordinal);
        Assert.Contains("UiSounds.Play(UiCue.ChargeGained)", hud, StringComparison.Ordinal);
        Assert.Contains("UiSounds.Play(UiCue.SkillReady)", hud, StringComparison.Ordinal);
        Assert.Contains("UiSounds.Play(UiCue.LevelUp)", hud, StringComparison.Ordinal);

        // Sons de l'interface : un fichier Ogg par son demandé par UiSounds.
        string sons = Path.Combine(Root, Folder, "Resources", "EdsInterface", "Sons");
        string sounds = Read("unity", "Assets", "EdS", "Game", "UiSounds.cs");
        string[] names = System.Text.RegularExpressions.Regex.Matches(sounds, @"\[UiCue\.\w+\] = \(""(\w+)""").Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal(13, names.Length);
        Assert.Equal(names.OrderBy(n => n, StringComparer.Ordinal), Directory.GetFiles(sons, "*.ogg").Select(f => Path.GetFileNameWithoutExtension(f)!).OrderBy(n => n, StringComparer.Ordinal));
        foreach (string name in names)
        {
            byte[] ogg = File.ReadAllBytes(Path.Combine(sons, name + ".ogg"));
            Assert.Equal("OggS"u8.ToArray(), ogg.Take(4).ToArray());
            Assert.InRange(ogg.Length, 1_000, 80_000);
        }

        // Textures des particules : une par système d'Effects, PNG de 128 × 128.
        string effects = Read("unity", "Assets", "EdS", "Game", "Effects.cs");
        string[] textures = System.Text.RegularExpressions.Regex.Matches(effects, @"Make\(root, template, Kind\.\w+, ""(\w+)""").Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal(12, textures.Length);
        string particules = Path.Combine(Root, Folder, "Resources", "EdsInterface", "Particules");
        Assert.Equal(textures.OrderBy(n => n, StringComparer.Ordinal), Directory.GetFiles(particules, "*.png").Select(f => Path.GetFileNameWithoutExtension(f)!).OrderBy(n => n, StringComparer.Ordinal));
        foreach (string texture in textures) Assert.Equal((128, 128), PngSize(Path.Combine(particules, texture + ".png")));
        string shader = Read("unity", "Assets", "EdS", "Game", "Particule.shader");
        Assert.Contains("Shader \"EdS/Particule\"", shader, StringComparison.Ordinal);
        Assert.Contains("Blend [_SrcBlend] [_DstBlend]", shader, StringComparison.Ordinal);
        Assert.Contains("Shader.Find(\"EdS/Particule\")", Read("unity", "Assets", "EdS", "Editor", "EdsBuildShaders.cs"), StringComparison.Ordinal);
        Assert.Contains("public const string ParticuleName = \"EdsParticule\";", Read("unity", "Assets", "EdS", "Editor", "EdsBuildShaders.cs"), StringComparison.Ordinal);
        Assert.Contains("public const string MaterialName = \"EdsParticule\";", effects, StringComparison.Ordinal);

        string licences = Read(Folder.Split(Path.DirectorySeparatorChar).Append("LICENCES.md").ToArray());
        Assert.Contains("Interface Sounds", licences, StringComparison.Ordinal);
        Assert.Contains("Particle Pack", licences, StringComparison.Ordinal);
        Assert.Contains("Music Jingles", licences, StringComparison.Ordinal);
        string credits = Read("docs", "CREDITS.md");
        Assert.Contains("Matthew Pablo", credits, StringComparison.Ordinal);
        Assert.Contains("Kenney", credits, StringComparison.Ordinal);

        // Branchements : gestes des compétences, coups reçus, attaques des adversaires, effets, caméra, musique, sons des écrans.
        string game = Read("unity", "Assets", "EdS", "Game", "EdsGame.cs");
        Assert.Contains("_effects = Effects.Create();", game, StringComparison.Ordinal);
        Assert.Contains("_music = StyleAudio.Create(_camera.Camera.transform);", game, StringComparison.Ordinal);
        // Revue Codex de la PR #63 : l'écouteur existe avant que la musique parte.
        Assert.Contains("EnsureListener();\n                _music = StyleAudio.Create(_camera.Camera.transform);", game.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.Contains("_mood = a.Mood();", game, StringComparison.Ordinal);   // relecture : quatre fois par seconde
        Assert.Contains("_music.Update(_mood,", game, StringComparison.Ordinal);
        // Relecture de l'essai 7 : la musique est détruite en quittant l'aventure (la suivante la recrée, ambiances comprises).
        Assert.Contains("_music?.Destroy();\n            _music = null;", game.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.Contains("_arena.Strike(skill.SkillId, skill.IsUltimate);", game, StringComparison.Ordinal);
        Assert.Contains("double shock = GameFeel.Shock(feedback.Kind, mine, feedback.Amount, maxHealth, boss);", game, StringComparison.Ordinal);
        Assert.Contains("foreach ((string actor, string attackId) in _arena.PlayAttacks(_combat))", game, StringComparison.Ordinal);
        // Relecture de l'essai 7 : les attaques immédiates (morsure…) font jouer leur geste avant le tour de l'assaillant.
        string arena = Read("unity", "Assets", "EdS", "Game", "ArenaView.cs");
        Assert.Contains("GameFeel.Imminent(view, target, now, lead)", arena, StringComparison.Ordinal);
        Assert.Contains("main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;", effects, StringComparison.Ordinal);
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(game, @"SyncSpace\(_combat, _camera\.Camera\.transform\.position, Time\.deltaTime\);\s+SyncGestures\(\);").Count);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(game, @"_arena\.Strike\(\);"));   // seulement pour une compétence introuvable
        Assert.Contains("(double right, double up, double roll) = GameFeel.Offset(_shock, Time.time);", Read("unity", "Assets", "EdS", "Game", "CameraRig.cs"), StringComparison.Ordinal);
        Assert.Contains("UiSounds.Play(book ? UiCue.Open : UiCue.Close);", Read("unity", "Assets", "EdS", "Game", "AdventureBook.cs"), StringComparison.Ordinal);
        Assert.Contains("UiSounds.Play(button.Enabled ? UiCue.Click : UiCue.Error);", Read("unity", "Assets", "EdS", "Game", "UiKit.cs"), StringComparison.Ordinal);
        Assert.Contains("UiSounds.Play(why != null ? UiCue.Error : delta > 0 ? UiCue.TalentAdded : UiCue.TalentRemoved);", Read("unity", "Assets", "EdS", "Game", "TalentTreeView.cs"), StringComparison.Ordinal);
        string style = Read("unity", "Assets", "EdS", "Editor", "EdsStyle.cs");
        Assert.Contains("catalog.SoundSets =", style, StringComparison.Ordinal);
        Assert.Contains("catalog.Music =", style, StringComparison.Ordinal);
        Assert.Contains("catalog.Ambiences =", style, StringComparison.Ordinal);
        Assert.Contains("AudioClipLoadType.Streaming", style, StringComparison.Ordinal);
    }
}

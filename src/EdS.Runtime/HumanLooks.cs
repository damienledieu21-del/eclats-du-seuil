using System;
using System.Collections.Generic;
using System.Linq;

namespace EdS.Runtime
{
    /// <summary>Sous-lot V2.9.2 : coiffure d'un corps humain (fichiers « Hair_… » des Universal Base Characters de Quaternius).</summary>
    public enum HumanHair
    {
        None = 0,
        Buzzed = 1,
        SimpleParted = 2,
        Long = 3,
        Buns = 4,
        BuzzedFemale = 5,
    }

    /// <summary>Sous-lot V2.9.2 : genre de tenue d'un corps humain (couleurs peintes par la préparation sur la peau du modèle).</summary>
    public enum HumanOutfit
    {
        Peasant = 0,
        Guard = 1,
        Sentinel = 2,
        Clergy = 3,
        Smith = 4,
        Merchant = 5,
        Ranger = 6,
        Bandit = 7,
        Hero = 8,
    }

    /// <summary>
    /// Sous-lot V2.9.2 : l'aspect d'un humain, tiré de son identifiant (toujours le même d'une partie à l'autre) : corps d'homme ou de
    /// femme, peau claire ou sombre, coiffure, barbe, couleur des cheveux, tenue (une des tenues peintes), taille et carrure.
    /// </summary>
    public readonly struct HumanLook
    {
        public HumanLook(bool female, bool darkSkin, HumanHair hair, bool beard, int hairTone, HumanOutfit outfit, int palette, double height, double width)
        {
            Female = female;
            DarkSkin = darkSkin;
            Hair = hair;
            Beard = beard;
            HairTone = hairTone;
            Outfit = outfit;
            Palette = palette;
            Height = height;
            Width = width;
        }

        public bool Female { get; }
        public bool DarkSkin { get; }
        public HumanHair Hair { get; }
        public bool Beard { get; }

        /// <summary>Couleur des cheveux : indice dans <see cref="HumanLooks.HairTones"/>.</summary>
        public int HairTone { get; }

        public HumanOutfit Outfit { get; }

        /// <summary>Tenue peinte : indice dans <see cref="HumanLooks.Palettes"/> (une texture par corps, peau et tenue).</summary>
        public int Palette { get; }

        /// <summary>Taille (m), pieds au sommet du crâne.</summary>
        public double Height { get; }

        /// <summary>Carrure : échelle de largeur (0,95 à 1,07).</summary>
        public double Width { get; }
    }

    /// <summary>
    /// Sous-lot V2.9.2 (4e vague, demande de Damien du 7 novembre 2026 : « villageois et personnages en vrais corps humanoïdes ») : règles
    /// pures des corps humains faits des Universal Base Characters de Quaternius (homme et femme, peaux claire et sombre, coiffures, barbe)
    /// animés en Humanoid par les Universal Animation Library 1 et 2 : reconnaissance des fichiers, noms des animations, aspect de chaque
    /// PNJ d'après son identifiant. Affichage seulement : rien ne revient vers le moteur.
    /// </summary>
    public static class HumanLooks
    {
        /// <summary>
        /// Couleurs des cheveux, multipliées par la texture grise des coiffures (gris moyen 0,56 : d'où des teintes claires) : noir, brun,
        /// châtain, roux, blond, gris.
        /// </summary>
        public static readonly (byte R, byte G, byte B)[] HairTones =
        {
            (48, 38, 32), (120, 82, 56), (176, 122, 78), (220, 104, 52), (255, 226, 150), (236, 232, 226),
        };

        /// <summary>
        /// Tenues peintes par la préparation sur la peau des corps : tunique (buste et manches), chausses (jambes), bottes, ceinture. Les
        /// premières sont celles des paysans ; puis les gardes du bourg (tabard bleu), les sentinelles (rouge), le clergé (robe de laine
        /// claire), la forge (tablier de cuir), les marchands, les rôdeurs (vert sombre), les bandits (cuir sombre), le héros (acier bleuté).
        /// </summary>
        public static readonly (HumanOutfit Outfit, (byte R, byte G, byte B) Tunic, (byte R, byte G, byte B) Trousers, (byte R, byte G, byte B) Boots)[] Palettes =
        {
            (HumanOutfit.Peasant, (138, 120, 92), (92, 78, 60), (58, 42, 30)),     // lin écru, chausses brunes
            (HumanOutfit.Peasant, (96, 112, 70), (84, 70, 54), (52, 38, 28)),      // vert de saule
            (HumanOutfit.Peasant, (150, 84, 58), (70, 66, 60), (56, 40, 28)),      // rouille
            (HumanOutfit.Peasant, (84, 96, 120), (98, 86, 66), (50, 36, 26)),      // bleu de guède passé
            (HumanOutfit.Peasant, (168, 150, 112), (110, 90, 64), (64, 46, 32)),   // toile claire
            (HumanOutfit.Guard, (46, 70, 128), (62, 62, 66), (40, 34, 30)),        // tabard du bourg
            (HumanOutfit.Sentinel, (122, 36, 34), (60, 58, 60), (38, 32, 28)),     // rouge des sentinelles
            (HumanOutfit.Clergy, (196, 190, 172), (180, 174, 158), (70, 56, 42)),  // robe de laine claire
            (HumanOutfit.Smith, (86, 62, 44), (64, 56, 50), (44, 32, 24)),         // tablier de cuir
            (HumanOutfit.Merchant, (112, 52, 86), (88, 70, 52), (60, 42, 30)),     // drap teint (prune)
            (HumanOutfit.Ranger, (58, 76, 52), (66, 60, 46), (46, 36, 26)),        // vert de forêt
            (HumanOutfit.Bandit, (54, 46, 42), (46, 42, 40), (30, 26, 24)),        // cuir sombre
            (HumanOutfit.Hero, (92, 106, 128), (70, 72, 80), (48, 40, 34)),        // acier bleuté
        };

        /// <summary>
        /// Genre des PNJ nommés (docs/ECRITURE.md, relu le 8 novembre 2026) et des rôles de leurs identifiants : vrai pour une femme. Les
        /// autres sont tirés de leur identifiant.
        /// </summary>
        private static readonly Dictionary<string, bool> KnownFemale = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            // Prénoms (relecture de la PR #117 : tous les PNJ nommés d'ECRITURE.md).
            ["rosalie"] = true, ["aubine"] = true, ["sauge"] = true, ["bertille"] = true, ["colombe"] = true, ["garance"] = true, ["mahaut"] = true,
            ["brunehilde"] = true, ["achille"] = false, ["aldric"] = false, ["ancel"] = false, ["colin"] = false, ["firmin"] = false, ["galeran"] = false,
            ["guerin"] = false, ["berenger"] = false,
            ["odile"] = true, ["albe"] = true, ["maelle"] = true, ["lison"] = true, ["ysolde"] = true, ["epiciere"] = true, ["apprentie"] = true,
            ["veuve"] = true, ["fermiere"] = true, ["maitresse"] = true, ["herboriste"] = true, ["habitant"] = true, ["habitante"] = true,
            ["boulanger"] = false, ["pretre"] = false, ["renart"] = false, ["gaspard"] = false, ["capitaine"] = false, ["doyen"] = false,
            ["fermier"] = false, ["intendant"] = false, ["armurier"] = false, ["thibaut"] = false, ["deserteur"] = false, ["archiviste"] = false,
            ["ysarn"] = false, ["aubergiste"] = false, ["mathurin"] = false, ["anselme"] = false,
        };

        /// <summary>Genre connu d'un nom ou d'un rôle (réduit : « Maëlle » → maelle) : vrai pour une femme, null s'il n'est pas connu.</summary>
        public static bool? KnownGender(string name) => KnownFemale.TryGetValue(Plain(name), out bool f) ? f : (bool?)null;

        /// <summary>Nom réduit sans accents (« Maëlle » → maelle, « Bérenger » → berenger).</summary>
        private static string Plain(string name)
        {
            // Sans String.Normalize (inopérant en mode de globalisation invariante) : les lettres accentuées du français, une à une.
            const string accented = "àâäéèêëîïôöùûüçœæ", plain = "aaaeeeeiioouuucoa";
            var chars = StyleRules.Key(name ?? string.Empty).ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                int at = accented.IndexOf(chars[i]);
                if (at >= 0) chars[i] = plain[at];
            }
            return new string(chars);
        }

        /// <summary>Âgés : cheveux gris.</summary>
        private static readonly string[] Elders = { "doyen", "veuve", "archiviste" };

        /// <summary>
        /// Aspect de l'humain <paramref name="id"/> (identifiant du villageois ou du PNJ : « npc.odile », « bourg/bavard1 ») dans le rôle
        /// <paramref name="role"/> (personnage : « paysan », « garde », « sentinelle », « bandit »…). Toujours le même pour le même identifiant.
        /// </summary>
        public static HumanLook For(string id, string role)
        {
            // Revue Codex de la PR #117 (tour 2) : les traits du corps (genre, peau, cheveux, barbe, taille, carrure) ne dépendent que de
            // l'identité ; un PNJ (« npc.… ») est le même dehors, dans une maison ou à l'auberge, quel que soit le rôle d'affichage passé
            // (son identifiant, « paysan »…) : son rôle est alors son identité. Le rôle d'un villageois du décor choisit sa tenue et sa part de femmes.
            string who = StyleRules.CharacterOf(id ?? string.Empty);
            bool npc = (id ?? string.Empty).StartsWith("npc.", StringComparison.Ordinal);
            string part = npc ? who : StyleRules.CharacterOf(role ?? string.Empty);
            string seed = id ?? string.Empty;
            int Roll(string salt, int count) => StyleRules.Variant(seed + "#" + salt, count);
            bool female = KnownFemale.TryGetValue(who, out bool f) ? f
                : KnownFemale.TryGetValue(part, out bool g) ? g
                : Roll("genre", 100) < FemaleShare(part);
            bool dark = Roll("peau", 100) < 22;
            bool elder = Elders.Contains(who) || Roll("age", 100) < 8;
            int tone = elder ? 5 : dark ? (Roll("teinte", 100) < 70 ? 0 : 1) : Roll("teinte", 5);
            HumanHair hair;
            int h = Roll("coiffure", 100);
            if (female) hair = h < 45 ? HumanHair.Long : h < 85 ? HumanHair.Buns : HumanHair.BuzzedFemale;
            else hair = h < 45 ? HumanHair.Buzzed : h < 90 ? HumanHair.SimpleParted : HumanHair.Long;
            bool beard = !female && Roll("barbe", 100) < (elder ? 70 : 45);
            HumanOutfit outfit = OutfitOf(who, part);
            int palette = PaletteOf(outfit, Roll("tenue", 997));
            double height = female ? 1.62 + 0.10 * Roll("taille", 1000) / 999.0 : 1.72 + 0.14 * Roll("taille", 1000) / 999.0;
            double width = 0.95 + 0.12 * Roll("carrure", 1000) / 999.0;
            return new HumanLook(female, dark, hair, beard, tone, outfit, palette, height, width);
        }

        /// <summary>Part de femmes (sur 100) d'un rôle sans genre connu.</summary>
        private static int FemaleShare(string role)
        {
            switch (role)
            {
                case "garde": case "sentinelle": case "bandit": return 25;
                case "archer": case "rodeur": return 35;
                default: return 50;
            }
        }

        /// <summary>Tenue d'après le nom du PNJ (<paramref name="who"/>) puis son rôle (<paramref name="role"/>).</summary>
        public static HumanOutfit OutfitOf(string who, string role)
        {
            foreach (string key in new[] { who ?? string.Empty, role ?? string.Empty })
                switch (key)
                {
                    case "garde": case "capitaine": case "archer": return HumanOutfit.Guard;
                    case "sentinelle": return HumanOutfit.Sentinel;
                    case "pretre": case "albe": case "doyen": case "fidele": case "archiviste": return HumanOutfit.Clergy;
                    case "maelle": case "armurier": case "apprentie": case "forgeron": return HumanOutfit.Smith;
                    case "renart": case "epiciere": case "boulanger": case "aubergiste": case "marchand": case "intendant": case "maitresse": return HumanOutfit.Merchant;
                    case "rodeur": return HumanOutfit.Ranger;
                    case "bandit": case "deserteur": return HumanOutfit.Bandit;
                    case "heros": case "hero": return HumanOutfit.Hero;
                }
            return HumanOutfit.Peasant;
        }

        /// <summary>Tenue peinte (indice de <see cref="Palettes"/>) pour <paramref name="outfit"/> : une des variantes, d'après <paramref name="roll"/>.</summary>
        public static int PaletteOf(HumanOutfit outfit, int roll)
        {
            var choices = new List<int>();
            for (int i = 0; i < Palettes.Length; i++)
                if (Palettes[i].Outfit == outfit) choices.Add(i);
            if (choices.Count == 0) return 0;
            return choices[Math.Abs(roll) % choices.Count];
        }

        /// <summary>
        /// Un rôle de villageois du décor (« paysan », « garde », « sentinelle ») ou un PNJ qui parle (« npc.… ») : avec les corps humains
        /// prêts et le réglage <c>Human Villagers</c>, il prend un corps humain plutôt que son personnage de Mixamo.
        /// </summary>
        public static bool IsVillagerRole(string role)
        {
            string key = StyleRules.CharacterOf(role ?? string.Empty);
            return key == "paysan" || key == "garde" || key == "sentinelle" || (role ?? string.Empty).StartsWith("npc.", StringComparison.Ordinal);
        }

        /// <summary>
        /// Le personnage <paramref name="role"/> prend-il un corps humain ? Jamais sans corps prêts (<paramref name="ready"/>), jamais le
        /// Gardien (il garde sa stature) ; un villageois oui si <paramref name="villagers"/> ; un autre (bandit, rôdeur, archer) seulement
        /// s'il n'a pas son personnage de Mixamo (<paramref name="hasMixamo"/>).
        /// </summary>
        public static bool PreferHuman(string role, bool hasMixamo, bool ready, bool villagers)
        {
            if (!ready) return false;
            string key = StyleRules.CharacterOf(role ?? string.Empty);
            if (key.Length == 0 || key == "gardien") return false;
            if (IsVillagerRole(role ?? string.Empty)) return villagers || !hasMixamo;
            return !hasMixamo;
        }

        /// <summary>Corps de base : vrai pour la femme, faux pour l'homme, null si le fichier n'en est pas un (« Superhero_Female_FullBody.fbx »).</summary>
        public static bool? BodyOf(string path)
        {
            string file = StyleRules.Key(System.IO.Path.GetFileNameWithoutExtension((path ?? string.Empty).Replace('\\', '/')));
            if (!(path ?? string.Empty).EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) return null;
            if (file.Contains("superherofemale") || file == "basefemale" || file == "humanfemale") return true;
            if (file.Contains("superheromale") || file == "basemale" || file == "humanmale") return false;
            return null;
        }

        /// <summary>
        /// Coiffure d'un fichier des Universal Base Characters (« Hair_Long.fbx », rangé dans « Rigged to Head Bone » : ses os sont ceux du
        /// corps) ; la barbe et les sourcils à part. Null pour un autre fichier, ou pour la version « Origin at 0 » (sans os).
        /// </summary>
        public static (HumanHair Hair, bool Beard, bool Eyebrows)? HairOf(string path)
        {
            string p = (path ?? string.Empty).Replace('\\', '/');
            if (!p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) return null;
            if (StyleRules.Key(p).Contains("originat0")) return null;
            string file = StyleRules.Key(System.IO.Path.GetFileNameWithoutExtension(p));
            switch (file)
            {
                case "hairbuzzed": return (HumanHair.Buzzed, false, false);
                case "hairsimpleparted": return (HumanHair.SimpleParted, false, false);
                case "hairlong": return (HumanHair.Long, false, false);
                case "hairbuns": return (HumanHair.Buns, false, false);
                case "hairbuzzedfemale": return (HumanHair.BuzzedFemale, false, false);
                case "hairbeard": return (HumanHair.None, true, false);
                case "eyebrowsregular": case "eyebrowsfemale": return (HumanHair.None, false, true);
                default: return null;
            }
        }

        /// <summary>Bibliothèque d'animations humanoïdes (« UAL1_Standard.fbx », « UAL2_Standard.fbx ») ; pas les variantes à déplacement intégré (« _RM »).</summary>
        public static bool IsAnimationLibrary(string path)
        {
            string p = (path ?? string.Empty).Replace('\\', '/');
            if (!p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) return false;
            string file = StyleRules.Key(System.IO.Path.GetFileNameWithoutExtension(p));
            return file == "ual1standard" || file == "ual2standard" || file == "ualstandard";
        }

        /// <summary>
        /// Texture de peau d'un corps (<paramref name="female"/>, <paramref name="dark"/>) parmi <paramref name="textures"/> :
        /// « T_Superhero_Female_Dark_BaseColor », « T_Superhero_Male_Ligh » (sic) ; null sinon.
        /// </summary>
        public static string? SkinTexture(IEnumerable<string> textures, bool female, bool dark)
        {
            string body = female ? "superherofemale" : "superheromale";
            return (textures ?? Array.Empty<string>()).Where(t => t != null).OrderBy(t => t, StringComparer.Ordinal).FirstOrDefault(t =>
            {
                string file = StyleRules.Key(System.IO.Path.GetFileNameWithoutExtension(t.Replace('\\', '/')));
                if (!file.StartsWith("t" + body, StringComparison.Ordinal)) return false;
                if (!female && file.StartsWith("tsuperherofemale", StringComparison.Ordinal)) return false;
                if (file.Contains("normal") || file.Contains("roughness")) return false;
                return dark ? file.Contains("dark") : file.Contains("ligh");
            });
        }

        /// <summary>Carte de relief d'un corps (« T_Superhero_Male_Normal ») ; null sinon.</summary>
        public static string? BodyNormal(IEnumerable<string> textures, bool female) =>
            (textures ?? Array.Empty<string>()).Where(t => t != null).OrderBy(t => t, StringComparer.Ordinal).FirstOrDefault(t =>
            {
                string file = StyleRules.Key(System.IO.Path.GetFileNameWithoutExtension(t.Replace('\\', '/')));
                return file == (female ? "tsuperherofemalenormal" : "tsuperheromalenormal");
            });

        /// <summary>Texture d'une coiffure (« T_Hair_1_BaseColor », <paramref name="n"/> = 1 ou 2) ou des yeux (<paramref name="n"/> = 0 : « T_Eye_Brown ») ; null sinon.</summary>
        public static string? HairTexture(IEnumerable<string> textures, int n) =>
            (textures ?? Array.Empty<string>()).Where(t => t != null).OrderBy(t => t, StringComparer.Ordinal).FirstOrDefault(t =>
            {
                string file = StyleRules.Key(System.IO.Path.GetFileNameWithoutExtension(t.Replace('\\', '/')));
                return n == 0 ? file == "teyebrown" : file == "thair" + n + "basecolor";
            });

        /// <summary>
        /// Nom donné à une animation des Universal Animation Library (nom de la prise : « Armature|Walk_Loop », « Sword_Regular_A »), et si elle
        /// tourne en boucle ; null pour celles que le jeu ne prend pas (pose en T, pistolet, nage, conduite, saut, zombie…). Les noms suivent les
        /// mots des règles d'animation (<see cref="StyleRules.Clip(IEnumerable{string}, StyleMotion)"/>, <see cref="TownModels.VillagerClip"/>) :
        /// « Hurt_Chest » est un coup reçu, pas une attaque (« Hit_Chest » l'aurait été) ; « Seated_Chat » ne passe pas pour une conversation
        /// debout.
        /// </summary>
        public static (string Name, bool Loop)? CanonicalClip(string take)
        {
            string t = take ?? string.Empty;
            int bar = t.LastIndexOf('|');
            string k = StyleRules.Key(bar >= 0 ? t.Substring(bar + 1) : t);
            return ClipNames.TryGetValue(k, out (string, bool) found) ? found : ((string, bool)?)null;
        }

        /// <summary>
        /// Sous-lot V2.9.3 : noms que la préparation donne aux animations des bibliothèques (« Idle », « Walk », « Attack_A »…), sans doublon,
        /// dans l'ordre ordinal. Le vérificateur d'essai dit lesquels manquent.
        /// </summary>
        public static IReadOnlyList<string> CanonicalNames() =>
            ClipNames.Values.Select(v => v.Name).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();

        private static readonly Dictionary<string, (string Name, bool Loop)> ClipNames = new Dictionary<string, (string, bool)>(StringComparer.Ordinal)
        {
            // UAL1 : vie courante.
            ["idleloop"] = ("Idle", true),
            ["idletalkingloop"] = ("Talk", true),
            ["idletorchloop"] = ("Idle_Torch", true),
            ["walkloop"] = ("Walk", true),
            ["walkformalloop"] = ("Walk_Formal", true),
            ["jogfwdloop"] = ("Run", true),
            ["sprintloop"] = ("Sprint", true),
            ["sittingidleloop"] = ("Seated_Rest", true),
            ["sittingtalkingloop"] = ("Seated_Chat", true),
            ["sittingenter"] = ("Seated_Down", false),
            ["sittingexit"] = ("Seated_Up", false),
            ["interact"] = ("Interact", false),
            ["pickuptable"] = ("Gather_Table", true),
            ["fixingkneeling"] = ("Gather_Kneel", true),
            ["pushloop"] = ("Work_Push", true),
            ["danceloop"] = ("Dance", true),
            ["roll"] = ("Roll", false),
            // UAL1 : combat.
            ["death01"] = ("Death", false),
            ["hitchest"] = ("Hurt_Chest", false),
            ["hithead"] = ("Hurt_Head", false),
            ["swordattack"] = ("Attack_Sword", false),
            ["swordidle"] = ("Ready_Sword", true),
            ["punchcross"] = ("Attack_Punch", false),
            ["punchjab"] = ("Attack_Jab", false),
            ["spellsimpleshoot"] = ("Cast", false),
            // UAL2 : travaux et attitudes.
            ["farmharvest"] = ("Gather_Harvest", true),
            ["farmplantseed"] = ("Gather_Plant", true),
            ["farmwatering"] = ("Gather_Water", true),
            ["treechoppingloop"] = ("Work_Chop", true),
            ["walkcarryloop"] = ("Walk_Carry", true),
            ["idlefoldarmsloop"] = ("Look_FoldArms", true),
            ["idlerailloop"] = ("Lean_Rail", true),   // accoudé à une rambarde : pas pris pour « regarder » (pas de rambarde)
            ["idlerailcall"] = ("Wave_Call", false),
            ["idlelanternloop"] = ("Idle_Lantern", true),
            ["idlenoloop"] = ("Talk_No", true),
            ["yes"] = ("Talk_Yes", true),
            ["consume"] = ("Drink", false),
            ["chestopen"] = ("Chest_Open", false),
            ["laytoidle"] = ("Rise", false),
            // UAL2 : combat à l'épée et au bouclier.
            ["idleshieldloop"] = ("Block_Shield", true),
            ["idleshieldbreak"] = ("Guard_Break", false),
            ["shieldoneshot"] = ("Guard_Impact", false),
            ["swordblock"] = ("Block_Sword", false),
            ["swordregulara"] = ("Attack_A", false),
            ["swordregularb"] = ("Attack_B", false),
            ["swordregularc"] = ("Attack_C", false),
            ["swordheavycombo"] = ("Attack_Heavy", false),
            ["swordregularcombo"] = ("Attack_Combo", false),
            ["sworddash"] = ("Attack_Dash", false),
            ["shielddash"] = ("Attack_Shield", false),
            ["meleehook"] = ("Attack_Hook", false),
            ["overhandthrow"] = ("Attack_Throw", false),
            ["hitknockback"] = ("Hurt_Knockback", false),
        };

        /// <summary>
        /// Sous-lot V2.9.2 : animation de travail d'un villageois qui cueille, selon son métier : la forge frappe (« Work_Chop »), les
        /// paysans récoltent, sèment ou arrosent, les marchands rangent leur étal ; null : la règle commune (<see cref="TownModels.VillagerClip"/>).
        /// </summary>
        public static string? WorkClip(IEnumerable<string> clips, HumanOutfit outfit, int variant)
        {
            var all = (clips ?? Array.Empty<string>()).ToList();
            string[] want = outfit == HumanOutfit.Smith ? new[] { "Work_Chop", "Gather_Kneel" }
                : outfit == HumanOutfit.Merchant ? new[] { "Gather_Table", "Interact" }
                : outfit == HumanOutfit.Peasant ? new[] { "Gather_Harvest", "Gather_Plant", "Gather_Water", "Gather_Kneel" }
                : new string[0];
            var found = want.Where(all.Contains).ToList();
            return found.Count == 0 ? null : found[Math.Abs(variant) % found.Count];
        }
    }
}

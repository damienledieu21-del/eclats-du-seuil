using System;
using System.Collections.Generic;
using System.Linq;
using EdS.World;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot V2.9.4 : famille d'une pièce de vêtement des « Modular Character Outfits - Fantasy » de Quaternius (CC0), d'après son nom :
    /// paysan, rôdeur, chevalier, noble, magicien.
    /// </summary>
    public enum WardrobeFamily
    {
        Peasant = 0,
        Ranger = 1,
        Knight = 2,
        Noble = 3,
        Wizard = 4,
    }

    /// <summary>Sous-lot V2.9.4 : place d'une pièce sur le corps.</summary>
    public enum WardrobeSlot
    {
        /// <summary>Haut : tunique, chemise, robe, cuirasse.</summary>
        Body = 0,

        /// <summary>Bras et mains (les mains sont dans la pièce : le corps ne garde que sa tête).</summary>
        Arms = 1,

        /// <summary>Bas : chausses, braies, jupe, jambières.</summary>
        Legs = 2,

        /// <summary>Chaussures, bottes, solerets.</summary>
        Feet = 3,

        /// <summary>Coiffe : capuche, casque, chapeau, couronne, cercle.</summary>
        Head = 4,

        /// <summary>Épaulières.</summary>
        Shoulders = 5,

        /// <summary>Cape, manteau.</summary>
        Back = 6,

        /// <summary>Gorgerin, col, écharpe.</summary>
        Neck = 7,

        /// <summary>Tablier, ceinture.</summary>
        Waist = 8,
    }

    /// <summary>Sous-lot V2.9.4 : saison des tenues (manteaux l'hiver, têtes nues l'été).</summary>
    public enum WardrobeSeason
    {
        Spring = 0,
        Summer = 1,
        Autumn = 2,
        Winter = 3,
    }

    /// <summary>
    /// Sous-lot V2.9.4 : une pièce de vêtement reconnue (famille, place, genre de coiffe ou de manteau, corps d'homme ou de femme) et son nom
    /// réduit (<see cref="Key"/> : pour préférer « spiked », « lion »…).
    /// </summary>
    public readonly struct WardrobeItem
    {
        public WardrobeItem(WardrobeFamily family, WardrobeSlot slot, string kind, int gender, string key)
        {
            Family = family;
            Slot = slot;
            Kind = kind ?? string.Empty;
            Gender = gender;
            Key = key ?? string.Empty;
        }

        public WardrobeFamily Family { get; }
        public WardrobeSlot Slot { get; }

        /// <summary>Genre de pièce : « hood », « helmet », « hat », « crown », « circlet », « pauldrons », « cloak », « gorget », « apron », « belt », ou vide.</summary>
        public string Kind { get; }

        /// <summary>0 : pour les deux corps (genre inconnu) ; 1 : homme ; 2 : femme.</summary>
        public int Gender { get; }

        public string Key { get; }
    }

    /// <summary>Sous-lot V2.9.4 : une place à habiller : les familles voulues dans l'ordre, les genres de pièce acceptés, obligatoire ou non.</summary>
    public readonly struct WardrobeRequest
    {
        public WardrobeRequest(WardrobeSlot slot, WardrobeFamily[] families, string[] kinds, bool required, int roll, string[]? prefer = null, string[]? avoid = null)
        {
            Slot = slot;
            Families = families ?? Array.Empty<WardrobeFamily>();
            Kinds = kinds ?? Array.Empty<string>();
            Required = required;
            Roll = roll;
            Prefer = prefer ?? Array.Empty<string>();
            Avoid = avoid ?? Array.Empty<string>();
        }

        public WardrobeSlot Slot { get; }
        public WardrobeFamily[] Families { get; }

        /// <summary>Genres de pièce acceptés (vide : tous), dans l'ordre de préférence.</summary>
        public string[] Kinds { get; }

        /// <summary>Haut, bras, bas et pieds : sans eux, pas de vrais vêtements (repli sur la tenue peinte, jamais un corps à moitié nu).</summary>
        public bool Required { get; }

        /// <summary>Tirage (d'après l'identifiant) qui choisit parmi les pièces qui conviennent.</summary>
        public int Roll { get; }

        /// <summary>Mots préférés dans le nom de la pièce (« lion », « spiked ») : pris d'abord s'il y en a.</summary>
        public string[] Prefer { get; }

        /// <summary>Mots refusés dans le nom de la pièce.</summary>
        public string[] Avoid { get; }
    }

    /// <summary>
    /// Sous-lot V2.9.4 : la tenue voulue pour un humain : les places à habiller, la variante de texture du pack (<see cref="Fabric"/>), la
    /// teinte (<see cref="Tint"/>, indice de <see cref="HumanWardrobe.Tints"/>) et l'usure (<see cref="Wear"/> : 0 neuve, 1 portée, 2 usée).
    /// </summary>
    public sealed class WardrobePlan
    {
        public WardrobePlan(HumanOutfit outfit, IReadOnlyList<WardrobeRequest> requests, int fabric, int tint, int wear, WardrobeSeason season, bool festive, bool rain)
        {
            Outfit = outfit;
            Requests = requests;
            Fabric = fabric;
            Tint = tint;
            Wear = wear;
            Season = season;
            Festive = festive;
            Rain = rain;
        }

        public HumanOutfit Outfit { get; }
        public IReadOnlyList<WardrobeRequest> Requests { get; }
        public int Fabric { get; }
        public int Tint { get; }
        public int Wear { get; }
        public WardrobeSeason Season { get; }
        public bool Festive { get; }
        public bool Rain { get; }
    }

    /// <summary>
    /// Sous-lot V2.9.4 (demande de Damien du 8 novembre 2026 : « de vrais vêtements médiévaux ») : règles pures des vrais vêtements des
    /// villageois, faits des pièces des « Modular Character Outfits - Fantasy » de Quaternius (CC0, même squelette que les Universal Base
    /// Characters) : reconnaissance des pièces d'après leur nom, tenue par métier (paysan, forge, auberge et boutique, garde, sentinelle,
    /// clergé, marchand aisé, rôdeur, bandit, héros), saison (manteau l'hiver, capuche sous la pluie), tenue de fête, usure selon le
    /// statut, et choix des pièces parmi celles déposées, avec replis. Toujours le même choix pour le même identifiant et le même contexte.
    /// Les traits du corps restent tirés de l'identifiant seul (<see cref="HumanLooks.For"/>) ; le rôle choisit la tenue. Affichage seulement.
    /// </summary>
    public static class HumanWardrobe
    {
        /// <summary>
        /// Teintes multipliées par les textures du pack (presque blanches : la texture garde ses couleurs, l'ensemble prend un ton) : neutre,
        /// lin, saule, rouille, guède, bleu de la garde, rouge des sentinelles, laine claire, cuir, prune, vert de forêt, nuit, fête (or,
        /// garance, bleuet, safran).
        /// </summary>
        public static readonly (byte R, byte G, byte B)[] Tints =
        {
            (255, 255, 255), (246, 236, 214), (222, 236, 206), (250, 214, 192), (208, 220, 246), (196, 212, 255), (255, 196, 190),
            (252, 250, 240), (236, 214, 190), (236, 204, 232), (206, 228, 202), (176, 172, 168),
            (255, 232, 168), (255, 186, 176), (196, 214, 255), (255, 222, 150),
        };

        /// <summary>Teintes de fête (indices de <see cref="Tints"/>).</summary>
        private static readonly int[] FestiveTints = { 12, 13, 14, 15 };

        /// <summary>Teintes ordinaires par tenue.</summary>
        private static int[] TintsOf(HumanOutfit outfit)
        {
            switch (outfit)
            {
                case HumanOutfit.Guard: return new[] { 5 };
                case HumanOutfit.Sentinel: return new[] { 6 };
                case HumanOutfit.Clergy: return new[] { 7, 0 };
                case HumanOutfit.Smith: return new[] { 8, 0 };
                case HumanOutfit.Merchant: return new[] { 9, 0, 1 };
                case HumanOutfit.Ranger: return new[] { 10, 0 };
                case HumanOutfit.Bandit: return new[] { 11, 8 };
                case HumanOutfit.Hero: return new[] { 0 };
                default: return new[] { 0, 1, 2, 3, 4 };
            }
        }

        /// <summary>
        /// Un fichier du pack de vêtements : rangé dans un dossier « Outfits », « Vetements » ou « Modular Character Outfits… », ou nommé
        /// « …Outfit… ». Il n'est jamais pris pour un héros (« Knight »), un décor ou un personnage de Mixamo.
        /// </summary>
        public static bool IsWardrobePath(string path)
        {
            string[] parts = (path ?? string.Empty).Replace('\\', '/').Split('/');
            for (int i = 0; i + 1 < parts.Length; i++)
            {
                string s = StyleRules.Key(parts[i]);
                if (s == "outfits" || s == "vetements" || s.StartsWith("modularcharacteroutfits", StringComparison.Ordinal)) return true;
            }
            return parts.Length > 0 && StyleRules.Key(parts[parts.Length - 1]).Contains("outfit");
        }

        /// <summary>Saison d'un mois (1 à 12) : décembre à février l'hiver, mars à mai le printemps, juin à août l'été, septembre à novembre l'automne.</summary>
        public static WardrobeSeason SeasonOf(int month)
        {
            int m = ((month - 1) % 12 + 12) % 12 + 1;
            if (m == 12 || m <= 2) return WardrobeSeason.Winter;
            if (m <= 5) return WardrobeSeason.Spring;
            if (m <= 8) return WardrobeSeason.Summer;
            return WardrobeSeason.Autumn;
        }

        /// <summary>Temps de pluie (bruine, averse, orage) : les capuches se lèvent.</summary>
        public static bool IsRain(WeatherKind kind) => kind == WeatherKind.Drizzle || kind == WeatherKind.Shower || kind == WeatherKind.Storm;

        /// <summary>Mots d'une pièce : le nom du fichier puis celui du maillage, coupés aux séparateurs et aux majuscules (« Male_RangerHood » → male, ranger, hood).</summary>
        private static List<string> Words(string text)
        {
            var words = new List<string>();
            var current = new System.Text.StringBuilder();
            char previous = ' ';
            foreach (char c in text ?? string.Empty)
            {
                bool split = !char.IsLetterOrDigit(c) || (char.IsUpper(c) && char.IsLower(previous)) || (char.IsDigit(c) != char.IsDigit(previous) && current.Length > 0);
                if (split && current.Length > 0)
                {
                    words.Add(current.ToString().ToLowerInvariant());
                    current.Clear();
                }
                if (char.IsLetterOrDigit(c)) current.Append(c);
                previous = c;
            }
            if (current.Length > 0) words.Add(current.ToString().ToLowerInvariant());
            return words;
        }

        private static bool Any(List<string> words, params string[] wanted) =>
            words.Any(w => wanted.Any(x => w == x || w == x + "s" || w == x + "es"));

        /// <summary>
        /// Pièce du fichier <paramref name="path"/> (maillage <paramref name="part"/>, ou le fichier entier si vide) : famille, place, genre,
        /// homme ou femme ; null pour un fichier hors du pack, une pièce de niveau de détail réduit (« LOD1 »), une tête, un corps nu, ou une
        /// pièce non reconnue.
        /// </summary>
        public static WardrobeItem? Classify(string path, string part) => Classify(path, part, false);

        /// <summary>
        /// Relecture indépendante de la PR #119 : dans un fichier de plusieurs maillages (<paramref name="meshOnly"/>), la place d'une pièce
        /// est lue sur le nom de son maillage seulement ; un maillage au nom inconnu (« Cube.001 », « Belts ») n'est pas pris pour la pièce
        /// principale du fichier. La famille et le corps peuvent venir du fichier.
        /// </summary>
        public static WardrobeItem? Classify(string path, string part, bool meshOnly)
        {
            string p = (path ?? string.Empty).Replace('\\', '/');
            if (!IsWardrobePath(p)) return null;
            string file = System.IO.Path.GetFileNameWithoutExtension(p);
            // Le maillage d'abord (un fichier peut porter plusieurs pièces), puis le fichier, puis le dossier.
            string folder = p.Contains('/') ? p.Substring(0, p.LastIndexOf('/')) : string.Empty;
            string lastFolder = folder.Contains('/') ? folder.Substring(folder.LastIndexOf('/') + 1) : folder;
            List<string> mesh = Words(part ?? string.Empty), named = Words(file), all = Words(lastFolder + " " + file + " " + (part ?? string.Empty));
            if (all.Any(w => w.StartsWith("lod", StringComparison.Ordinal) && w != "lod0" && w.Length > 3)) return null;
            if (all.Any(w => w == "lod") && all.Any(w => w == "1" || w == "2" || w == "3")) return null;
            // Une tête, des yeux, des cheveux ou un corps nu livrés avec le pack ne sont jamais des vêtements, même dans « Peasant_Body.fbx ».
            if (Any(mesh, "eye", "eyebrow", "teeth", "tongue", "hair", "beard", "superhero", "naked", "nude") || (Any(mesh, "head", "face") && !Gear(mesh))) return null;
            string key = StyleRules.Key(file + (part ?? string.Empty));
            int gender = all.Contains("female") || all.Contains("woman") || all.Contains("women") ? 2 : all.Contains("male") || all.Contains("man") || all.Contains("men") ? 1 : 0;
            WardrobeFamily? family = FamilyOf(mesh) ?? FamilyOf(named) ?? FamilyOf(all);
            if (family == null) return null;
            (WardrobeSlot Slot, string Kind)? slot = meshOnly ? SlotOf(mesh) : SlotOf(mesh) ?? SlotOf(named);
            if (slot == null) return null;
            return new WardrobeItem(family.Value, slot.Value.Slot, slot.Value.Kind, gender, key);
        }

        private static WardrobeFamily? FamilyOf(List<string> w)
        {
            if (Any(w, "peasant", "villager", "farmer", "commoner", "peasent")) return WardrobeFamily.Peasant;
            if (Any(w, "ranger", "rogue", "hunter", "archer")) return WardrobeFamily.Ranger;
            if (Any(w, "knight", "plate", "soldier", "paladin", "warrior")) return WardrobeFamily.Knight;
            if (Any(w, "noble", "king", "queen", "royal", "lord", "lady", "prince", "princess")) return WardrobeFamily.Noble;
            if (Any(w, "wizard", "mage", "sorcerer", "witch", "priest", "monk", "cleric")) return WardrobeFamily.Wizard;
            return null;
        }

        /// <summary>Un mot de coiffe (« Head_Hood » est une capuche, « Head » seul une tête).</summary>
        private static bool Gear(List<string> w) => Any(w, "hood", "cowl", "helmet", "helm", "armet", "visor", "bascinet", "crown", "circlet", "tiara", "diadem", "hat", "cap", "beret", "coif");

        private static (WardrobeSlot, string)? SlotOf(List<string> w)
        {
            // Les têtes, cheveux et corps nus des Universal Base Characters ne sont pas des vêtements.
            if (Any(w, "hair", "eyebrow", "eye", "beard", "superhero", "naked", "nude") || (Any(w, "head", "face") && !Gear(w))) return null;
            if (Any(w, "hood", "cowl")) return (WardrobeSlot.Head, "hood");
            if (Any(w, "helmet", "helm", "armet", "visor", "bascinet")) return (WardrobeSlot.Head, "helmet");
            if (Any(w, "crown")) return (WardrobeSlot.Head, "crown");
            if (Any(w, "circlet", "tiara", "diadem")) return (WardrobeSlot.Head, "circlet");
            if (Any(w, "hat", "cap", "beret", "coif")) return (WardrobeSlot.Head, "hat");
            if (Any(w, "pauldron", "shoulder", "spaulder")) return (WardrobeSlot.Shoulders, "pauldrons");
            if (Any(w, "cloak", "cape", "mantle", "coat")) return (WardrobeSlot.Back, "cloak");
            if (Any(w, "gorget", "collar", "scarf", "neck")) return (WardrobeSlot.Neck, "gorget");
            if (Any(w, "apron")) return (WardrobeSlot.Waist, "apron");
            if (Any(w, "belt", "sash")) return (WardrobeSlot.Waist, "belt");
            if (Any(w, "arm", "sleeve", "hand", "glove", "gauntlet", "bracer")) return (WardrobeSlot.Arms, string.Empty);
            if (Any(w, "leg", "pant", "trouser", "skirt", "breeches", "greave")) return (WardrobeSlot.Legs, string.Empty);
            if (Any(w, "feet", "foot", "boot", "shoe", "sabaton")) return (WardrobeSlot.Feet, string.Empty);
            if (Any(w, "body", "torso", "chest", "top", "shirt", "tunic", "robe", "dress", "cuirass", "upper")) return (WardrobeSlot.Body, string.Empty);
            return null;
        }

        /// <summary>Nom réduit sans accents (comme <see cref="HumanLooks.KnownGender"/>).</summary>
        private static string Who(string id) => StyleRules.CharacterOf(id ?? string.Empty);

        private static readonly string[] WellOff = { "renart", "marchand", "intendant", "maitresse", "capitaine", "doyen", "heros", "hero" };
        private static readonly string[] Aproned = { "boulanger", "epiciere", "aubergiste", "fermiere" };
        private static readonly string[] Poor = { "bandit", "deserteur", "veuve", "mendiant" };

        /// <summary>
        /// La tenue de l'humain <paramref name="id"/> dans le rôle <paramref name="role"/> (comme <see cref="HumanLooks.For"/>), d'aspect
        /// <paramref name="look"/>, à la saison <paramref name="season"/>, sous la pluie ou non, un jour de fête ou non.
        /// </summary>
        public static WardrobePlan Plan(string id, string role, HumanLook look, WardrobeSeason season, bool rain, bool festive) =>
            Plan(id, role, look, season, rain, festive, int.MaxValue);

        /// <summary>
        /// Comme <see cref="Plan(string, string, HumanLook, WardrobeSeason, bool, bool)"/>, avec au plus <paramref name="maxOptional"/> pièces
        /// facultatives, dans l'ordre : coiffe, cape, épaulières, col, tablier (<see cref="MaxOptional"/>).
        /// </summary>
        public static WardrobePlan Plan(string id, string role, HumanLook look, WardrobeSeason season, bool rain, bool festive, int maxOptional)
        {
            string seed = id ?? string.Empty;
            int Roll(string salt, int count) => StyleRules.Variant(seed + "#vetement#" + salt, count);
            bool npc = seed.StartsWith("npc.", StringComparison.Ordinal);
            string who = Who(seed), part = npc ? who : Who(role ?? string.Empty);
            HumanOutfit outfit = look.Outfit;
            bool wellOff = WellOff.Contains(who) || WellOff.Contains(part);
            bool aproned = Aproned.Contains(who) || Aproned.Contains(part);

            var F = new Dictionary<string, WardrobeFamily[]>
            {
                ["peasant"] = new[] { WardrobeFamily.Peasant, WardrobeFamily.Ranger },
                ["ranger"] = new[] { WardrobeFamily.Ranger, WardrobeFamily.Peasant },
                ["knight"] = new[] { WardrobeFamily.Knight, WardrobeFamily.Ranger, WardrobeFamily.Peasant },
                ["noble"] = new[] { WardrobeFamily.Noble, WardrobeFamily.Peasant, WardrobeFamily.Ranger },
                ["wizard"] = new[] { WardrobeFamily.Wizard, WardrobeFamily.Noble, WardrobeFamily.Peasant },
                ["cloak"] = new[] { WardrobeFamily.Ranger, WardrobeFamily.Noble, WardrobeFamily.Knight, WardrobeFamily.Wizard, WardrobeFamily.Peasant },
            };
            string body, arms, legs, feet;
            switch (outfit)
            {
                case HumanOutfit.Guard: case HumanOutfit.Hero: body = arms = legs = feet = "knight"; break;
                case HumanOutfit.Sentinel: body = "ranger"; arms = "ranger"; legs = "knight"; feet = "ranger"; break;
                case HumanOutfit.Clergy: body = arms = legs = feet = "wizard"; break;
                case HumanOutfit.Smith: body = "peasant"; arms = "ranger"; legs = "peasant"; feet = "ranger"; break;
                case HumanOutfit.Merchant: body = arms = legs = feet = wellOff || festive ? "noble" : "peasant"; break;
                case HumanOutfit.Ranger: case HumanOutfit.Bandit: body = arms = legs = feet = "ranger"; break;
                default: body = arms = legs = feet = festive && Roll("fete", 100) < 35 ? "noble" : "peasant"; break;
            }
            // Le capitaine, en fête, laisse la cuirasse pour le drap noble ; les gardes gardent l'armure.
            if (festive && (who == "capitaine" || part == "capitaine")) body = "noble";

            var requests = new List<WardrobeRequest>
            {
                new WardrobeRequest(WardrobeSlot.Body, F[body], Array.Empty<string>(), true, Roll("haut", 997)),
                new WardrobeRequest(WardrobeSlot.Arms, F[arms], Array.Empty<string>(), true, Roll("bras", 997)),
                new WardrobeRequest(WardrobeSlot.Legs, F[legs], Array.Empty<string>(), true, Roll("bas", 997)),
                new WardrobeRequest(WardrobeSlot.Feet, F[feet], Array.Empty<string>(), true, Roll("pieds", 997)),
            };
            void Add(WardrobeSlot slot, string families, string[] kinds, string salt, string[]? prefer = null, string[]? avoid = null) =>
                requests.Add(new WardrobeRequest(slot, F[families], kinds, false, Roll(salt, 997), prefer, avoid));

            // Coiffe : le métier, puis la pluie et la saison ; jamais deux coiffes.
            bool head = false;
            switch (outfit)
            {
                case HumanOutfit.Guard:
                    if (!festive && who != "capitaine" && part != "capitaine" && (season != WardrobeSeason.Summer ? Roll("casque", 100) < 70 : Roll("casque", 100) < 45))
                    {
                        Add(WardrobeSlot.Head, "knight", new[] { "helmet" }, "casque", null, new[] { "horn", "horned" });
                        head = true;
                    }
                    break;
                case HumanOutfit.Ranger:
                    if (Roll("capuche", 100) < 70 || rain) { Add(WardrobeSlot.Head, "ranger", new[] { "hood" }, "capuche"); head = true; }
                    break;
                case HumanOutfit.Bandit:
                    if (Roll("capuche", 100) < 85 || rain) { Add(WardrobeSlot.Head, "ranger", new[] { "hood" }, "capuche"); head = true; }
                    break;
                case HumanOutfit.Clergy:
                    if (who == "doyen" || part == "doyen" || festive) { Add(WardrobeSlot.Head, "wizard", new[] { "hat", "hood" }, "chapeau"); head = true; }
                    break;
                case HumanOutfit.Merchant:
                    if (festive && (who == "maitresse" || who == "intendant")) { Add(WardrobeSlot.Head, "noble", new[] { "circlet", "hat" }, "cercle"); head = true; }
                    else if (wellOff && Roll("chapeau", 100) < 50) { Add(WardrobeSlot.Head, "noble", new[] { "hat" }, "chapeau"); head = true; }
                    break;
                case HumanOutfit.Hero:
                    break;   // le visage du héros reste visible
            }
            if (!head && outfit != HumanOutfit.Hero && outfit != HumanOutfit.Guard)
            {
                // Sous la pluie et l'hiver, on se couvre ; l'été, tête nue (le chapeau de paille du paysan, s'il y en a un).
                bool cover = rain || (season == WardrobeSeason.Winter && Roll("hiver", 100) < 60) || (season == WardrobeSeason.Autumn && Roll("automne", 100) < 25);
                if (cover && outfit != HumanOutfit.Smith) Add(WardrobeSlot.Head, "cloak", new[] { "hood" }, "capuche");
                else if (!festive && season == WardrobeSeason.Summer && outfit == HumanOutfit.Peasant && Roll("paille", 100) < 30)
                    Add(WardrobeSlot.Head, "peasant", new[] { "hat" }, "paille", null, new[] { "crown" });
            }

            // Épaulières : la garde, le héros, les sentinelles ; le capitaine les plus belles (lion) ; les bandits, piquées.
            if (outfit == HumanOutfit.Hero || who == "capitaine" || part == "capitaine")
                Add(WardrobeSlot.Shoulders, "knight", new[] { "pauldrons" }, "epaules", new[] { "lion", "noble", "golden" }, new[] { "spike", "spiked" });
            else if (outfit == HumanOutfit.Guard && Roll("epaules", 100) < 50)
                Add(WardrobeSlot.Shoulders, "knight", new[] { "pauldrons" }, "epaules", null, new[] { "spike", "spiked", "lion" });
            else if (outfit == HumanOutfit.Sentinel || (outfit == HumanOutfit.Ranger && Roll("epaules", 100) < 50))
                Add(WardrobeSlot.Shoulders, "ranger", new[] { "pauldrons" }, "epaules", null, new[] { "lion" });
            else if (outfit == HumanOutfit.Bandit && Roll("epaules", 100) < 30)
                Add(WardrobeSlot.Shoulders, "knight", new[] { "pauldrons" }, "epaules", new[] { "spike", "spiked" }, new[] { "lion" });

            // Cape : l'hiver presque tous (pas la forge : le feu tient chaud), l'automne un sur trois ; le héros et le capitaine souvent ;
            // jamais l'été (sauf la fête des nobles).
            bool cloak = season == WardrobeSeason.Winter ? outfit != HumanOutfit.Smith && Roll("manteau", 100) < 85
                : season == WardrobeSeason.Autumn ? Roll("manteau", 100) < 33
                : season == WardrobeSeason.Spring ? Roll("manteau", 100) < 10
                : false;
            if (outfit == HumanOutfit.Hero || who == "capitaine" || part == "capitaine") cloak |= Roll("cape", 100) < 60;
            if (festive && wellOff) cloak = true;
            if (cloak) Add(WardrobeSlot.Back, "cloak", new[] { "cloak" }, "manteau");
            // Gorgerin des gardes l'hiver ; tablier de la forge, de l'auberge, du fournil et de l'épicerie (s'il y en a un dans le pack).
            if (outfit == HumanOutfit.Guard && season == WardrobeSeason.Winter) Add(WardrobeSlot.Neck, "knight", new[] { "gorget" }, "col");
            if (outfit == HumanOutfit.Smith || aproned) Add(WardrobeSlot.Waist, "peasant", new[] { "apron", "belt" }, "tablier");

            // Usure selon le statut : neuve pour les gens aisés, le clergé et le héros ; usée pour les bandits, les déserteurs, la veuve ; les
            // paysans de l'une à l'autre. Un jour de fête, on sort ses beaux habits.
            int wear = wellOff || outfit == HumanOutfit.Clergy || outfit == HumanOutfit.Hero ? 0
                : Poor.Contains(who) || Poor.Contains(part) || outfit == HumanOutfit.Bandit ? 2
                : outfit == HumanOutfit.Peasant ? new[] { 0, 1, 1, 1, 2, 2 }[Roll("usure", 6)]
                : 1;
            if (festive) wear = Math.Min(wear, outfit == HumanOutfit.Bandit ? 2 : 0);
            int[] tints = festive && outfit != HumanOutfit.Guard && outfit != HumanOutfit.Bandit && outfit != HumanOutfit.Hero ? FestiveTints : TintsOf(outfit);
            int tint = tints[Roll("teinte", tints.Length)];
            // Plafond des pièces facultatives (téléphone) : les obligatoires d'abord, puis la coiffe, la cape, les épaulières, le col, le tablier.
            WardrobeSlot[] order = { WardrobeSlot.Head, WardrobeSlot.Back, WardrobeSlot.Shoulders, WardrobeSlot.Neck, WardrobeSlot.Waist };
            var kept = requests.Where(q => q.Required).ToList();
            kept.AddRange(requests.Where(q => !q.Required).OrderBy(q => Array.IndexOf(order, q.Slot)).Take(Math.Max(0, maxOptional)));
            return new WardrobePlan(outfit, kept, Roll("etoffe", 3), tint, wear, season, festive, rain);
        }

        /// <summary>
        /// Les pièces choisies parmi <paramref name="items"/> pour <paramref name="plan"/> et ce corps (<paramref name="female"/>) : un indice
        /// de <paramref name="items"/> par demande (-1 : rien à cette place), ou null s'il manque une pièce obligatoire (haut, bras, bas,
        /// pieds) : le corps garde alors sa tenue peinte. Pour chaque place : les familles voulues dans l'ordre, puis, pour une pièce
        /// obligatoire, n'importe quelle famille ; d'abord les pièces de ce corps, puis celles faites pour les deux ; les mots préférés
        /// d'abord ; le tirage de la demande choisit parmi les pièces qui restent (dans l'ordre de <paramref name="items"/>).
        /// </summary>
        public static int[]? Resolve(WardrobePlan plan, IReadOnlyList<WardrobeItem> items, bool female)
        {
            if (plan == null || items == null) return null;
            int want = female ? 2 : 1;
            var picks = new int[plan.Requests.Count];
            for (int r = 0; r < plan.Requests.Count; r++)
            {
                WardrobeRequest q = plan.Requests[r];
                picks[r] = -1;
                IEnumerable<WardrobeFamily> families = q.Families;
                if (q.Required) families = families.Concat(Enum.GetValues(typeof(WardrobeFamily)).Cast<WardrobeFamily>().Where(f => !q.Families.Contains(f)));
                foreach (WardrobeFamily family in families)
                {
                    var fit = new List<int>();
                    foreach (int gender in new[] { want, 0 })
                    {
                        for (int i = 0; i < items.Count; i++)
                        {
                            WardrobeItem it = items[i];
                            if (it.Slot != q.Slot || it.Family != family || it.Gender != gender) continue;
                            if (q.Kinds.Length > 0 && !q.Kinds.Contains(it.Kind)) continue;
                            if (q.Avoid.Any(a => it.Key.Contains(a))) continue;
                            fit.Add(i);
                        }
                        if (fit.Count > 0) break;
                    }
                    if (fit.Count == 0) continue;
                    // Le premier genre de pièce accepté d'abord (le chapeau avant la capuche du doyen), puis les mots préférés.
                    foreach (string kind in q.Kinds)
                    {
                        var same = fit.Where(i => items[i].Kind == kind).ToList();
                        if (same.Count > 0)
                        {
                            fit = same;
                            break;
                        }
                    }
                    var preferred = fit.Where(i => q.Prefer.Any(w => items[i].Key.Contains(w))).ToList();
                    if (preferred.Count > 0) fit = preferred;
                    picks[r] = fit[Math.Abs(q.Roll) % fit.Count];
                    break;
                }
                if (picks[r] < 0 && q.Required) return null;
            }
            return picks;
        }

        /// <summary>
        /// Couleur finale (0 à 1) d'une pièce : la teinte <paramref name="tint"/> assombrie et ternie par l'usure <paramref name="wear"/>
        /// (0 neuve, 1 portée : un peu plus terne, 2 usée : passée et brunie).
        /// </summary>
        public static (double R, double G, double B) Color(int tint, int wear)
        {
            (byte r, byte g, byte b) = Tints[Math.Max(0, Math.Min(Tints.Length - 1, tint))];
            double R = r / 255.0, G = g / 255.0, B = b / 255.0;
            int w = Math.Max(0, Math.Min(2, wear));
            double grey = (R + G + B) / 3.0, fade = new[] { 0.0, 0.15, 0.35 }[w], dark = new[] { 1.0, 0.93, 0.82 }[w];
            R = (R + (grey - R) * fade) * dark;
            G = (G + (grey - G) * fade) * dark * (w == 2 ? 0.97 : 1.0);
            B = (B + (grey - B) * fade) * dark * (w == 2 ? 0.90 : 1.0);
            return (R, G, B);
        }

        /// <summary>Lissé d'une pièce selon l'usure (un tissu neuf a un peu de lustre, un tissu usé aucun).</summary>
        public static double Smoothness(int wear) => new[] { 0.22, 0.14, 0.06 }[Math.Max(0, Math.Min(2, wear))];

        /// <summary>Une coiffe qui cache les cheveux (capuche, casque) : la coiffure n'est pas posée (elle traverserait).</summary>
        public static bool HidesHair(string kind) => kind == "hood" || kind == "helmet";

        /// <summary>
        /// Coiffure portée sous la coiffe <paramref name="kind"/> (relecture de la PR #119) : rien sous une capuche ou un casque ; une coiffure
        /// courte (rase) sous un chapeau, une couronne ou un cercle, qui traverseraient les cheveux longs, les chignons et la raie ; la coiffure
        /// d'origine sans coiffe.
        /// </summary>
        public static HumanHair HairUnder(string kind, HumanHair hair, bool female)
        {
            if (HidesHair(kind)) return HumanHair.None;
            if (kind == "hat" || kind == "crown" || kind == "circlet")
                return hair == HumanHair.None ? HumanHair.None : female ? HumanHair.BuzzedFemale : HumanHair.Buzzed;
            return hair;
        }

        /// <summary>
        /// Relecture de la PR #119 : sous les vrais vêtements, le corps doit être remplacé par sa tête seule (<paramref name="headVertices"/> :
        /// sommets du corps réduit, null s'il n'a pas été fait), faite du même maillage (<paramref name="bodyVertices"/>) ; sinon le corps
        /// peint resterait dessous (deux tenues l'une dans l'autre) : pas de vrais vêtements.
        /// </summary>
        public static bool HeadFits(int? headVertices, int bodyVertices) => headVertices.HasValue && headVertices.Value > 0 && headVertices.Value == bodyVertices;

        /// <summary>
        /// Pièces facultatives au plus (coiffe, cape, épaulières, col, tablier) : une seule sur téléphone (chaque pièce est un maillage animé
        /// de plus ; relecture de la PR #119), toutes sur ordinateur.
        /// </summary>
        public static int MaxOptional(bool phone) => phone ? 1 : 5;

        /// <summary>Noms réduits des matériaux de peau connus (les mains des bras des pièces, le corps des Universal Base Characters).</summary>
        private static readonly string[] SkinMaterials =
        {
            "miregularmale", "miregularfemale", "mregularmale", "mregularfemale", "regularmale", "regularfemale",
            "misuperheromale", "misuperherofemale", "msuperheromale", "msuperherofemale", "superheromale", "superherofemale",
        };

        /// <summary>
        /// Matériau de peau d'une pièce : seulement les noms connus (« MI_Regular_Male », « MI_Superhero_Female » ; relecture de la PR #119 :
        /// « Body », « Regular_Belt » sont des étoffes), avec un éventuel suffixe d'instance d'Unity (« (Instance) ») ; sinon c'est une étoffe.
        /// </summary>
        public static bool IsSkinMaterial(string name)
        {
            string n = (name ?? string.Empty).Replace(" (Instance)", string.Empty);
            return SkinMaterials.Contains(StyleRules.Key(n));
        }

        /// <summary>
        /// Texture d'étoffe d'une famille parmi <paramref name="textures"/> (couleur seulement, pas les cartes de relief, de rugosité, de
        /// métal ni la peau), pour ce corps (<paramref name="gender"/> : 0 les deux, 1 homme, 2 femme), triées : les trois variantes du pack.
        /// </summary>
        public static IReadOnlyList<string> FabricTextures(IEnumerable<string> textures, WardrobeFamily family, int gender)
        {
            return (textures ?? Array.Empty<string>()).Where(t => t != null && IsWardrobePath(t) && IsColorMap(t)).Where(t =>
            {
                List<string> w = Words(System.IO.Path.GetFileNameWithoutExtension(t.Replace('\\', '/')));
                if (FamilyOf(w) != family) return false;
                int g = w.Contains("female") ? 2 : w.Contains("male") ? 1 : 0;
                return g == 0 || gender == 0 || g == gender;
            }).OrderBy(t => t, StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// Carte de couleur d'une étoffe (relecture de la PR #119) : le nom dit « BaseColor », « Albedo », « Diffuse » ou « Color » ; jamais une
        /// carte de relief, de rugosité, de métal, d'occlusion, d'opacité, de brillance, de détail, ni la peau.
        /// </summary>
        public static bool IsColorMap(string path)
        {
            List<string> w = Words(System.IO.Path.GetFileNameWithoutExtension((path ?? string.Empty).Replace('\\', '/')));
            if (Any(w, "normal", "nrm", "rough", "roughness", "metal", "metallic", "ao", "occlusion", "orm", "emissive", "emission", "mask", "height",
                "opacity", "alpha", "transparency", "specular", "spec", "gloss", "glossiness", "detail", "bump", "displacement", "regular", "skin", "superhero"))
                return false;
            return Any(w, "color", "colour", "albedo", "diffuse", "basecolor", "diff");
        }

        private static readonly string[] StemNoise =
        {
            "t", "tx", "tex", "texture", "mi", "m", "mat", "material", "base", "color", "colour", "basecolor", "albedo", "diffuse", "diff", "d",
            "male", "female", "i", "ii", "iii", "iv", "a", "b", "c", "v", "var", "variant", "variation", "png", "tga", "jpg",
        };

        /// <summary>
        /// Racine du nom d'une étoffe, commune à un matériau et à ses textures (relecture de la PR #119 : relier texture et pièce par le nom) :
        /// « MI_Ranger » et « T_Ranger_2_BaseColor » → ranger ; « MI_Ranger_Belts » et « T_Ranger_Belts_1_BaseColor » → rangerbelts. Sans les
        /// préfixes, les mots de couleur, le corps et le numéro de variante.
        /// </summary>
        public static string FabricStem(string name)
        {
            string n = System.IO.Path.GetFileNameWithoutExtension((name ?? string.Empty).Replace(" (Instance)", string.Empty).Replace('\\', '/'));
            return string.Concat(Words(n).Where(w => !StemNoise.Contains(w) && !w.All(char.IsDigit)));
        }

        /// <summary>Texture de peau du pack pour les mains (« T_Regular_Male_Dark_BaseColor ») ; null sinon.</summary>
        public static string? SkinTexture(IEnumerable<string> textures, bool female, bool dark) =>
            (textures ?? Array.Empty<string>()).Where(t => t != null && IsWardrobePath(t)).OrderBy(t => t, StringComparer.Ordinal).FirstOrDefault(t =>
            {
                List<string> w = Words(System.IO.Path.GetFileNameWithoutExtension(t.Replace('\\', '/')));
                if (!Any(w, "regular", "skin")) return false;
                if (Any(w, "normal", "rough", "roughness", "metal", "ao", "orm")) return false;
                if ((female ? !w.Contains("female") : !w.Contains("male"))) return false;
                return dark ? w.Contains("dark") : !w.Contains("dark");
            });
    }
}

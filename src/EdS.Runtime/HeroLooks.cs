using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Saves;

namespace EdS.Runtime
{
    /// <summary>Sous-lot V2.16 : ce que le créateur de personnage laisse changer.</summary>
    public enum HeroPart
    {
        Body = 0,
        Hair = 1,
        Beard = 2,
        Skin = 3,
        HairTone = 4,
        Outfit = 5,
    }

    /// <summary>
    /// Sous-lot V2.16 (première version du créateur de personnage, souhait de Damien du 9 novembre 2026) : règles pures de l'aspect du héros.
    /// Le joueur choisit un corps (homme ou femme de Quaternius, ou un personnage fait dans MakeHuman), une coiffure, la barbe, la peau
    /// (claire ou sombre), la couleur des cheveux et celle de la tenue ; le choix est gardé dans la sauvegarde (<see cref="SavedLook"/>,
    /// version 17). Un personnage de MakeHuman a sa propre peau, ses cheveux et ses vêtements : seul le corps se choisit alors. Affichage
    /// seulement : rien ne revient vers le moteur.
    /// </summary>
    public static class HeroLooks
    {
        public const string Male = SavedLook.Male;
        public const string Female = SavedLook.Female;
        public const string MadePrefix = SavedLook.MadePrefix;

        /// <summary>Noms des coiffures, dans l'ordre de <see cref="HumanHair"/>.</summary>
        public static readonly string[] HairNames = { "Aucune", "Rasée", "Raie sur le côté", "Longue", "Chignons", "Courte (femme)" };

        /// <summary>Noms des couleurs de cheveux, dans l'ordre de <see cref="HumanLooks.HairTones"/>.</summary>
        public static readonly string[] HairToneNames = { "Noir", "Brun", "Châtain", "Roux", "Blond", "Gris" };

        /// <summary>Noms des tenues, dans l'ordre de <see cref="HumanLooks.Palettes"/>.</summary>
        public static readonly string[] PaletteNames =
        {
            "Lin écru", "Vert de saule", "Rouille", "Bleu de guède", "Toile claire", "Tabard bleu", "Rouge des sentinelles", "Robe de laine",
            "Tablier de cuir", "Prune", "Vert de forêt", "Cuir sombre", "Acier bleuté",
        };

        /// <summary>Teinte des vrais vêtements (indice de <see cref="HumanWardrobe.Tints"/>) pour chaque tenue de <see cref="HumanLooks.Palettes"/>.</summary>
        private static readonly int[] ClothesTints = { 1, 2, 3, 4, 1, 5, 6, 7, 8, 9, 10, 11, 0 };

        /// <summary>Teinte des vrais vêtements qui rappelle la tenue peinte <paramref name="palette"/> (hors bornes : neutre).</summary>
        public static int ClothesTint(int palette) => palette >= 0 && palette < ClothesTints.Length ? ClothesTints[palette] : 0;

        /// <summary>Aspect de départ : celui que le jeu donne au héros quand rien n'est choisi (<see cref="HumanLooks.For"/>).</summary>
        public static SavedLook Default()
        {
            HumanLook h = HumanLooks.For("heros", "heros");
            return new SavedLook(h.Female ? Female : Male, (int)h.Hair, h.Beard, h.DarkSkin, h.HairTone, h.Palette);
        }

        /// <summary>Le corps est-il celui d'un personnage de MakeHuman ?</summary>
        public static bool IsMade(string? body) => body != null && body.StartsWith(MadePrefix, StringComparison.Ordinal);

        /// <summary>Corps proposés : l'homme, la femme, puis chaque personnage de MakeHuman trouvé, dans l'ordre alphabétique.</summary>
        public static IReadOnlyList<string> Bodies(IReadOnlyList<MadeHumanModel>? made) =>
            new[] { Male, Female }.Concat((made ?? Array.Empty<MadeHumanModel>()).Select(m => MadePrefix + m.Name)).ToList();

        /// <summary>Nom du corps pour l'écran : « Homme », « Femme », ou le nom du dossier du personnage (« Gaspard »).</summary>
        public static string BodyName(string body, IReadOnlyList<MadeHumanModel>? made)
        {
            if (body == Male) return "Homme";
            if (body == Female) return "Femme";
            MadeHumanModel? model = MadeOf(body, made);
            return model != null ? model.Folder : body.StartsWith(MadePrefix, StringComparison.Ordinal) ? body.Substring(MadePrefix.Length) + " (absent)" : body;
        }

        /// <summary>Le personnage de MakeHuman que désigne ce corps, ou null (corps de Quaternius, ou personnage disparu).</summary>
        public static MadeHumanModel? MadeOf(string? body, IReadOnlyList<MadeHumanModel>? made) =>
            IsMade(body) && made != null ? made.FirstOrDefault(m => m.Name == body!.Substring(MadePrefix.Length)) : null;

        /// <summary>Le choix <paramref name="part"/> change-t-il quelque chose pour ce corps ? (Un personnage de MakeHuman a sa peau, ses cheveux et ses vêtements.)</summary>
        public static bool Applies(HeroPart part, string body)
        {
            if (part == HeroPart.Body) return true;
            if (IsMade(body)) return false;
            return part != HeroPart.Beard || body == Male;
        }

        /// <summary>Coiffures permises pour ce corps (homme : rasée, raie, longue ; femme : longue, chignons, courte), avec « aucune ».</summary>
        public static IReadOnlyList<HumanHair> HairsFor(string body) => body == Female
            ? new[] { HumanHair.None, HumanHair.Long, HumanHair.Buns, HumanHair.BuzzedFemale }
            : new[] { HumanHair.None, HumanHair.Buzzed, HumanHair.SimpleParted, HumanHair.Long };

        /// <summary>
        /// Ramène <paramref name="look"/> dans ce qui existe : un personnage de MakeHuman disparu redonne le corps d'homme ; une coiffure
        /// ou une barbe qui ne va pas au corps est remplacée par la première permise (jamais d'erreur : le jeu ouvre toujours).
        /// </summary>
        public static SavedLook Normalize(SavedLook? look, IReadOnlyList<MadeHumanModel>? made)
        {
            SavedLook l = look ?? Default();
            string body = l.Body;
            if (IsMade(body)) return MadeOf(body, made) != null ? l : With(l, Male, l.Hair, l.Beard);
            IReadOnlyList<HumanHair> hairs = HairsFor(body);
            int hair = hairs.Contains((HumanHair)l.Hair) ? l.Hair : (int)hairs[1];
            return With(l, body, hair, body == Male && l.Beard);
        }

        private static SavedLook With(SavedLook l, string body, int hair, bool beard) =>
            new SavedLook(body, hair, beard, l.DarkSkin, l.HairTone, l.Palette);

        /// <summary>
        /// Passe au choix suivant (<paramref name="step"/> = 1) ou précédent (−1) de <paramref name="part"/>, en boucle. Un choix qui ne s'applique
        /// pas à ce corps (<see cref="Applies"/>) ne change rien. Changer de corps remet une coiffure et une barbe qui lui vont.
        /// </summary>
        public static SavedLook Cycle(SavedLook look, HeroPart part, int step, IReadOnlyList<MadeHumanModel>? made)
        {
            SavedLook l = Normalize(look, made);
            if (!Applies(part, l.Body)) return l;
            int dir = step < 0 ? -1 : 1;
            switch (part)
            {
                case HeroPart.Body:
                {
                    IReadOnlyList<string> bodies = Bodies(made);
                    int at = Math.Max(0, bodies.ToList().IndexOf(l.Body));
                    return Normalize(With(l, bodies[(at + dir + bodies.Count) % bodies.Count], l.Hair, l.Beard), made);
                }
                case HeroPart.Hair:
                {
                    List<HumanHair> hairs = HairsFor(l.Body).ToList();
                    int at = Math.Max(0, hairs.IndexOf((HumanHair)l.Hair));
                    return With(l, l.Body, (int)hairs[(at + dir + hairs.Count) % hairs.Count], l.Beard);
                }
                case HeroPart.Beard: return With(l, l.Body, l.Hair, !l.Beard);
                case HeroPart.Skin: return new SavedLook(l.Body, l.Hair, l.Beard, !l.DarkSkin, l.HairTone, l.Palette);
                case HeroPart.HairTone: return new SavedLook(l.Body, l.Hair, l.Beard, l.DarkSkin, (l.HairTone + dir + HumanLooks.HairTones.Length) % HumanLooks.HairTones.Length, l.Palette);
                default: return new SavedLook(l.Body, l.Hair, l.Beard, l.DarkSkin, l.HairTone, (l.Palette + dir + HumanLooks.Palettes.Length) % HumanLooks.Palettes.Length);
            }
        }

        /// <summary>Texte du choix courant de <paramref name="part"/> pour l'écran (« Longue », « Châtain », « Prune »…).</summary>
        public static string ValueText(SavedLook look, HeroPart part, IReadOnlyList<MadeHumanModel>? made)
        {
            SavedLook l = Normalize(look, made);
            if (!Applies(part, l.Body)) return "fait par le personnage";
            switch (part)
            {
                case HeroPart.Body: return BodyName(l.Body, made);
                case HeroPart.Hair: return HairNames[l.Hair];
                case HeroPart.Beard: return l.Beard ? "Oui" : "Non";
                case HeroPart.Skin: return l.DarkSkin ? "Sombre" : "Claire";
                case HeroPart.HairTone: return HairToneNames[l.HairTone];
                default: return PaletteNames[l.Palette];
            }
        }

        /// <summary>Titre de chaque choix pour l'écran.</summary>
        public static string PartName(HeroPart part)
        {
            switch (part)
            {
                case HeroPart.Body: return "Corps";
                case HeroPart.Hair: return "Coiffure";
                case HeroPart.Beard: return "Barbe";
                case HeroPart.Skin: return "Peau";
                case HeroPart.HairTone: return "Cheveux";
                default: return "Tenue";
            }
        }

        /// <summary>
        /// L'aspect de Quaternius du héros (corps, coiffure, barbe, peau, cheveux, tenue) : la taille et la carrure restent celles du héros par
        /// défaut. La tenue, elle, est <see cref="HumanOutfit"/> de la palette choisie.
        /// </summary>
        public static HumanLook ToHumanLook(SavedLook look)
        {
            HumanLook baseHero = HumanLooks.For("heros", "heros");
            bool female = look.Body == Female;
            double height = female ? 1.66 : 1.78;
            return new HumanLook(female, look.DarkSkin, (HumanHair)look.Hair, look.Body == Male && look.Beard, look.HairTone, HumanLooks.Palettes[look.Palette].Outfit,
                look.Palette, height, baseHero.Width);
        }

        /// <summary>Le jeu garde-t-il l'aspect par défaut (rien de choisi, ou ce qui est choisi est l'aspect par défaut) ?</summary>
        public static bool IsDefault(SavedLook? look) => look == null || look.Equals(Default());
    }
}

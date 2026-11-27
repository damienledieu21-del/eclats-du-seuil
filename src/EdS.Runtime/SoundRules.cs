using System;

namespace EdS.Runtime
{
    /// <summary>V2.54 (passe son des Vallons) : le grand milieu sonore où se trouve le personnage.</summary>
    public enum SoundBiome
    {
        /// <summary>Plein air sans lieu particulier : plaines, routes, pré.</summary>
        Open = 0,
        Town = 1,
        Forest = 2,
        Marsh = 3,
        Lake = 4,

        /// <summary>En altitude : col, crêtes, hautes collines, tour de signal (rafales).</summary>
        Heights = 5,

        /// <summary>Dans une maison, une boutique, l'auberge (zone intérieure).</summary>
        Indoors = 6,
        Cave = 7,
        Sewer = 8,
        Crypt = 9,

        /// <summary>Galeries des éclats, du Seuil, mines.</summary>
        Gallery = 10,
        Drowned = 11,
        Envers = 12,
    }

    /// <summary>V2.54 : les quatre volumes réglables du mixage.</summary>
    public enum SoundBus
    {
        Music = 0,
        Ambience = 1,
        Effects = 2,
        Interface = 3,
    }

    /// <summary>V2.54 : priorité d'un bruitage face à la limite de voix (un pas cède avant un coup, un coup avant une mort).</summary>
    public enum SoundPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
    }

    /// <summary>
    /// V2.54 (passe son des Vallons, demande de Damien) : toutes les règles de choix du son, pures et testées : le milieu d'après la zone
    /// et les lieux (<see cref="BiomeOf"/>), la musique d'un lieu et d'un moment (<see cref="Music"/>, ses replis <see cref="MusicChain"/> et
    /// la durée de ses fondus <see cref="FadeSeconds"/>), le volume des nappes des milieux (<see cref="BedVolume"/>), le sol sous les pas
    /// (<see cref="Step"/>), le mixage (<see cref="Gain"/>, <see cref="StepPercent"/>) et la limite de voix (<see cref="VoiceLimit"/>,
    /// <see cref="Admit"/>). Affichage seulement : rien ne change dans le monde.
    /// </summary>
    public static class SoundRules
    {
        // --- Milieu -----------------------------------------------------------------------------------------------------

        /// <summary>
        /// Milieu sonore : d'abord la zone (souterrains d'après le début de son identifiant : « egouts_… », « crypte_… », « galeries_… » et
        /// « mine_… », « grottes_noyees_… », « grotte_… », « envers_… » ; un étage <paramref name="envers"/> ; un intérieur), puis les lieux
        /// du dehors (<paramref name="places"/>, <paramref name="placeIds"/> : marais, altitude, forêt, bourg, lac), sinon le plein air.
        /// </summary>
        public static SoundBiome BiomeOf(string? zoneId, bool indoor, bool underground, bool envers, AmbiencePlace places, string[]? placeIds)
        {
            string zone = zoneId ?? string.Empty;
            if (envers || Starts(zone, "envers")) return SoundBiome.Envers;
            if (Starts(zone, "egouts")) return SoundBiome.Sewer;
            if (Starts(zone, "crypte")) return SoundBiome.Crypt;
            if (Starts(zone, "grottes_noyees")) return SoundBiome.Drowned;
            if (Starts(zone, "galeries") || Starts(zone, "mine")) return SoundBiome.Gallery;
            if (Starts(zone, "grotte")) return SoundBiome.Cave;
            if (underground) return SoundBiome.Cave;
            if (indoor) return SoundBiome.Indoors;
            if ((places & AmbiencePlace.Crypt) != 0) return SoundBiome.Crypt;
            bool marsh = false, heights = false;
            if (placeIds != null)
                foreach (string id in placeIds)
                {
                    marsh |= IsMarsh(id);
                    heights |= IsHeights(id);
                }
            if (marsh) return SoundBiome.Marsh;
            if ((places & AmbiencePlace.Town) != 0) return SoundBiome.Town;
            if (heights) return SoundBiome.Heights;
            if ((places & AmbiencePlace.Forest) != 0) return SoundBiome.Forest;
            if ((places & AmbiencePlace.Lake) != 0) return SoundBiome.Lake;
            return SoundBiome.Open;
        }

        /// <summary>Un lieu de marais : « marais… », « tourbiere… », « pilotis », « roseliere… », « etang… » (mot entier ou début de mot).</summary>
        public static bool IsMarsh(string? placeId) =>
            HasWord(placeId, "marais", true) || HasWord(placeId, "tourbiere", true) || HasWord(placeId, "pilotis", false)
            || HasWord(placeId, "roseliere", true) || HasWord(placeId, "etang", true) || HasWord(placeId, "fondriere", true);

        /// <summary>Un lieu en altitude : « col », « crete… », « cretes », « cairns », « hautes_collines », « tour_de_signal ».</summary>
        public static bool IsHeights(string? placeId) =>
            HasWord(placeId, "col", false) || HasWord(placeId, "crete", true) || HasWord(placeId, "cairns", false)
            || (HasWord(placeId, "hautes", false) && HasWord(placeId, "collines", false)) || HasWord(placeId, "signal", false);

        /// <summary>Le milieu est-il sous terre (ni pluie, ni vent, ni oiseaux) ?</summary>
        public static bool IsUnderground(SoundBiome biome) =>
            biome == SoundBiome.Cave || biome == SoundBiome.Sewer || biome == SoundBiome.Crypt || biome == SoundBiome.Gallery || biome == SoundBiome.Drowned;

        // --- Musique ----------------------------------------------------------------------------------------------------

        /// <summary>
        /// Musique d'un lieu et d'un moment, à partir de celle des règles d'avant (<paramref name="baseMusic"/> :
        /// <see cref="StyleRules.MusicFor(bool, bool, bool, bool, bool, double, bool, bool, double, bool, AmbiencePlace)"/>) : le Gardien, les
        /// attaques et les scènes ne changent pas ; un combat sous terre ou dans l'Envers prend <see cref="StyleMusic.CombatDungeon"/> ; hors
        /// combat, chaque souterrain a sa musique, l'Envers la sienne ; l'exploration devient nocturne la nuit (<paramref name="night"/> d'au
        /// moins 0,5) ou hivernale (<paramref name="winter"/>), la nuit passant devant l'hiver. Le bourg, l'auberge, la forge et les fêtes
        /// restent.
        /// </summary>
        public static StyleMusic Music(StyleMusic baseMusic, SoundBiome biome, double night, bool winter)
        {
            if (baseMusic == StyleMusic.Boss || baseMusic == StyleMusic.Story) return baseMusic;
            if (baseMusic == StyleMusic.Combat) return IsUnderground(biome) || biome == SoundBiome.Envers ? StyleMusic.CombatDungeon : StyleMusic.Combat;
            switch (biome)
            {
                case SoundBiome.Envers: return StyleMusic.Envers;
                case SoundBiome.Sewer: return StyleMusic.Sewers;
                case SoundBiome.Crypt: return StyleMusic.Crypt;
                case SoundBiome.Gallery: return StyleMusic.Galleries;
                case SoundBiome.Drowned: return StyleMusic.Drowned;
                case SoundBiome.Cave: return StyleMusic.Caves;
            }
            if (baseMusic != StyleMusic.Exploration) return baseMusic;
            if (!double.IsNaN(night) && night >= 0.5) return StyleMusic.ExplorationNight;
            return winter ? StyleMusic.ExplorationWinter : StyleMusic.Exploration;
        }

        /// <summary>
        /// Repli d'une musique absente (fichier non déposé), un pas à la fois ; null quand il n'y a plus que l'exploration. Les musiques
        /// d'avant gardent leur repli (<see cref="StyleRules.MusicFallback"/>) ; les nouvelles : nuit et hiver vers l'exploration, galeries et
        /// grottes noyées vers les grottes, grottes et égouts vers la crypte, l'Envers vers le Seuil, le combat sous terre vers le combat.
        /// </summary>
        public static StyleMusic? MusicFallback(StyleMusic wanted)
        {
            switch (wanted)
            {
                case StyleMusic.ExplorationNight:
                case StyleMusic.ExplorationWinter: return StyleMusic.Exploration;
                case StyleMusic.Galleries:
                case StyleMusic.Drowned: return StyleMusic.Caves;
                case StyleMusic.Caves:
                case StyleMusic.Sewers: return StyleMusic.Crypt;
                case StyleMusic.Envers: return StyleMusic.Threshold;
                case StyleMusic.CombatDungeon: return StyleMusic.Combat;
                default: return StyleRules.MusicFallback(wanted);
            }
        }

        /// <summary>
        /// La première musique de la chaîne de replis de <paramref name="wanted"/> pour laquelle <paramref name="available"/> est vrai ;
        /// null si aucune (la suite choisit l'exploration). Au plus huit pas : aucune boucle possible.
        /// </summary>
        public static StyleMusic? MusicChain(StyleMusic wanted, Func<StyleMusic, bool> available)
        {
            if (available == null) throw new ArgumentNullException(nameof(available));
            StyleMusic? m = wanted;
            for (int i = 0; i < 8 && m is StyleMusic music; i++)
            {
                if (available(music)) return music;
                m = MusicFallback(music);
            }
            return null;
        }

        /// <summary>Le combat (et le Gardien) : leur musique part tout de suite et repart du début.</summary>
        public static bool IsFight(StyleMusic music) => music == StyleMusic.Combat || music == StyleMusic.Boss || music == StyleMusic.CombatDungeon;

        /// <summary>
        /// Durée (s) du fondu enchaîné de <paramref name="from"/> à <paramref name="to"/> : 1 s pour entrer en combat (le danger s'entend
        /// tout de suite), 4 s pour en sortir, 5 s pour entrer dans l'Envers ou en sortir, 3 s entre le jour et la nuit, 2,5 s sinon.
        /// </summary>
        public static double FadeSeconds(StyleMusic? from, StyleMusic to)
        {
            if (IsFight(to)) return 1;
            if (from is StyleMusic f && IsFight(f)) return 4;
            if (to == StyleMusic.Envers || from == StyleMusic.Envers) return 5;
            if (to == StyleMusic.ExplorationNight || from == StyleMusic.ExplorationNight) return 3;
            return 2.5;
        }

        // --- Nappes des milieux -----------------------------------------------------------------------------------------

        /// <summary>Les nappes des milieux de la passe son (marais, souterrains, Envers, sous l'eau, rafales) : <see cref="BedVolume"/>.</summary>
        public static bool IsBed(StyleAmbience ambience) => ambience >= StyleAmbience.Marsh && ambience <= StyleAmbience.Gusts;

        /// <summary>
        /// Volume (0 à 1) d'une nappe de milieu : celle du milieu où l'on est (<paramref name="biome"/>), plus calme en combat (moitié) ;
        /// sous l'eau (<paramref name="underwater"/>), seule la nappe sous l'eau joue ; le marais se tait un peu sous la pluie et chante
        /// plus la nuit ; les rafales suivent la pluie. Les autres nappes : 0.
        /// </summary>
        public static double BedVolume(StyleAmbience ambience, SoundBiome biome, bool fighting, bool underwater, double night, double rain)
        {
            double calm = fighting ? 0.5 : 1;
            night = Clamp01(night);
            rain = Clamp01(rain);
            if (underwater) return ambience == StyleAmbience.Underwater ? 0.9 : 0;
            switch (ambience)
            {
                case StyleAmbience.Marsh: return biome == SoundBiome.Marsh ? 0.7 * calm * (0.8 + 0.2 * night) * (1 - 0.5 * rain) : 0;
                case StyleAmbience.Cave: return biome == SoundBiome.Cave ? 0.75 * calm : 0;
                case StyleAmbience.Sewer: return biome == SoundBiome.Sewer ? 0.8 * calm : 0;
                case StyleAmbience.Gallery: return biome == SoundBiome.Gallery ? 0.7 * calm : 0;
                case StyleAmbience.Drowned: return biome == SoundBiome.Drowned ? 0.8 * calm : 0;
                case StyleAmbience.Envers: return biome == SoundBiome.Envers ? 0.8 : 0;   // le bourdon ne baisse pas en combat
                case StyleAmbience.Gusts: return biome == SoundBiome.Heights ? Math.Min(1, 0.6 * calm * (1 + 0.6 * rain)) : 0;
                default: return 0;
            }
        }

        /// <summary>
        /// Les nappes du monde d'avant (oiseaux, vent, rivière, pluie, lieux) se taisent-elles ici ? Oui dans l'Envers, sous l'eau et sous
        /// terre (sauf la crypte, qui a déjà sa nappe « Crypt » parmi elles).
        /// </summary>
        public static bool MutesWorld(SoundBiome biome, bool underwater) =>
            underwater || biome == SoundBiome.Envers || (IsUnderground(biome) && biome != SoundBiome.Crypt);

        // --- Pas ------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Le bruit d'un pas : <paramref name="outdoor"/> est celui du sol du dehors (<see cref="StyleRules.StepSound"/>) ; dans l'eau
        /// (<paramref name="wading"/>) ou les grottes noyées : l'eau ; sous terre et dans l'Envers : la pierre ; dans le marais : la boue ;
        /// dans la neige (<paramref name="snow"/> d'au moins 0,5) dehors, hors du plancher : la neige ; sinon le sol du dehors.
        /// </summary>
        public static StyleSound Step(StyleSound outdoor, SoundBiome biome, double snow, bool wading)
        {
            if (wading || biome == SoundBiome.Drowned) return StyleSound.StepWater;
            if (IsUnderground(biome) || biome == SoundBiome.Envers) return StyleSound.StepStone;
            if (biome == SoundBiome.Indoors) return StyleSound.StepWood;
            if (biome == SoundBiome.Marsh) return StyleSound.StepMud;
            if (!double.IsNaN(snow) && snow >= 0.5 && outdoor != StyleSound.StepWood) return StyleSound.StepSnow;
            return outdoor;
        }

        /// <summary>Le son est-il un pas ?</summary>
        public static bool IsStep(StyleSound sound) =>
            sound == StyleSound.StepGrass || sound == StyleSound.StepGravel || sound == StyleSound.StepLeaves || sound == StyleSound.StepWood
            || (sound >= StyleSound.StepStone && sound <= StyleSound.StepMud);

        // --- Mixage et voix -------------------------------------------------------------------------------------------

        /// <summary>Volume par défaut (pourcentage) d'un canal du mixage.</summary>
        public static int DefaultPercent(SoundBus bus) =>
            bus == SoundBus.Music ? 70 : bus == SoundBus.Ambience ? 80 : bus == SoundBus.Effects ? 90 : 80;

        /// <summary>Volume borné de 0 à 100 %, par pas de 10.</summary>
        public static int ClampPercent(int percent) => (int)Math.Round(Math.Max(0, Math.Min(100, percent)) / 10.0, MidpointRounding.AwayFromZero) * 10;

        /// <summary>Un pas de 10 % de plus (<paramref name="direction"/> positif) ou de moins.</summary>
        public static int StepPercent(int percent, int direction) => ClampPercent(ClampPercent(percent) + Math.Sign(direction) * 10);

        /// <summary>Gain (0 à 1) d'un pourcentage : courbe au carré, plus fidèle à l'oreille qu'une droite (50 % ≈ −12 dB).</summary>
        public static double Gain(int percent)
        {
            double p = ClampPercent(percent) / 100.0;
            return p * p;
        }

        /// <summary>Le canal suivant (le libellé du réglage passe de la musique à l'ambiance, aux effets, à l'interface, puis revient).</summary>
        public static SoundBus NextBus(SoundBus bus) => (SoundBus)(((int)bus + 1) % 4);

        /// <summary>Nom d'un canal dans le réglage.</summary>
        public static string BusName(SoundBus bus) =>
            bus == SoundBus.Music ? "Musique" : bus == SoundBus.Ambience ? "Ambiance" : bus == SoundBus.Effects ? "Effets" : "Interface";

        /// <summary>Texte du réglage : « Musique : 70 % ».</summary>
        public static string Label(SoundBus bus, int percent) => BusName(bus) + " : " + ClampPercent(percent) + " %";

        /// <summary>
        /// Bruitages joués en même temps au plus, selon la machine : 16 sur téléphone, 24 sur le PC modeste de Damien, 32 au-delà (les
        /// musiques et les nappes n'en font pas partie).
        /// </summary>
        public static int VoiceLimit(DeviceTier tier) => tier == DeviceTier.Phone ? 16 : tier == DeviceTier.Modest ? 24 : 32;

        /// <summary>
        /// Un bruitage de priorité <paramref name="priority"/> peut-il partir avec <paramref name="playing"/> bruitages en cours, pour une
        /// limite de <paramref name="limit"/> ? Une priorité basse (pas, bêtes) s'arrête aux trois quarts de la limite, une normale à la
        /// limite ; une haute (mort, niveau, interface) part toujours.
        /// </summary>
        public static bool Admit(SoundPriority priority, int playing, int limit)
        {
            if (priority == SoundPriority.High) return true;
            int cap = priority == SoundPriority.Low ? limit * 3 / 4 : limit;
            return playing < Math.Max(1, cap);
        }

        /// <summary>Priorité d'un bruitage du jeu.</summary>
        public static SoundPriority PriorityOf(StyleSound sound)
        {
            if (IsStep(sound)) return SoundPriority.Low;
            switch (sound)
            {
                case StyleSound.Death:
                case StyleSound.Roar:
                case StyleSound.Dive:
                case StyleSound.Emerge:
                case StyleSound.DoorOpen:
                case StyleSound.ChestOpen:
                case StyleSound.Shatter:
                case StyleSound.Portal: return SoundPriority.High;
                case StyleSound.Howl:
                case StyleSound.Bellow:
                case StyleSound.HareDash:
                case StyleSound.BoarGrumble:
                case StyleSound.Caw:
                case StyleSound.Moo:
                case StyleSound.Bark:
                case StyleSound.Neigh:
                case StyleSound.Rooster:
                case StyleSound.Hens:
                case StyleSound.Sheep:
                case StyleSound.Owl:
                case StyleSound.Hooves:
                case StyleSound.Bray: return SoundPriority.Low;
                default: return SoundPriority.Normal;
            }
        }

        /// <summary>Écart minimal (s) entre deux départs du même bruitage : 0,06 s (une salve de coups ne fait qu'un son), 0,12 s pour un pas.</summary>
        public static double MinGap(StyleSound sound) => IsStep(sound) ? 0.12 : 0.06;

        // --- Outils ---------------------------------------------------------------------------------------------------

        private static double Clamp01(double v) => double.IsNaN(v) ? 0 : Math.Max(0, Math.Min(1, v));

        private static bool Starts(string id, string word) =>
            id.StartsWith(word, StringComparison.OrdinalIgnoreCase) && (id.Length == word.Length || !char.IsLetter(id[word.Length]));

        /// <summary>Un mot de <paramref name="id"/> (coupé aux signes) vaut-il <paramref name="word"/> (ou commence-t-il par lui) ?</summary>
        private static bool HasWord(string? id, string word, bool prefix)
        {
            if (string.IsNullOrEmpty(id)) return false;
            int i = 0;
            while (i < id!.Length)
            {
                while (i < id.Length && !char.IsLetter(id[i])) i++;
                int start = i;
                while (i < id.Length && char.IsLetter(id[i])) i++;
                int length = i - start;
                if (length == 0) continue;
                if ((prefix ? length >= word.Length : length == word.Length)
                    && string.Compare(id, start, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) == 0) return true;
            }
            return false;
        }
    }
}

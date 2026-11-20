using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Zones;
using EdS.Domain.Space;

namespace EdS.Runtime
{
    /// <summary>Sous-lot V2.53 : un écran de chargement peint (un grand lieu des Vallons) et la clé de sa phrase d'Orsédia.</summary>
    public sealed class LoadingScreen
    {
        /// <summary>Identifiant court (« bourg_aube »).</summary>
        public string Id { get; }

        /// <summary>Numéro du dossier de livraison (1 à 12).</summary>
        public int Number { get; }

        /// <summary>Clé du texte du jeu (<c>text.loading.{Id}</c>).</summary>
        public string TextKey => _textKey ?? "text.loading." + Id;

        /// <summary>Sous-lot F6 : fichier de l'image (« ch_fete_01 »), ou null : celui du lieu (<see cref="LoadingScreens.ResourcePath"/>).</summary>
        public string? File { get; }

        private readonly string? _textKey;

        internal LoadingScreen(int number, string id, string file, string textKey)
        {
            Number = number;
            Id = id;
            File = file;
            _textKey = textKey;
        }

        internal LoadingScreen(int number, string id)
        {
            Number = number;
            Id = id;
        }
    }

    /// <summary>
    /// Sous-lot V2.53 (sujet 13, « L'âme du jeu » : écrans de chargement peints avec un texte d'Orsédia) : le choix de l'image et de la phrase
    /// selon la destination, sans Unity. Chaque grand lieu des Vallons a son image ; la destination (un lieu des données, le premier du
    /// fichier qui a une image) donne l'écran, à défaut le village. Aucune liste de conseils n'existe dans le jeu : la phrase du lieu seule.
    /// Affichage seulement : rien ici ne touche à la partie.
    /// </summary>
    public static class LoadingScreens
    {
        /// <summary>Les douze écrans, dans l'ordre du dossier de livraison.</summary>
        public static IReadOnlyList<LoadingScreen> All { get; } = new[]
        {
            new LoadingScreen(1, "bourg_aube"), new LoadingScreen(2, "village"), new LoadingScreen(3, "vallee_cascade"),
            new LoadingScreen(4, "chateau_coteaux"), new LoadingScreen(5, "foret_loups"), new LoadingScreen(6, "hautes_collines"),
            new LoadingScreen(7, "abbaye"), new LoadingScreen(8, "marais_lac"), new LoadingScreen(9, "carriere_mine"),
            new LoadingScreen(10, "fort_bandits"), new LoadingScreen(11, "terres_seuil"), new LoadingScreen(12, "col_pont_murmures"),
        };

        /// <summary>Écran de repli : le village.</summary>
        public static LoadingScreen Default => All[1];

        /// <summary>
        /// Sous-lot F6 : pendant une fête d'Orsédia, l'écran de chargement est son image d'ambiance (n° <paramref name="feast"/>, 1 à 12 :
        /// « ch_fete_01 »…), avec son accroche (<paramref name="motto"/>, clé de texte).
        /// </summary>
        public static LoadingScreen ForFeast(int feast, string motto)
        {
            if (feast < 1 || feast > 12) throw new ArgumentOutOfRangeException(nameof(feast));
            return new LoadingScreen(100 + feast, "fete_" + feast.ToString("00"), "ch_fete_" + feast.ToString("00"), motto ?? throw new ArgumentNullException(nameof(motto)));
        }

        // Lieu des données (identifiant) -> numéro d'écran. Les plaines n'ont pas d'image à elles : le lieu suivant qui les contient décide.
        private static readonly Dictionary<string, int> PlaceScreens = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["bourg"] = 1, ["ville_du_bourg"] = 1, ["place_du_marche_de_la_ville"] = 1, ["eglise_de_la_ville"] = 1,
            ["village"] = 2, ["moulin"] = 2, ["forge"] = 2, ["four_de_firmin"] = 2, ["champs"] = 2, ["champs_du_sud"] = 2, ["vergers"] = 2, ["vergers_du_sud"] = 2, ["pre_nord"] = 2,
            ["riviere"] = 3, ["vallee_de_la_riviere"] = 3, ["cascade"] = 3, ["moulin_a_foulon"] = 3, ["cabane_du_pecheur"] = 3, ["pont_suspendu"] = 3,   // V2.31
            ["coteaux_du_chateau"] = 4, ["route_d_havre_d_ambre"] = 4,
            ["domaine_du_chateau"] = 4, ["pressoir_du_chateau"] = 4, ["chai_du_chateau"] = 4, ["vignes_du_chateau"] = 4,   // V2.33
            ["foret_des_loups"] = 5, ["foret_des_loups_profonde"] = 5, ["camp_des_rodeurs"] = 5,
            ["arbre_millenaire"] = 5, ["taniere_de_la_meute"] = 5, ["cabane_du_bucheron"] = 5, ["lisiere_de_la_foret"] = 5,   // V2.32
            ["hautes_collines"] = 6, ["hautes_pierres"] = 6, ["tour_de_guet"] = 6, ["epaulement_est"] = 6,
            ["bergerie_des_collines"] = 6, ["cairns_de_la_crete"] = 6, ["tour_de_signal"] = 6,   // V2.34
            ["ruines"] = 7, ["abbaye_en_ruine"] = 7,
            ["nef_de_l_abbaye"] = 7, ["cloitre_de_l_abbaye"] = 7, ["dortoir_de_l_abbaye"] = 7, ["jardin_de_l_abbaye"] = 7,   // V2.35
            ["lac"] = 8, ["grand_lac"] = 8, ["rives_du_lac"] = 8,
            ["cabanes_sur_pilotis"] = 8, ["ile_des_saules_dormants"] = 8,   // V2.36
            ["carriere"] = 9, ["hameau"] = 9, ["grande_carriere"] = 9, ["feu_de_camp"] = 9,
            ["village_des_mineurs"] = 9, ["carreau_de_la_mine"] = 9, ["gradins_de_la_carriere"] = 9,   // V2.37
            ["fort_des_bandits"] = 10, ["route_du_fort"] = 10,
            ["enceinte_du_fort"] = 10, ["tour_des_bandits"] = 10,   // V2.38
            ["seuil"] = 11, ["coeur_du_seuil"] = 11, ["terres_corrompues"] = 11, ["landes_du_seuil"] = 11,
            ["cercle_des_pierres_levees"] = 11,   // V2.39
            ["gorge_du_pont"] = 12, ["route_du_col"] = 12,
            ["passerelle_des_ronces"] = 12, ["pont_des_murmures"] = 12, ["col_des_cretes_de_fer"] = 12,   // V2.40
            ["abords_de_la_porte_nord"] = 1, ["poste_de_peage"] = 4, ["route_de_l_est"] = 7, ["pont_effondre"] = 7,   // V2.40
        };

        /// <summary>Écran d'un lieu des données (null s'il n'en a pas).</summary>
        public static LoadingScreen? ForPlace(string placeId) =>
            PlaceScreens.TryGetValue(placeId ?? throw new ArgumentNullException(nameof(placeId)), out int n) ? All[n - 1] : null;

        /// <summary>Écran de la destination <paramref name="at"/> dans <paramref name="zone"/> : le premier lieu (ordre du fichier) qui a une image, sinon le village.</summary>
        public static LoadingScreen For(ZoneDefinition zone, GroundVector? at)
        {
            if (zone is null) throw new ArgumentNullException(nameof(zone));
            if (at is GroundVector p)
                foreach (Place place in zone.PlacesAt(p))
                    if (ForPlace(place.Id) is LoadingScreen found) return found;
            return Default;
        }

        /// <summary>Chemin dans <c>Resources</c> (sans extension) : 1 920 × 1 080 sur PC, 1 280 × 720 sur téléphone et PC modeste (<see cref="MachineTier.Light"/>).</summary>
        public static string ResourcePath(LoadingScreen screen, DeviceTier tier)
        {
            if (screen is null) throw new ArgumentNullException(nameof(screen));
            string folder = "EdsInterface/Chargements/" + (MachineTier.Light(tier) ? "1280" : "1920") + "/";
            if (screen.File != null) return folder + screen.File;   // sous-lot F6 : l'image d'ambiance de la fête
            return folder + "ch_" + screen.Number.ToString("00") + "_" + FileSuffix[screen.Number - 1];
        }

        private static readonly string[] FileSuffix =
        {
            "bourg_aube", "village", "vallee_cascade", "chateau_coteaux", "foret_loups", "hautes_collines",
            "abbaye", "marais_lac", "carriere_mine", "fort_bandits", "terres_seuil", "col_pont_murmures",
        };

        /// <summary>Durée minimale de l'écran (secondes) : assez pour lire la phrase, sans attendre pour rien.</summary>
        public const float HoldSeconds = 3f;

        /// <summary>Durée du fondu d'entrée et de sortie (secondes).</summary>
        public const float FadeSeconds = 0.6f;

        /// <summary>Opacité de l'écran <paramref name="elapsed"/> secondes après son début (fondu d'entrée, plateau, fondu de sortie ; 0 après <see cref="HoldSeconds"/>).</summary>
        public static float Opacity(float elapsed)
        {
            if (elapsed < 0f || elapsed >= HoldSeconds) return 0f;
            float inn = Math.Min(1f, elapsed / FadeSeconds);
            float outt = Math.Min(1f, (HoldSeconds - elapsed) / FadeSeconds);
            return Math.Min(inn, outt);
        }
    }
}

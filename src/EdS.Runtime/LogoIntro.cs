using System;

namespace EdS.Runtime
{
    /// <summary>Sous-lot V2.54 : les versions du logo livrées par le dossier « Logo définitif » (numéros du dossier).</summary>
    public enum LogoVersion
    {
        /// <summary>01 : logo complet doré, fêlure turquoise et éclats.</summary>
        Complete = 1,

        /// <summary>02 : même composition, sans fêlure ni éclats (premier calque de l'ouverture).</summary>
        Plain = 2,

        /// <summary>03 : la fêlure, sa lumière et ses éclats seuls (second calque, à superposer à 02 sans décalage).</summary>
        Crack = 3,

        /// <summary>04 : version blanche, la fêlure en réserve transparente (fond clair, très petite taille).</summary>
        White = 4,

        /// <summary>06 : logo complet avec la devise « Ce qui est brisé peut encore briller. ».</summary>
        Motto = 6,
    }

    /// <summary>
    /// Sous-lot V2.54 (sujet 13, « L'âme du jeu » : logo définitif, logo vivant) : les règles de l'ouverture du logo au lancement et du choix de sa
    /// version, sans Unity. Au lancement, 02 apparaît, puis le calque 03 « s'ouvre » le long de la fêlure (de haut en bas) pendant que sa lueur
    /// pulse doucement, puis 06 (avec la devise) apparaît par-dessus ; un geste abrège. Le temps est celui de l'affichage (secondes réelles données
    /// par l'interface) : aucune horloge ici, rien ne touche à la partie ni au cœur déterministe. Les fonctions sont pures (aucune allocation).
    /// </summary>
    public static class LogoIntro
    {
        /// <summary>Fondu d'entrée du logo sans fêlure (secondes).</summary>
        public const float FadeInSeconds = 0.8f;

        /// <summary>Début de l'ouverture de la fêlure (secondes).</summary>
        public const float CrackStart = 1.2f;

        /// <summary>Fin de l'ouverture : la fêlure est entièrement révélée (1,6 s d'ouverture).</summary>
        public const float CrackEnd = 2.8f;

        /// <summary>Période de la pulsation de la lueur, après l'ouverture (secondes).</summary>
        public const float PulseSeconds = 1.6f;

        /// <summary>Amplitude de la pulsation : la lueur varie entre 1 - amplitude et 1.</summary>
        public const float PulseDepth = 0.25f;

        /// <summary>Début du fondu vers la version avec devise.</summary>
        public const float MottoStart = 2.8f;

        /// <summary>Fin de ce fondu.</summary>
        public const float MottoEnd = 3.6f;

        /// <summary>Début du fondu de sortie (on lit la devise jusque-là).</summary>
        public const float OutStart = 5.0f;

        /// <summary>Durée totale ; le logo disparaît entièrement à cet instant.</summary>
        public const float TotalSeconds = 5.6f;

        /// <summary>Un geste n'abrège pas avant cet instant (évite qu'une touche appuyée au lancement saute le logo).</summary>
        public const float SkipAfter = 0.4f;

        /// <summary>Haut de la fêlure, en fraction de la hauteur de l'image (bords des pixels non transparents du calque 03 : 201 sur 1 024, marge comprise).</summary>
        public const float CrackTop = 0.19f;

        /// <summary>Bas de la fêlure, en fraction de la hauteur de l'image (832 sur 1 024, marge comprise).</summary>
        public const float CrackBottom = 0.82f;

        /// <summary>Largeur d'affichage (pixels) sous laquelle la devise est illisible : on montre 01 au lieu de 06.</summary>
        public const float MottoMinWidth = 700f;

        /// <summary>Largeur d'affichage (pixels) sous laquelle le logo doré est trop fin : on montre la version blanche.</summary>
        public const float WhiteMaxWidth = 256f;

        /// <summary>Rapport largeur / hauteur de toutes les images du logo (2 : 1).</summary>
        public const float Aspect = 2f;

        /// <summary>L'ouverture est en cours à l'instant <paramref name="elapsed"/>.</summary>
        public static bool Active(float elapsed) => elapsed >= 0f && elapsed < TotalSeconds;

        /// <summary>Opacité d'ensemble : fondu d'entrée de 02, plateau, fondu de sortie (0 hors de l'ouverture).</summary>
        public static float Overall(float elapsed)
        {
            if (!Active(elapsed)) return 0f;
            float inn = Math.Min(1f, elapsed / FadeInSeconds);
            float outt = Math.Min(1f, (TotalSeconds - elapsed) / (TotalSeconds - OutStart));
            return Math.Min(inn, outt);
        }

        /// <summary>Part de la fêlure révélée, de 0 à 1 (adoucie au début et à la fin) ; la révélation descend du haut au bas de la fêlure.</summary>
        public static float CrackReveal(float elapsed)
        {
            if (elapsed <= CrackStart) return 0f;
            if (elapsed >= CrackEnd) return 1f;
            float t = (elapsed - CrackStart) / (CrackEnd - CrackStart);
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// Intensité de la lueur de la fêlure : 1 pendant l'ouverture, puis une pulsation douce (1 - <see cref="PulseDepth"/> à 1, période
        /// <see cref="PulseSeconds"/>) qui part de 1 pour ne pas faire de saut.
        /// </summary>
        public static float Glow(float elapsed)
        {
            if (elapsed <= CrackEnd) return 1f;
            float phase = (elapsed - CrackEnd) / PulseSeconds * 2f * (float)Math.PI;
            return 1f - PulseDepth * (0.5f - 0.5f * (float)Math.Cos(phase));
        }

        /// <summary>Opacité de la version avec devise, qui s'ajoute au-dessus (0 avant <see cref="MottoStart"/>, 1 après <see cref="MottoEnd"/>).</summary>
        public static float MottoOpacity(float elapsed)
        {
            if (elapsed <= MottoStart) return 0f;
            if (elapsed >= MottoEnd) return 1f;
            float t = (elapsed - MottoStart) / (MottoEnd - MottoStart);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Instant d'après un geste du joueur : on saute à la fin de l'ouverture (devise lisible, puis fondu de sortie) si le geste vient assez tard.</summary>
        public static float AfterGesture(float elapsed) =>
            elapsed >= SkipAfter && elapsed < OutStart ? OutStart : elapsed;

        /// <summary>
        /// Version fixe à montrer (écran titre, menu, petite taille) : blanche sur fond clair ou très petite (moins de
        /// <see cref="WhiteMaxWidth"/> pixels), sinon avec devise si elle reste lisible (au moins <see cref="MottoMinWidth"/> pixels), sinon complète.
        /// </summary>
        public static LogoVersion Fixed(bool lightBackground, float displayWidthPixels)
        {
            if (lightBackground || displayWidthPixels < WhiteMaxWidth) return LogoVersion.White;
            return displayWidthPixels >= MottoMinWidth ? LogoVersion.Motto : LogoVersion.Complete;
        }

        /// <summary>La devise se montre-t-elle à cette largeur d'affichage (pixels) ?</summary>
        public static bool ShowsMotto(float displayWidthPixels) => displayWidthPixels >= MottoMinWidth;

        /// <summary>Chemin dans <c>Resources</c> (sans extension) : 2 048 × 1 024 sur PC, 1 024 × 512 sur téléphone et PC modeste (<see cref="MachineTier.Light"/>).</summary>
        public static string ResourcePath(LogoVersion version, DeviceTier tier)
        {
            string name;
            switch (version)
            {
                case LogoVersion.Complete: name = "01_complet"; break;
                case LogoVersion.Plain: name = "02_sans_felure"; break;
                case LogoVersion.Crack: name = "03_felure"; break;
                case LogoVersion.White: name = "04_blanc"; break;
                case LogoVersion.Motto: name = "06_devise"; break;
                default: throw new ArgumentOutOfRangeException(nameof(version));
            }
            return "EdsInterface/Logo/" + (MachineTier.Light(tier) ? "1024" : "2048") + "/logo_" + name;
        }
    }
}

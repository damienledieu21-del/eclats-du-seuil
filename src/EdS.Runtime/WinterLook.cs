using System;
using System.Collections.Generic;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot V2.44.3 (l'hiver dans Unity, décisions de Damien du 20 novembre 2026) : règles portables de l'affichage de l'hiver, appliquées
    /// par Unity (<c>SnowCover</c>, <c>SpaceView</c>, <c>CellStreamView</c>) : quels modèles reçoivent la neige posée, quand son double se
    /// montre, et où l'eau gèle (seulement là où la glace est praticable : la bande du lac gelé, réponse de Damien du 20 novembre 2026 ; les
    /// rivières et le reste du lac restent libres). Affichage seulement.
    /// </summary>
    public static class WinterLook
    {
        /// <summary>Intervalle (s) entre deux recherches de nouveaux modèles à enneiger.</summary>
        public const float RescanSeconds = 2f;

        /// <summary>Doubles de neige au plus (garde-fou de rendu sur mobile, à mesurer).</summary>
        public const int MaxOverlays = 6000;

        /// <summary>Nom des doubles de neige (ignorés par le fondu des obstacles qui cachent le joueur).</summary>
        public const string OverlayName = "Neige posée";

        /// <summary>File de rendu à partir de laquelle un matériau est découpé ou transparent (pas de neige posée).</summary>
        public const int CutoutQueue = 2450;

        /// <summary>Shaders qui ont leur propre neige ou n'en portent pas (feuillage, herbe, eau, effets, neige elle-même).</summary>
        public static IReadOnlyList<string> SkippedShaders { get; } =
            new[] { "EdS/Feuillage", "EdS/Herbe", "EdS/Eau", "EdS/Particule", "EdS/Annonce", "EdS/Echo", "EdS/Faille", "EdS/Neige" };

        /// <summary>Identifiant d'obstacle d'eau où la glace est praticable (la bande du passage <c>glace_du_lac</c>).</summary>
        public const string WalkableIceTag = "glace_du_lac";

        /// <summary>Revue Codex de la PR #232 : verdict sur un modèle ; un refus passager (modèle coupé, en fondu) est réexaminé plus tard.</summary>
        public enum Verdict
        {
            Accepted = 0,

            /// <summary>Refus passager : modèle coupé ou matériau découpé ou transparent (peut-être en fondu) ; réexaminé à la recherche suivante.</summary>
            Later = 1,

            /// <summary>Refus définitif : double de neige, ancêtre animé, aucun matériau, matériau absent ou shader qui a sa propre neige.</summary>
            Never = 2,
        }

        /// <summary>
        /// Comme <see cref="Accepts"/>, en distinguant le refus passager (<see cref="Verdict.Later"/> : coupé, file découpée ou transparente) du
        /// refus définitif (<see cref="Verdict.Never"/>).
        /// </summary>
        public static Verdict Judge(string name, bool enabled, bool animated, IReadOnlyList<(string Shader, int Queue)> materials)
        {
            if (materials is null) throw new ArgumentNullException(nameof(materials));
            if (animated || name == OverlayName || materials.Count == 0) return Verdict.Never;
            bool later = !enabled;
            for (int i = 0; i < materials.Count; i++)
            {
                (string shader, int queue) = materials[i];
                if (shader is null) return Verdict.Never;
                for (int k = 0; k < SkippedShaders.Count; k++)
                    if (shader == SkippedShaders[k]) return Verdict.Never;
                if (queue >= CutoutQueue) later = true;
            }
            return later ? Verdict.Later : Verdict.Accepted;
        }

        /// <summary>
        /// Revue Codex de la PR #232 : grille de la couche de neige posée sur le terrain du catalogue (un <c>Terrain</c> d'Unity, sans
        /// MeshRenderer) : pas en mètres pour un côté <paramref name="side"/>, au moins 2 m et au plus <see cref="TerrainSnowCells"/> cases
        /// par côté (moins de 65 536 sommets, maillage de 16 bits, compatible mobile).
        /// </summary>
        public static float TerrainSnowStep(float side) => Math.Max(2f, side / TerrainSnowCells);

        /// <summary>Cases au plus par côté de la couche de neige du terrain.</summary>
        public const int TerrainSnowCells = 200;

        /// <summary>
        /// Un modèle reçoit la neige posée : affiché, pas un double de neige, aucun ancêtre animé (bêtes, objets tenus, parties rigides d'un
        /// modèle animé), et chacun de ses matériaux opaque (file sous <see cref="CutoutQueue"/>) d'un shader qui n'a pas sa propre neige.
        /// </summary>
        public static bool Accepts(string name, bool enabled, bool animated, IReadOnlyList<(string Shader, int Queue)> materials)
        {
            if (materials is null) throw new ArgumentNullException(nameof(materials));
            if (!enabled || animated || name == OverlayName || materials.Count == 0) return false;
            for (int i = 0; i < materials.Count; i++)
            {
                (string shader, int queue) = materials[i];
                if (shader is null || queue >= CutoutQueue) return false;
                for (int k = 0; k < SkippedShaders.Count; k++)
                    if (shader == SkippedShaders[k]) return false;
            }
            return true;
        }

        /// <summary>
        /// Le double de neige se montre : il neige, le modèle est affiché et pas en fondu (son premier matériau est encore opaque ; un obstacle
        /// qui cache le joueur passe en transparent ou est coupé : sa neige le suit).
        /// </summary>
        public static bool OverlayShown(bool snowing, bool enabled, int firstQueue) => snowing && enabled && firstQueue < CutoutQueue;

        /// <summary>L'eau de l'obstacle <paramref name="obstacleId"/> gèle en hiver : seulement la bande praticable du lac.</summary>
        public static bool IceWater(string obstacleId) => obstacleId != null && obstacleId.IndexOf(WalkableIceTag, StringComparison.Ordinal) >= 0;
    }
}

using System.Text.RegularExpressions;

namespace EdS.Domain.Builds
{
    /// <summary>
    /// Identifiants stables des définitions : minuscules, segments séparés par des points, préfixés par leur domaine
    /// (<c>class.bastion</c>, <c>tree.bastion.rempart</c>, <c>talent.bastion.rempart.garde_stable</c>,
    /// <c>skill.bastion.frappe_du_rempart</c>). Un identifiant publié n'est jamais renommé ni réutilisé.
    /// </summary>
    public static class StableId
    {
        private static readonly Regex Pattern = new Regex(@"^[a-z]+(\.[a-z0-9_]+)+$", RegexOptions.CultureInvariant);

        public static bool IsValid(string? id) => id != null && Pattern.IsMatch(id);

        /// <summary>Vrai si l'identifiant est valide et commence par le préfixe attendu (ex. « talent »).</summary>
        public static bool HasPrefix(string? id, string prefix) => IsValid(id) && id!.StartsWith(prefix + ".", System.StringComparison.Ordinal);
    }
}

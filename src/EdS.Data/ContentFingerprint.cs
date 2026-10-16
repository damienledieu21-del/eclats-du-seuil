using System.Security.Cryptography;
using System.Text;

namespace EdS.Data
{
    /// <summary>Empreinte SHA-256 d'une écriture canonique, au format <c>sha256:…</c>.</summary>
    public static class ContentFingerprint
    {
        public const string Prefix = "sha256:";

        public static string Compute(string canonicalText)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(canonicalText);
            byte[] hash;
            using (SHA256 sha = SHA256.Create())
                hash = sha.ComputeHash(bytes);

            var hex = new StringBuilder(Prefix.Length + hash.Length * 2);
            hex.Append(Prefix);
            foreach (byte b in hash) hex.Append(b.ToString("x2"));
            return hex.ToString();
        }
    }
}

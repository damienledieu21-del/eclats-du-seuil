using System.Collections.Generic;
using System.Linq;
using EdS.Data.Zones;
using EdS.World;

namespace EdS.Runtime
{
    public sealed partial class AdventureSession
    {
        /// <summary>
        /// V2.52 (le contenu de l'Envers des douze lieux) : secret de la zone à portée du personnage vivant (coffre ou stèle de lore, le plus
        /// proche), ou null. Sans repère sur la carte : on le trouve en explorant ; à portée, l'indication dit son nom.
        /// </summary>
        public ZoneSecret? SecretInReach => World.IsDefeated ? null : World.SecretsInReach().FirstOrDefault();

        /// <summary>V2.52 : indication à l'écran devant le secret à portée (« F : fouiller le coffre renversé »), ou null.</summary>
        public string? SecretHint => SecretInReach is ZoneSecret s
            ? string.Format(Text(s.Kind == SecretKind.Lore ? "text.ui.secret.read_hint" : World.IsSecretOpened(s.Id) ? "text.ui.secret.empty_hint" : "text.ui.secret.open_hint"), s.Name)
            : null;

        /// <summary>V2.52 : le secret <paramref name="id"/> a déjà été fouillé (affichage : coffre ouvert).</summary>
        public bool IsSecretOpened(string id) => World.IsSecretOpened(id);

        /// <summary>
        /// V2.52 : fouille le secret à portée : son texte s'affiche, ses récompenses (une seule fois) entrent dans l'inventaire, en entier ou
        /// pas du tout. Une stèle déjà lue se relit ; un coffre déjà vidé le dit. Faux, avec un message et sans rien changer, si la fouille est
        /// refusée (pas de secret à portée, combat, sac plein, registre plein).
        /// </summary>
        public bool OpenSecret()
        {
            if (!(SecretInReach is ZoneSecret secret) || Dialogue != null) return false;
            var given = new List<(string Item, int Count)>();
            SecretRefusal refusal = World.OpenSecret(secret.Id, given);
            switch (refusal)
            {
                case SecretRefusal.None:
                    // Revue Codex de la PR #262 (P2) : objets et clé du registre changés hors des événements du tick : à la prochaine sauvegarde.
                    _dirty = true;
                    Say(secret.Text);
                    foreach ((string item, int count) in given) Say("text.ui.item_given", ItemName(item), count);
                    return true;
                case SecretRefusal.AlreadyOpened:
                    if (secret.Kind == SecretKind.Lore) { Say(secret.Text); return true; }
                    Say("text.ui.secret.empty", secret.Name);
                    return false;
                case SecretRefusal.InCombat:
                    Say("text.ui.secret.in_combat");
                    return false;
                case SecretRefusal.BagFull:
                    Say("text.ui.secret.bag_full");
                    return false;
                default:
                    Say("text.ui.secret.unavailable");
                    return false;
            }
        }
    }
}

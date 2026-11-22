using System;
using System.Collections.Generic;
using EdS.Data.Corruption;
using EdS.Data.Saves;
using EdS.Data.Zones;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.World;

namespace EdS.Runtime
{
    public sealed partial class AdventureSession
    {
        /// <summary>Sous-lot V2.42.1 : la source puis les terres que la corruption peut gagner, avec leur palier d'aujourd'hui (vide : aucune).</summary>
        public IReadOnlyList<CorruptionView> Corruption => World.Corruption;

        /// <summary>
        /// Décision de Damien du 21 novembre 2026 : la zone d'une partie qui s'ouvre, ses créatures plus fortes de 10 % par palier de leur terre
        /// corrompue (jour du monde de la sauvegarde, même horloge que la session).
        /// </summary>
        private ZoneDefinition Corrupted(ZoneDefinition zone, WorldSave save) =>
            _corruption is null || !(save.Character is SavedCharacter c) ? zone
                : CorruptionRules.Strengthen(zone, _corruption, c.Corruption, new WorldClock(zone.Setup.Combat.TickMilliseconds).At(save.WorldTick).Day);

        /// <summary>
        /// Revue de la PR #242 : portée de la touche d'action pour un autel, celle du monde qui fait foi (<see cref="WorldSession.PurifyRange"/>
        /// depuis le bord de l'emprise du personnage) : l'indication « F : purifier l'autel » n'apparaît que là où la purification est possible.
        /// </summary>
        public Fixed AltarReach => World.Zone.Engine.View(World.CombatantId).Radius is Fixed r ? checked(WorldSession.PurifyRange + r) : WorldSession.PurifyRange;

        /// <summary>Sous-lot V2.42.1 : terre corrompue dont l'autel est à portée (pour le purifier), ou null.</summary>
        public CorruptionView? AltarInReach
        {
            get
            {
                IReadOnlyList<CorruptionView> all = Corruption;
                if (all.Count == 0 || World.IsDefeated || !(Me is GroundVector p)) return null;
                Fixed reach = AltarReach;
                for (int i = 0; i < all.Count; i++)
                    if (!all[i].IsSource && all[i].IsCorrupted && Within(p, all[i].Altar, reach)) return all[i];
                return null;
            }
        }

        /// <summary>Sous-lot V2.42.1 : purifie l'autel à portée ; un message dit pourquoi si c'est refusé.</summary>
        public bool PurifyAltar()
        {
            if (!(AltarInReach is CorruptionView v)) return false;
            CorruptionRefusal refusal = World.PurifyAltar(v.Area.Id);
            if (refusal == CorruptionRefusal.TooFar) Say("text.ui.corruption.too_far");
            else if (refusal == CorruptionRefusal.NotCorrupted) Say("text.ui.corruption.clean");
            else if (refusal == CorruptionRefusal.NoItem) Say("text.ui.corruption.no_item");
            else if (refusal == CorruptionRefusal.Full) Say("text.ui.corruption.full");
            if (refusal == CorruptionRefusal.None) _dirty = true;
            return refusal == CorruptionRefusal.None;
        }

        /// <summary>Sous-lot V2.42.1 : nom d'une terre pour les textes (« les landes grises »), sinon son identifiant.</summary>
        public string CorruptionPlace(string area)
        {
            string key = $"text.corruption.area.{area}";
            return Texts.Has(key) ? Text(key) : area;
        }

        /// <summary>Sous-lot V2.42.1 : annonce d'un changement de la corruption (terre gagnée, plus forte, reculée, autel purifié).</summary>
        private void CorruptionNoticeSaid(CorruptionNotice n)
        {
            string key = n.Kind switch
            {
                CorruptionNoticeKind.Spread => "text.corruption.spread",
                CorruptionNoticeKind.Grew => "text.corruption.grew",
                CorruptionNoticeKind.Receded => n.Stage == 0 ? "text.corruption.receded" : "text.corruption.weakened",
                _ => "text.corruption.purified",
            };
            Say(key, CorruptionPlace(n.Area), n.Stage);
            if (n.Kind != CorruptionNoticeKind.Purified) return;
            _dirty = true;
            if (n.Xp > 0) Say("text.ui.corruption.purified_xp", n.Xp);
        }

        /// <summary>
        /// Sous-lot V2.42.1 : à quel point la corruption marque le point <paramref name="p"/>, en millièmes (0 : rien ; 1 000 : au cœur d'une
        /// terre au palier maximal) : pour chaque terre corrompue, son palier sur le maximal fois <c>1 − d²/r²</c> ; la plus forte compte.
        /// Calcul entier sans débordement (rayon ≤ 400 m), affichage seulement.
        /// </summary>
        public static int CorruptionFadeAt(IReadOnlyList<CorruptionView> all, GroundVector p)
        {
            if (all is null) throw new ArgumentNullException(nameof(all));
            long best = 0;
            foreach (CorruptionView v in all)
            {
                if (!v.IsCorrupted) continue;
                long r2 = checked(v.Radius.Raw * v.Radius.Raw), d2 = Distance2(p, v.Position);
                if (r2 <= 0 || d2 >= r2) continue;
                long ratio = Math.Min(1000, (r2 - d2) / Math.Max(1, r2 / 1000));   // 1 − d²/r², en millièmes (r ≥ 10 m : r²/1 000 ≥ 10⁷)
                long fade = checked(ratio * v.Stage) / v.MaxStage;
                if (fade > best) best = fade;
            }
            return (int)Math.Min(1000, best);
        }

        /// <summary>Sous-lot V2.42.1 : la même marque là où est le personnage.</summary>
        public int CorruptionFade => Me is GroundVector p ? CorruptionFadeAt(Corruption, p) : 0;

        /// <summary>Sous-lot V2.42.1 : libellé de la terre corrompue où est le personnage (« Terre corrompue · palier 2 sur 3 — … »), ou null.</summary>
        public string? CorruptionLabel
        {
            get
            {
                if (!(Me is GroundVector p) || !(World.CorruptionAt(p) is CorruptionView v)) return null;
                if (ReferenceEquals(v, _corruptionLabelOf)) return _corruptionLabel;
                string stage = Texts.Has($"text.ui.corruption.stage.{v.Stage}") ? " — " + Text($"text.ui.corruption.stage.{v.Stage}") : string.Empty;
                _corruptionLabelOf = v;
                return _corruptionLabel = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    Text(v.IsSource ? "text.ui.corruption.source" : "text.ui.corruption.label"), v.Stage, v.MaxStage, CorruptionPlace(v.Area.Id)) + stage;
            }
        }

        private CorruptionView? _corruptionLabelOf;
        private string? _corruptionLabel;
    }
}

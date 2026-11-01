using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Items;
using EdS.Data.Saves;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;

namespace EdS.World
{
    /// <summary>Sous-lot V1.8c : motif d'un refus de bonus (rien n'a changé : ni l'objet, ni les places de bonus).</summary>
    public enum BoonRefusal
    {
        None = 0,

        /// <summary>L'objet n'est pas un objet de bonus (ou il est absent du catalogue).</summary>
        NotBoon = 1,

        /// <summary>Aucun exemplaire de l'objet dans l'inventaire.</summary>
        NothingThere = 2,

        /// <summary>Personnage absent ou tombé.</summary>
        Unavailable = 3,

        /// <summary>En plein combat (<see cref="WorldSession.InCombat"/>) : on mange, on boit un élixir ou on huile sa lame hors combat.</summary>
        InCombat = 4,
    }

    /// <summary>Sous-lot V1.8c : un bonus fini à un tick (sa place et son objet).</summary>
    public sealed class BoonExpired
    {
        public BoonKind Kind { get; }
        public string Item { get; }

        internal BoonExpired(BoonKind kind, string item)
        {
            Kind = kind;
            Item = item;
        }

        public override string ToString() => $"{BoonKinds.Id(Kind)} {Item}";
    }

    /// <summary>
    /// Sous-lot V1.8c (sujet 36, choix de Damien du 30 octobre 2026) : les trois places de bonus du personnage, actives en même temps : un
    /// plat (cuisine), un élixir (alchimie), une huile ou pierre d'arme (forge), une seule de chaque sorte.
    /// <list type="bullet">
    /// <item>Consommer un objet de bonus (<see cref="UseBoon"/>) est atomique : hors combat, l'objet quitte l'inventaire et occupe sa place,
    /// avec la durée pleine de sa qualité (<see cref="ItemCatalog.BoonTicks"/>, de 30 minutes à 1 heure de jeu) ; refusé, rien ne change.</item>
    /// <item><b>Remplacement</b> : un nouvel objet de la même place remplace l'ancien bonus, qui est perdu (sans rien rendre, le temps qui lui
    /// restait compris) ; le nouveau part de sa durée pleine, même s'il est le même objet (on « reprend » un plat). Les deux autres places ne
    /// changent pas.</item>
    /// <item>Le temps qui reste baisse d'un tick à chaque tick simulé de la partie (<see cref="Step"/>), y compris quand le personnage est
    /// tombé ; jamais hors ligne (seule la session qui tourne le fait baisser : règle du serveur). À zéro, le bonus quitte sa place à ce tick
    /// précis (<see cref="SessionStep.BoonsExpired"/>).</item>
    /// <item>Les bonus sont <b>gardés quand le personnage tombe</b> : la reprise ouvre une session sur <see cref="Save"/>, qui les garde avec
    /// leur temps.</item>
    /// <item>Effet en combat : le bonus des objets des bonus actifs s'ajoute au bonus de l'équipement (<see cref="BoonGear"/>, dans la limite
    /// du combat), à l'entrée dans une session (halte, porte, voyage, reprise), comme un équipement changé : le combat en cours garde les
    /// bonus de son entrée (<see cref="EnteredBoons"/>).</item>
    /// </list>
    /// </summary>
    public sealed partial class WorldSession
    {
        /// <summary>Bonus actif de chaque place (indice : <see cref="BoonKind"/>), ou null.</summary>
        private readonly SavedBoon?[] _boons;

        /// <summary>Bonus actifs, dans l'ordre des places, avec le temps qui leur reste (une copie).</summary>
        public IReadOnlyList<SavedBoon> ActiveBoons => _boons.Where(b => b != null).Select(b => b!).ToList();

        /// <summary>Bonus actif de la place <paramref name="kind"/>, ou null.</summary>
        public SavedBoon? BoonAt(BoonKind kind) => Enum.IsDefined(typeof(BoonKind), kind) ? _boons[(int)kind] : null;

        /// <summary>Bonus actifs à l'entrée dans la session : ceux dont le combat en cours a reçu l'effet.</summary>
        public IReadOnlyList<SavedBoon> EnteredBoons { get; }

        /// <summary>Refus qu'aurait <see cref="UseBoon"/> maintenant (rien ne change).</summary>
        public BoonRefusal CanUseBoon(string item)
        {
            if (item is null || !_items.TryGetItem(item, out ItemDefinition definition) || definition.Boon is null) return BoonRefusal.NotBoon;
            if (Inventory.Available(item) < 1) return BoonRefusal.NothingThere;
            CombatantView me = Zone.Engine.View(_character.Id);
            if (_defeated || !me.IsPresent || me.IsDead) return BoonRefusal.Unavailable;
            return InCombat ? BoonRefusal.InCombat : BoonRefusal.None;
        }

        /// <summary>
        /// Consomme un exemplaire de l'objet de bonus <paramref name="item"/> : il quitte l'inventaire et occupe sa place de bonus avec sa durée
        /// pleine, en remplaçant le bonus de la même place s'il y en avait un (<paramref name="replaced"/>, perdu). Refusé
        /// (<see cref="CanUseBoon"/>) : rien ne change. L'effet en combat vient à la prochaine entrée dans une session.
        /// </summary>
        public BoonRefusal UseBoon(string item, out SavedBoon? replaced)
        {
            replaced = null;
            BoonRefusal refusal = CanUseBoon(item);
            if (refusal != BoonRefusal.None) return refusal;
            ItemDefinition definition = _items.Item(item);
            var boon = new SavedBoon(definition.Boon!.Value, item, _items.BoonTicks(definition));   // construit avant toute mutation
            if (Inventory.Remove(item, 1) != InventoryRefusal.None) return BoonRefusal.NothingThere;   // contrôlé plus haut : jamais
            replaced = _boons[(int)boon.Kind];
            _boons[(int)boon.Kind] = boon;
            return BoonRefusal.None;
        }

        /// <summary>Comme <see cref="UseBoon(string, out SavedBoon?)"/>, sans rendre le bonus remplacé.</summary>
        public BoonRefusal UseBoon(string item) => UseBoon(item, out _);

        /// <summary>Un tick de jeu passe pour chaque bonus actif ; rend ceux qui finissent à ce tick, dans l'ordre des places.</summary>
        private IReadOnlyList<BoonExpired> TickBoons()
        {
            List<BoonExpired>? expired = null;
            for (int k = 0; k < _boons.Length; k++)
            {
                if (!(_boons[k] is SavedBoon b)) continue;
                if (b.Ticks > 1)
                {
                    _boons[k] = b.WithTicks(b.Ticks - 1);
                    continue;
                }
                _boons[k] = null;
                (expired ??= new List<BoonExpired>()).Add(new BoonExpired(b.Kind, b.Item));
            }
            return expired ?? (IReadOnlyList<BoonExpired>)Array.Empty<BoonExpired>();
        }

        /// <summary>
        /// Bonus de l'équipement <paramref name="gear"/> plus celui des bonus actifs <paramref name="boons"/> (<see cref="ItemCatalog.BoonBonus"/>),
        /// chaque statistique ramenée à la limite du combat (<see cref="GearBonus.MaxValue"/> ; un catalogue validé ne l'atteint jamais : son
        /// pire cumul compte les trois places de bonus).
        /// </summary>
        public static GearBonus BoonGear(GearBonus gear, ItemCatalog items, IEnumerable<SavedBoon> boons)
        {
            if (gear is null) throw new ArgumentNullException(nameof(gear));
            if (items is null) throw new ArgumentNullException(nameof(items));
            GearBonus extra = items.BoonBonus(boons);
            var values = new int[GearBonus.Fields.Count];
            for (int i = 0; i < values.Length; i++) values[i] = (int)Math.Min((long)gear[i] + extra[i], GearBonus.MaxValue);
            return GearBonus.FromValues(values);
        }

        /// <summary>
        /// Bonus sauvegardés accordés au catalogue actuel (règle sûre de la relecture de la PR #78 : un réglage du catalogue ne rend jamais une
        /// partie illisible) : un bonus dont l'objet est absent du catalogue, n'est plus un objet de bonus ou n'est plus de cette place est
        /// retiré ; un temps au-delà de la durée actuelle de l'objet y est ramené. Chaque changement est dit dans <paramref name="notes"/>.
        /// </summary>
        private static SavedBoon?[] SettleBoons(IReadOnlyList<SavedBoon> saved, ItemCatalog items, out IReadOnlyList<string> notes)
        {
            var list = new List<string>();
            var boons = new SavedBoon?[BoonKinds.Count];
            foreach (SavedBoon b in saved)
            {
                if (!items.TryGetItem(b.Item, out ItemDefinition item) || item.Boon != b.Kind)
                {
                    list.Add($"bonus « {BoonKinds.Id(b.Kind)} » : « {b.Item} » n'est plus un objet de bonus de cette place, retiré.");
                    continue;
                }
                int full = items.BoonTicks(item);
                if (b.Ticks > full) list.Add($"bonus « {BoonKinds.Id(b.Kind)} » : {b.Ticks} ticks ramenés à la durée de « {b.Item} » ({full}).");
                boons[(int)b.Kind] = b.Ticks > full ? b.WithTicks(full) : b;
            }
            notes = list;
            return boons;
        }
    }
}

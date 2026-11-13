using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Crafting;
using EdS.Data.Items;
using EdS.Data.Merchants;
using EdS.Data.Saves;
using EdS.Simulation.Engine;

namespace EdS.World
{
    /// <summary>Sous-lot V1.4 : motif d'un refus de réparation (rien n'a changé : ni la bourse, ni l'équipement).</summary>
    public enum RepairRefusal
    {
        None = 0,

        /// <summary>Aucune pièce entamée, portée ou dans le sac.</summary>
        NothingToRepair = 1,

        /// <summary>Ni à un lieu de réparation (la forge), ni près d'un marchand qui répare (ou personnage absent, tombé, dans un intérieur).</summary>
        NotHere = 2,

        /// <summary>Le marchand demandé ne répare pas, ou sa boutique est fermée à ce moment de la journée.</summary>
        Closed = 3,

        /// <summary>Or insuffisant : la bourse ne couvre pas le prix.</summary>
        NotEnoughMoney = 4,

        /// <summary>En plein combat (<see cref="WorldSession.InCombat"/>) : on répare hors combat.</summary>
        InCombat = 5,
    }

    /// <summary>Sous-lot V1.4 : usure appliquée à la fin d'un combat : points perdus par chaque pièce portée, pièces arrivées à zéro.</summary>
    public sealed class WearReport
    {
        /// <summary>Dégâts reçus pendant le combat (PV perdus), ceux qui ont usé l'équipement.</summary>
        public long Damage { get; }

        /// <summary>Points de solidité perdus par chaque pièce portée qui s'use.</summary>
        public int Lost { get; }

        /// <summary>Objets portés arrivés à zéro par ce combat (ils ne donnent plus leurs bonus), dans l'ordre des places.</summary>
        public IReadOnlyList<string> WornOut { get; }

        internal WearReport(long damage, int lost, IReadOnlyList<string> wornOut)
        {
            Damage = damage;
            Lost = lost;
            WornOut = wornOut;
        }
    }

    /// <summary>
    /// Sous-lot V1.4 : forge, usure et réparation. <b>Usure</b> (plan des Vallons solo : « chaque combat use l'équipement porté selon les
    /// dégâts reçus ») : la partie relève les dégâts reçus par le personnage à chaque tick (compteur du moteur ; un soin ne les efface pas) ;
    /// à la fin du combat (le personnage n'est plus en combat, ou il est tombé), chaque pièce portée à l'entrée dans la zone (celles du
    /// combat, où qu'elles soient maintenant : portées ou dans le sac) qui s'use perd
    /// <c>dégâts × wear.perFullHealth / PV maximaux</c> points de solidité (catalogue des objets), arrondis vers le bas, le reste gardé pour
    /// le combat suivant de la session. L'usure se fait <b>hors du moteur de combat</b> : ni les règles ni les journaux ne changent, et elle
    /// prend effet à la prochaine entrée (le combat en cours garde ses bonus d'entrée, comme un équipement changé). Une sauvegarde prise en
    /// plein combat compte déjà les dégâts reçus. <b>Réparation</b> contre de l'or, à un lieu de réparation (la forge) ou chez un marchand
    /// qui répare (l'armurier) : toutes les pièces entamées, portées et dans le sac, d'un coup, en une opération atomique.
    /// </summary>
    public sealed partial class WorldSession
    {
        /// <summary>
        /// Dégâts reçus par le personnage au dernier relevé (compteur du moteur, <see cref="CombatantCounters.DamageTaken"/>) : les dégâts du
        /// combat se lisent par différence, sans qu'un soin du même tick en cache une partie (relecture indépendante de la PR #80).
        /// </summary>
        private long _lastTaken;

        /// <summary>
        /// Pièces portées à l'entrée dans la zone, celles du combat (le moteur garde l'équipement d'entrée) : c'est elles que l'usure use, où
        /// qu'elles soient maintenant (relecture indépendante de la PR #80 : changer d'équipement après l'entrée n'évite pas l'usure).
        /// Remarque de Codex sur la PR #83 : suivies par <b>identité</b> (la même instance, que l'inventaire déplace entre les places et le
        /// sac ; chaque usure ou réparation remplace l'instance suivie par la nouvelle), jamais par valeur : une copie identique n'est pas
        /// confondue avec la pièce du combat, et une pièce rangée dans le sac, réparée là puis remise, continue de s'user.
        /// </summary>
        private IReadOnlyList<ItemStack> _entryPieces = Array.Empty<ItemStack>();


        /// <summary>Dégâts reçus depuis la dernière usure appliquée (combat en cours).</summary>
        private long _pendingDamage;

        /// <summary>
        /// Reste de la division de l'usure (dégâts × perte, modulo les PV maximaux), gardé pour le combat suivant de la session. Il n'est
        /// pas sauvegardé. Relecture indépendante de la PR #83 : au début d'une session (−1), le premier point est arrondi vers le haut
        /// (reste de départ : le diviseur moins 1) ; ainsi un petit combat use toujours un peu, même si chaque halte ouvre une nouvelle
        /// session, et une session use exactement l'arrondi vers le haut de ses dégâts.
        /// </summary>
        private long _wearCarry = -1;

        /// <summary>
        /// Sous-lot V1.8 : reste de la division de l'usure propre à chaque pièce du combat (même ordre que <see cref="_entryPieces"/>), avec sa
        /// résistance : une pièce résistante perd exactement moins sur la durée, sans paliers d'arrondi. Pas sauvegardé, comme
        /// <see cref="_wearCarry"/> : absent (ou −1) au début d'une session, le premier point de chaque pièce est arrondi vers le haut.
        /// </summary>
        private long[] _pieceCarry = Array.Empty<long>();

        /// <summary>Sous-lot V1.4 : dégâts reçus dans le combat en cours, pas encore passés dans l'usure.</summary>
        public long PendingDamage => _pendingDamage;

        /// <summary>Relève la baisse des PV du personnage depuis le tick précédent ; applique l'usure à la fin du combat.</summary>
        private WearReport? TrackWear(bool defeated)
        {
            CombatantView me = Zone.Engine.View(CombatantId);
            long taken = me.Counters.DamageTaken;
            if (taken > _lastTaken) _pendingDamage = checked(_pendingDamage + (taken - _lastTaken));
            _lastTaken = Math.Max(_lastTaken, taken);
            if (_pendingDamage == 0 || (!defeated && !_defeated && InCombat)) return null;
            long damage = _pendingDamage;
            int lost = WearLoss(damage, _wearCarry, me.MaxHealth, out long carry);
            IReadOnlyList<int> losses = PieceLosses(damage, me.MaxHealth, _pieceCarry, out long[] carries);
            _pendingDamage = 0;
            _wearCarry = carry;
            _pieceCarry = carries;
            _entryPieces = Inventory.WearPieces(_entryPieces, losses, out IReadOnlyList<string> wornOut);
            return new WearReport(damage, lost, wornOut);
        }

        /// <summary>
        /// Sous-lot V1.8 (réponse de Damien : l'usure dépend du niveau et de la rareté de l'objet) : perte de chaque pièce du combat pour
        /// <paramref name="damage"/> dégâts reçus : <c>(dégâts × perFullHealth × 100 + reste) / (PV maximaux × (100 + résistance))</c>, arrondi
        /// vers le bas, au plus la solidité ; le reste de chaque pièce est gardé. Une pièce sans résistance perd comme avant. Arithmétique
        /// vérifiée.
        /// </summary>
        private IReadOnlyList<int> PieceLosses(long damage, long? maxHealth, long[] carry, out long[] newCarry)
        {
            int n = _entryPieces.Count;
            newCarry = new long[n];
            var losses = new int[n];
            if (damage <= 0 || maxHealth is not long max || max <= 0 || _items.WearPerFullHealth == 0)
            {
                for (int k = 0; k < n; k++) newCarry[k] = k < carry.Length ? carry[k] : -1;
                return losses;
            }
            for (int k = 0; k < n; k++)
            {
                long resist = _items.TryGetItem(_entryPieces[k].Item, out ItemDefinition item) ? 100L + _items.WearResistPercent(item) : 100L;
                long denominator = checked(max * resist);
                // Relecture de la PR #83 : au début de la session (reste absent ou −1), le premier point est arrondi vers le haut.
                long start = k < carry.Length && carry[k] >= 0 ? carry[k] : denominator - 1;
                long numerator = checked(damage * _items.WearPerFullHealth * 100 + start);
                newCarry[k] = numerator % denominator;
                losses[k] = (int)Math.Min(_items.WearMax, numerator / denominator);
            }
            return losses;
        }

        /// <summary>
        /// Points de solidité perdus pour <paramref name="damage"/> dégâts reçus (avec le reste <paramref name="carry"/> du combat précédent) :
        /// <c>(dégâts × perFullHealth + reste) / PV maximaux</c>, arrondi vers le bas, au plus la solidité ; le nouveau reste dans
        /// <paramref name="newCarry"/>. Sans PV maximaux connus, rien. Arithmétique vérifiée.
        /// </summary>
        private int WearLoss(long damage, long carry, long? maxHealth, out long newCarry)
        {
            newCarry = carry;
            if (damage <= 0 || maxHealth is not long max || max <= 0 || _items.WearPerFullHealth == 0) return 0;
            long numerator = checked(damage * _items.WearPerFullHealth + (carry >= 0 ? carry : max - 1));   // −1 : début de session, vers le haut
            newCarry = numerator % max;
            return (int)Math.Min(_items.WearMax, numerator / max);
        }

        /// <summary>
        /// Inventaire tel qu'il serait sauvegardé : l'usure des dégâts déjà reçus dans le combat en cours comptée (sur une copie ; la partie
        /// ne change pas).
        /// </summary>
        private Inventory InventoryToSave()
        {
            if (_pendingDamage == 0) return Inventory;
            Inventory copy = Inventory.Clone();
            copy.WearPieces(_entryPieces, PieceLosses(_pendingDamage, Zone.Engine.View(CombatantId).MaxHealth, _pieceCarry, out _), out _);
            return copy;
        }

        /// <summary>
        /// Sous-lot V1.4 : prix de la réparation de toutes les pièces entamées (portées et dans le sac), en cuivre : pour chacune, la part
        /// <see cref="RepairRules.Percent"/> de sa valeur (marchands ; 0 sans valeur) au prorata de la solidité qui manque, arrondie vers le
        /// haut, au moins <see cref="RepairRules.Minimum"/>. 0 s'il n'y a rien à réparer. Les dégâts du combat en cours ne comptent pas encore.
        /// </summary>
        /// <exception cref="InvalidOperationException">Partie sans métiers (règles de réparation).</exception>
        public long RepairPrice()
        {
            long total = 0;
            foreach ((string? _, int _, ItemStack stack) in Inventory.Damaged()) total = checked(total + RepairPrice(stack));
            return total;
        }

        /// <summary>Sous-lot V1.8 : prix de la réparation d'une pièce entamée (même règle que <see cref="RepairPrice()"/>, pour elle seule).</summary>
        public long RepairPrice(ItemStack stack)
        {
            if (stack is null) throw new ArgumentNullException(nameof(stack));
            RepairRules rules = (Workshop ?? throw new InvalidOperationException("Partie sans métiers.")).Book.Repair;
            if (stack.Wear is null) return 0;
            long value = Market != null && Market.Book.TryGetValue(stack.Item, out long v) ? v : 0;
            return rules.Price(value, _items.WearMax - _items.WearLeft(stack), _items.WearMax);
        }

        /// <summary>
        /// Sous-lot V1.8 (réponse de Damien du 31 octobre 2026 : réparer « tout d'un coup ou pièce par pièce ») : répare une seule pièce
        /// entamée, portée à la place <paramref name="slot"/> ou (slot null) dans le sac à la place <paramref name="place"/>, contre
        /// <see cref="RepairPrice(ItemStack)"/>, aux mêmes conditions que <see cref="Repair"/> (lieu, quelque chose à réparer là, bourse).
        /// Tout ou rien.
        /// </summary>
        public RepairRefusal RepairPiece(string? merchant, string? slot, int place, out long paid)
        {
            paid = 0;
            RepairRefusal here = CanRepairHere(merchant);
            if (here != RepairRefusal.None) return here;
            ItemStack? piece = slot != null
                ? (Inventory.Damaged().FirstOrDefault(d => d.Slot == slot).Stack)
                : place >= 0 && place < Inventory.Stacks.Count && Inventory.Stacks[place].Wear != null ? Inventory.Stacks[place] : null;
            if (piece is null) return RepairRefusal.NothingToRepair;
            long price = RepairPrice(piece);
            if (!Coins.TryAdd(Money, -price, out long left)) return RepairRefusal.NotEnoughMoney;
            ItemStack? before = Inventory.RepairOne(slot, place, out ItemStack? repaired);
            if (before is null || repaired is null) return RepairRefusal.NothingToRepair;
            // La pièce réparée, si elle est une des pièces du combat (portée ou rangée dans le sac depuis l'entrée), le reste : remarque de
            // Codex sur la PR #83, reconnue par son identité (pas par sa valeur : une copie identique n'est pas la pièce du combat).
            _entryPieces = Track(_entryPieces, new[] { (before, repaired) });
            _character = _character.WithMoney(left);
            paid = price;
            return RepairRefusal.None;
        }

        /// <summary>Remarque de Codex sur la PR #83 : remplace chaque pièce suivie par sa nouvelle instance (même identité seulement).</summary>
        private static IReadOnlyList<ItemStack> Track(IReadOnlyList<ItemStack> tracked, IEnumerable<(ItemStack Before, ItemStack After)> replaced)
        {
            var pieces = tracked.ToList();
            foreach ((ItemStack before, ItemStack after) in replaced)
                for (int i = 0; i < pieces.Count; i++)
                    if (ReferenceEquals(pieces[i], before)) pieces[i] = after;
            return pieces;
        }

        /// <summary>
        /// Sous-lot V1.4 : on peut réparer ici : hors combat ; chez le marchand <paramref name="merchant"/> s'il répare
        /// (<see cref="RepairRules.Merchants"/>) et que sa boutique est ouverte au personnage (<see cref="CanTrade"/>), ou, sans marchand
        /// (null), à un lieu de réparation (<see cref="RepairRules.Places"/> : la forge).
        /// </summary>
        public RepairRefusal CanRepairHere(string? merchant = null)
        {
            RepairRules rules = (Workshop ?? throw new InvalidOperationException("Partie sans métiers.")).Book.Repair;
            if (InCombat) return RepairRefusal.InCombat;
            if (merchant != null)
            {
                if (!rules.Merchants.Contains(merchant) || Market is null || Market.Book.Merchants.All(m => m.Id != merchant)) return RepairRefusal.Closed;
                TradeRefusal open = FullTrade(merchant);   // revue Codex de la PR #135 : pas de réparation à la porte
                return open == TradeRefusal.None ? RepairRefusal.None : open == TradeRefusal.Closed ? RepairRefusal.Closed : RepairRefusal.NotHere;
            }
            return rules.Places.Any(p => ((INarrativeWorld)this).InPlace(p)) ? RepairRefusal.None : RepairRefusal.NotHere;
        }

        /// <summary>
        /// Sous-lot V1.4 : répare toutes les pièces entamées (portées et dans le sac) contre <see cref="RepairPrice"/>, chez
        /// <paramref name="merchant"/> ou (null) à la forge : lieu (<see cref="CanRepairHere"/>), quelque chose à réparer, bourse suffisante.
        /// Tout ou rien : la bourse et l'équipement changent ensemble. <paramref name="paid"/> rend le prix payé (0 en cas de refus). Une
        /// pièce réparée retrouve ses bonus à la prochaine entrée dans une zone.
        /// </summary>
        public RepairRefusal Repair(string? merchant, out long paid)
        {
            paid = 0;
            RepairRefusal here = CanRepairHere(merchant);
            if (here != RepairRefusal.None) return here;
            if (Inventory.Damaged().Count == 0) return RepairRefusal.NothingToRepair;
            long price = RepairPrice();
            if (!Coins.TryAdd(Money, -price, out long left)) return RepairRefusal.NotEnoughMoney;
            // Réparées, ce sont toujours les pièces du combat (remarque de Codex sur la PR #83 : suivies par identité).
            _entryPieces = Track(_entryPieces, Inventory.RepairAll());
            _character = _character.WithMoney(left);
            paid = price;
            return RepairRefusal.None;
        }

        /// <summary>Contrôle fait avant l'ouverture de la zone : les marchands qui réparent sont des marchands chargés.</summary>
        private static void CheckRepairMerchants(CraftingBook? crafting, Market? market)
        {
            if (crafting is null || market is null) return;
            foreach (string merchant in crafting.Repair.Merchants)
                if (market.Book.Merchants.All(m => m.Id != merchant))
                    throw new ArgumentException($"Marchand de réparation « {merchant} » absent des marchands.", nameof(crafting));
        }
    }
}

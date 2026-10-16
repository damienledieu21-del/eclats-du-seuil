using System;

namespace EdS.Simulation.Engine
{
    /// <summary>
    /// Maillon de la chaîne causale (lot 7.3) : identité d'un déclencheur traversé et maillon parent. Liste chaînée immuable :
    /// étendre une chaîne ne modifie jamais celle d'une branche sœur (P7, C7). Seule une réaction l'étend (P2), donc sa longueur
    /// est au plus la profondeur de l'effet, elle-même bornée par <see cref="MaxLength"/>.
    /// </summary>
    internal sealed class TriggerChain
    {
        /// <summary>Plafond absolu (<c>CombatRules.AbsoluteMaxTriggerDepth</c>) : une chaîne plus longue est une erreur de programmation.</summary>
        public const int MaxLength = 8;

        public string Trigger { get; }
        public TriggerChain? Parent { get; }
        public int Length { get; }

        private TriggerChain(string trigger, TriggerChain? parent)
        {
            Trigger = trigger;
            Parent = parent;
            Length = (parent?.Length ?? 0) + 1;
        }

        /// <summary>Nouvelle chaîne <c>chain ∪ {trigger}</c> ; <paramref name="chain"/> reste inchangée.</summary>
        public static TriggerChain Extend(TriggerChain? chain, string trigger)
        {
            if (trigger is null) throw new ArgumentNullException(nameof(trigger));
            if (chain != null && chain.Length >= MaxLength)
                throw new InvalidOperationException($"Chaîne causale au-delà de {MaxLength} déclencheurs.");
            return new TriggerChain(trigger, chain);
        }

        /// <summary>Appartenance : au plus <see cref="MaxLength"/> comparaisons ordinales, sans table de hachage.</summary>
        public static bool Contains(TriggerChain? chain, string trigger)
        {
            for (TriggerChain? link = chain; link != null; link = link.Parent)
                if (string.Equals(link.Trigger, trigger, StringComparison.Ordinal)) return true;
            return false;
        }
    }

    /// <summary>
    /// Provenance d'un effet de jeu (lot 7.3, plan §3) : racine, profondeur, chaîne des déclencheurs traversés, dernier déclencheur,
    /// effet parent (<see cref="Cause"/>, eid), combattant crédité et, pour un effet programmé pour le compte d'une altération,
    /// l'instance et la génération qui servent à l'annulation. Immuable : une conséquence directe reçoit une copie dont seule la
    /// cause change (<see cref="CausedBy"/>), une réaction une copie étendue (<see cref="ReactionTo"/>).
    /// </summary>
    internal sealed class Provenance
    {
        public string Root { get; }
        public int Depth { get; }
        public TriggerChain? Chain { get; }
        public string? Trigger { get; }

        /// <summary>eid de l'effet parent immédiat ; null pour l'effet racine.</summary>
        public long? Cause { get; }

        public string? Source { get; }
        public long? SourceInstance { get; }
        public int? SourceGeneration { get; }

        private Provenance(string root, int depth, TriggerChain? chain, string? trigger, long? cause, string? source,
            long? sourceInstance, int? sourceGeneration)
        {
            Root = root;
            Depth = depth;
            Chain = chain;
            Trigger = trigger;
            Cause = cause;
            Source = source;
            SourceInstance = sourceInstance;
            SourceGeneration = sourceGeneration;
        }

        /// <summary>Entrée du scénario (§2.1) : nouvelle racine, profondeur 0, chaîne vide ; son premier effet a <c>cause: null</c>.</summary>
        public static Provenance NewRoot(string root, string? source) =>
            new Provenance(root ?? throw new ArgumentNullException(nameof(root)), 0, null, null, null, source, null, null);

        /// <summary>Conséquence directe de l'effet <paramref name="eid"/> (P3) : tout est hérité, seule la cause change.</summary>
        public Provenance CausedBy(long eid) => new Provenance(Root, Depth, Chain, Trigger, eid, Source, SourceInstance, SourceGeneration);

        /// <summary>Même provenance, autre combattant crédité (auteur de l'effet).</summary>
        public Provenance WithSource(string? source) => new Provenance(Root, Depth, Chain, Trigger, Cause, source, SourceInstance, SourceGeneration);

        /// <summary>Effet programmé pour le compte d'une instance d'altération (échéance, tick de DOT, état accordé).</summary>
        public Provenance ForInstance(long instance, int generation) => new Provenance(Root, Depth, Chain, Trigger, Cause, Source, instance, generation);

        /// <summary>
        /// Réaction du déclencheur <paramref name="trigger"/> (porté par <paramref name="bearer"/>) à l'effet <paramref name="eid"/>
        /// de cette provenance : même racine, profondeur + 1, chaîne étendue, cause = effet observé, source = porteur (§3.2).
        /// </summary>
        public Provenance ReactionTo(long eid, string trigger, string bearer, long? sourceInstance = null, int? sourceGeneration = null) =>
            new Provenance(Root, checked(Depth + 1), TriggerChain.Extend(Chain, trigger), trigger, eid, bearer, sourceInstance, sourceGeneration);
    }
}

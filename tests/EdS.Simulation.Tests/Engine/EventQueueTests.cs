using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using FsCheck.Xunit;

namespace EdS.Simulation.Tests.Engine;

[Trait("Category", "Engine")]
public class EventQueueTests
{
    [Fact]
    public void Ordre_tick_puis_phase_puis_insertion()
    {
        var queue = new EventQueue<string>();
        queue.Enqueue(2, CombatPhase.Expirations, "t2-expiration");
        queue.Enqueue(1, CombatPhase.Commands, "t1-commande-a");
        queue.Enqueue(1, CombatPhase.Expirations, "t1-expiration");
        queue.Enqueue(1, CombatPhase.Commands, "t1-commande-b");
        queue.Enqueue(0, CombatPhase.EndOfTick, "t0-fin");

        var order = new List<string>();
        while (queue.Count > 0) order.Add(queue.Dequeue());
        Assert.Equal(new[] { "t0-fin", "t1-expiration", "t1-commande-a", "t1-commande-b", "t2-expiration" }, order);
    }

    [Fact]
    public void Retrait_uniquement_au_tick_et_a_la_phase_demandes()
    {
        var queue = new EventQueue<string>();
        queue.Enqueue(5, CombatPhase.Commands, "x");
        Assert.False(queue.TryDequeue(4, CombatPhase.Commands, out _));
        Assert.False(queue.TryDequeue(5, CombatPhase.Expirations, out _));
        Assert.True(queue.TryDequeue(5, CombatPhase.Commands, out string item));
        Assert.Equal("x", item);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void Tick_negatif_refuse() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventQueue<int>().Enqueue(-1, CombatPhase.Commands, 0));

    [Property(MaxTest = 500)]
    public bool Sortie_identique_a_un_tri_stable(int[] raw)
    {
        // Beaucoup de doublons (tick et phase) pour éprouver le départage par ordre d'insertion.
        var queue = new EventQueue<int>();
        var items = raw.Select((r, i) => (Tick: (long)Math.Abs(r % 4), Phase: (CombatPhase)(1 + Math.Abs(r / 4 % 6)), Index: i)).ToList();
        foreach (var item in items) queue.Enqueue(item.Tick, item.Phase, item.Index);
        var expected = items.OrderBy(i => i.Tick).ThenBy(i => i.Phase).ThenBy(i => i.Index).Select(i => i.Index);
        var actual = new List<int>();
        while (queue.Count > 0) actual.Add(queue.Dequeue());
        return expected.SequenceEqual(actual);
    }
}

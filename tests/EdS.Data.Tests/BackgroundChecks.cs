using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;

namespace EdS.Data.Tests;

/// <summary>
/// Tests plus rapides (décision de Damien du 29 octobre 2026 : « mêmes tests, même nombre, mêmes résultats ; on change seulement la façon
/// de les faire tourner ») : contrôles indépendants d'un test, exécutés sur des fils de fond pendant que le fil du test continue. Chaque
/// contrôle confié est exécuté une fois, en entier ; quand la file est pleine, le fil du test l'exécute lui-même (aucun contrôle n'est
/// abandonné). Un contrôle qui échoue fait échouer le test avec son exception d'origine (entre plusieurs échecs déjà constatés, celui
/// au plus petit numéro d'ordre) : à l'appel suivant de <see cref="Run"/>, au plus tard à <see cref="Wait"/>, qui attend la fin de tous les
/// contrôles et vérifie qu'ils ont tous été menés au bout.
/// Les contrôles ne doivent rien partager de modifiable avec le fil du test (le moteur et les définitions le garantissent : aucun état
/// global modifiable, définitions immuables).
/// </summary>
internal sealed class BackgroundChecks : IDisposable
{
    private readonly BlockingCollection<(long Order, Action Check)> _queue;
    private readonly Thread[] _workers;
    private readonly object _gate = new();
    private (long Order, ExceptionDispatchInfo Failure)? _failure;
    private long _done;
    private long _given;
    private volatile bool _stopping;

    /// <summary>Un fil de fond par cœur au-delà du premier (au moins un, au plus quatre), file bornée à deux contrôles par fil.</summary>
    public BackgroundChecks() : this(Math.Clamp(Environment.ProcessorCount - 1, 1, 4)) { }

    public BackgroundChecks(int workers)
    {
        if (workers < 1) throw new ArgumentOutOfRangeException(nameof(workers));
        _queue = new BlockingCollection<(long, Action)>(new ConcurrentQueue<(long, Action)>(), 2 * workers);
        _workers = new Thread[workers];
        for (int i = 0; i < workers; i++)
        {
            _workers[i] = new Thread(Work) { IsBackground = true, Name = "contrôles de fond " + i };
            _workers[i].Start();
        }
    }

    /// <summary>Nombre de contrôles terminés sans erreur (fils de fond et fil du test).</summary>
    public long Done => Interlocked.Read(ref _done);

    /// <summary>
    /// Confie <paramref name="check"/> (numéro d'ordre <paramref name="order"/>, par exemple le tick) à un fil de fond, ou l'exécute sur le fil
    /// appelant si la file est pleine. Relance d'abord l'échec d'un contrôle précédent.
    /// </summary>
    public void Run(long order, Action check)
    {
        ThrowIfFailed();
        _given++;
        if (_queue.TryAdd((order, check))) return;
        check();
        Interlocked.Increment(ref _done);
    }

    /// <summary>Attend la fin de tous les contrôles confiés et relance l'échec du premier (dans l'ordre) qui a échoué.</summary>
    public void Wait()
    {
        _queue.CompleteAdding();
        foreach (Thread worker in _workers) worker.Join();
        ThrowIfFailed();
        if (Done != _given) throw new InvalidOperationException($"{_given} contrôle(s) confié(s), {Done} terminé(s).");
    }

    private void ThrowIfFailed()
    {
        lock (_gate) _failure?.Failure.Throw();
    }

    private void Work()
    {
        foreach ((long order, Action check) in _queue.GetConsumingEnumerable())
        {
            if (_stopping) continue;
            try
            {
                check();
                Interlocked.Increment(ref _done);
            }
            catch (Exception e)
            {
                lock (_gate)
                    if (_failure is null || order < _failure.Value.Order) _failure = (order, ExceptionDispatchInfo.Capture(e));
            }
        }
    }

    /// <summary>Arrête les fils de fond (contrôles en attente abandonnés) : seulement quand le test a déjà échoué ou après <see cref="Wait"/>.</summary>
    public void Dispose()
    {
        _stopping = true;
        if (!_queue.IsAddingCompleted) _queue.CompleteAdding();
        foreach (Thread worker in _workers) worker.Join();
        _queue.Dispose();
    }
}

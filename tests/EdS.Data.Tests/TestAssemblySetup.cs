using System.Runtime.CompilerServices;
using System.Threading;

namespace EdS.Data.Tests;

/// <summary>
/// V2.56 (CI fiable) : réglages du processus de test, exécutés une fois au chargement de l'assembly.
/// </summary>
internal static class TestAssemblySetup
{
    [ModuleInitializer]
    internal static void RaiseThreadPoolFloor()
    {
        // Les tests TCP s'appuient sur des tâches (connexions, continuations) avec des échéances réelles de 10 à 60 s. Le pool de fils ne
        // crée au-delà de son plancher (un fil par cœur : 2 sur la CI) qu'environ un fil par seconde ; quand plusieurs tests parallèles
        // occupent les fils, une tâche attend alors plusieurs secondes. Un plancher plus haut évite cette file d'attente, sans toucher
        // à ce que les tests vérifient.
        ThreadPool.GetMinThreads(out int worker, out int io);
        ThreadPool.SetMinThreads(System.Math.Max(worker, 32), System.Math.Max(io, 32));
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Saves;
using EdS.Data.Story;

namespace EdS.World
{
    /// <summary>
    /// Sous-lot V1.9.5 (choix de Damien du 5 novembre 2026 : « au moment du chapitre 5, avec la conversion des sauvegardes ») : la fin de
    /// l'ancienne mission (étapes <c>manifestation</c>, <c>rodeur</c>, <c>chef</c>, <c>retour</c> de <see cref="Chapter1"/>) passe à la quête
    /// du chapitre 5 (<see cref="Chapter5"/>), sous les mêmes identifiants d'étape. La structure de la sauvegarde ne change pas (toujours la
    /// version 12) : l'avancement des quêtes est converti <b>à la lecture</b>, contre l'histoire chargée, avant son contrôle.
    /// <list type="bullet">
    /// <item>le chapitre 1 arrêté à l'une des quatre étapes : il est fini, et le chapitre 5 reprend à cette étape, compteur compris ;</item>
    /// <item>la mission finie (le fait <see cref="FinishedFact"/> appris, qu'Odile pose à la dernière étape) : les deux chapitres sont finis ;</item>
    /// <item>sinon (chapitre 1 avant ces étapes, ou fini chez Gaspard), rien ne change.</item>
    /// </list>
    /// Aucun effet n'est rejoué : ni expérience, ni pièces, ni objets (les récompenses déjà reçues ne sont jamais redonnées). La quête suivie
    /// suit l'étape. La conversion ne s'applique qu'à une histoire où ces étapes sont au chapitre 5 et plus au chapitre 1 ; une partie déjà
    /// convertie, ou qui a déjà le chapitre 5, est rendue telle quelle (la conversion est idempotente). L'écriture suivante donne l'état converti.
    /// </summary>
    public static class StoryConversions
    {
        public const string Chapter1 = "quest.traces_sous_le_moulin", Chapter5 = "quest.le_seuil", FinishedFact = "fact.odile.retour";

        /// <summary>Étapes passées du chapitre 1 au chapitre 5, dans leur ordre.</summary>
        public static readonly IReadOnlyList<string> MovedSteps = Array.AsReadOnly(new[] { "manifestation", "rodeur", "chef", "retour" });

        /// <summary>L'histoire <paramref name="book"/> a les étapes au chapitre 5 (et plus au chapitre 1) : la conversion s'y applique.</summary>
        public static bool Applies(StoryBook book)
        {
            if (book is null) throw new ArgumentNullException(nameof(book));
            return book.HasQuest(Chapter1) && book.HasQuest(Chapter5)
                && MovedSteps.All(s => book.Quest(Chapter1).StepIndex(s) < 0 && book.Quest(Chapter5).StepIndex(s) >= 0);
        }

        /// <summary>L'état narratif <paramref name="saved"/> converti pour l'histoire <paramref name="book"/> (le même objet s'il n'y a rien à faire).</summary>
        public static SavedStory Upgrade(StoryBook book, SavedStory saved)
        {
            if (saved is null) throw new ArgumentNullException(nameof(saved));
            if (!Applies(book) || !saved.Quests.TryGetValue(Chapter1, out QuestProgress old) || saved.Quests.ContainsKey(Chapter5)) return saved;
            QuestProgress moved;
            if (!old.IsDone && !old.IsChoosingReward && MovedSteps.Contains(old.Step)) moved = old;
            else if (old.IsDone && saved.Facts.Contains(FinishedFact)) moved = new QuestProgress(QuestProgress.Done);
            else return saved;
            var quests = saved.Quests.ToDictionary(q => q.Key, q => q.Value, StringComparer.Ordinal);
            quests[Chapter1] = new QuestProgress(QuestProgress.Done);
            quests[Chapter5] = moved;
            // Une quête suivie au chapitre 1 était en cours, donc à l'une des étapes passées : le suivi passe au chapitre 5.
            string? tracked = saved.Tracked == Chapter1 ? (moved.IsDone ? null : Chapter5) : saved.Tracked;
            return new SavedStory(saved.Facts, quests, saved.Trust, saved.Discoveries, tracked, saved.Guild, saved.Hunted);
        }
    }
}

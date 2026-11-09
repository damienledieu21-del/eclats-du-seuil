using EdS.Data.Saves;

namespace EdS.Runtime
{
    public sealed partial class AdventureSession
    {
        /// <summary>Sous-lot V2.16 : aspect du héros choisi dans le créateur de personnage, ou null (aspect par défaut).</summary>
        public SavedLook? HeroLook => World.Look;

        /// <summary>
        /// Sous-lot V2.16 : enregistre l'aspect choisi pour le héros ; il part avec la prochaine sauvegarde (écriture immédiate : le choix ne doit
        /// pas se perdre si le jeu s'arrête). Sans effet si l'aspect ne change pas. Atomique : si l'écriture échoue (disque plein, accès refusé :
        /// voir <see cref="Save"/>), l'ancien aspect est remis et l'exception remonte ; une nouvelle tentative avec le même aspect écrit alors bien.
        /// </summary>
        public void ChooseLook(SavedLook? look)
        {
            if (Equals(World.Look, look)) return;
            SavedLook? before = World.Look;
            World.SetLook(look);
            try
            {
                Save();
            }
            catch
            {
                World.SetLook(before);
                throw;
            }
        }
    }
}

using EdS.Data.Saves;

namespace EdS.World
{
    public sealed partial class WorldSession
    {
        /// <summary>Sous-lot V2.16 : aspect du héros choisi dans le créateur de personnage (sauvegarde v17), ou null : l'aspect par défaut du jeu.</summary>
        public SavedLook? Look => _character.Look;

        /// <summary>Sous-lot V2.16 : change l'aspect du héros (null : retour à l'aspect par défaut). N'influence ni le combat ni l'histoire.</summary>
        public void SetLook(SavedLook? look) => _character = _character.WithLook(look);
    }
}

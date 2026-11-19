using System.Collections.Generic;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot F4 (les 12 fêtes d'Orsédia dans le jeu) : l'étal de fête de Maître Florentin sur la place, pendant chaque fête : la tente ou la
    /// table du marché (packs de l'Asset Store) devant lui, habillée des décorations Tripo de la fête du mois (<see cref="FeastLooks.Stall"/> :
    /// places libres, jamais dans un mur ni sur l'étal). Refait quand la fête change, retiré après elle ; affichage seulement, sans collision.
    /// </summary>
    public sealed partial class WorldMarkers
    {
        private List<IReadOnlyList<(double X, double Z)>> _feastObstacles;
        private GameObject _feastStall;
        private string _feastStallKey;

        private void SyncFeastStall(AdventureSession adventure)
        {
            FeastStallLook look = adventure.FeastStallLook;
            string key = look != null ? look.Key : null;
            if (key == _feastStallKey) return;
            _feastStallKey = key;
            if (_feastStall != null) Object.Destroy(_feastStall);
            _feastStall = null;
            if (look == null || _root == null) return;
            _feastStall = new GameObject("Étal de fête (F4) " + look.Festival);
            Transform holder = _feastStall.transform;
            holder.SetParent(_root.transform, false);
            string merchant = adventure.World.FeastBook != null ? adventure.World.FeastBook.Merchant : null;
            float face = merchant != null && _npcs.TryGetValue(merchant, out Marker m) ? m.RestYaw : 0f;
            var layout = FeastLooks.Stall(look.X, look.Z, face, look.Decor, _feastObstacles ?? new List<IReadOnlyList<(double X, double Z)>>());
            Vector3 at = StyleWorld.OnGround(new Vector3((float)layout.X, 0f, (float)layout.Z));
            // Comme les étals du marché : la tente sur ordinateur, sinon la table ; le forain se tient derrière, face aux visiteurs.
            GameObject stall = Wave5View.Has(Wave5Role.TraderTent) ? Wave5View.Place(holder, Wave5Role.TraderTent, "étal du forain (tente)", at, face + 180f) : null;
            if (stall == null && Wave5View.Has(Wave5Role.TraderTable)) Wave5View.Place(holder, Wave5Role.TraderTable, "étal du forain", at, face + 180f);
            foreach (FeastPiece piece in layout.Pieces)
                LotCLook.PlaceFeast(holder, piece.Code, "décor de fête " + piece.Code, StyleWorld.OnGround(new Vector3((float)piece.X, 0f, (float)piece.Z)), (float)piece.Yaw);
        }
    }
}

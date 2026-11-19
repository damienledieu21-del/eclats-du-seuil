using System.Collections.Generic;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot F4 (les 12 fêtes d'Orsédia dans le jeu) : l'étal de fête de Maître Florentin sur la place, pendant chaque fête : la tente ou la
    /// table du marché (packs de l'Asset Store) devant lui, habillée des décorations Tripo de la fête du mois (<see cref="FeastLooks.Stall"/> :
    /// places libres, jamais dans un mur ni sur l'étal), et (sous-lot F5) le décor du mois sur les places. Refait quand la fête change, retiré
    /// après elle ; affichage seulement, sans collision.
    /// </summary>
    public sealed partial class WorldMarkers
    {
        private GameObject _feastStall;
        private string _feastStallKey;

        private void SyncFeastStall(AdventureSession adventure)
        {
            // Relecture indépendante de la PR #218 : jamais en intérieur (le cœur refuse aussi hors de la zone de l'histoire).
            FeastStallLook look = adventure.Indoors ? null : adventure.FeastStallLook;
            string key = look != null ? look.Key : null;
            if (key == _feastStallKey) return;
            _feastStallKey = key;
            if (_feastStall != null) Object.Destroy(_feastStall);
            _feastStall = null;
            if (look == null || _root == null) return;
            _feastStall = new GameObject("Étal de fête (F4) " + look.Festival);
            Transform holder = _feastStall.transform;
            holder.SetParent(_root.transform, false);
            // Relecture indépendante de la PR #218 : la pose (étal hors obstacles, rues, PNJ, portes) est calculée par le cœur, une fois par fête.
            var found = adventure.FeastStallLayout;
            if (found == null) return;
            var layout = found.Value;
            Vector3 at = StyleWorld.OnGround(new Vector3((float)layout.X, 0f, (float)layout.Z));
            // Comme les étals du marché : la tente sur ordinateur, sinon la table ; le forain se tient derrière, face aux visiteurs.
            GameObject stall = Wave5View.Has(Wave5Role.TraderTent) ? Wave5View.Place(holder, Wave5Role.TraderTent, "étal du forain (tente)", at, (float)layout.Yaw + 180f) : null;
            if (stall == null && Wave5View.Has(Wave5Role.TraderTable)) Wave5View.Place(holder, Wave5Role.TraderTable, "étal du forain", at, (float)layout.Yaw + 180f);
            foreach (FeastPiece piece in layout.Pieces)
                LotCLook.PlaceFeast(holder, piece.Code, "décor de fête " + piece.Code, StyleWorld.OnGround(new Vector3((float)piece.X, 0f, (float)piece.Z)), (float)piece.Yaw);
            // Sous-lot F5 (décor du mois) : les cinq décorations de la fête sur la place du village et la place du marché du Bourg, à leurs
            // places validées (AdventureSession.FeastDecorPieces : hors obstacles, routes, portes, PNJ, étal) ; retirées avec l'étal après la fête.
            int rank = 0;
            foreach (FeastPiece piece in adventure.FeastDecorPieces)
                LotCLook.PlaceFeast(holder, piece.Code, "décor du mois " + rank++ + " " + piece.Code, StyleWorld.OnGround(new Vector3((float)piece.X, 0f, (float)piece.Z)), (float)piece.Yaw);
        }
    }
}

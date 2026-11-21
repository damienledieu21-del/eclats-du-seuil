using System.Collections.Generic;
using EdS.Data.Seasons;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.44.4 (vendanges, décisions de Damien du 20 novembre 2026) : les rangs de vigne des coteaux du château, en trois états
    /// (<see cref="VineLook"/>) : vert, mûr avec ses grappes (modèle C_RE_S06 du lot C, sinon des grains faits par le code), vendangé
    /// (feuillage d'automne clairsemé, puis cep nu l'hiver). Piquets et fil toujours là. Pendant les vendanges, la hotte et le panier du lot C
    /// (C_RE_S07, C_RE_S08) attendent au bout des rangs. Formes faites par le code en attendant les modèles des rangs (Tripo). Affichage
    /// seulement : le rang reste un obstacle bas du moteur.
    /// </summary>
    public sealed partial class SpaceView
    {
        private sealed class VineRow
        {
            public GameObject Leaves;
            public Vector3 LeafScale;
            public GameObject Grapes;
            public GameObject Trellis;   // piquets et fil (seuls l'hiver quand les modèles sont là)
            public GameObject Models;    // modules du modèle de l'état montré
            public GameObject Row;
            public string Id;
            public float X0, Z0, X1, Z1;
        }

        private readonly List<VineRow> _vineRows = new List<VineRow>();
        private Material _vineLeaves, _vineWood, _vineGrape;
        private VineRowLook? _vineShown;
        private GameObject _vintage;   // hotte et panier, posés une fois, montrés pendant les vendanges

        /// <summary>Un rang de vigne : piquets, fil, feuillage et grappes (cachées hors des vendanges).</summary>
        private void VineRowShape(string id, List<Vector3> points)
        {
            if (_vineWood == null)
            {
                _vineWood = ArenaView.MakeMaterial(new Color(0.36f, 0.26f, 0.16f));
                _vineLeaves = ArenaView.MakeMaterial(new Color(0.27f, 0.47f, 0.17f));
                _vineGrape = ArenaView.MakeMaterial(new Color(0.24f, 0.08f, 0.22f));
            }
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            foreach (Vector3 p in points)
            {
                x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); z0 = Mathf.Min(z0, p.z); z1 = Mathf.Max(z1, p.z);
            }
            var row = new GameObject(id + " (rang de vigne)");
            row.transform.SetParent(_root.transform, false);
            var trellis = new GameObject("Palissage");
            trellis.transform.SetParent(row.transform, false);
            bool alongX = x1 - x0 >= z1 - z0;
            float length = alongX ? x1 - x0 : z1 - z0;
            Vector3 center = new Vector3((x0 + x1) / 2f, 0f, (z0 + z1) / 2f);
            float ground = StyleWorld.StandHeight(center.x, center.z);
            // Piquets tous les 5 m et fil de palissage.
            foreach ((double x, double z) in VineLook.Along(x0, z0, x1, z1, VineLook.StakeSpacing))
            {
                GameObject stake = Primitive(PrimitiveType.Cylinder, trellis.transform, _vineWood);
                float h = StyleWorld.StandHeight((float)x, (float)z);
                stake.transform.position = new Vector3((float)x, h + 0.65f, (float)z);
                stake.transform.localScale = new Vector3(0.07f, 0.65f, 0.07f);
            }
            GameObject wire = Primitive(PrimitiveType.Cube, trellis.transform, _vineWood);
            wire.transform.position = new Vector3(center.x, ground + 1.05f, center.z);
            wire.transform.localScale = alongX ? new Vector3(length, 0.02f, 0.02f) : new Vector3(0.02f, 0.02f, length);
            // Feuillage : une haie basse le long du fil.
            GameObject leaves = Primitive(PrimitiveType.Capsule, row.transform, _vineLeaves);
            leaves.transform.position = new Vector3(center.x, ground + 0.95f, center.z);
            leaves.transform.rotation = Quaternion.Euler(0f, 0f, alongX ? 90f : 0f) * (alongX ? Quaternion.identity : Quaternion.Euler(90f, 0f, 0f));
            Vector3 scale = new Vector3(0.75f, length / 2f, 0.6f);
            leaves.transform.localScale = scale;
            // Grappes : construites au premier besoin (Grapes), jamais quand les modèles Tripo sont là.
            var models = new GameObject("Modules");
            models.transform.SetParent(row.transform, false);
            _vineRows.Add(new VineRow { Leaves = leaves, LeafScale = scale, Trellis = trellis, Models = models, Row = row, Id = id, X0 = x0, Z0 = z0, X1 = x1, Z1 = z1 });
        }

        /// <summary>Grappes du rang tous les 2,5 m, de chaque côté en quinconce (relecture de la PR #233 : faites au premier besoin).</summary>
        private void BuildGrapes(VineRow row, string code)
        {
            var grapes = new GameObject("Grappes");
            grapes.transform.SetParent(row.Row.transform, false);
            bool alongX = row.X1 - row.X0 >= row.Z1 - row.Z0;
            int i = 0;
            foreach ((double x, double z) in VineLook.Along(row.X0, row.Z0, row.X1, row.Z1, VineLook.GrapeSpacing))
            {
                float side = (i++ % 2 == 0 ? 0.32f : -0.32f);
                Vector3 at = new Vector3((float)x + (alongX ? 0f : side), 0f, (float)z + (alongX ? side : 0f));
                at.y = StyleWorld.StandHeight(at.x, at.z) + 0.7f;
                if (code == null || LotCLook.Place(grapes.transform, code, row.Id + "/grappe" + i, at, 0f, 1.2f) == null)
                {
                    GameObject berry = Primitive(PrimitiveType.Sphere, grapes.transform, _vineGrape);
                    berry.transform.position = at;
                    berry.transform.localScale = new Vector3(0.14f, 0.2f, 0.14f);
                }
            }
            row.Grapes = grapes;
        }

        /// <summary>Relecture de la PR #233 : matériaux de la vigne détruits avec le décor, état remis à zéro.</summary>
        private void ReleaseVines()
        {
            if (_vineWood != null) Object.Destroy(_vineWood);
            if (_vineLeaves != null) Object.Destroy(_vineLeaves);
            if (_vineGrape != null) Object.Destroy(_vineGrape);
            _vineWood = _vineLeaves = _vineGrape = null;
            _vineRows.Clear();
            _vineShown = null;
            _vintage = null;
        }

        private static GameObject Primitive(PrimitiveType type, Transform parent, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>Chaque image : l'état de la vigne (changement seulement) et les objets de vendange.</summary>
        public void Vines(VineState? state, int season)
        {
            if (_vineRows.Count == 0) return;
            VineRowLook look = VineLook.Look(state, season);
            if (_vineShown is VineRowLook shown && shown.Equals(look)) return;
            _vineShown = look;
            var color = new Color(look.R, look.G, look.B);
            _vineLeaves.SetColor("_BaseColor", color);
            _vineLeaves.SetColor("_Color", color);
            // Modèles Tripo des rangs (Decor/vendanges) : modules de l'état montré ; relecture de la PR #233 : sans le modèle de la pièce
            // demandée, retour aux formes faites par le code (l'hiver, cep nu : aucune pièce, piquets et fil seuls si les modèles sont là).
            StyleCatalog catalog = StyleCatalog.Loaded;
            string piece = VineLook.ModelPiece(state, season);
            GameObject prefab = catalog != null && piece != null ? catalog.OwnDecorPiece(piece) : null;
            bool modeled = prefab != null || (piece == null && catalog != null && catalog.OwnDecorPiece("rang_vigne_vert") != null);
            string grapeCode = look.Grapes && !modeled ? LotCModels.ForNode("raisin_vignes_ouest") : null;
            foreach (VineRow row in _vineRows)
            {
                if (row.Models == null) continue;
                for (int c = row.Models.transform.childCount - 1; c >= 0; c--) Object.Destroy(row.Models.transform.GetChild(c).gameObject);
                if (modeled)
                {
                    row.Leaves.SetActive(false);
                    if (row.Grapes != null) row.Grapes.SetActive(false);
                    row.Trellis.SetActive(prefab == null);
                    if (prefab != null) Modules(row, prefab, piece);
                    continue;
                }
                row.Trellis.SetActive(true);
                if (row.Leaves != null)
                {
                    row.Leaves.SetActive(look.Leaves > 0f);
                    row.Leaves.transform.localScale = new Vector3(row.LeafScale.x * Mathf.Max(0.3f, look.Leaves), row.LeafScale.y, row.LeafScale.z * Mathf.Max(0.3f, look.Leaves));
                }
                if (look.Grapes && row.Grapes == null) BuildGrapes(row, grapeCode);
                if (row.Grapes != null) row.Grapes.SetActive(look.Grapes);
            }
            Vintage(VineLook.PropsShown(state));
        }

        /// <summary>Modules du modèle <paramref name="prefab"/> le long du rang, étirés à sa longueur et à sa largeur, posés sur le sol.</summary>
        private static void Modules(VineRow row, GameObject prefab, string piece)
        {
            bool alongX = row.X1 - row.X0 >= row.Z1 - row.Z0;
            IReadOnlyList<(double X, double Z)> spots = VineLook.Along(row.X0, row.Z0, row.X1, row.Z1, VineLook.ModuleLength);
            for (int i = 0; i + 1 < spots.Count; i++)
            {
                float x = (float)((spots[i].X + spots[i + 1].X) / 2), z = (float)((spots[i].Z + spots[i + 1].Z) / 2);
                double length = alongX ? spots[i + 1].X - spots[i].X : spots[i + 1].Z - spots[i].Z;
                (double sx, double sy, double sz) = VineLook.ModuleScale(piece, length);
                var holder = new GameObject(piece + " " + i);
                holder.transform.SetParent(row.Models.transform, false);
                holder.transform.position = new Vector3(x, StyleWorld.StandHeight(x, z), z);
                holder.transform.rotation = Quaternion.Euler(0f, alongX ? 0f : 90f, 0f);
                holder.transform.localScale = new Vector3((float)sx, (float)sy, (float)sz);
                GameObject model = Object.Instantiate(prefab, holder.transform, false);
                foreach (Collider c in model.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            }
        }

        private void Vintage(bool shown)
        {
            if (shown && _vintage == null)
            {
                _vintage = new GameObject("Vendanges");
                _vintage.transform.SetParent(_root.transform, false);
                foreach (VintageProp prop in VineLook.Props)
                {
                    string code = LotCModels.ForVillage(prop.Key, prop.Key);
                    if (code == null) continue;
                    Vector3 at = StyleWorld.OnGround(new Vector3((float)prop.X, 0f, (float)prop.Z));
                    LotCLook.Place(_vintage.transform, code, prop.Key, at, (float)prop.Yaw);
                }
            }
            if (_vintage != null) _vintage.SetActive(shown);
        }
    }
}

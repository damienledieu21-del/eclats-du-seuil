using System.Collections.Generic;
using EdS.Domain.Space;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// V2.25.4 (« arbres de l'atelier ») : les arbres faits à l'atelier remplacent ceux téléchargés partout où le jeu pose des arbres
    /// (obstacles « arbre… », bois, lisière, et les arbres de décor des vergers, des berges et des abords du Seuil). Essence, âge, lacet et
    /// échelle viennent de <see cref="AtelierTrees"/> (tirés de la place, sans hasard). Chaque arbre a son LODGroup : LOD1, LOD2, imposteur
    /// (quad tourné vers la caméra, cellule de l'angle le plus proche parmi 8 et de la saison), puis rien ; distances selon la machine
    /// (<see cref="DeviceQuality.Tier"/>). Saison : un seul maillage, l'atlas du feuillage change (copies des matières, une par essence) ;
    /// feuilles au sol en automne seulement. Veines de l'arbre corrompu : lueur d'après l'heure, comme les pierres du Seuil. Rien n'est
    /// alloué à chaque image.
    /// </summary>
    public sealed partial class SpaceView
    {
        /// <summary>Menu d'essai des saisons : saison forcée à l'affichage (0 printemps à 3 hiver), -1 : celle du monde.</summary>
        public static int ForcedSeason = -1;

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

        /// <summary>Un imposteur posé : son rendu, son quad, l'arbre (lacet seul), sa rotation de repos, sa texture, la cellule montrée.</summary>
        private sealed class TreeBoard
        {
            public Renderer Renderer;
            public Transform Quad;
            public Transform Tree;
            public Quaternion Rest;
            public Texture2D Texture;
            public int Column = -1;
            public int Season = -1;
        }

        private AdventureMap _treeMap;
        private readonly List<TreeBoard> _boards = new List<TreeBoard>();
        private readonly MaterialPropertyBlock _boardBlock = new MaterialPropertyBlock();
        private readonly Dictionary<string, Material> _treeMaterials = new Dictionary<string, Material>();
        private readonly List<(Material Material, TreeLook Look)> _foliage = new List<(Material, TreeLook)>();
        private readonly List<Material> _treeGlow = new List<Material>();
        private readonly List<GameObject> _groundLeaves = new List<GameObject>();
        private MaterialCopies _treeCopies;
        private int _worldSeason = AtelierTrees.DefaultSeason;
        private int _treeSeason = -1;
        private float _shownTreeGlow = -1f;

        /// <summary>Saison du monde (0 à 3), donnée par la partie à chaque image ; l'été sans partie.</summary>
        public void Season(int season) => _worldSeason = AtelierTrees.Season(season);

        /// <summary>Saison montrée aux arbres (forcée par le menu d'essai, sinon celle du monde).</summary>
        private int ShownSeason => AtelierTrees.Shown(ForcedSeason, _worldSeason);

        /// <summary>
        /// Pose l'arbre de l'atelier <paramref name="spot"/> (sur le sol affiché, au plus bas sous son pied, sauf <paramref name="lifted"/> :
        /// l'obstacle le monte lui-même) ; null sans arbre de l'atelier de cette essence dans le catalogue.
        /// </summary>
        private GameObject AtelierTree(TreeSpot spot, bool occludes, bool lifted = false)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            TreeLook look = catalog != null ? catalog.TreeOf(AtelierTrees.Name(spot.Essence)) : null;
            if (look == null) return null;
            int index = look.IndexOf(spot.Model);
            if (index < 0) index = StyleRules.Variant(spot.Model, look.Models.Length);
            GameObject prefab = index >= 0 ? look.Models[index] : null;
            if (prefab == null) return null;
            float x = (float)spot.X, z = (float)spot.Z;
            var holder = new GameObject(spot.Model + " (arbre)");
            holder.transform.SetParent(_root.transform, false);
            holder.transform.position = new Vector3(x, lifted ? 0f : Ground(x, z, 0.5f), z);   // V2.24 : rien ne flotte
            holder.transform.rotation = Quaternion.Euler(0f, (float)spot.Yaw, 0f);
            holder.transform.localScale = Vector3.one * (float)spot.Scale;
            GameObject tree = Object.Instantiate(prefab, holder.transform, false);
            foreach (Collider c in tree.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            var near = new List<Renderer>();
            var far = new List<Renderer>();
            Renderer board = null;
            bool ground = AtelierTrees.GroundLeaves(ShownSeason);
            foreach (Transform t in tree.GetComponentsInChildren<Transform>(true))
            {
                string part = AtelierTrees.PartOf(t.name);
                if (part == null) continue;
                Renderer r = t.GetComponent<Renderer>();
                if (part == "collision")
                {
                    Object.Destroy(t.gameObject);   // collision de l'atelier : le moteur garde ses obstacles
                    continue;
                }
                if (r == null) continue;
                if (part == "LOD3")
                {
                    board = r;
                    continue;
                }
                TreeMaterials(r, look);
                if (part == "LOD1" || part == "sol") near.Add(r);
                if (part == "LOD2" || part == "sol") far.Add(r);
                if (part == "sol")
                {
                    _groundLeaves.Add(t.gameObject);
                    t.gameObject.SetActive(ground);
                }
            }
            TreeLods(tree, near, far, board);
            if (board != null)
            {
                board.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _boards.Add(new TreeBoard
                {
                    Renderer = board,
                    Quad = board.transform,
                    Tree = holder.transform,
                    Rest = Quaternion.Inverse(holder.transform.rotation) * board.transform.rotation,
                    Texture = look.Impostors != null && index < look.Impostors.Length ? look.Impostors[index] : null,
                });
            }
            if (occludes) AddModelOccluders(tree);
            return holder;
        }

        /// <summary>LODGroup de l'arbre : LOD1 (et feuilles au sol), LOD2, imposteur, puis rien ; seuils tirés des distances de la machine.</summary>
        private static void TreeLods(GameObject tree, List<Renderer> near, List<Renderer> far, Renderer board)
        {
            LODGroup group = tree.GetComponent<LODGroup>();
            if (group == null) group = tree.AddComponent<LODGroup>();
            Renderer[] boards = board != null ? new[] { board } : new Renderer[0];
            group.SetLODs(new[] { new LOD(0.5f, near.ToArray()), new LOD(0.2f, far.ToArray()), new LOD(0.01f, boards) });
            group.RecalculateBounds();
            Camera camera = Camera.main;
            var h = AtelierTrees.LodHeights(group.size * tree.transform.lossyScale.y, DeviceQuality.Tier, camera != null ? camera.fieldOfView : 60f);
            group.SetLODs(new[] { new LOD((float)h.Lod1, near.ToArray()), new LOD((float)h.Lod2, far.ToArray()), new LOD((float)h.Impostor, boards) });
        }

        /// <summary>Feuillage : la copie de la saison (une par essence) ; écorce de l'arbre corrompu : la copie lumineuse.</summary>
        private void TreeMaterials(Renderer r, TreeLook look)
        {
            Material[] materials = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                Material m = materials[i];
                if (m == null) continue;
                bool foliage = AtelierTrees.IsFoliage(m.name), glow = AtelierTrees.IsGlowingBark(m.name);
                if (!foliage && !glow) continue;
                if (!_treeMaterials.TryGetValue(m.name, out Material copy) || copy == null)
                {
                    copy = new Material(m) { name = m.name + " (arbres)" };
                    if (_treeCopies == null)
                    {
                        var owner = new GameObject("Arbres de l'atelier (matières)");
                        owner.transform.SetParent(_root.transform, false);
                        _treeCopies = owner.AddComponent<MaterialCopies>();
                    }
                    _treeCopies.Copies.Add(copy);
                    _treeMaterials[m.name] = copy;
                    if (foliage)
                    {
                        _foliage.Add((copy, look));
                        Leaves(copy, look, ShownSeason);
                    }
                    else
                    {
                        _treeGlow.Add(copy);
                        _shownTreeGlow = -1f;
                    }
                }
                materials[i] = copy;
                changed = true;
            }
            if (changed) r.sharedMaterials = materials;
        }

        private static void Leaves(Material m, TreeLook look, int season)
        {
            Texture2D color = look.Leaves != null && season < look.Leaves.Length ? look.Leaves[season] : null;
            Texture2D normal = look.LeafNormals != null && season < look.LeafNormals.Length ? look.LeafNormals[season] : null;
            if (color != null) m.SetTexture("_BaseMap", color);
            if (normal != null) m.SetTexture("_BumpMap", normal);
        }

        /// <summary>
        /// V2.25.4 : chaque image (vue de la caméra) : saison des arbres (si elle change), lueur des veines de l'arbre corrompu, et
        /// imposteurs visibles tournés vers la caméra, cellule changée seulement quand l'angle ou la saison change. Sans allocation.
        /// </summary>
        public void Trees(Vector3 camera)
        {
            if (_boards.Count == 0 && _foliage.Count == 0 && _treeGlow.Count == 0) return;
            int season = ShownSeason;
            if (season != _treeSeason) TreeSeason(season);
            TreeGlow(StyleWorld.Lamps);
            for (int i = 0; i < _boards.Count; i++)
            {
                TreeBoard b = _boards[i];
                if (b.Renderer == null || b.Tree == null || !b.Renderer.isVisible) continue;
                Vector3 local = b.Tree.InverseTransformPoint(camera);
                b.Quad.rotation = b.Tree.rotation * Quaternion.Euler(0f, Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, 0f) * b.Rest;
                int column = AtelierTrees.ImpostorColumn(local.x, local.z);
                if (column == b.Column && season == b.Season) continue;
                b.Column = column;
                b.Season = season;
                (double u, double v) = AtelierTrees.ImpostorOffset(column, season);
                _boardBlock.Clear();
                if (b.Texture != null) _boardBlock.SetTexture(BaseMapId, b.Texture);
                _boardBlock.SetVector(BaseMapStId, new Vector4(1f, 1f, (float)u, (float)v));
                b.Renderer.SetPropertyBlock(_boardBlock);
            }
        }

        private void TreeSeason(int season)
        {
            _treeSeason = season;
            for (int i = 0; i < _foliage.Count; i++)
                if (_foliage[i].Material != null) Leaves(_foliage[i].Material, _foliage[i].Look, season);
            bool ground = AtelierTrees.GroundLeaves(season);
            for (int i = 0; i < _groundLeaves.Count; i++)
                if (_groundLeaves[i] != null) _groundLeaves[i].SetActive(ground);
        }

        private void TreeGlow(float lamps)
        {
            if (_treeGlow.Count == 0 || Mathf.Abs(lamps - _shownTreeGlow) <= 0.002f) return;
            _shownTreeGlow = lamps;
            Color emission = Color.white * (float)AtelierTrees.CorruptGlow(lamps);
            for (int i = 0; i < _treeGlow.Count; i++)
                if (_treeGlow[i] != null) _treeGlow[i].SetColor("_GlowColor", emission);
        }

        /// <summary>
        /// V2.25.4 : arbres de décor posés par le code (<see cref="AtelierTrees.DecorTrees"/>) : rangs des vergers, saules et aulnes des
        /// berges, arbres corrompus autour des pierres du Seuil. Seulement avec les arbres de l'atelier ; le moteur ne les connaît pas.
        /// </summary>
        private void AtelierDecorTrees(CombatSpace space)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            if (catalog == null || !catalog.HasAtelierTrees || _treeMap == null) return;
            foreach (TreeSpot spot in AtelierTrees.DecorTrees(_treeMap, space.Obstacles)) AtelierTree(spot, true);
        }
    }
}

using System.Linq;
using EdS.Domain.Space;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// V2.30.2 : le Bourg d'Aube fortifié habillé des modèles de l'atelier (<see cref="BourgModels"/>) : palissade en modules, tours,
    /// portes ouvertes, ronde couverte aux portes, tours de guet. Un obstacle n'est habillé que si toutes ses pièces sont au catalogue
    /// (<see cref="StyleCatalog.OwnDecorPiece"/>) ; sinon la forme faite par le code reste. Affichage seulement.
    /// </summary>
    public sealed partial class SpaceView
    {
        /// <summary>Habillage du Bourg calculé sur les obstacles de l'espace (vide hors du Bourg).</summary>
        private BourgDress _bourg = BourgDress.Empty;

        /// <summary>Marge (m) autour de la fenêtre dans laquelle les pièces du Bourg sont posées.</summary>
        private const float BourgMargin = 40f;

        /// <summary>V2.30.2 : l'obstacle <paramref name="id"/> est-il habillé par des modèles du Bourg tous présents au catalogue ?</summary>
        private bool BourgCovers(string id)
        {
            if (_bourg == null || !_bourg.Needs.TryGetValue(id, out System.Collections.Generic.IReadOnlyList<string> needs)) return false;
            StyleCatalog catalog = StyleCatalog.Loaded;
            if (catalog == null) return false;
            bool all = true;
            foreach (string piece in needs)
                if (catalog.OwnDecorPiece(piece) == null) all = false;
            if (all) return true;
            // Kit du Bourg (25 novembre 2026) : une maison du kit absente laisse l'ancienne maison type de l'atelier.
            return _bourg.Fallbacks.TryGetValue(id, out string old) && catalog.OwnDecorPiece(old) != null;
        }

        /// <summary>
        /// V2.30.2 : pose les pièces du Bourg proches de la fenêtre, chacune sur le sol affiché (au plus bas sous son pied, V2.24), tournée
        /// vers l'extérieur ; les portes ont leurs vantaux rabattus vers la ville et la herse levée (<see cref="BourgModels.OpenParts"/>).
        /// Aucune collision (le moteur garde ses obstacles) ; elles s'effacent quand elles cachent le joueur.
        /// </summary>
        private void BourgDecor()
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            if (catalog == null || _bourg == null || _bourg.Pieces.Count == 0) return;
            float minX = Raw(_space.Min.X.Raw) - BourgMargin, maxX = Raw(_space.Max.X.Raw) + BourgMargin;
            float minZ = Raw(_space.Min.Z.Raw) - BourgMargin, maxZ = Raw(_space.Max.Z.Raw) + BourgMargin;
            var walled = new System.Collections.Generic.Dictionary<string, float>();
            foreach (BourgPiece near in _bourg.Near(minX, minZ, maxX, maxZ, 0))
            {
                if (near.Covers != null && !BourgCovers(near.Covers)) continue;
                BourgPiece p = near;
                // Kit du Bourg : repli sur l'ancienne maison type, à sa propre place, quand le modèle du kit manque ; sa lanterne de porte
                // suit (BourgFallback.When : la maison dont elle dépend).
                if (p.Fallback != null && catalog.OwnDecorPiece(p.Fallback.When ?? p.Piece) == null) p = p.Fallback.For(near);
                GameObject prefab = catalog.OwnDecorPiece(p.Piece);
                if (prefab == null) continue;
                var holder = new GameObject(prefab.name + " (Bourg)");
                holder.transform.SetParent(_root.transform, false);
                float x = (float)p.X, z = (float)p.Z;
                // V2.30.5 : fossé et ponts au niveau du bord (pas au fond creusé) ; pièces murales à leur hauteur de pose sur la façade.
                float ground = BourgModels.OnRim(p.Piece) ? RimGround(p) : Ground(x, z, (float)BourgModels.Foot(p.Piece));
                holder.transform.position = new Vector3(x, ground + (float)p.Height, z);
                holder.transform.rotation = Quaternion.Euler(0f, (float)p.Yaw, 0f);
                // Kit du Bourg : sols (place, rue pavée, ruelle, bordure) couchés sur la pente du sol affiché, à sa hauteur au centre ; posés au
                // plus bas de leur pied, ils s'enterraient sur les pentes et ne laissaient voir que des coins clairs épars dans l'herbe.
                if (BourgModels.IsGround(p.Piece)) GroundOnSlope(holder.transform, p.Piece, x, z, (float)p.Yaw);
                GameObject model = Object.Instantiate(prefab, holder.transform, false);
                foreach (Collider c in model.GetComponentsInChildren<Collider>()) Object.Destroy(c);
                var panes = new System.Collections.Generic.List<Renderer>();
                BourgOpen(holder.transform, model.transform, p.Piece);
                // Relecture indépendante : occulteurs pour les grandes pièces seulement (tours, portes, bâtiments).
                if (BourgModels.Occludes(p.Piece)) AddModelOccluders(model);
                BourgNight(p, holder.transform, model, panes);
                KitLod(model, p.Piece, catalog, panes);
                // V2.30.3 : un bâtiment habillé arrête la caméra à son emprise (comme un modèle de l'atelier), jusqu'au haut de ses pièces.
                if (p.Covers != null && Blocks(TownRules.BuildingFor(p.Covers)) && TownRules.BuildingFor(p.Covers) != StyleBuilding.None)
                {
                    var box = BourgModels.Box(p.Piece);
                    float top = box != null ? (float)box.Value.Top : 8f;
                    walled[p.Covers] = walled.TryGetValue(p.Covers, out float seen) ? Mathf.Max(seen, top) : top;
                }
            }
            foreach (SpaceObstacle o in _space.Obstacles)
            {
                if (!walled.TryGetValue(o.Id, out float top)) continue;
                float x0 = Raw(o.Points.Min(q => q.X.Raw)), x1 = Raw(o.Points.Max(q => q.X.Raw));
                float z0 = Raw(o.Points.Min(q => q.Z.Raw)), z1 = Raw(o.Points.Max(q => q.Z.Raw));
                float ground = Ground((x0 + x1) / 2f, (z0 + z1) / 2f, 0.4f);
                _cameraBoxes.Add(new CameraBox(x0, ground, z0, x1, ground + top, z1));
            }
        }

        /// <summary>
        /// Kit du Bourg (décision de Damien du 25 novembre 2026) : une maison du kit reçoit sa version lointaine (« …_lod1 », même place) et
        /// un LODGroup : détaillée de près, lointaine plus loin, rien au-delà (seuils de <see cref="BourgKit.LodHeights"/> selon la machine).
        /// Sans version lointaine au catalogue, la maison détaillée reste seule.
        /// </summary>
        private static void KitLod(GameObject model, string piece, StyleCatalog catalog, System.Collections.Generic.List<Renderer> windows)
        {
            if (BourgKit.Find(piece) == null) return;
            GameObject farPrefab = catalog.OwnDecorPiece(BourgKit.Far(piece));
            if (farPrefab == null) return;
            GameObject far = Object.Instantiate(farPrefab, model.transform.parent, false);
            far.name = farPrefab.name;
            foreach (Collider c in far.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            // Relecture de la PR #255 : les vitres de nuit de la maison (sous le support) s'effacent avec la maison détaillée.
            var nearList = new System.Collections.Generic.List<Renderer>(model.GetComponentsInChildren<Renderer>());
            nearList.AddRange(windows);
            Renderer[] near = nearList.ToArray(), distant = far.GetComponentsInChildren<Renderer>();
            if (near.Length == 0 || distant.Length == 0) return;
            GameObject owner = model.transform.parent.gameObject;
            LODGroup group = owner.GetComponent<LODGroup>();
            if (group == null) group = owner.AddComponent<LODGroup>();
            group.SetLODs(new[] { new LOD(0.1f, near), new LOD(0.01f, distant) });
            group.RecalculateBounds();
            Camera camera = Camera.main;
            var h = BourgKit.LodHeights(group.size, DeviceQuality.Tier, camera != null ? camera.fieldOfView : 60f);
            group.SetLODs(new[] { new LOD((float)h.Far, near), new LOD((float)h.Cull, distant) });
        }

        /// <summary>
        /// Kit du Bourg : un sol plat de l'atelier (modules de 6 m) couché sur la pente du sol affiché, monté juste assez pour qu'aucun coin ne
        /// passe sous le sol (<see cref="BourgGround.OnSlope"/>).
        /// </summary>
        private static void GroundOnSlope(Transform holder, string piece, float x, float z, float yaw)
        {
            // Revue Codex de la PR #255 : calcul pur et testé (BourgGround.OnSlope) ; ici, seulement la pose.
            var pose = BourgGround.OnSlope((px, pz) => StyleWorld.StandHeight((float)px, (float)pz), piece, x, z, yaw);
            Vector3 normal = new Vector3((float)pose.Nx, (float)pose.Ny, (float)pose.Nz);
            holder.position = new Vector3(x, (float)pose.Y, z);
            holder.rotation = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, yaw, 0f);
        }

        /// <summary>V2.30.5 : sol d'une pièce du fossé : le plus bas des deux bords (de part et d'autre de son axe, au-delà du creux).</summary>
        private static float RimGround(BourgPiece p)
        {
            var box = BourgModels.Box(p.Piece);
            float reach = box != null ? (float)System.Math.Max(-box.Value.Y0, box.Value.Y1) + 0.3f : 4f;
            float a = (float)p.Yaw * Mathf.Deg2Rad, ox = Mathf.Sin(a) * reach, oz = Mathf.Cos(a) * reach;
            float x = (float)p.X, z = (float)p.Z;
            return Mathf.Min(StyleWorld.StandHeight(x + ox, z + oz), StyleWorld.StandHeight(x - ox, z - oz));
        }

        /// <summary>
        /// V2.30.5 (décision de Damien du 16 novembre 2026 : façades vivantes) : la nuit d'un bâtiment habillé. Une lanterne sur potence (R21)
        /// reçoit la lumière de porte ; un monument habillé (église, halle, écuries, chaque bâtiment de la cour du prévôt) a la lanterne de
        /// porte faite par le code, sur la façade de son modèle (<see cref="BourgModels.DoorLanternOf"/>) ; les fenêtres de façade d'un bâtiment habillé (entre ses volets « Volet_…_G » et « …_D », rez-de-chaussée
        /// et premier étage côté rue, deux au plus) prennent le fond de fenêtre qui s'allume la nuit.
        /// </summary>
        private void BourgNight(BourgPiece p, Transform holder, GameObject model, System.Collections.Generic.List<Renderer> windows)
        {
            float a = (float)p.Yaw * Mathf.Deg2Rad;
            (int X, int Z) facing = (Mathf.RoundToInt(Mathf.Sin(a)), Mathf.RoundToInt(Mathf.Cos(a)));
            if (p.Piece == BourgModels.WallLantern)
            {
                string id = "lanterne/" + p.X.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "," + p.Z.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                var lamp = new NightLamp(id, NightLampKind.DoorLantern, p.X, p.Height + 0.6, p.Z, p.Yaw);   // hauteur au-dessus du sol (posée sur le sol affiché par NightDecor)
                _lampSpots.Add(lamp);
                _lightOnly.Add(lamp.Id);
                return;
            }
            if (p.Covers == null || BourgModels.IsWall(p.Covers) || BourgModels.IsTower(p.Covers) || BourgModels.IsDitch(p.Covers)) return;
            StyleBuilding kind = TownRules.BuildingFor(p.Covers);
            // Revue Codex de la PR #204 : la lanterne de porte d'un monument va sur la façade de la pièce posée (pas sur la boîte de l'obstacle).
            if (BourgModels.HasDoorLantern(p.Piece))
            {
                (double lx, double lz) = BourgModels.DoorLanternOf(p);
                _lampSpots.Add(new NightLamp("porte/" + p.Covers + "/" + p.Piece, NightLampKind.DoorLantern, lx, NightLamps.DoorLanternHeight, lz, p.Yaw));
            }
            int lit = 0;
            foreach (Renderer left in model.GetComponentsInChildren<Renderer>())
            {
                string name = left.transform.name;
                if (lit >= 2 || !name.StartsWith("Volet_", System.StringComparison.Ordinal) || !name.EndsWith("_G", System.StringComparison.Ordinal)
                    || !(name.Contains("_av") || name.Contains("rdc"))) continue;
                Transform right = FindChild(model.transform, name.Substring(0, name.Length - 2) + "_D");
                Renderer r = right != null ? right.GetComponent<Renderer>() : null;
                if (r == null) continue;
                Vector3 c = (left.bounds.center + r.bounds.center) / 2f;
                float width = Mathf.Max(0.4f, Vector3.Distance(left.bounds.center, r.bounds.center) - 0.5f);
                Vector3 size = facing.X != 0 ? new Vector3(0.06f, 1f, width) : new Vector3(width, 1f, 0.06f);
                Pane(Box(p.Covers + " (fenêtre " + lit + ")", c, size, WindowGlass(), 1f), holder, windows);
                lit++;
            }
            // Revue Codex de la PR #204 : un modèle sans volets séparés (boutiques, prévôt, corps de garde, écuries) : ses fenêtres de façade.
            if (lit == 0)
                foreach (var w in BourgModels.FacadeWindows(p.Piece))
                {
                    Vector3 c = holder.TransformPoint(new Vector3((float)w.X, (float)w.Y, (float)w.Z));
                    Vector3 size = facing.X != 0 ? new Vector3(0.06f, (float)w.Height, (float)w.Width) : new Vector3((float)w.Width, (float)w.Height, 0.06f);
                    Pane(Box(p.Covers + " (fenêtre " + lit++ + ")", c, size, WindowGlass(), 1f), holder, windows);
                }
        }

        /// <summary>Relecture de la PR #255 : une vitre de nuit rangée sous le support du bâtiment (même place), son rendu gardé pour le LOD.</summary>
        private static void Pane(GameObject pane, Transform holder, System.Collections.Generic.List<Renderer> windows)
        {
            if (pane == null) return;
            pane.transform.SetParent(holder, true);
            Renderer r = pane.GetComponent<Renderer>();
            if (r != null) windows.Add(r);
        }

        /// <summary>Vantaux tournés autour de leur charnière (repère du support), herse levée.</summary>
        private static void BourgOpen(Transform holder, Transform model, string piece)
        {
            foreach (BourgModels.OpenPart part in BourgModels.OpenParts(piece))
            {
                Transform t = FindChild(model, part.Name);
                if (t == null) continue;
                // Le support n'est tourné qu'autour de la verticale : sa verticale est celle du monde.
                if (part.Turn != 0)
                {
                    Vector3 hinge = holder.TransformPoint(new Vector3((float)part.X, 0f, (float)part.Z));
                    Quaternion turn = Quaternion.Euler(0f, (float)part.Turn, 0f);
                    t.position = hinge + turn * (t.position - hinge);
                    t.rotation = turn * t.rotation;
                }
                if (part.Lift != 0) t.position += Vector3.up * (float)part.Lift;
            }
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChild(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}

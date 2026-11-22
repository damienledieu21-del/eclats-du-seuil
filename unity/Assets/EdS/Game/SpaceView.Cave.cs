using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Space;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Chantier V2.46 (grotte derrière la cascade) : décor d'un dessous. Sol de roche, parois, stalagmites, pilier, bassin, cristaux,
    /// éboulis et coffre d'après les obstacles (<see cref="CaveLooks.PieceFor"/>), rideau d'eau sur la porte qui mène dehors ; pénombre
    /// (<see cref="StyleWorld.SetCave"/>) : ni ciel, ni soleil, ni pluie, ni neige, ni glace. Les modèles du kit de grotte sont pris dans
    /// <c>Resources/Grotte/&lt;nom&gt;</c> (<see cref="CaveLooks.Models"/>) ; en repli (modèle absent), des
    /// formes simples les remplacent.
    /// </summary>
    public sealed partial class SpaceView
    {
        private static readonly Dictionary<string, GameObject> CaveModels = new Dictionary<string, GameObject>();

        /// <summary>Le modèle du kit nommé <paramref name="name"/>, ou null (cherché une seule fois).</summary>
        private static GameObject CaveModel(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            GameObject found;
            if (!CaveModels.TryGetValue(name, out found))
            {
                found = Resources.Load<GameObject>(CaveLooks.Folder + name);
                CaveModels[name] = found;
            }
            return found;
        }

        /// <summary>Construit la grotte de <paramref name="map"/> (un dessous).</summary>
        private void Cave(CombatSpace space, AdventureMap map)
        {
            _cave = true;
            _mine = MineLooks.IsMine(space.Obstacles);   // V2.47.2 : la mine de la carrière (pièces du kit de mine)
            _crypt = CryptLooks.IsCrypt(space.Obstacles);   // V2.48.2 : les cryptes de l'abbaye et de la chapelle
            StyleWorld.SetCave(true);
            float minX = Raw(space.Min.X.Raw), minZ = Raw(space.Min.Z.Raw), maxX = Raw(space.Max.X.Raw), maxZ = Raw(space.Max.Z.Raw);
            Material rock = Built(StyleSurface.Stone, new Color(0.3f, 0.29f, 0.28f), out float rockTile);
            Material dark = Tinted(rock, new Color(0.55f, 0.55f, 0.6f), "roche sombre");
            Material crystal = _caveCrystal = ArenaView.MakeMaterial(new Color(0.45f, 0.75f, 0.95f));
            Material wood = Built(StyleSurface.Wood, new Color(0.4f, 0.28f, 0.18f), out float woodTile);

            // Sol de roche, un peu plus grand que l'aire ; tout autour, la roche monte (on ne voit jamais le vide).
            Box("Sol de la grotte", new Vector3((minX + maxX) / 2f, -0.05f, (minZ + maxZ) / 2f), new Vector3(maxX - minX + 4f, 0.1f, maxZ - minZ + 4f), dark, rockTile);
            float h = (float)CaveLooks.WallHeight;
            RockWall("Roche ouest", new Vector3(minX - 1f, h / 2f, (minZ + maxZ) / 2f), new Vector3(2f, h, maxZ - minZ + 4f), rock, rockTile);
            RockWall("Roche est", new Vector3(maxX + 1f, h / 2f, (minZ + maxZ) / 2f), new Vector3(2f, h, maxZ - minZ + 4f), rock, rockTile);
            RockWall("Roche nord", new Vector3((minX + maxX) / 2f, h / 2f, maxZ + 1f), new Vector3(maxX - minX + 4f, h, 2f), rock, rockTile);
            RockWall("Roche sud", new Vector3((minX + maxX) / 2f, h / 2f, minZ - 1f), new Vector3(maxX - minX + 4f, h, 2f), rock, rockTile);

            foreach (SpaceObstacle o in space.Obstacles)
            {
                CavePiece piece = CaveLooks.PieceFor(o.Id);
                if (piece == CavePiece.None && CryptObstacle(o, rock, rockTile, wood, woodTile)) continue;   // V2.48.2 : pièces des cryptes
                if (piece == CavePiece.None && MineObstacle(o, rock, rockTile, wood, woodTile)) continue;   // V2.47.2 : pièces de la mine
                var points = o.Points.Select(p => new Vector3(Raw(p.X.Raw), 0f, Raw(p.Z.Raw))).ToList();
                float x0 = points.Min(p => p.x), x1 = points.Max(p => p.x), z0 = points.Min(p => p.z), z1 = points.Max(p => p.z);
                // Le bassin garde son eau, avec ou sans le modèle de sa margelle.
                if (piece == CavePiece.Pool) _waters.Add(Prism(o.Id + " eau", points.Select(p => new Vector3(p.x, 0.05f, p.z)).ToList(), 0.04f, _water, false));
                if (CaveProp(piece, o.Id, x0, z0, x1, z1)) continue;
                // Formes de remplacement (repli sans le modèle du kit).
                switch (piece)
                {
                    case CavePiece.Pool:
                        break;
                    case CavePiece.Crystals:
                        Prism(o.Id, points, (float)CaveLooks.HeightOf(piece), crystal, false, 1f);
                        CaveGlow(o.Id, (x0 + x1) / 2f, (z0 + z1) / 2f, new Color(0.5f, 0.8f, 1f), 1.2f, 5f);
                        break;
                    case CavePiece.Chest:
                        Prism(o.Id, points, (float)CaveLooks.HeightOf(piece), wood, false, woodTile);
                        break;
                    case CavePiece.Stalagmite:
                        StalagmiteShape(o.Id, (x0 + x1) / 2f, (z0 + z1) / 2f, Mathf.Max(x1 - x0, z1 - z0) / 2f, rock);
                        break;
                    default:
                        Prism(o.Id, points, (float)CaveLooks.HeightOf(piece), rock, piece == CavePiece.Wall || piece == CavePiece.Pillar, rockTile);
                        break;
                }
            }

            if (_mine) MineBridges(space, wood, woodTile);   // V2.47.2 : passerelles sur la rivière souterraine
            if (_crypt) CryptArches(space, rock, rockTile);   // V2.48.2 : voûtes des passages entre deux grilles

            // Le rideau d'eau, vu de dedans, sur les portes qui mènent dehors, tourné vers la grotte ; le jour du dehors passe à travers.
            foreach (MapDoor door in map.Doors)
            {
                // V2.48.2 : dans une crypte, ni rideau d'eau : l'escalier de crypte (et le jour de la nef sur la porte du dehors).
                if (_crypt)
                {
                    CryptDoor(door, minX, minZ, maxX, maxZ, rock, rockTile);
                    continue;
                }
                // V2.47.2 : dans la mine, ni rideau d'eau : le jour de la bouche, l'ascenseur, les échelles.
                if (_mine)
                {
                    MineDoor(door, minX, minZ, maxX, maxZ, wood, woodTile);
                    continue;
                }
                if (!door.Outside) continue;
                var facing = CaveLooks.CurtainFacing(door.X, door.Z, minX, minZ, maxX, maxZ);
                Vector3 at = new Vector3((float)door.X, 0f, (float)door.Z);
                Vector3 outward = new Vector3((float)facing.OutX, 0f, (float)facing.OutZ);
                Quaternion turn = Quaternion.Euler(0f, (float)facing.Yaw, 0f);
                GameObject model = CaveModel(CaveLooks.CurtainModel);
                if (model != null)
                {
                    GameObject copy = Object.Instantiate(model, _root.transform, false);
                    copy.name = "Rideau d'eau (" + door.Id + ")";
                    foreach (Collider c in copy.GetComponentsInChildren<Collider>()) Object.Destroy(c);
                    copy.transform.rotation = turn;
                    copy.transform.position = at + outward * 0.8f;
                }
                else
                {
                    GameObject sheet = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    sheet.name = "Rideau d'eau (" + door.Id + ")";
                    Object.Destroy(sheet.GetComponent<Collider>());
                    sheet.transform.SetParent(_root.transform, false);
                    sheet.transform.rotation = turn;
                    sheet.transform.position = at + outward * 0.9f + new Vector3(0f, h / 2f, 0f);
                    sheet.transform.localScale = new Vector3(5f, h, 0.15f);
                    sheet.GetComponent<Renderer>().sharedMaterial = _water;
                    _waters.Add(sheet);
                }
                Vector3 glow = at - outward * 1.5f;
                _curtains.Add(CaveGlow("Jour du rideau (" + door.Id + ")", glow.x, glow.z, new Color(0.75f, 0.85f, 1f), 0f, (float)CaveLooks.CurtainReach));
            }
        }

        /// <summary>
        /// Revue Codex de la PR #245 : mur de roche du pourtour (6 m), enregistré comme occulteur (fondu ou coupé comme les autres murs
        /// quand il cache le personnage), à la différence de <see cref="Box"/>.
        /// </summary>
        private GameObject RockWall(string name, Vector3 center, Vector3 size, Material material, float tile)
        {
            float y = center.y - size.y / 2f, hx = size.x / 2f, hz = size.z / 2f;
            var points = new List<Vector3>
            {
                new Vector3(center.x - hx, y, center.z - hz), new Vector3(center.x + hx, y, center.z - hz),
                new Vector3(center.x + hx, y, center.z + hz), new Vector3(center.x - hx, y, center.z + hz),
            };
            return Prism(name, points, size.y, material, true, tile);
        }

        /// <summary>Le modèle du kit pour cette pièce, posé dans l'emprise de l'obstacle ; faux sans lui.</summary>
        private bool CaveProp(CavePiece piece, string id, float x0, float z0, float x1, float z1)
        {
            GameObject model = CaveModel(CaveLooks.ModelFor(piece));
            if (model == null) return false;
            var look = new GameObject(id + " (" + model.name + ")");
            GameObject copy = Object.Instantiate(model, look.transform, false);
            foreach (Collider c in look.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            look.transform.SetParent(_root.transform, false);
            if (StyleLook.VisibleBounds(look, out Bounds b) && b.size.x > 0.01f && b.size.z > 0.01f)
            {
                // Étiré à l'emprise de l'obstacle (le modèle remplit son rocher), hauteur gardée en proportion du plus grand côté.
                float sx = (x1 - x0) / b.size.x, sz = (z1 - z0) / b.size.z;
                float sy = Mathf.Max(sx, sz);
                look.transform.localScale = new Vector3(sx, sy, sz);
                StyleLook.VisibleBounds(look, out b);
                look.transform.position += new Vector3((x0 + x1) / 2f - b.center.x, -b.min.y, (z0 + z1) / 2f - b.center.z);
            }
            if (piece == CavePiece.Wall || piece == CavePiece.Pillar) AddModelOccluders(look);
            if (piece == CavePiece.Crystals) CaveGlow(id, (x0 + x1) / 2f, (z0 + z1) / 2f, new Color(0.5f, 0.8f, 1f), 1.2f, 5f);
            return true;
        }

        /// <summary>Stalagmite de remplacement : trois cônes de roche (cylindres effilés) de hauteurs différentes.</summary>
        private void StalagmiteShape(string id, float x, float z, float radius, Material rock)
        {
            float top = (float)CaveLooks.HeightOf(CavePiece.Stalagmite);
            for (int i = 0; i < 3; i++)
            {
                float k = 1f - 0.25f * i;
                GameObject cone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cone.name = id + " " + (i + 1);
                Object.Destroy(cone.GetComponent<Collider>());
                cone.transform.SetParent(_root.transform, false);
                float dx = i == 0 ? 0f : (i == 1 ? 0.35f : -0.3f) * radius, dz = i == 2 ? 0.3f * radius : -0.2f * radius;
                cone.transform.position = new Vector3(x + dx, top * k / 2f, z + dz);
                cone.transform.localScale = new Vector3(radius * k, top * k / 2f, radius * k);
                cone.GetComponent<Renderer>().sharedMaterial = rock;
            }
        }

        /// <summary>Lumière ponctuelle de la grotte (cristaux, jour du rideau d'eau).</summary>
        private Light CaveGlow(string name, float x, float z, Color color, float intensity, float range)
        {
            var go = new GameObject("Lumière " + name);
            go.transform.SetParent(_root.transform, false);
            go.transform.position = new Vector3(x, 2.2f, z);
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = range;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        /// <summary>
        /// Chantier V2.46 : le jour qui passe par le rideau d'eau suit la lumière du dehors (nuit : éteint). <paramref name="daylight"/> de
        /// 0 à 1 ; appelé à chaque image par le jeu.
        /// </summary>
        private void CaveDaylight(float daylight)
        {
            if (!_cave) return;
            foreach (Light light in _curtains)
                if (light != null) light.intensity = 2.2f * (float)CaveLooks.CurtainLight(0, daylight);
        }

        private bool _cave;
        private Material _caveCrystal;
        private readonly List<Light> _curtains = new List<Light>();
    }
}

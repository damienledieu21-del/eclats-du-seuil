using System.Collections.Generic;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot 12.8 : repères d'une zone du monde, en formes simples (provisoire) : les PNJ (capsules dorées), les nœuds de récolte
    /// (sphère verte pour une herbe, cube gris pour un filon ; cachés tant qu'ils repoussent) et les points sûrs (disques bleus au sol). Leurs
    /// noms s'affichent au-dessus d'eux quand le joueur est assez près. Depuis l'essai du 12.9 : la forge (enclume sombre). Affichage seulement : portées et règles sont celles du cœur.
    /// </summary>
    public sealed class WorldMarkers
    {
        private const float NpcLabelRange = 15f, NodeLabelRange = 7f, SafeLabelRange = 12f;

        private sealed class Marker
        {
            public WorldMarker Data;
            public GameObject Body;
            public float Height;
            public float Range;
            public Color Color;
        }

        private readonly GameObject _root;
        private readonly List<Marker> _markers = new List<Marker>();
        private readonly Dictionary<string, Marker> _nodes = new Dictionary<string, Marker>();

        public WorldMarkers(AdventureSession adventure)
        {
            _root = new GameObject("Repères");
            foreach (WorldMarker npc in adventure.Npcs())
            {
                Marker m = Add(npc, PrimitiveType.Capsule, new Vector3(0.8f, 1f, 0.8f), 1f, new Color(0.95f, 0.78f, 0.3f), 2.3f, NpcLabelRange);
                // Essai réaliste (demande de Damien du 26 octobre 2026) : le PNJ prend son personnage Mixamo (dossier Odile, Maelle,
                // Gaspard) s'il existe, et respire en attendant (l'attente tourne en boucle sans autre mise à jour).
                StyleBody look = StyleLook.Dress(m.Body, StyleSlot.Character, npc.Id, 1.75f);
                if (look != null) look.Update(m.Body.transform.position, false, 0f);
            }
            foreach (WorldMarker node in adventure.Nodes())
            {
                bool herb = node.Profession == "herboristerie";
                Marker m = Add(node, herb ? PrimitiveType.Sphere : PrimitiveType.Cube, herb ? new Vector3(0.7f, 0.5f, 0.7f) : new Vector3(0.8f, 0.7f, 0.8f),
                    herb ? 0.25f : 0.35f, herb ? new Color(0.35f, 0.8f, 0.35f) : new Color(0.55f, 0.55f, 0.6f), 1.2f, NodeLabelRange);
                _nodes[node.Id] = m;
            }
            foreach (WorldMarker safe in adventure.SafePoints())
                Add(safe, PrimitiveType.Cylinder, new Vector3(3f, 0.02f, 3f), 0.02f, new Color(0.3f, 0.55f, 1f), 0.6f, SafeLabelRange);
            // Essai du 12.9 : la forge (enclume sombre au centre de l'atelier), où l'on fabrique.
            foreach (WorldMarker workshop in adventure.Workshops())
                Add(workshop, PrimitiveType.Cube, new Vector3(1.2f, 0.8f, 0.7f), 0.4f, new Color(0.3f, 0.3f, 0.34f), 1.6f, NpcLabelRange);
        }

        private Marker Add(WorldMarker data, PrimitiveType shape, Vector3 scale, float y, Color color, float height, float range)
        {
            GameObject body = GameObject.CreatePrimitive(shape);
            body.name = data.Id;
            Object.Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(_root.transform, false);
            body.transform.position = new Vector3((float)data.X, y, (float)data.Z);
            body.transform.localScale = scale;
            body.GetComponent<Renderer>().sharedMaterial = ArenaView.MakeMaterial(color);
            var marker = new Marker { Data = data, Body = body, Height = height, Range = range, Color = color };
            _markers.Add(marker);
            return marker;
        }

        /// <summary>Nœuds récoltés cachés jusqu'à leur repousse.</summary>
        public void Sync(AdventureSession adventure)
        {
            foreach (WorldMarker node in adventure.Nodes())
                if (_nodes.TryGetValue(node.Id, out Marker m))
                {
                    m.Data = node;
                    m.Body.SetActive(node.Ready);
                }
        }

        /// <summary>Noms des repères proches du joueur (dans <c>OnGUI</c>, après <see cref="Hud.Begin"/>).</summary>
        public void Labels(Camera camera, Vector3 player)
        {
            foreach (Marker m in _markers)
            {
                if (!m.Body.activeSelf) continue;
                Vector3 at = m.Body.transform.position;
                float dx = at.x - player.x, dz = at.z - player.z;
                if (dx * dx + dz * dz > m.Range * m.Range) continue;
                Vector3 screen = camera.WorldToScreenPoint(new Vector3(at.x, m.Height, at.z));
                if (screen.z <= 0f) continue;
                float x = screen.x / Hud.Scale, y = (Screen.height - screen.y) / Hud.Scale;
                Hud.Centered(new Rect(x - 200f, y - 30f, 400f, 28f), Hud.Plain(m.Data.Name), Color.Lerp(m.Color, Color.white, 0.5f));
            }
        }

        public void Destroy() => Object.Destroy(_root);
    }
}

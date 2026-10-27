using System.Collections.Generic;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>Un ennemi à montrer sur la carte (en vue seulement : la carte ne montre pas ce que le Bastion ne voit pas).</summary>
    public struct MapEnemy
    {
        public float X, Z;
        public bool Boss;

        public MapEnemy(float x, float z, bool boss)
        {
            X = x;
            Z = z;
            Boss = boss;
        }
    }

    /// <summary>
    /// Sous-lot 12.9d (essai de Damien du 25 octobre 2026 : « une mini carte en haut à droite qu'on puisse agrandir si besoin ») : la carte
    /// de la zone, nord en haut. En petit en haut à droite (sous elle, la fiche de la cible passe à sa gauche) ; L, ou toucher la carte,
    /// l'ouvre en grand avec les noms des lieux, des PNJ et des points sûrs. L'image du décor est calculée par le cœur
    /// (<see cref="AdventureMap.Render"/>) une fois par session ; les repères sont redessinés à chaque image.
    /// </summary>
    public sealed class MiniMap
    {
        /// <summary>Cadre de la petite carte (écran virtuel de 1080 de haut), en haut à droite.</summary>
        public const float Width = 300f, Height = 210f, Margin = 14f;

        public static Rect MiniRect => new Rect(Hud.Width - Width - Margin, Margin, Width, Height);

        private const int TextureWidth = 512;
        private Texture2D _texture;

        /// <summary>Carte dont la texture est l'image : une autre carte (nouvelle session, ou nouvelle aventure après l'entraînement) la refait.</summary>
        private AdventureMap _source;

        private static readonly Color Frame = new Color(0f, 0f, 0f, 0.55f), You = new Color(1f, 0.9f, 0.2f), Goal = new Color(1f, 0.55f, 0.1f),
            Person = new Color(0.95f, 0.95f, 0.95f), Safe = new Color(0.35f, 0.9f, 0.45f), Forge = new Color(0.6f, 0.6f, 0.66f),
            Gather = new Color(0.75f, 0.85f, 0.6f), Enemy = new Color(0.95f, 0.2f, 0.15f), Chief = new Color(0.75f, 0.35f, 1f), Name = new Color(1f, 1f, 1f, 0.85f);

        private Texture2D Texture(AdventureSession a)
        {
            // Revue Codex de la PR #53 : la clé est la carte elle-même, pas le numéro de session (une nouvelle aventure repart à 1).
            AdventureMap map = a.Map;
            if (_texture != null && ReferenceEquals(_source, map)) return _texture;
            int height = System.Math.Max(1, (int)(TextureWidth * map.Depth / map.Width));
            if (_texture != null) Object.Destroy(_texture);
            _texture = new Texture2D(TextureWidth, height, TextureFormat.RGBA32, false);
            _texture.filterMode = FilterMode.Bilinear;
            _texture.LoadRawTextureData(map.Render(TextureWidth, height));
            _texture.Apply();
            _source = map;
            return _texture;
        }

        /// <summary>Rectangle de l'image dans <paramref name="box"/>, proportions de la zone gardées, centré.</summary>
        private static Rect Fit(Rect box, AdventureMap map)
        {
            float ratio = (float)(map.Width / map.Depth);
            float w = box.width, h = w / ratio;
            if (h > box.height)
            {
                h = box.height;
                w = h * ratio;
            }
            return new Rect(box.x + (box.width - w) / 2f, box.y + (box.height - h) / 2f, w, h);
        }

        private static Vector2 At(Rect image, AdventureMap map, double x, double z)
        {
            (double u, double v) = map.ToMap(x, z);
            return new Vector2(image.x + (float)u * image.width, image.y + (float)v * image.height);
        }

        private static void Dot(Vector2 p, float size, Color color) => Hud.Fill(new Rect(p.x - size / 2f, p.y - size / 2f, size, size), color);

        /// <summary>La petite carte, en haut à droite.</summary>
        public void DrawMini(AdventureSession a, IReadOnlyList<MapEnemy> enemies)
        {
            Rect box = MiniRect;
            Hud.Panel(new Rect(box.x - 4f, box.y - 4f, box.width + 8f, box.height + 8f), Frame);
            Draw(a, box, enemies, false);
        }

        /// <summary>La carte en grand, dans <paramref name="box"/>, avec les noms et la légende.</summary>
        public void DrawLarge(AdventureSession a, Rect box, IReadOnlyList<MapEnemy> enemies)
        {
            Draw(a, new Rect(box.x, box.y, box.width, box.height - 40f), enemies, true);
            Hud.Small(new Rect(box.x, box.y + box.height - 34f, box.width, 30f), a.Text("text.ui.map.legend"));
        }

        private void Draw(AdventureSession a, Rect box, IReadOnlyList<MapEnemy> enemies, bool large)
        {
            AdventureMap map = a.Map;
            Rect image = Fit(box, map);
            GUI.DrawTexture(image, Texture(a));
            float scale = large ? 2f : 1f;
            if (large)
                foreach (MapPlace place in map.Places)
                {
                    Vector2 c = At(image, map, place.CenterX, place.CenterZ);
                    Hud.Centered(new Rect(c.x - 120f, c.y - 14f, 240f, 28f), Hud.Plain(place.Name), new Color(1f, 1f, 1f, 0.45f));
                }
            foreach (MapEnemy e in enemies) Dot(At(image, map, e.X, e.Z), (e.Boss ? 9f : 6f) * scale, e.Boss ? Chief : Enemy);
            MapMarker player = null, goal = null;
            foreach (MapMarker m in a.MapMarkers())
            {
                Vector2 p = At(image, map, m.X, m.Z);
                switch (m.Kind)
                {
                    case MapMarkerKind.Player: player = m; continue;
                    case MapMarkerKind.Objective: goal = m; continue;
                    case MapMarkerKind.Node: Dot(p, 3f * scale, Gather); continue;
                    case MapMarkerKind.Npc: Dot(p, 6f * scale, Person); break;
                    case MapMarkerKind.SafePoint: Dot(p, 7f * scale, Safe); break;
                    case MapMarkerKind.Workshop: Dot(p, 7f * scale, Forge); break;
                }
                if (large) Hud.Centered(new Rect(p.x - 120f, p.y + 8f, 240f, 26f), Hud.Plain(m.Name), Name);
            }
            if (goal != null)
            {
                Vector2 g = At(image, map, goal.X, goal.Z);
                Hud.Centered(new Rect(g.x - 20f * scale, g.y - 16f * scale, 40f * scale, 32f * scale), "★", Goal);
                if (large) Hud.Centered(new Rect(g.x - 120f, g.y - 44f, 240f, 26f), Hud.Plain(goal.Name), Goal);
            }
            if (player != null)
            {
                Vector2 me = At(image, map, player.X, player.Z);
                Dot(me, 10f * scale, Color.black);
                Dot(me, 7f * scale, You);
            }
            if (!large) Hud.Small(new Rect(box.x, box.y + box.height + 4f, box.width, 24f), a.Text("text.ui.map.hint"));
        }
    }
}

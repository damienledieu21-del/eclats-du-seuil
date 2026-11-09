using System.Collections.Generic;
using EdS.Data.Merchants;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.14 (décision de Damien du 9 novembre 2026) : l'affichage des vraies boutiques et du grand jour de marché, dehors, au
    /// bourg : enseignes de bois pendues à côté des portes (leur nom et « ouverte » ou « fermée » sont des étiquettes des repères), fumée de
    /// la cheminée du fournil quand le four chauffe (effet de fumée du pack, sinon des bouffées faites par le code), étals de la place
    /// (tente et table garnie du pack RPG Food Props si présents, sinon une table et un auvent de couleur faits par le code) qui
    /// s'installent le matin et se démontent le soir : la structure à l'installation et au démontage, les marchandises pendant le marché,
    /// rien le reste de la semaine. Affichage seulement : le cœur reste maître des horaires, des stocks et des prix.
    /// </summary>
    public sealed class ShopsView
    {
        private sealed class Puff
        {
            public Transform Body;
            public float Age;
        }

        private sealed class Chimney
        {
            public string Id;
            public Vector3 Top;
            public GameObject Pack;
            public readonly List<Puff> Puffs = new List<Puff>();
        }

        private readonly GameObject _root;
        private readonly List<GameObject> _structure = new List<GameObject>();
        private readonly List<GameObject> _goods = new List<GameObject>();
        private readonly List<Chimney> _chimneys = new List<Chimney>();
        private readonly List<Material> _owned = new List<Material>();
        private MarketPhase _shown = (MarketPhase)(-1);
        private Material _smoke;

        /// <summary>Hauteur (m) d'une cheminée de maison du bourg (estimée : le modèle de la maison varie).</summary>
        private const float ChimneyHeight = 7.2f;

        /// <summary>Durée de vie (s) d'une bouffée de fumée faite par le code ; bouffées au plus par cheminée.</summary>
        private const float PuffLife = 6f;
        private const int MaxPuffs = 8;

        public ShopsView(Transform parent, AdventureSession adventure, List<IReadOnlyList<(double X, double Z)>> obstacles)
        {
            _root = new GameObject("Boutiques et marché (V2.14)");
            _root.transform.SetParent(parent, false);
            if (adventure == null || adventure.Indoors) return;
            foreach (WorldMarker sign in adventure.ShopSigns()) Signboard(sign);
            foreach (ShopChimney c in adventure.Chimneys())
            {
                var chimney = new Chimney { Id = c.Id, Top = new Vector3((float)c.X, ChimneyHeight, (float)c.Z) };
                chimney.Pack = Effects.Ambient(StyleVfx.ForgeSmoke, _root.transform, chimney.Top);
                _chimneys.Add(chimney);
            }
            foreach (MarketStallView stall in adventure.MarketStalls()) Stall(stall, obstacles);
            Sync(adventure, 0f);
        }

        /// <summary>
        /// Sous-lot V2.14 (décision de Damien : la forge de Maëlle, un vrai atelier couvert) : sur chaque atelier, une charpente de bois (six
        /// poteaux, deux sablières) et un toit d'appentis de planches, ouvert sur les côtés pour la fumée du foyer.
        /// </summary>
        public void Workshops(IReadOnlyList<WorldMarker> workshops)
        {
            if (workshops == null) return;
            Material wood = Made(new Color(0.36f, 0.24f, 0.14f));
            Material roof = Made(new Color(0.33f, 0.26f, 0.2f));
            foreach (WorldMarker w in workshops)
            {
                var holder = new GameObject(w.Id + " (atelier couvert)");
                holder.transform.SetParent(_root.transform, false);
                holder.transform.position = new Vector3((float)w.X, 0f, (float)w.Z);
                const float width = 6f, depth = 4.6f, low = 2.6f, high = 3.6f;
                for (int i = 0; i < 3; i++)
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float x = (i - 1) * width / 2f, z = s * depth / 2f;
                        float h = s < 0 ? low : high;
                        Child(holder, "poteau", new Vector3(x, h / 2f, z), new Vector3(0.18f, h, 0.18f), wood);
                    }
                Child(holder, "sablière basse", new Vector3(0f, low, -depth / 2f), new Vector3(width + 0.3f, 0.16f, 0.16f), wood);
                Child(holder, "sablière haute", new Vector3(0f, high, depth / 2f), new Vector3(width + 0.3f, 0.16f, 0.16f), wood);
                float slope = Mathf.Atan2(high - low, depth) * Mathf.Rad2Deg;
                GameObject cover = Child(holder, "toit de planches", new Vector3(0f, (low + high) / 2f + 0.12f, 0f), new Vector3(width + 0.8f, 0.08f, depth + 0.9f), roof);
                cover.transform.localRotation = Quaternion.Euler(-slope, 0f, 0f);
            }
        }

        /// <summary>Enseigne : potence de fer et planche de bois peinte, à 2,6 m, le long de la façade.</summary>
        private void Signboard(WorldMarker sign)
        {
            var at = new Vector3((float)sign.X, 0f, (float)sign.Z);
            Material wood = Made(new Color(0.42f, 0.27f, 0.14f));
            Material iron = Made(new Color(0.12f, 0.11f, 0.1f));
            Material paint = Made(new Color(0.78f, 0.62f, 0.3f));
            Block(sign.Id + " (potence)", at + new Vector3(0f, 2.95f, 0f), new Vector3(0.06f, 0.06f, 0.9f), iron);
            Block(sign.Id + " (planche)", at + new Vector3(0f, 2.55f, 0.2f), new Vector3(0.06f, 0.55f, 0.9f), wood);
            Block(sign.Id + " (peinture)", at + new Vector3(0.035f, 2.55f, 0.2f), new Vector3(0.01f, 0.36f, 0.66f), paint);
        }

        /// <summary>
        /// Un étal : celui du pack (tente si l'ordinateur le permet, table garnie et caisses), sinon une table, un auvent de la couleur du
        /// genre d'étal et des marchandises faites par le code.
        /// </summary>
        private void Stall(MarketStallView stall, List<IReadOnlyList<(double X, double Z)>> obstacles)
        {
            Transform root = _root.transform;
            int before = root.childCount;
            GameObject packed = WorldMarkers.MarketStall(root, stall.X, stall.Z, (float)stall.Face, obstacles, !Application.isMobilePlatform, "étal " + stall.Id);
            if (packed != null)
            {
                for (int i = before; i < root.childCount; i++)
                {
                    GameObject g = root.GetChild(i).gameObject;
                    (g.name.Contains("victuailles") ? _goods : _structure).Add(g);
                }
                return;
            }
            var holder = new GameObject("étal " + stall.Id + " (forme simple)");
            holder.transform.SetParent(root, false);
            holder.transform.position = new Vector3((float)stall.X, 0f, (float)stall.Z);
            holder.transform.rotation = Quaternion.Euler(0f, (float)stall.Face, 0f);
            Material wood = Made(new Color(0.46f, 0.32f, 0.19f));
            Material awning = Made(AwningColor(stall.Kind));
            Child(holder, "plateau", new Vector3(0f, 0.85f, 0f), new Vector3(1.8f, 0.06f, 0.8f), wood);
            foreach ((float sx, float sz) in new[] { (1f, 1f), (1f, -1f), (-1f, 1f), (-1f, -1f) })
            {
                Child(holder, "pied", new Vector3(sx * 0.82f, 0.42f, sz * 0.34f), new Vector3(0.07f, 0.84f, 0.07f), wood);
                Child(holder, "montant", new Vector3(sx * 0.86f, 1.15f, sz * 0.38f - 0.05f), new Vector3(0.05f, 2.3f, 0.05f), wood);
            }
            Child(holder, "auvent", new Vector3(0f, 2.3f, 0.05f), new Vector3(2f, 0.04f, 1.1f), awning).transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            _structure.Add(holder);
            // Les marchandises : un tas par genre (couleurs), posé sur le plateau.
            var goods = new GameObject("étal " + stall.Id + " (marchandises)");
            goods.transform.SetParent(root, false);
            goods.transform.position = holder.transform.position;
            goods.transform.rotation = holder.transform.rotation;
            Color[] colors = GoodsColors(stall.Kind);
            for (int i = 0; i < 6; i++)
            {
                GameObject item = GameObject.CreatePrimitive(i % 2 == 0 ? PrimitiveType.Sphere : PrimitiveType.Cube);
                item.name = "marchandise";
                Object.Destroy(item.GetComponent<Collider>());
                item.transform.SetParent(goods.transform, false);
                item.transform.localPosition = new Vector3(-0.65f + i * 0.26f, 0.98f, (i % 3 - 1) * 0.18f);
                item.transform.localScale = Vector3.one * (0.16f + 0.02f * (StyleRules.Variant(stall.Id + i, 4)));
                item.GetComponent<Renderer>().sharedMaterial = Made(colors[i % colors.Length]);
            }
            _goods.Add(goods);
        }

        private static Color AwningColor(string kind)
        {
            switch (kind)
            {
                case "cheese": return new Color(0.85f, 0.75f, 0.35f);
                case "spices": case "exotic": return new Color(0.7f, 0.25f, 0.15f);
                case "cloth": return new Color(0.25f, 0.35f, 0.6f);
                case "flowers": return new Color(0.75f, 0.45f, 0.6f);
                case "honey": return new Color(0.9f, 0.62f, 0.15f);
                default: return new Color(0.82f, 0.8f, 0.7f);
            }
        }

        private static Color[] GoodsColors(string kind)
        {
            switch (kind)
            {
                case "cheese": return new[] { new Color(0.95f, 0.85f, 0.5f), new Color(0.9f, 0.78f, 0.4f) };
                case "spices": return new[] { new Color(0.8f, 0.35f, 0.1f), new Color(0.9f, 0.7f, 0.15f), new Color(0.45f, 0.2f, 0.1f) };
                case "exotic": return new[] { new Color(1f, 0.55f, 0.05f), new Color(0.2f, 0.55f, 0.2f), new Color(0.85f, 0.15f, 0.1f) };
                case "flowers": return new[] { new Color(0.9f, 0.3f, 0.4f), new Color(0.95f, 0.9f, 0.3f), new Color(0.5f, 0.4f, 0.9f) };
                case "pottery": return new[] { new Color(0.65f, 0.38f, 0.22f), new Color(0.55f, 0.32f, 0.2f) };
                case "cloth": return new[] { new Color(0.55f, 0.18f, 0.16f), new Color(0.2f, 0.32f, 0.52f), new Color(0.82f, 0.76f, 0.6f) };
                case "honey": return new[] { new Color(0.95f, 0.65f, 0.1f), new Color(0.9f, 0.85f, 0.6f) };
                case "meat": return new[] { new Color(0.95f, 0.93f, 0.85f), new Color(0.7f, 0.45f, 0.3f) };
                default: return new[] { new Color(0.6f, 0.5f, 0.35f) };
            }
        }

        /// <summary>
        /// À chaque image : étals montrés selon la phase de la place (structure à l'installation et au démontage, tout pendant le marché,
        /// rien sinon) ; fumée des cheminées dont le four chauffe.
        /// </summary>
        public void Sync(AdventureSession adventure, float deltaTime)
        {
            if (adventure == null) return;
            MarketPhase phase = adventure.MarketPhaseNow;
            if (phase != _shown)
            {
                _shown = phase;
                foreach (GameObject g in _structure) if (g != null) g.SetActive(phase != MarketPhase.Calm);
                foreach (GameObject g in _goods) if (g != null) g.SetActive(phase == MarketPhase.Open);
            }
            if (_chimneys.Count == 0) return;
            IReadOnlyList<ShopChimney> now = adventure.Chimneys();
            foreach (Chimney c in _chimneys)
            {
                bool lit = false;
                foreach (ShopChimney s in now) if (s.Id == c.Id) lit = s.Lit;
                if (c.Pack != null)
                {
                    if (c.Pack.activeSelf != lit) c.Pack.SetActive(lit);
                    continue;
                }
                Smoke(c, lit, deltaTime);
            }
        }

        /// <summary>Fumée faite par le code : des bouffées grises qui montent, grossissent et s'effacent.</summary>
        private void Smoke(Chimney c, bool lit, float deltaTime)
        {
            if (lit && deltaTime > 0f && c.Puffs.Count < MaxPuffs && (c.Puffs.Count == 0 || c.Puffs[c.Puffs.Count - 1].Age > PuffLife / MaxPuffs))
            {
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                body.name = c.Id + " (fumée)";
                Object.Destroy(body.GetComponent<Collider>());
                body.transform.SetParent(_root.transform, false);
                body.transform.position = c.Top;
                if (_smoke == null) _smoke = Made(new Color(0.72f, 0.72f, 0.74f));   // une seule matière pour toutes les bouffées
                body.GetComponent<Renderer>().sharedMaterial = _smoke;
                c.Puffs.Add(new Puff { Body = body.transform });
            }
            for (int i = c.Puffs.Count - 1; i >= 0; i--)
            {
                Puff p = c.Puffs[i];
                p.Age += deltaTime;
                if (p.Age >= PuffLife || p.Body == null)
                {
                    if (p.Body != null) Object.Destroy(p.Body.gameObject);
                    c.Puffs.RemoveAt(i);
                    continue;
                }
                float t = p.Age / PuffLife;
                p.Body.position = c.Top + new Vector3(0.5f * t, 2.2f * t, 0.3f * Mathf.Sin(t * 3f));
                p.Body.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.2f, t) * (1f - t * 0.6f);
            }
        }

        private GameObject Block(string name, Vector3 center, Vector3 size, Material material)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(_root.transform, false);
            g.transform.position = center;
            g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = material;
            return g;
        }

        private static GameObject Child(GameObject parent, string name, Vector3 local, Vector3 size, Material material)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent.transform, false);
            g.transform.localPosition = local;
            g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = material;
            return g;
        }

        /// <summary>Matériau fait pour l'affichage, détruit avec lui.</summary>
        private Material Made(Color color)
        {
            Material m = ArenaView.MakeMaterial(color);
            _owned.Add(m);
            return m;
        }

        public void Destroy()
        {
            Object.Destroy(_root);
            foreach (Material m in _owned)
                if (m != null) Object.Destroy(m);
            _owned.Clear();
        }
    }
}

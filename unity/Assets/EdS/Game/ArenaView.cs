using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Combat;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.Simulation.Engine;
using EdS.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot 9.3 : arène construite par le code (graphismes provisoires : formes simples). Le joueur est une capsule bleue avec un
    /// bouclier visible en garde (doré pendant la fenêtre de parade) ; les assaillants sont des capsules rouges, le mannequin un
    /// cylindre, un allié une capsule verte ; la cible est cerclée de jaune, l'allié visé par les soutiens de vert (lot 10, à deux). Les
    /// positions sont purement visuelles : le cœur n'a pas de système de positions. Sert au combat solo comme au combat en réseau
    /// (<see cref="ICombatClient"/>, sous-lot 10.4). Sous-lot 11.6 : dans un combat positionné, le décor vient de l'espace du scénario
    /// (<see cref="SpaceView"/>), chaque combattant est à la place que lui donne le moteur (entre deux ticks, l'affichage glisse de la
    /// place précédente à la suivante), avec son orientation et sa taille ; un ennemi hors de vue du joueur n'est pas affiché.
    /// </summary>
    public sealed class ArenaView
    {
        public const float Radius = 14f;

        private readonly GameObject _root;
        private readonly Dictionary<string, GameObject> _bodies = new Dictionary<string, GameObject>();

        /// <summary>Sous-lot V2.9.2 : oursons (décor) posés près de leur ourse, par ourse ; détruits au prochain Populate.</summary>
        private readonly Dictionary<string, GameObject> _cubs = new Dictionary<string, GameObject>();

        /// <summary>
        /// Revue Codex de la PR #117 (tour 3) : ourses habillées dont les oursons attendent la vraie place de leur mère (au moment de
        /// l'habillage, le corps est encore sur une place provisoire ou à l'origine) : ils sont posés au premier Place où elle est là.
        /// </summary>
        private readonly HashSet<string> _cubsPending = new HashSet<string>();
        private readonly Dictionary<string, Renderer> _renderers = new Dictionary<string, Renderer>();

        /// <summary>Sous-lot V1.9.8 : matériaux des formes qui brillent (<see cref="Glow"/>), détruits au prochain <see cref="Populate"/>.</summary>
        private readonly Dictionary<string, Material> _glowing = new Dictionary<string, Material>();
        private readonly Dictionary<string, Color> _baseColors = new Dictionary<string, Color>();
        private GameObject _player;
        private string _playerId;

        /// <summary>Essai de style : corps habillés d'un modèle (aucun sans catalogue).</summary>
        private readonly Dictionary<string, StyleBody> _looks = new Dictionary<string, StyleBody>();

        /// <summary>Sous-lot V2.4 : bêtes paisibles dessinées en formes simples (cerfs, lièvres), par combattant.</summary>
        private readonly Dictionary<string, WildlifeLook> _wild = new Dictionary<string, WildlifeLook>();

        /// <summary>Sous-lot V2.9 : voix des bêtes (hurlements, brame, grognements, lièvre qui détale), faites au premier besoin.</summary>
        private BeastVoices _voices;

        // Sous-lot V2.9.1 : échos fantômes (shader « EdS/Echo »), la nuit lissée et les cicatrices du Seuil qui les rendent plus forts.
        private readonly EchoGhosts _ghosts = new EchoGhosts();
        private float _night;
        private float _nightTarget;
        private IReadOnlyList<ScarView> _scarList;

        /// <summary>Sous-lot V2.9.1 : les effets en particules (braises et traînée des échos) ; null : sans particules.</summary>
        public Effects Effects { get; set; }
        private GameObject _shield;
        private Renderer _shieldRenderer;
        private GameObject _targetRing;
        private GameObject _allyRing;
        private Material _shieldGuard;
        private Material _shieldParry;
        private Material _shieldBroken;

        // Sous-lot 11.6 : combat positionné (null sinon) ; places du tick précédent et du tick affiché, pour glisser entre les deux.
        private SpaceView _space;
        private IReadOnlyList<ConvexPolygon> _obstacles;
        private GameObject _defaultGround;
        private readonly Dictionary<string, Vector3> _from = new Dictionary<string, Vector3>();
        private readonly Dictionary<string, Vector3> _to = new Dictionary<string, Vector3>();
        private long _shownTick = -1;

        /// <summary>Matériau modèle emporté par la construction (nom partagé avec <c>EdsBuildShaders.TemplateName</c>).</summary>
        public const string TemplateName = "EdsCouleur";

        private static Material _template;
        private static bool _templateLoaded;

        public Transform Player => _player != null ? _player.transform : null;

        /// <summary>Sous-lot V1.2 : l'équipement visible du joueur (arme, bouclier, cape).</summary>
        private GearLook _gear;

        /// <summary>Sous-lot V1.2 : montre l'arme, le bouclier et la cape portés par le joueur (rien ne se refait s'ils n'ont pas changé).</summary>
        public void ShowGear(VisibleGear gear) => _gear?.Show(gear);

        /// <summary>
        /// Sous-lot 12.8 : couleur d'un combattant choisie par le jeu (le chef d'une zone, par exemple) ; null : couleur ordinaire (ennemi
        /// rouge, allié vert, mannequin brun).
        /// </summary>
        public System.Func<string, Color?> Paint { get; set; }

        /// <summary>Combat positionné affiché (décor du scénario, places du moteur).</summary>
        public bool Spatial => _space != null;

        public ArenaView()
        {
            _root = new GameObject("Arène");
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ground.name = "Sol";
            ground.transform.SetParent(_root.transform, false);
            ground.transform.localScale = new Vector3(Radius * 2f + 2f, 0.05f, Radius * 2f + 2f);
            ground.transform.position = new Vector3(0f, -0.05f, 0f);
            ground.GetComponent<Renderer>().sharedMaterial = MakeMaterial(new Color(0.22f, 0.23f, 0.26f));
            _defaultGround = ground;

            if (Object.FindFirstObjectByType<Light>() == null)
            {
                var sun = new GameObject("Soleil");
                sun.transform.SetParent(_root.transform, false);
                Light light = sun.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                light.shadows = Application.isMobilePlatform ? LightShadows.None : LightShadows.Soft;   // sous-lot 11.8 : allégé sur téléphone
                sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            _shieldGuard = MakeMaterial(new Color(0.65f, 0.7f, 0.78f));
            _shieldParry = MakeMaterial(new Color(1f, 0.82f, 0.25f));
            _shieldBroken = MakeMaterial(new Color(0.75f, 0.15f, 0.12f));
        }

        /// <summary>
        /// Matériau opaque compatible avec le rendu actif (URP si configuré, sinon le rendu intégré). Correctif du sous-lot 11.8 : dans un
        /// jeu construit, il copie le matériau modèle emporté par la construction (<c>Resources/EdsCouleur</c>, voir
        /// <c>EdsBuildShaders</c>), seul moyen d'avoir le shader sur le téléphone ; dans l'éditeur, il le cherche par son nom comme avant.
        /// Sans shader, une erreur claire plutôt qu'un matériau vide.
        /// </summary>
        public static Material MakeMaterial(Color color)
        {
            if (!_templateLoaded)
            {
                _template = Resources.Load<Material>(TemplateName);
                _templateLoaded = true;
            }
            Material material;
            if (_template != null) material = new Material(_template);
            else
            {
                Shader shader = null;
                if (GraphicsSettings.currentRenderPipeline != null) shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                    throw new System.InvalidOperationException("aucun shader de couleur dans le jeu (URP Lit ou Standard) : reconstruire le jeu depuis "
                        + "l'éditeur, rendu URP configuré.");
                material = new Material(shader);
            }
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            return material;
        }

        /// <summary>
        /// Remplace les combattants affichés par ceux d'un nouveau combat. Vallons agrandis : <paramref name="map"/> (carte de l'aventure) donne
        /// les routes et les champs du décor ; un nouveau combat sur le même espace garde le décor (<see cref="SpaceView.Key"/>).
        /// </summary>
        public void Populate(ICombatClient combat, AdventureMap map = null)
        {
            foreach (GameObject body in _bodies.Values) Object.Destroy(body);
            _bodies.Clear();
            foreach (GameObject den in _cubs.Values) if (den != null) Object.Destroy(den);
            _cubs.Clear();
            _cubsPending.Clear();
            _looks.Clear();
            _wild.Clear();
            _voices?.Clear();
            _renderers.Clear();
            foreach (Material material in _glowing.Values)
                if (material != null) Object.Destroy(material);
            _glowing.Clear();
            foreach (Material material in _echoMaterials.Values)
                if (material != null) Object.Destroy(material);
            _echoMaterials.Clear();
            _ghosts.Clear();
            _baseColors.Clear();
            if (_player != null) Object.Destroy(_player);
            if (_targetRing != null) Object.Destroy(_targetRing);
            if (_allyRing != null) Object.Destroy(_allyRing);
            if (_space != null && combat.Space != null && _space.Key == SpaceView.KeyOf(combat.Space, map)) _space.Reset();
            else
            {
                if (_space != null) _space.Destroy();
                _space = combat.Space != null ? new SpaceView(combat.Space, _root.transform, map) : null;
            }
            _obstacles = CombatDisplay.SightObstacles(combat.Space);   // sous-lot 11.9 : la vue (obstacles bas exclus)
            _defaultGround.SetActive(_space == null);
            _from.Clear();
            _to.Clear();
            _shownTick = -1;

            _player = Body(combat.PlayerId, PrimitiveType.Capsule, new Color(0.25f, 0.45f, 0.95f), Vector3.zero);
            _playerId = combat.PlayerId;
            Fit(_player, combat.Player().View);
            Look(combat.PlayerId, _player, true);
            _gear = new GearLook(_player.transform, _looks.ContainsKey(combat.PlayerId));   // sous-lot V1.2 : arme, bouclier et cape visibles
            _shield = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _shield.name = "Bouclier";
            Object.Destroy(_shield.GetComponent<Collider>());
            _shield.transform.SetParent(_player.transform, false);
            _shield.transform.localPosition = new Vector3(0f, 0.1f, 0.55f);
            _shield.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _shield.transform.localScale = new Vector3(0.9f, 0.04f, 1.1f);
            _shieldRenderer = _shield.GetComponent<Renderer>();
            _shield.SetActive(false);

            List<OtherState> others = combat.Others().ToList();
            for (int i = 0; i < others.Count; i++)
            {
                OtherState other = others[i];
                // Ennemis en arc de cercle devant le joueur, alliés à ses côtés.
                float spread = others.Count == 1 ? 0f : Mathf.Lerp(-40f, 40f, i / (float)(others.Count - 1));
                Vector3 position = other.IsEnemy
                    ? Quaternion.Euler(0f, spread, 0f) * new Vector3(0f, 0f, 6f)
                    : new Vector3(2.5f * (i % 2 == 0 ? -1f : 1f), 0f, -1.5f);
                GameObject body = Body(other, position);
                body.transform.LookAt(new Vector3(0f, body.transform.position.y, 0f));
            }
            if (_space != null) Place(combat, true);

            _targetRing = Ring("Cible", 0.85f, new Color(1f, 0.85f, 0.42f), new Color(1f, 0.6f, 0.75f));
            _allyRing = Ring("Allié visé", 0.75f, new Color(0.56f, 0.96f, 0.91f), new Color(0.75f, 1f, 0.95f));
            _allyRing.SetActive(false);
        }

        /// <summary>
        /// Anneau au sol sous la cible ou l'allié visé. Essai réaliste 6 (« Fragments du Seuil ») : un cercle de lumière (shader des
        /// annonces : bord lumineux et ondes qui s'éloignent, aux couleurs de l'aube) ; sans ce shader, le disque plat d'avant.
        /// </summary>
        private static readonly Dictionary<string, (Mesh Mesh, Material Material)> RingLooks = new Dictionary<string, (Mesh Mesh, Material Material)>();

        private GameObject Ring(string name, float radius, Color color, Color edge)
        {
            Material template = Annonces.Template;
            if (template == null)
            {
                GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.name = name;
                Object.Destroy(disc.GetComponent<Collider>());
                disc.transform.SetParent(_root.transform, false);
                disc.transform.localScale = new Vector3(2f * radius, 0.01f, 2f * radius);
                disc.GetComponent<Renderer>().sharedMaterial = MakeMaterial(color);
                return disc;
            }
            var ring = new GameObject(name);
            ring.transform.SetParent(_root.transform, false);
            // Forme et matériau faits une fois par anneau (le combat suivant les reprend).
            if (!RingLooks.TryGetValue(name, out (Mesh Mesh, Material Material) look))
            {
                look.Mesh = new Mesh();
                Annonces.Fill(look.Mesh, TelegraphShapes.Circle(0, 0, radius), false);
                look.Material = new Material(template);
                look.Material.SetColor("_Color", color);
                look.Material.SetColor("_Hot", edge);
                look.Material.SetFloat("_Progress", 0f);
                look.Material.SetFloat("_Opacity", 0.9f);
                RingLooks[name] = look;
            }
            ring.AddComponent<MeshFilter>().sharedMesh = look.Mesh;
            MeshRenderer renderer = ring.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = look.Material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ring;
        }

        /// <summary>Corps d'un autre combattant (forme et couleur selon son genre et son camp), à sa taille.</summary>
        private GameObject Body(OtherState other, Vector3 position)
        {
            PrimitiveType shape = other.View.Kind == CombatantKind.Dummy ? PrimitiveType.Cylinder : PrimitiveType.Capsule;
            Color color = Paint?.Invoke(other.Id) ?? (!other.IsEnemy ? new Color(0.3f, 0.8f, 0.4f)
                : other.View.Kind == CombatantKind.Dummy ? new Color(0.6f, 0.45f, 0.3f)
                : new Color(0.85f, 0.2f, 0.2f));
            GameObject body = Body(other.Id, shape, color, position);
            Fit(body, other.View);
            Look(other.Id, body, false);
            return body;
        }

        /// <summary>
        /// Essai de style : habille le corps s'il a un modèle (le chevalier, les loups, les sangliers ; depuis l'essai réaliste, l'archer, le
        /// rôdeur et le Gardien s'ils ont leur personnage Mixamo, à la hauteur réglée dans le catalogue).
        /// </summary>
        private void Look(string id, GameObject body, bool player)
        {
            StyleSlot slot = StyleRules.ForCombatant(id, player);
            // Sous-lot V1.9.8 : Croc-Gris est un loup bien plus grand que les autres (StyleRules.BeastHeight).
            StyleBody look = StyleLook.Dress(body, slot, id, slot == StyleSlot.Hero || slot == StyleSlot.Character ? 1.8f : (float)StyleRules.BeastHeight(id));
            if (look != null) _looks[id] = look;
            if (look != null && !player && StyleRules.Cubs(id) > 0 && !_cubs.ContainsKey(id)) _cubsPending.Add(id);
            // Sous-lot V2.4 : un cerf ou un lièvre sans modèle prend sa forme simple (corps, pattes, bois ou oreilles). Revue Codex
            // de la PR #115 : avant l'habit d'écho, pour qu'un gibier sorti du Seuil soit fantôme tout entier (pas seulement sa capsule cachée).
            if (!player && look == null && Wildlife.KindOf(id) is WildlifeKind kind && kind != WildlifeKind.None) _wild[id] = new WildlifeLook(body, kind);
            // Sous-lot V2.1 : les échos de la nuit, pâles ; V2.9.1 : tous les échos (EchoLooks.Of), fantômes si le shader des échos est là.
            // Décision de Damien du 7 novembre 2026 : est un écho ce qui revient après sa mort ou ce qui sort du Seuil.
            EchoKind echo = player ? EchoKind.None
                : EchoLooks.Of(id, _fromSeuil != null && _fromSeuil.Contains(id), _slainBefore != null && _slainBefore(id),
                    _afterDeath != null && _afterDeath(id));
            if (echo != EchoKind.None && !_ghosts.Dress(id, body, echo, look != null ? look.Height : 1.6f)) Echo(body);
        }

        /// <summary>
        /// Sous-lot V2.9.2 (décision de Damien du 8 novembre 2026 : « une ourse avec ses oursons, à ne pas déranger ») : deux oursons du
        /// modèle de l'ours, petits, posés à 3 m de leur mère à son arrivée ; décor seulement (jamais des combattants : on ne les tue pas) ; ils
        /// jouent leur attente, s'assoient ou se couchent, chacun à son moment. Une seule fois par ourse et par combat affiché.
        /// </summary>
        private void Cubs(string id, Vector3 mother)
        {
            int count = StyleRules.Cubs(id);
            StyleCatalog catalog = StyleCatalog.Loaded;
            if (count <= 0 || _cubs.ContainsKey(id) || catalog == null || catalog.Bear == null) return;
            var den = new GameObject("Oursons de " + id);
            den.transform.SetParent(_root.transform, false);
            _cubs[id] = den;
            for (int i = 0; i < count; i++)
            {
                GameObject cub = StyleLook.Fit(catalog.Bear, 0.75f + 0.08f * i, 0f, null, false, out float bottom, out GameObject _, true);
                float k = cub.transform.localScale.y;
                cub.name = "Ourson " + (i + 1);
                cub.transform.SetParent(den.transform, false);
                (double cx, double cz) = StyleRules.CubSpot(mother.x, mother.z, i);
                var at = new Vector3((float)cx, 0f, (float)cz);
                cub.transform.position = new Vector3(at.x, StyleWorld.StandHeight(at.x, at.z) - bottom * k, at.z);
                cub.transform.rotation = Quaternion.Euler(0f, 40f + 150f * i, 0f);
                Animation animation = cub.GetComponentInChildren<Animation>();
                if (animation == null) continue;
                string pose = null;
                foreach (AnimationState state in animation)
                {
                    int bar = state.name.LastIndexOf('|');
                    string tail = StyleRules.Key(bar >= 0 ? state.name.Substring(bar + 1) : state.name);
                    state.wrapMode = WrapMode.Loop;
                    if (tail == (i == 0 ? "sit" : "idle2") || (pose == null && tail == "idle")) pose = state.name;
                }
                if (pose == null) continue;
                animation.Play(pose);
                AnimationState playing = animation[pose];
                if (playing != null) playing.time = playing.length * (0.3f + 0.4f * i);
            }
        }

        /// <summary>
        /// Sous-lot V2.1 : matériaux pâlis des échos, un par matériau d'origine, partagés par tous les échos (relecture de la PR #99 : pas une
        /// copie par écho qui revient) ; détruits au prochain Populate.
        /// </summary>
        private readonly Dictionary<Material, Material> _echoMaterials = new Dictionary<Material, Material>();

        /// <summary>
        /// Sous-lot V2.1 (MONDE.md : « une bête qu'on tue et qui revient, mon grand-père appelait ça un écho ») : un écho est un loup pâle,
        /// bleuté et faiblement lumineux, qu'on reconnaît de loin la nuit. Sans asset : les couleurs de son modèle (ou de sa forme simple).
        /// </summary>
        private void Echo(GameObject body)
        {
            var pale = new Color(0.72f, 0.86f, 1f);
            foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;   // les matériaux d'origine, jamais copiés ici
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null) continue;
                    if (_echoMaterials.TryGetValue(source, out Material shared) && shared != null)
                    {
                        materials[i] = shared;
                        continue;
                    }
                    var m = new Material(source);
                    _echoMaterials[source] = m;
                    materials[i] = m;
                    string color = m.HasProperty("_BaseColor") ? "_BaseColor" : m.HasProperty("_Color") ? "_Color" : null;
                    if (color != null)
                    {
                        Color c = m.GetColor(color);
                        Color tinted = Color.Lerp(c, pale, 0.65f);
                        tinted.a = c.a;
                        m.SetColor(color, tinted);
                    }
                    if (m.HasProperty("_EmissionColor"))
                    {
                        m.EnableKeyword("_EMISSION");
                        m.SetColor("_EmissionColor", new Color(0.18f, 0.32f, 0.55f));
                    }
                }
                renderer.sharedMaterials = materials;
            }
        }

        /// <summary>
        /// Sous-lot V2.9.1 : avant <see cref="Sync"/>, la nuit (les échos y brillent plus) et les cicatrices du Seuil (null : aucune ; un écho
        /// sur une cicatrice est plus solide et plus vif, selon son palier).
        /// </summary>
        public void Echoes(bool night, IReadOnlyList<ScarView> scars, ICollection<string> fromSeuil = null, System.Func<string, bool> slainBefore = null,
            System.Func<string, bool> afterDeath = null)
        {
            _afterDeath = afterDeath;
            _nightTarget = night ? 1f : 0f;
            _scarList = scars;
            _fromSeuil = fromSeuil;
            _slainBefore = slainBefore;
        }

        // Décision de Damien du 7 novembre 2026 (PR #115) : les créatures sorties du Seuil (faille, échos rôdeurs) et les chefs déjà tués.
        private ICollection<string> _fromSeuil;
        private System.Func<string, bool> _slainBefore;
        private System.Func<string, bool> _afterDeath;   // revue Codex de la PR #115 : vie revenue après une vraie mort

        /// <summary>Sous-lot V2.9.1 : le combattant est-il un écho fantôme (il meurt en braises, sans la poussière d'une bête) ?</summary>
        public bool IsGhost(string id) => _ghosts.Has(id);

        private void SyncGhosts(ICombatClient combat)
        {
            _night = Mathf.MoveTowards(_night, _nightTarget, Time.deltaTime * 0.25f);   // quatre secondes de fondu
            _ghosts.BeginFrame();
            foreach (OtherState other in combat.Others())
            {
                if (!_ghosts.Has(other.Id) || !_bodies.TryGetValue(other.Id, out GameObject body) || body == null) continue;
                CombatantView v = other.View;
                // Relecture de la PR #115 : pas encore arrivé ou parti : il rejouera sa formation à son retour.
                if (!v.IsPresent || v.HasLeft)
                {
                    _ghosts.Absent(other.Id);
                    continue;
                }
                double health = v.MaxHealth is long max && max > 0 && v.Health is long h ? (double)h / max : 1.0;
                Vector3 at = body.transform.position;
                int stage = EchoLooks.ScarStageAt(_scarList, at.x, at.z);
                _ghosts.Update(other.Id, body, v.IsDead, health, stage, _night, Effects, Time.deltaTime);
            }
            _ghosts.EndFrame();
        }

        /// <summary>
        /// Sous-lot V1.9.8 : fait briller la forme simple d'un combattant (l'Écho de la Rupture, silhouette de lumière) ; sans effet sur un
        /// combattant habillé d'un modèle ou absent.
        /// </summary>
        public void Glow(string id, Color emission)
        {
            if (!_renderers.TryGetValue(id, out Renderer renderer) || renderer == null) return;
            if (_ghosts.Has(id)) return;   // relecture de la PR #115 : l'écho fantôme brille par son contour (matériau partagé gardé)
            // Relecture de la PR #96 : l'instance du matériau est gardée, et détruite au prochain Populate.
            if (!_glowing.TryGetValue(id, out Material material) || material == null)
            {
                material = renderer.material;
                _glowing[id] = material;
            }
            if (!material.HasProperty("_EmissionColor")) return;
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
        }

        /// <summary>Essai de style : le joueur lance une compétence (son modèle joue l'attaque).</summary>
        public void Strike()
        {
            if (_playerId != null && _looks.TryGetValue(_playerId, out StyleBody look)) look.Strike();
        }

        /// <summary>
        /// Essai réaliste 7 (demande de Damien du 28 octobre 2026 : « coups variés ») : le joueur lance la compétence
        /// <paramref name="skillId"/> ; son modèle joue le geste de cette compétence.
        /// </summary>
        public void Strike(string skillId, bool ultimate)
        {
            if (_playerId != null && _looks.TryGetValue(_playerId, out StyleBody look)) look.Strike(skillId, ultimate);
        }

        /// <summary>Essai réaliste 7 : le combattant <paramref name="id"/> reçoit un coup, gardé ou non (son modèle réagit s'il le peut).</summary>
        public void Hurt(string id, bool guarded)
        {
            if (id != null && _looks.TryGetValue(id, out StyleBody look)) look.Hurt(guarded);
        }

        /// <summary>Essai réaliste 7 : pieds d'un combattant affiché (sur le tablier du pont s'il y est), ou null.</summary>
        /// <summary>Revue Codex de la PR #102 : hauteur du centre d'un corps au-dessus de ses pieds (m) ; partagée avec le compagnon.</summary>
        public const float FeetOffset = 1f;

        public Vector3? Feet(string id) => id != null && _bodies.TryGetValue(id, out GameObject body) && body.activeSelf ? body.transform.position - Vector3.up * FeetOffset : (Vector3?)null;

        /// <summary>Essai réaliste 7 : hauteur affichée d'un combattant (son modèle, sinon la capsule de 2 m).</summary>
        public float Height(string id) => id != null && _looks.TryGetValue(id, out StyleBody look) ? look.Height : 2f;

        /// <summary>Essai réaliste 7 : attaques déjà jouées par les modèles des adversaires (auteur, attaque, tick d'impact ou du tour).</summary>
        private readonly HashSet<(string, string, long)> _struck = new HashSet<(string, string, long)>();

        // Relecture de l'essai 7 : listes réutilisées d'une image à l'autre (rien d'alloué à chaque image pour les attaques).
        private readonly HashSet<(string, string, long)> _live = new HashSet<(string, string, long)>();
        private readonly List<(string Actor, string Attack)> _started = new List<(string Actor, string Attack)>();
        private readonly Dictionary<string, CombatantView> _views = new Dictionary<string, CombatantView>();

        /// <summary>Avance du geste d'un adversaire sur l'impact de son attaque (secondes).</summary>
        private const double WindUp = 0.45;

        /// <summary>
        /// Essai réaliste 7 : jusqu'ici, les adversaires ne jouaient jamais leur attaque. Chaque attaque annoncée au sol fait jouer le
        /// geste de son auteur un peu avant l'impact (<see cref="WindUp"/>), une seule fois. Relecture de l'essai 7 : les attaques
        /// immédiates (morsure du loup, flèche, éclat, griffe), qui ne sont jamais annoncées, font jouer le leur un peu avant le tour de
        /// l'assaillant si sa cible est à portée (<see cref="GameFeel.Imminent"/>). Le Gardien rugit pour son onde, bondit pour sa ruée
        /// (<see cref="StyleRules.AttackAnimation"/>). Rend les attaques qui viennent de commencer (auteur, attaque), pour leurs cris et
        /// leurs effets ; la liste rendue est réutilisée à l'appel suivant.
        /// </summary>
        public IReadOnlyList<(string Actor, string Attack)> PlayAttacks(ICombatClient combat)
        {
            _started.Clear();
            _live.Clear();
            double now = combat.Tick + combat.TickProgress;
            double lead = WindUp / combat.TickSeconds;
            foreach (AnnouncedAttack attack in combat.Announced())
            {
                if (attack.Actor == combat.PlayerId) continue;
                var key = (attack.Actor, attack.Attack, attack.ImpactTick);
                _live.Add(key);
                double toImpact = attack.ImpactTick - now;
                if (toImpact > lead || toImpact < -0.25 / combat.TickSeconds || !_struck.Add(key)) continue;
                if (_looks.TryGetValue(attack.Actor, out StyleBody look)) look.Attack(attack.Attack);
                _started.Add((attack.Actor, attack.Attack));
            }
            _views.Clear();
            CombatantView me = combat.Player().View;
            _views[me.Id] = me;
            IReadOnlyList<OtherState> others = combat.Others();
            foreach (OtherState other in others) _views[other.View.Id] = other.View;
            foreach (OtherState other in others)
            {
                CombatantView view = other.View;
                if (!other.IsEnemy || !(view.NextAttackTick is long tick)) continue;
                CombatantView target = view.CurrentTarget != null && _views.TryGetValue(view.CurrentTarget, out CombatantView t) ? t : null;
                if (!(GameFeel.Imminent(view, target, now, lead) is AssailantAttack attack)) continue;
                var key = (view.Id, attack.Id, tick);
                _live.Add(key);
                if (!_struck.Add(key)) continue;
                if (_looks.TryGetValue(view.Id, out StyleBody look)) look.Attack(attack.Id);
                _started.Add((view.Id, attack.Id));
            }
            // Les attaques finies sont oubliées (la liste ne grandit pas pendant l'aventure) ; un tour encore à venir reste retenu.
            _struck.RemoveWhere(k => !_live.Contains(k) && k.Item3 < now - 1);
            return _started;
        }

        /// <summary>
        /// Sous-lot 12.8 : une zone du monde fait entrer des combattants en cours de route (réapparitions, formes du chef) : chacun reçoit
        /// son corps à sa première apparition dans le moteur, placé ensuite par <see cref="Place"/>.
        /// </summary>
        private void Discover(ICombatClient combat)
        {
            foreach (OtherState other in combat.Others())
                if (!_bodies.ContainsKey(other.Id)) Body(other, Vector3.zero).SetActive(false);
        }

        private GameObject Body(string id, PrimitiveType shape, Color color, Vector3 position)
        {
            GameObject body = GameObject.CreatePrimitive(shape);
            body.name = id;
            body.transform.SetParent(_root.transform, false);
            body.transform.position = position + Vector3.up;
            Renderer renderer = body.GetComponent<Renderer>();
            renderer.material = MakeMaterial(color);
            _bodies[id] = body;
            _renderers[id] = renderer;
            _baseColors[id] = color;
            return body;
        }

        /// <summary>Position d'affichage d'un combattant (au-dessus de sa tête), ou null s'il n'est pas affiché.</summary>
        public Vector3? Head(string id) => _bodies.TryGetValue(id, out GameObject body) && body.activeSelf ? body.transform.position + Vector3.up * 1.3f : (Vector3?)null;

        /// <summary>
        /// Revue Codex de la PR #34 : le corps du combattant est-il affiché (présent et, pour un ennemi d'un combat positionné, en vue entre
        /// les places glissées) ? La fiche de la cible suit le corps.
        /// </summary>
        public bool Shown(string id) => Head(id) != null;

        /// <summary>Position au sol d'un combattant affiché, ou null.</summary>
        public Vector3? Ground(string id)
        {
            if (!_bodies.TryGetValue(id, out GameObject body) || !body.activeSelf) return null;
            Vector3 p = body.transform.position;
            return new Vector3(p.x, 0f, p.z);
        }

        /// <summary>Sous-lot 11.6 : taille de l'emprise du moteur (capsule de 1 m de diamètre à l'échelle 1).</summary>
        private static void Fit(GameObject body, CombatantView view)
        {
            if (view.Radius is EdS.Domain.Numerics.Fixed r)
            {
                float d = r.Raw / 5000f;   // diamètre en mètres
                body.transform.localScale = new Vector3(d, 1f, d);
            }
        }

        private static Vector3 World(GroundVector p) => new Vector3(p.X.Raw / 10000f, 0f, p.Z.Raw / 10000f);

        private static float Yaw(GroundVector facing) => Mathf.Atan2(facing.X.Raw, facing.Z.Raw) * Mathf.Rad2Deg;

        /// <summary>
        /// Sous-lot 11.6 : places du moteur. Au changement de tick, la place affichée part de celle du tick précédent vers la nouvelle ;
        /// entre deux ticks, elle glisse selon <see cref="ICombatClient.TickProgress"/>. Un absent, ou un ennemi hors de vue du joueur,
        /// n'est pas affiché ; revue Codex de la PR #34 : la vue se juge entre les places affichées (glissées), pas entre celles du
        /// moteur, pour qu'un ennemi qui contourne une paroi n'apparaisse qu'une fois réellement sorti de derrière elle. Sous-lot 11.7 : en
        /// réseau, le joueur est montré à sa place prédite (<see cref="ICombatClient.PredictedPosition"/>), qui sert aussi d'œil pour la vue.
        /// </summary>
        private void Place(ICombatClient combat, bool snap)
        {
            CombatantView me = combat.Player().View;
            var views = new List<CombatantView> { me };
            // Revue Codex de la PR #32 (20e tour) : le joueur absent (avant son arrivée, après son départ) reste caché.
            var present = new Dictionary<string, bool> { [combat.PlayerId] = me.IsPresent };
            var enemies = new HashSet<string>();
            foreach (OtherState other in combat.Others())
            {
                views.Add(other.View);
                present[other.Id] = other.View.IsPresent;
                if (other.IsEnemy) enemies.Add(other.Id);
            }
            bool newTick = combat.Tick != _shownTick;
            _shownTick = combat.Tick;
            float t = snap ? 1f : (float)combat.TickProgress;
            (double X, double Z)? predicted = combat.PredictedPosition();
            var shown = new Dictionary<string, Vector3>();
            foreach (CombatantView v in views)
            {
                if (!_bodies.TryGetValue(v.Id, out GameObject body)) continue;
                // Revue Codex de la PR #34 : un combattant sans place (renfort pas encore arrivé) est caché, barre comprise (Head le suit).
                if (v.Position is not GroundVector p)
                {
                    body.SetActive(false);
                    continue;
                }
                Vector3 target = World(p);
                if (snap || !_to.ContainsKey(v.Id))
                {
                    _from[v.Id] = target;
                    _to[v.Id] = target;
                }
                else if (newTick)
                {
                    _from[v.Id] = _to[v.Id];
                    _to[v.Id] = target;
                }
                shown[v.Id] = Vector3.Lerp(_from[v.Id], _to[v.Id], t);
                // Essai réaliste 5 : sur le pont cintré, le corps monte avec le tablier (affichage seulement).
                Vector3 place = shown[v.Id] + Vector3.up * (1f + StyleWorld.StandHeight(shown[v.Id].x, shown[v.Id].z));
                // Sous-lot 11.7 : en réseau, le joueur est montré à sa place prédite, rejointe en douceur (un écart corrigé glisse).
                if (v.Id == combat.PlayerId && predicted.HasValue)
                {
                    Vector3 aim = new Vector3((float)predicted.Value.X, 1f + StyleWorld.StandHeight((float)predicted.Value.X, (float)predicted.Value.Z),
                        (float)predicted.Value.Z);
                    place = snap ? aim : Vector3.Lerp(body.transform.position, aim, 1f - Mathf.Exp(-18f * Time.deltaTime));
                    shown[v.Id] = place - Vector3.up;
                }
                body.transform.position = place;
                // Revue Codex de la PR #117 (tour 3) : les oursons, une fois la vraie place de leur mère connue (et elle là).
                if (_cubsPending.Count > 0 && _cubsPending.Contains(v.Id) && present.TryGetValue(v.Id, out bool mother) && mother)
                {
                    _cubsPending.Remove(v.Id);
                    Cubs(v.Id, shown[v.Id]);
                }
                if (v.Facing is GroundVector f)
                    body.transform.rotation = Quaternion.Slerp(body.transform.rotation, Quaternion.Euler(0f, Yaw(f), 0f), snap ? 1f : 0.3f);
            }
            bool meShown = shown.TryGetValue(combat.PlayerId, out Vector3 eye);
            foreach (CombatantView v in views)
            {
                if (!_bodies.TryGetValue(v.Id, out GameObject body) || !shown.TryGetValue(v.Id, out Vector3 at)) continue;
                bool visible = present.TryGetValue(v.Id, out bool here) && here;
                if (visible && meShown && enemies.Contains(v.Id))
                    visible = CombatDisplay.InSightAt(Raw(eye.x), Raw(eye.z), Raw(at.x), Raw(at.z), _obstacles);
                body.SetActive(visible);
                // Les oursons ne sont là qu'avec leur mère (l'ourse n'est là que le jour).
                if (_cubs.TryGetValue(v.Id, out GameObject den) && den != null) den.SetActive(here);
            }
        }

        /// <summary>Mètres affichés en dix-millièmes de mètre (unité des places du moteur).</summary>
        private static long Raw(float metres) => (long)System.Math.Round(metres * 10000.0);

        /// <summary>Sous-lot 11.6 : attaques annoncées au sol et coupe du décor qui cache le joueur (combat positionné seulement).</summary>
        /// <summary>Sous-lot V2.1 : le décor de la nuit (lanternes, fenêtres, feux follets, hiboux, villageois rentrés).</summary>
        public void Night(Vector3 camera, bool worldNight, float deltaTime) => _space?.Night(camera, worldNight, deltaTime);

        /// <summary>Sous-lot V2.9 (2e vague) : les bêtes de la ferme et des champs, et leurs cris (<see cref="SpaceView.Life"/>).</summary>
        public void Life(Vector3 player, int minute, float deltaTime) => _space?.Life(player, minute, deltaTime);

        /// <summary>Sous-lot V2.9 (2e vague) : le bruit d'un pas à cette place (herbe sans décor).</summary>
        public StyleSound StepSound(Vector3 at, bool forest) => _space != null ? _space.StepSound(at.x, at.z, forest) : StyleSound.StepGrass;

        /// <summary>Sous-lot V2.2 : le décor du temps (flaques, villageois qui s'abritent).</summary>
        public void Weather(WeatherLook look) => _space?.Weather(look);

        /// <summary>Sous-lot V2.3 : la minute du monde, pour les allers-retours des villageois du décor.</summary>
        public void Village(int minute) => _space?.Village(minute);

        /// <summary>Revue Codex de la PR #103 : le corps visible d'un PNJ d'une pièce suit son repère (emploi du temps).</summary>
        public bool MoveSpeaker(string id, float x, float z, float deltaTime) => _space != null && _space.MoveSpeaker(id, x, z, deltaTime);

        /// <summary>Sous-lot V2.3.1 : montre ou cache le corps visible d'un PNJ de la pièce (il sort par la porte, ou rentre).</summary>
        public bool ShowSpeaker(string id, bool shown) => _space != null && _space.ShowSpeaker(id, shown);

        public void SyncSpace(ICombatClient combat, Vector3 camera, float deltaTime)
        {
            if (_space == null || _player == null) return;
            // Essai réaliste 6 : une annonce d'attaque imparable est rouge vif et pulsée (attaque suivante de son auteur).
            var unblockable = new HashSet<string>();
            foreach (OtherState other in combat.Others())
                if (other.View.NextAttack is { } next && next.Unblockable) unblockable.Add(other.Id + "/" + next.Id);
            _space.Zones(combat.Announced(), combat.Tick + combat.TickProgress, Ground, (actor, attack) => unblockable.Contains(actor + "/" + attack));
            // V1.10 (revue Codex, 3e tour) : Cut attend le milieu du corps (la position du corps affiché, les pieds plus 1 m), pas les pieds :
            // les points sondés sont alors bien le bas des jambes, le milieu et la tête (0,3 / 1 / 2 m au-dessus des pieds).
            _space.Cut(camera, _player.transform.position, deltaTime);
        }

        /// <summary>
        /// Met l'arène à l'état du combat : garde, parade, cible, allié visé (null : aucun), combattants hors de combat, adversaires
        /// tournés vers le joueur.
        /// </summary>
        public void Sync(ICombatClient combat, PlayerState player, string allyId)
        {
            CombatantView me = player.View;
            bool broken = me.States.Contains("guardBreak");
            // Revue Codex de la PR #32 (20e tour) : le joueur absent (avant son arrivée, après son départ) est caché, bouclier compris.
            _player.SetActive(me.IsPresent);
            // Essai réaliste 6 (essai de Damien du 27 octobre 2026 : « Le bouclier se modélise comme ça quand je l'active c'est à corriger ») :
            // le disque ne sert plus qu'au corps sans modèle ; le chevalier garde son vrai bouclier, joue sa garde (clip « Block » s'il est
            // là) et l'interface affiche « Garde levée », « Parade » ou « Garde brisée ».
            bool styled = _looks.ContainsKey(combat.PlayerId);
            _shield.SetActive(!styled && me.IsPresent && (me.Guarding || me.ParryOpen || broken));
            _shieldRenderer.sharedMaterial = me.ParryOpen ? _shieldParry : broken ? _shieldBroken : _shieldGuard;
            if (_space != null)
            {
                Discover(combat);
                Place(combat, false);
            }

            foreach (OtherState other in combat.Others())
            {
                if (!_bodies.TryGetValue(other.Id, out GameObject body)) continue;
                if (_space != null)
                {
                    // Place gère déjà la visibilité en combat positionné (présent et en vue).
                    Tint(other.Id, other.View.IsDead, other.View.States.Contains("stun"));
                    continue;   // orientation donnée par le moteur
                }
                // Revue Codex de la PR #32 (16e tour) : un absent (attendu, refusé, parti) est caché ; il apparaît à son arrivée.
                body.SetActive(other.View.IsPresent);
                if (!other.View.IsPresent) continue;
                Vector3 look = _player.transform.position;
                look.y = body.transform.position.y;
                // Revue Codex de la PR #32 (30e tour) : pas vers un joueur caché (absent).
                if (me.IsPresent && other.IsEnemy && other.View.Kind != CombatantKind.Dummy && (look - body.transform.position).sqrMagnitude > 0.01f)
                    body.transform.rotation = Quaternion.Slerp(body.transform.rotation, Quaternion.LookRotation(look - body.transform.position), 0.15f);
                Tint(other.Id, other.View.IsDead, other.View.States.Contains("stun"));
            }
            Tint(combat.PlayerId, me.IsDead, me.States.Contains("stun"));
            SyncGhosts(combat);   // sous-lot V2.9.1
            // Essai de style : les modèles suivent l'allure affichée de leur corps.
            if (_looks.TryGetValue(combat.PlayerId, out StyleBody mine)) mine.Update(_player.transform.position, me.IsDead, Time.deltaTime, me.Guarding || me.ParryOpen);
            foreach (OtherState other in combat.Others())
            {
                if (_looks.TryGetValue(other.Id, out StyleBody look) && _bodies.TryGetValue(other.Id, out GameObject shown) && shown.activeSelf)
                    look.Update(shown.transform.position, other.View.IsDead, Time.deltaTime, false, other.View.Wander);   // V2.4.1 : broute, fouille
                // Sous-lot V2.4 : les bêtes paisibles galopent ou bondissent quand elles fuient. V2.4.1 : elles broutent, guettent, se tapissent.
                if (_wild.TryGetValue(other.Id, out WildlifeLook wild) && _bodies.TryGetValue(other.Id, out GameObject animal) && animal.activeSelf)
                    wild.Update(animal.transform.position, other.View.IsDead, Time.deltaTime, other.View.Wander);
                // Sous-lot V2.9 : les bêtes donnent de la voix (modèle ou formes simples).
                if (_voices != null && _bodies.TryGetValue(other.Id, out GameObject heard) && heard.activeSelf)
                    _voices.Hear(other.Id, heard.transform.position, other.View.IsDead, Time.deltaTime);
            }

            if (player.TargetId != null && _bodies.TryGetValue(player.TargetId, out GameObject target) && target.activeSelf)
            {
                _targetRing.SetActive(true);
                Vector3 p = target.transform.position;
                // Au sol, ou sur le tablier du pont (essai réaliste 5) ; essai réaliste 6 : à 8 cm, sous les annonces (16 cm).
                _targetRing.transform.position = new Vector3(p.x, p.y - 0.92f, p.z);
            }
            else _targetRing.SetActive(false);

            if (allyId != null && _bodies.TryGetValue(allyId, out GameObject ally))
            {
                _allyRing.SetActive(true);
                Vector3 p = ally.transform.position;
                _allyRing.transform.position = new Vector3(p.x, p.y - 0.93f, p.z);
            }
            else _allyRing.SetActive(false);
        }

        /// <summary>
        /// Sous-lot V2.9 : après <see cref="Sync"/>, les bêtes vues donnent de la voix (<see cref="BeastVoices"/>) ; rien sans catalogue de style.
        /// <paramref name="listener"/> : la caméra ; <paramref name="minute"/> : minute du jour du monde ; <paramref name="deltaTime"/> : 0 en pause.
        /// </summary>
        public void Voices(Vector3 listener, int minute, bool night, bool mist, float deltaTime)
        {
            if (_voices == null)
            {
                if (StyleCatalog.Loaded == null) return;
                _voices = new BeastVoices(_root.transform);
                return;   // les bêtes seront notées à la prochaine image
            }
            _voices.Update(listener, minute, night, mist, deltaTime);
        }

        private void Tint(string id, bool dead, bool stunned)
        {
            // Essai de style (revue Codex de la PR #55) : un corps habillé assombrit ou fait clignoter son modèle, à partir de ses couleurs.
            // Relecture de la PR #115 : un écho fantôme n'est pas teinté (ses matériaux sont partagés ; « renderer.material » en ferait une
            // copie à chaque corps, jamais détruite, sans effet sur le shader des échos) : sa mort passe par la dissolution.
            if (_ghosts.Has(id)) return;
            if (_looks.TryGetValue(id, out StyleBody look)) look.Tint(dead, stunned);
            if (!_renderers.TryGetValue(id, out Renderer renderer)) return;
            ApplyColor(renderer.material, Shade(_baseColors[id], dead, stunned));
        }

        /// <summary>Teinte d'un combattant : assombri s'il est mort, clignotant jaune s'il est étourdi, sinon sa couleur.</summary>
        internal static Color Shade(Color color, bool dead, bool stunned)
        {
            if (dead) return Color.Lerp(color, Color.black, 0.75f);
            if (stunned) return Color.Lerp(color, new Color(1f, 1f, 0.4f), 0.5f + 0.5f * Mathf.Sin(Time.time * 12f));
            return color;
        }

        internal static void ApplyColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        /// <summary>Déplace le joueur dans l'arène (purement visuel) et l'oriente ; sans effet dans un combat positionné (le moteur le déplace).</summary>
        public void MovePlayer(Vector3 delta, float yaw)
        {
            if (_space != null) return;
            Vector3 p = _player.transform.position + delta;
            Vector3 flat = new Vector3(p.x, 0f, p.z);
            if (flat.magnitude > Radius - 1f) flat = flat.normalized * (Radius - 1f);
            _player.transform.position = new Vector3(flat.x, p.y, flat.z);
            _player.transform.rotation = Quaternion.Slerp(_player.transform.rotation, Quaternion.Euler(0f, yaw, 0f), 0.25f);
        }
    }
}

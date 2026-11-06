using System.Collections.Generic;
using System.Linq;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.2 (météo) : le décor du temps, sans asset payant. Des flaques (disques plats, sombres et lisses, qui reflètent le ciel)
    /// toujours aux mêmes places le long des chemins, qui grandissent avec le sol mouillé et sèchent après la pluie ; sous une vraie pluie,
    /// les villageois du décor rentrent (un sur trois reste dehors, <see cref="WeatherCycle.StaysOutInRain"/>). Un seul matériau pour toutes
    /// les flaques ; leur taille ne change que quand le sol change (rien à chaque image).
    /// </summary>
    public sealed partial class SpaceView
    {
        private const int MaxPuddles = 48;

        private readonly List<(Puddle Data, Transform Body)> _puddles = new List<(Puddle, Transform)>();
        private Material _puddleMaterial;
        private GameObject _weatherRoot;
        private float _shownWet = -1f;
        private bool _sheltering;

        /// <summary>Pose les flaques (cachées) le long des chemins, hors des bâtiments et des obstacles.</summary>
        private void WeatherDecor()
        {
            _weatherRoot = new GameObject("Météo (V2.2)");
            _weatherRoot.transform.SetParent(_root.transform, false);
            _puddleMaterial = ArenaView.MakeMaterial(new Color(0.16f, 0.17f, 0.19f));
            if (_puddleMaterial.HasProperty("_Smoothness")) _puddleMaterial.SetFloat("_Smoothness", 0.96f);
            if (_puddleMaterial.HasProperty("_Metallic")) _puddleMaterial.SetFloat("_Metallic", 0.1f);
            bool Free(double x, double z) => !_shapes.Any(o => x > o.MinX - 0.6 && x < o.MaxX + 0.6 && z > o.MinZ - 0.6 && z < o.MaxZ + 0.6);
            int max = Application.isMobilePlatform ? MaxPuddles / 2 : MaxPuddles;
            foreach (Puddle p in WeatherCycle.Puddles(_roads, Free, max))
            {
                GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.name = p.Id;
                Object.Destroy(disc.GetComponent<Collider>());
                disc.transform.SetParent(_weatherRoot.transform, false);
                disc.transform.position = new Vector3((float)p.X, (float)p.Y, (float)p.Z);   // revue Codex de la PR #101 : au-dessus des pavés
                disc.transform.rotation = Quaternion.Euler(0f, (float)p.Yaw, 0f);
                Renderer renderer = disc.GetComponent<Renderer>();
                renderer.sharedMaterial = _puddleMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                disc.SetActive(false);
                _puddles.Add((p, disc.transform));
            }
        }

        /// <summary>
        /// À chaque image dehors : les flaques d'après le sol mouillé, les villageois du décor qui s'abritent sous une vraie pluie (appliqué
        /// par <see cref="Night"/>, avec ceux qui rentrent la nuit).
        /// </summary>
        public void Weather(WeatherLook look)
        {
            _sheltering = look.Rain >= WeatherCycle.ShelterRain;
            float wet = (float)look.Wetness;
            if (Mathf.Abs(wet - _shownWet) < 0.01f) return;
            _shownWet = wet;
            float size = (float)WeatherCycle.PuddleSize(wet);
            foreach ((Puddle data, Transform body) in _puddles)
            {
                if (body == null) continue;
                bool show = size > 0.02f;
                if (body.gameObject.activeSelf != show) body.gameObject.SetActive(show);
                if (!show) continue;
                float r = (float)data.Radius * 2f * size;
                body.localScale = new Vector3(r * (float)data.Stretch, 0.004f, r);
            }
        }

        private void ReleaseWeather()
        {
            if (_puddleMaterial != null) Object.Destroy(_puddleMaterial);
            _puddleMaterial = null;
            _puddles.Clear();
        }
    }
}

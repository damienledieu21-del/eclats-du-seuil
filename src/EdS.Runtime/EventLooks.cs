using System;
using System.Collections.Generic;
using EdS.Data.Zones;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot V2.5 (événements du monde) : ce que Unity montre et fait entendre des événements, en code pur (testé hors de Unity), sans
    /// asset : la faille du Seuil en formes simples (des éclats sombres et violets qui tournent autour d'un cœur pâle, des échos de lumière),
    /// la fumée et les débris d'un hameau pillé, la cloche du hameau (son de synthèse). Affichage seulement : rien ne change dans le monde.
    /// </summary>
    public static class EventLooks
    {
        /// <summary>Éclats de la faille (en couronne) et lueurs qui en sortent.</summary>
        public const int RiftShards = 9, RiftMotes = 6;

        public static readonly (double R, double G, double B) RiftDark = (0.16, 0.06, 0.26), RiftEdge = (0.56, 0.36, 0.95), RiftCore = (0.86, 0.9, 1.0),
            Smoke = (0.33, 0.32, 0.31), Debris = (0.36, 0.25, 0.16);

        /// <summary>
        /// Pièces de la faille à l'instant <paramref name="time"/> (secondes), centrées sur sa place, pieds au sol : un cœur pâle et étiré qui
        /// palpite, une couronne d'éclats sombres qui tournent lentement, des lueurs qui montent et s'éteignent. <paramref name="opening"/> de
        /// 0 à 1 : la faille qui s'ouvre (tout grandit) ou se referme.
        /// </summary>
        public static IReadOnlyList<WildlifePart> Rift(double time, double opening = 1)
        {
            double o = Math.Max(0, Math.Min(1, opening));
            double pulse = 1 + 0.08 * Math.Sin(time * 3.1);
            var parts = new List<WildlifePart>
            {
                new WildlifePart("coeur", WildlifeShape.Sphere, 0, 1.6 * o, 0, 0.35 * o * pulse, 2.6 * o * pulse, 0.12 * o, RiftCore),
                new WildlifePart("bord", WildlifeShape.Sphere, 0, 1.6 * o, 0, 0.9 * o, 3.2 * o, 0.2 * o, RiftEdge),
            };
            for (int i = 0; i < RiftShards; i++)
            {
                double a = time * 0.6 + i * 2 * Math.PI / RiftShards;
                double r = 1.4 * o, h = 0.4 + 2.4 * ((i * 7) % RiftShards) / RiftShards;
                parts.Add(new WildlifePart("eclat" + i, WildlifeShape.Cube, Math.Cos(a) * r, h * o, Math.Sin(a) * r * 0.35, 0.18 * o, 0.55 * o, 0.06 * o,
                    i % 3 == 0 ? RiftEdge : RiftDark, pitch: (time * 40 + i * 37) % 360));
            }
            for (int i = 0; i < RiftMotes; i++)
            {
                double phase = (time * 0.35 + i / (double)RiftMotes) % 1.0;
                double a = i * 2.4;
                parts.Add(new WildlifePart("lueur" + i, WildlifeShape.Sphere, Math.Cos(a) * 0.5 * phase, (0.6 + 3.2 * phase) * o, Math.Sin(a) * 0.5 * phase,
                    0.12 * (1 - phase) * o, 0.12 * (1 - phase) * o, 0.12 * (1 - phase) * o, RiftCore));
            }
            return parts;
        }

        /// <summary>
        /// Places de la fumée et des débris d'un lieu pillé (<paramref name="place"/>), toujours les mêmes : trois colonnes de fumée et six
        /// caisses brisées, réparties dans le rectangle du lieu à 15 % de ses bords.
        /// </summary>
        public static IReadOnlyList<(string Kind, double X, double Z)> Aftermath(Place place)
        {
            if (place is null) throw new ArgumentNullException(nameof(place));
            double x0 = place.Min.X.Raw / 10000.0, z0 = place.Min.Z.Raw / 10000.0, x1 = place.Max.X.Raw / 10000.0, z1 = place.Max.Z.Raw / 10000.0;
            double W(double f) => x0 + (x1 - x0) * (0.15 + 0.7 * f);
            double D(double f) => z0 + (z1 - z0) * (0.15 + 0.7 * f);
            var list = new List<(string, double, double)>
            {
                ("fumee", W(0.3), D(0.6)), ("fumee", W(0.7), D(0.35)), ("fumee", W(0.55), D(0.85)),
            };
            for (int i = 0; i < 6; i++) list.Add(("debris", W(((i * 37) % 100) / 100.0), D(((i * 61 + 13) % 100) / 100.0)));
            return list;
        }

        /// <summary>Durée de la cloche, en secondes (trois coups).</summary>
        public const double BellSeconds = 3.6;

        /// <summary>
        /// La cloche du hameau, en échantillons mono de -1 à 1 à <paramref name="rate"/> Hz : trois coups (0 s, 1,2 s, 2,4 s), chacun une
        /// fondamentale à 392 Hz et ses partiels inharmoniques de cloche (×2,0, ×2,4, ×3,0, ×4,2), qui s'éteignent chacun à son rythme.
        /// </summary>
        public static float[] Bell(int rate = 22050)
        {
            if (rate < 8000 || rate > 96000) throw new ArgumentOutOfRangeException(nameof(rate));
            int n = (int)(BellSeconds * rate);
            var data = new float[n];
            double[] ratio = { 1.0, 2.0, 2.4, 3.0, 4.2 }, gain = { 0.5, 0.3, 0.22, 0.14, 0.08 }, decay = { 1.6, 2.4, 3.2, 4.5, 6.0 };
            for (int strike = 0; strike < 3; strike++)
            {
                int start = (int)(strike * 1.2 * rate);
                for (int i = start; i < n; i++)
                {
                    double t = (i - start) / (double)rate;
                    double v = 0;
                    for (int k = 0; k < ratio.Length; k++) v += gain[k] * Math.Exp(-decay[k] * t) * Math.Sin(2 * Math.PI * 392 * ratio[k] * t);
                    data[i] += (float)(v * Math.Min(1, t * 400));   // attaque nette, sans clic
                }
            }
            float peak = 0f;
            foreach (float v in data) peak = Math.Max(peak, Math.Abs(v));
            if (peak > 0) for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.85f;
            return data;
        }
    }
}

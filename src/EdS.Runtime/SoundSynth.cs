using System;

namespace EdS.Runtime
{
    /// <summary>
    /// V2.54 (passe son des Vallons) : sons de synthèse joués quand aucun fichier n'est déposé (« repli synthétique ») : nappes en boucle
    /// des milieux (<see cref="Bed"/> : marais, grotte, égouts, galeries, grottes noyées, sous l'eau, rafales, Envers) et bruitages courts
    /// (<see cref="Cue"/> : pas selon le sol, coups, parade, mort, sorts, butin, coffre, porte, plongée…). Tout est tiré d'un générateur à
    /// congruence et d'une graine : le même son à chaque fois, sans horloge ni hasard ; valeurs dans [-1, 1]. Aucun fichier son.
    /// </summary>
    public static class SoundSynth
    {
        /// <summary>Durée (s) d'une boucle de nappe.</summary>
        public const int BedSeconds = 8;

        /// <summary>Crête d'un son normalisé.</summary>
        public const double Peak = 0.85;

        /// <summary>La nappe <paramref name="kind"/> a-t-elle une synthèse ?</summary>
        public static bool HasBed(StyleAmbience kind) => SoundRules.IsBed(kind);

        /// <summary>Le bruitage <paramref name="sound"/> a-t-il une synthèse ?</summary>
        public static bool HasCue(StyleSound sound)
        {
            if (SoundRules.IsStep(sound)) return true;
            switch (sound)
            {
                case StyleSound.Hit:
                case StyleSound.Block:
                case StyleSound.Parry:
                case StyleSound.Death:
                case StyleSound.Swing:
                case StyleSound.Cast:
                case StyleSound.Coin:
                case StyleSound.Potion:
                case StyleSound.Gather:
                case StyleSound.Craft:
                case StyleSound.ChestOpen:
                case StyleSound.DoorOpen:
                case StyleSound.Dive:
                case StyleSound.Emerge:
                case StyleSound.Hurt:
                case StyleSound.Glass:
                case StyleSound.Portal: return true;
                default: return false;
            }
        }

        /// <summary>
        /// Nappe de synthèse de <see cref="BedSeconds"/> s à <paramref name="rate"/> échantillons par seconde (mono), qui tourne en boucle
        /// sans à-coup (la fin revient en fondu sur le début). L'Envers reprend son bourdon (<see cref="EnversLook.Drone"/>).
        /// </summary>
        public static float[] Bed(StyleAmbience kind, int rate, uint seed)
        {
            CheckRate(rate);
            if (!HasBed(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (kind == StyleAmbience.Envers) return EnversLook.Drone(rate);
            int count = rate * BedSeconds, fade = rate / 2;
            var raw = new double[count + fade];
            var g = new Gen(seed ^ (uint)kind * 7919u);
            switch (kind)
            {
                case StyleAmbience.Marsh: Marsh(raw, rate, g); break;
                case StyleAmbience.Cave: Cave(raw, rate, g, 0.18, 1.0); break;
                case StyleAmbience.Sewer: Sewer(raw, rate, g); break;
                case StyleAmbience.Gallery: Gallery(raw, rate, g); break;
                case StyleAmbience.Drowned: Drowned(raw, rate, g); break;
                case StyleAmbience.Underwater: Underwater(raw, rate, g); break;
                case StyleAmbience.Gusts: Gusts(raw, rate, g); break;
            }
            var looped = new double[count];
            for (int i = 0; i < count; i++)
            {
                double w = i < fade ? (double)i / fade : 1;
                looped[i] = raw[i] * w + (i < fade ? raw[count + i] * (1 - w) : 0);
            }
            return Normalize(looped, 0.6);
        }

        /// <summary>
        /// Bruitage de synthèse <paramref name="sound"/> à <paramref name="rate"/> échantillons par seconde ; <paramref name="variant"/>
        /// change un peu le grain et la hauteur (deux pas de suite ne sonnent pas pareil). Court : moins d'une seconde et demie.
        /// </summary>
        public static float[] Cue(StyleSound sound, int rate, int variant)
        {
            CheckRate(rate);
            if (!HasCue(sound)) throw new ArgumentOutOfRangeException(nameof(sound));
            var g = new Gen(unchecked((uint)sound * 2654435761u ^ (uint)variant * 40503u));
            double tune = 1 + 0.06 * (EnversLook.Hash01((int)sound, variant) - 0.5) * 2;
            switch (sound)
            {
                case StyleSound.StepGrass: return Burst(rate, 0.11, g, 0.35, 0.08, 0.025, 0, 0, tune);
                case StyleSound.StepGravel: return Grainy(rate, 0.13, g, 0.55, 0.012, 140 * tune);
                case StyleSound.StepLeaves: return Grainy(rate, 0.16, g, 0.75, 0.02, 0);
                case StyleSound.StepWood: return Burst(rate, 0.12, g, 0.25, 0.03, 0.02, 110 * tune, 0.7, tune);
                case StyleSound.StepStone: return Burst(rate, 0.08, g, 0.6, 0.012, 0.008, 260 * tune, 0.35, tune);
                case StyleSound.StepWater: return Splash(rate, 0.28, g, 0.6, tune);
                case StyleSound.StepSnow: return Grainy(rate, 0.18, g, 0.18, 0.006, 90 * tune);
                case StyleSound.StepMud: return Squelch(rate, 0.22, g, tune);
                case StyleSound.Hit: return Burst(rate, 0.22, g, 0.5, 0.05, 0.03, 85 * tune, 1.0, tune);
                case StyleSound.Block: return Ring(rate, 0.5, g, new[] { 523.0, 1311, 2087, 3120 }, 0.12, 0.6, tune);
                case StyleSound.Parry: return Ring(rate, 0.9, g, new[] { 1210.0, 2950, 4100, 5230 }, 0.25, 0.35, tune);
                case StyleSound.Craft: return Ring(rate, 0.7, g, new[] { 880.0, 1740, 2640 }, 0.18, 0.5, tune);
                case StyleSound.Death: return Thud(rate, 0.9, g, 60 * tune);
                case StyleSound.Gather: return Burst(rate, 0.18, g, 0.45, 0.035, 0.02, 180 * tune, 0.6, tune);
                case StyleSound.Swing: return Whoosh(rate, 0.26, g, 400 * tune, 1800 * tune);
                case StyleSound.Cast: return Shimmer(rate, 0.65, g, 440 * tune, true);
                case StyleSound.Portal: return Shimmer(rate, 1.3, g, 110 * tune, false);
                case StyleSound.Coin: return Pings(rate, 0.45, new[] { 2093.0 * tune, 2637 * tune }, 0.07);
                case StyleSound.Glass: return Ring(rate, 0.8, g, new[] { 2400.0, 3700, 5100, 6600 }, 0.08, 0.9, tune);
                case StyleSound.Potion: return Bubbles(rate, 0.55, g, 6, 260 * tune);
                case StyleSound.ChestOpen: return Creak(rate, 0.6, g, 140 * tune, true);
                case StyleSound.DoorOpen: return Creak(rate, 1.1, g, 95 * tune, true);
                case StyleSound.Dive: return Mix(Splash(rate, 0.6, g, 1.0, tune), Bubbles(rate, 1.2, g, 10, 200 * tune), 0.15, rate);
                case StyleSound.Emerge: return Mix(Splash(rate, 0.4, g, 0.8, tune), Gasp(rate, 0.6, g), 0.1, rate);
                case StyleSound.Hurt: return Voice(rate, 0.24, 135 * tune, 620, 1050);
                default: throw new ArgumentOutOfRangeException(nameof(sound));
            }
        }

        // --- Nappes ---------------------------------------------------------------------------------------------------

        private static void Marsh(double[] o, int rate, Gen g)
        {
            var water = new Lp(380, rate);
            var insects = new Bp(5200, 6, rate);
            for (int i = 0; i < o.Length; i++)
            {
                double t = (double)i / rate;
                o[i] = water.Run(g.Noise()) * 0.5 + insects.Run(g.Noise()) * 0.25 * (0.6 + 0.4 * Math.Sin(2 * Math.PI * 13 * t));
            }
            // Coassements : un bourdonnement de 180 à 300 Hz, battu à 24 Hz, de temps en temps.
            for (double at = 0.4 + g.Unit() * 0.6; at < (double)o.Length / rate - 0.5; at += 0.6 + g.Unit() * 1.4)
            {
                double f = 180 + g.Unit() * 120, len = 0.18 + g.Unit() * 0.2;
                int start = (int)(at * rate);
                for (int k = 0; k < (int)(len * rate) && start + k < o.Length; k++)
                {
                    double t = (double)k / rate;
                    double env = Math.Sin(Math.PI * t / len);
                    double buzz = Math.Sign(Math.Sin(2 * Math.PI * f * t)) * (0.5 + 0.5 * Math.Sin(2 * Math.PI * 24 * t));
                    o[start + k] += 0.35 * env * buzz;
                }
            }
        }

        private static void Cave(double[] o, int rate, Gen g, double dripEvery, double air)
        {
            var low = new Lp(220, rate);
            for (int i = 0; i < o.Length; i++) o[i] = low.Run(g.Noise()) * 0.45 * air;
            Drips(o, rate, g, 1.1, 0.5);
            Echo(o, rate, 0.19, 0.35);
            Echo(o, rate, 0.41, 0.18);
        }

        private static void Drips(double[] o, int rate, Gen g, double every, double level)
        {
            for (double at = g.Unit() * every; at < (double)o.Length / rate - 0.2; at += every * (0.5 + g.Unit()))
            {
                double f0 = 1400 + g.Unit() * 1200;
                int start = (int)(at * rate);
                double phase = 0;
                for (int k = 0; k < rate / 8 && start + k < o.Length; k++)
                {
                    double t = (double)k / rate;
                    phase += 2 * Math.PI * f0 * (1 - 0.45 * Math.Min(1, t / 0.03)) / rate;
                    o[start + k] += level * Math.Exp(-t / 0.025) * Math.Sin(phase);
                }
            }
        }

        private static void Sewer(double[] o, int rate, Gen g)
        {
            var flow = new Lp(900, rate);
            var deep = new Lp(160, rate);
            for (int i = 0; i < o.Length; i++)
            {
                double n = g.Noise();
                double t = (double)i / rate;
                o[i] = flow.Run(n) * 0.35 * (0.8 + 0.2 * Math.Sin(2 * Math.PI * 0.37 * t)) + deep.Run(n) * 0.5;
            }
            BubbleTrain(o, rate, g, 0.35, 300, 700, 0.25);
            Drips(o, rate, g, 1.7, 0.3);
            Echo(o, rate, 0.23, 0.3);
        }

        private static void Gallery(double[] o, int rate, Gen g)
        {
            var rumble = new Lp(90, rate);
            for (int i = 0; i < o.Length; i++) o[i] = rumble.Run(g.Noise()) * 1.4;
            // Un étai qui craque, une fois ou deux par boucle ; des cailloux qui roulent.
            for (double at = 1 + g.Unit() * 2; at < (double)o.Length / rate - 1; at += 3 + g.Unit() * 3)
            {
                float[] creak = Creak(rate, 0.7, g, 70 + g.Unit() * 40, false);
                int start = (int)(at * rate);
                for (int k = 0; k < creak.Length && start + k < o.Length; k++) o[start + k] += creak[k] * 0.25;
            }
            for (double at = 0.5 + g.Unit(); at < (double)o.Length / rate - 0.5; at += 2 + g.Unit() * 2.5)
            {
                int start = (int)(at * rate);
                for (int c = 0; c < 6; c++)
                {
                    int s = start + (int)(c * (0.03 + g.Unit() * 0.06) * rate);
                    for (int k = 0; k < rate / 100 && s + k < o.Length; k++) o[s + k] += 0.2 * g.Noise() * Math.Exp(-k / (rate / 600.0)) * (1 - c / 7.0);
                }
            }
            Echo(o, rate, 0.27, 0.3);
        }

        private static void Drowned(double[] o, int rate, Gen g)
        {
            var lap = new Lp(600, rate);
            for (int i = 0; i < o.Length; i++)
            {
                double t = (double)i / rate;
                // Clapotis : l'eau monte et redescend contre la pierre toutes les 2 s environ.
                double wave = Math.Max(0, Math.Sin(2 * Math.PI * t / 2.0)) * 0.7 + Math.Max(0, Math.Sin(2 * Math.PI * t / 2.0 * 1.5 + 1)) * 0.3;
                o[i] = lap.Run(g.Noise()) * (0.15 + 0.6 * wave);
            }
            Drips(o, rate, g, 1.4, 0.35);
            Echo(o, rate, 0.31, 0.4);
            Echo(o, rate, 0.62, 0.2);
        }

        private static void Underwater(double[] o, int rate, Gen g)
        {
            var muffle = new Lp(180, rate);
            for (int i = 0; i < o.Length; i++)
            {
                double t = (double)i / rate;
                o[i] = muffle.Run(g.Noise()) * 1.2 + 0.08 * Math.Sin(2 * Math.PI * 48 * t);
            }
            BubbleTrain(o, rate, g, 0.5, 250, 600, 0.2);
        }

        private static void Gusts(double[] o, int rate, Gen g)
        {
            double cut = 400, y = 0, y2 = 0;
            for (int i = 0; i < o.Length; i++)
            {
                double t = (double)i / rate;
                double gust = 0.5 + 0.3 * Math.Sin(2 * Math.PI * t / 4.0) + 0.2 * Math.Sin(2 * Math.PI * t / 1.7 + 0.8);
                cut = 250 + 900 * gust;
                double a = 1 - Math.Exp(-2 * Math.PI * cut / rate);
                y += (g.Noise() - y) * a;
                y2 += (y - y2) * a;
                o[i] = y2 * (0.3 + 0.7 * gust);
            }
        }

        private static void BubbleTrain(double[] o, int rate, Gen g, double every, double fLow, double fHigh, double level)
        {
            for (double at = g.Unit() * every; at < (double)o.Length / rate - 0.1; at += every * (0.3 + g.Unit() * 1.4))
            {
                double f = fLow + g.Unit() * (fHigh - fLow), phase = 0;
                int start = (int)(at * rate);
                for (int k = 0; k < rate / 20 && start + k < o.Length; k++)
                {
                    double t = (double)k / rate;
                    phase += 2 * Math.PI * f * (1 + 8 * t) / rate;
                    o[start + k] += level * Math.Exp(-t / 0.015) * Math.Sin(phase);
                }
            }
        }

        // --- Bruitages ------------------------------------------------------------------------------------------------

        /// <summary>Bruit filtré et bref (pas, coup), avec un corps grave facultatif (<paramref name="body"/> Hz).</summary>
        private static float[] Burst(int rate, double seconds, Gen g, double bright, double decay, double attack, double body, double bodyLevel, double tune)
        {
            var o = new double[(int)(rate * seconds)];
            var lp = new Lp(600 + 5000 * bright * tune, rate);
            var hp = new Lp(120, rate);
            double phase = 0;
            for (int i = 0; i < o.Length; i++)
            {
                double t = (double)i / rate;
                double env = Math.Min(1, t / Math.Max(0.001, attack * 0.3)) * Math.Exp(-t / decay);
                double n = lp.Run(g.Noise());
                n -= hp.Run(n);
                phase += 2 * Math.PI * body * (1 - 0.3 * Math.Min(1, t / 0.08)) / rate;
                o[i] = env * (n + bodyLevel * Math.Sin(phase) * Math.Exp(-t / (decay * 1.5)));
            }
            return Normalize(o, Peak);
        }

        /// <summary>Crissement fait de petits grains (gravier, feuilles, neige), avec un corps grave facultatif.</summary>
        private static float[] Grainy(int rate, double seconds, Gen g, double bright, double grain, double body)
        {
            var o = new double[(int)(rate * seconds)];
            var lp = new Lp(500 + 6000 * bright, rate);
            double gate = 0, phase = 0;
            for (int i = 0; i < o.Length; i++)
            {
                double t = (double)i / rate;
                if (g.Unit() < 1.0 / (grain * rate)) gate = 0.4 + 0.6 * g.Unit();
                gate *= Math.Exp(-1.0 / (0.004 * rate));
                double env = Math.Sin(Math.PI * Math.Min(1, t / seconds));
                phase += 2 * Math.PI * body / rate;
                o[i] = env * (lp.Run(g.Noise()) * (0.3 + gate) + (body > 0 ? 0.3 * Math.Sin(phase) * Math.Exp(-t / 0.04) : 0));
            }
            return Normalize(o, Peak);
        }

        private static float[] Splash(int rate, double seconds, Gen g, double size, double tune)
        {
            var o = new double[(int)(rate * seconds)];
            double y = 0;
            for (int i = 0; i < o.Length; i++)
            {
                double t = (double)i / rate;
                double cut = (3000 * size + 800) * Math.Exp(-t / (seconds * 0.4)) + 300;
                y += (g.Noise() - y) * (1 - Math.Exp(-2 * Math.PI * cut * tune / rate));
                o[i] = y * Math.Min(1, t / 0.005) * Math.Exp(-t / (seconds * 0.35));
            }
            BubbleTrain(o, rate, g, seconds / 4, 400, 900, 0.3 * size);
            return Normalize(o, Peak);
        }

        private static float[] Squelch(int rate, double seconds, Gen g, double tune)
        {
            var o = new double[(int)(rate * seconds)];
            var lp = new Lp(700, rate);
            double phase = 0;
            for (int i = 0; i < o.Length; i++)
            {
                double t = (double)i / rate;
                phase += 2 * Math.PI * (220 - 140 * Math.Min(1, t / seconds)) * tune / rate;
                double env = Math.Sin(Math.PI * Math.Min(1, t / seconds));
                o[i] = env * (lp.Run(g.Noise()) * 0.7 + 0.5 * Math.Sin(phase + 3 * Math.Sin(phase * 0.5)));
            }
            return Normalize(o, Peak);
        }

        /// <summary>Résonance métallique : partiels inharmoniques qui s'éteignent, sur un claquement de bruit.</summary>
        private static float[] Ring(int rate, double seconds, Gen g, double[] partials, double decay, double click, double tune)
        {
            var o = new double[(int)(rate * seconds)];
            for (int i = 0; i < o.Length; i++)
            {
                double t = (double)i / rate, s = 0;
                for (int k = 0; k < partials.Length; k++)
                    s += Math.Sin(2 * Math.PI * partials[k] * tune * t) * Math.Exp(-t / (decay * (1 + 0.6 * (partials.Length - k) / partials.Length))) / (k + 1);
                o[i] = s + click * g.Noise() * Math.Exp(-t / 0.006);
            }
            return Normalize(o, Peak);
        }

        private static float[] Thud(int rate, double seconds, Gen g, double f)
        {
            var o = new double[(int)(rate * seconds)];
            var lp = new Lp(400, rate);
            double phase = 0;
            for (int i = 0; i < o.Length; i++)
            {
                double t = (double)i / rate;
                phase += 2 * Math.PI * f * (1 - 0.4 * Math.Min(1, t / 0.3)) / rate;
                // Un corps qui tombe, puis l'équipement qui retombe (un second choc plus faible).
                double second = t > 0.22 ? Math.Exp(-(t - 0.22) / 0.08) * 0.5 : 0;
                o[i] = (Math.Sin(phase) * Math.Exp(-t / 0.18) + lp.Run(g.Noise()) * (Math.Exp(-t / 0.05) + second)) * Math.Min(1, t / 0.004);
            }
            return Normalize(o, Peak);
        }

        private static float[] Whoosh(int rate, double seconds, Gen g, double from, double to)
        {
            var o = new double[(int)(rate * seconds)];
            var bp = new Bp(from, 2, rate);
            for (int i = 0; i < o.Length; i++)
            {
                double t = (double)i / rate, x = t / seconds;
                bp.Tune(from + (to - from) * Math.Sin(Math.PI * x * 0.5), 2, rate);
                o[i] = bp.Run(g.Noise()) * Math.Sin(Math.PI * x);
            }
            return Normalize(o, Peak);
        }

        /// <summary>Scintillement d'un sort (<paramref name="rise"/> : il monte) ou bourdonnement d'un portail (il descend et vibre).</summary>
        private static float[] Shimmer(int rate, double seconds, Gen g, double f, bool rise)
        {
            var o = new double[(int)(rate * seconds)];
            var bp = new Bp(2000, 4, rate);
            double p1 = 0, p2 = 0, p3 = 0;
            for (int i = 0; i < o.Length; i++)
            {
                double t = (double)i / rate, x = t / seconds;
                double glide = rise ? 1 + 1.5 * x : 1.6 - 0.6 * x;
                double vib = 1 + 0.02 * Math.Sin(2 * Math.PI * 6 * t);
                p1 += 2 * Math.PI * f * glide * vib / rate;
                p2 += 2 * Math.PI * f * 1.5 * glide * vib / rate;
                p3 += 2 * Math.PI * f * 2.01 * glide / rate;
                bp.Tune(rise ? 1500 + 4000 * x : 3000 - 2000 * x, 4, rate);
                double env = Math.Min(1, t / 0.04) * Math.Min(1, (seconds - t) / (seconds * 0.5));
                o[i] = env * (0.5 * Math.Sin(p1) + 0.3 * Math.Sin(p2) + 0.2 * Math.Sin(p3) + 0.6 * bp.Run(g.Noise()));
            }
            return Normalize(o, Peak);
        }

        private static float[] Pings(int rate, double seconds, double[] notes, double gap)
        {
            var o = new double[(int)(rate * seconds)];
            for (int n = 0; n < notes.Length; n++)
            {
                int start = (int)(n * gap * rate);
                for (int k = 0; start + k < o.Length; k++)
                {
                    double t = (double)k / rate;
                    o[start + k] += Math.Exp(-t / 0.08) * (Math.Sin(2 * Math.PI * notes[n] * t) + 0.3 * Math.Sin(2 * Math.PI * notes[n] * 2.76 * t));
                }
            }
            return Normalize(o, Peak);
        }

        private static float[] Bubbles(int rate, double seconds, Gen g, int count, double f)
        {
            var o = new double[(int)(rate * seconds)];
            for (int b = 0; b < count; b++)
            {
                int start = (int)(g.Unit() * (o.Length - rate / 10));
                double fb = f * (0.7 + 0.8 * g.Unit()), phase = 0;
                for (int k = 0; k < rate / 12 && start + k < o.Length; k++)
                {
                    double t = (double)k / rate;
                    phase += 2 * Math.PI * fb * (1 + 6 * t) / rate;
                    o[start + k] += Math.Exp(-t / 0.02) * Math.Sin(phase);
                }
            }
            return Normalize(o, Peak);
        }

        /// <summary>Grincement de bois (charnière) : une suite d'à-coups à <paramref name="f"/> Hz qui ondule ; <paramref name="latch"/> : un loquet claque à la fin.</summary>
        private static float[] Creak(int rate, double seconds, Gen g, double f, bool latch)
        {
            var o = new double[(int)(rate * seconds)];
            var bp = new Bp(900, 5, rate);
            double phase = 0;
            for (int i = 0; i < o.Length; i++)
            {
                double t = (double)i / rate, x = t / seconds;
                phase += 2 * Math.PI * f * (1 + 0.25 * Math.Sin(2 * Math.PI * 3 * t) + 0.1 * g.Noise()) / rate;
                double stick = Math.Pow(Math.Max(0, Math.Sin(phase)), 12);
                o[i] = bp.Run(stick * 4 + 0.05 * g.Noise()) * Math.Sin(Math.PI * Math.Min(1, x * 1.1));
            }
            if (latch)
            {
                int start = (int)(o.Length * 0.85);
                for (int k = 0; start + k < o.Length; k++) o[start + k] += 0.8 * g.Noise() * Math.Exp(-k / (rate * 0.01));
            }
            return Normalize(o, Peak);
        }

        private static float[] Gasp(int rate, double seconds, Gen g)
        {
            var o = new double[(int)(rate * seconds)];
            var bp = new Bp(1400, 2, rate);
            for (int i = 0; i < o.Length; i++)
            {
                double t = (double)i / rate, x = t / seconds;
                o[i] = bp.Run(g.Noise()) * Math.Sin(Math.PI * x) * (x < 0.5 ? 1 : 0.6);
            }
            return Normalize(o, Peak);
        }

        /// <summary>Courte voix (« han ») : une suite d'impulsions à <paramref name="f"/> Hz passée dans deux formants.</summary>
        private static float[] Voice(int rate, double seconds, double f, double f1, double f2)
        {
            var o = new double[(int)(rate * seconds)];
            var a = new Bp(f1, 6, rate);
            var b = new Bp(f2, 8, rate);
            double phase = 0;
            for (int i = 0; i < o.Length; i++)
            {
                double t = (double)i / rate, x = t / seconds;
                phase += f * (1 - 0.25 * x) / rate;
                double pulse = phase % 1 < 0.08 ? 1 : 0;
                double s = pulse;
                o[i] = (a.Run(s) + 0.6 * b.Run(s)) * Math.Min(1, t / 0.015) * Math.Exp(-t / (seconds * 0.45));
            }
            return Normalize(o, Peak);
        }

        // --- Outils ---------------------------------------------------------------------------------------------------

        private static float[] Mix(float[] a, float[] b, double offset, int rate)
        {
            int shift = (int)(offset * rate);
            var o = new double[Math.Max(a.Length, b.Length + shift)];
            for (int i = 0; i < a.Length; i++) o[i] += a[i];
            for (int i = 0; i < b.Length; i++) o[i + shift] += 0.7 * b[i];
            return Normalize(o, Peak);
        }

        private static void Echo(double[] o, int rate, double delay, double level)
        {
            int d = (int)(delay * rate);
            for (int i = o.Length - 1; i >= d; i--) o[i] += o[i - d] * level;
        }

        private static float[] Normalize(double[] o, double peak)
        {
            double max = 1e-9;
            foreach (double v in o) max = Math.Max(max, Math.Abs(v));
            var f = new float[o.Length];
            for (int i = 0; i < o.Length; i++) f[i] = (float)(o[i] / max * peak);
            return f;
        }

        private static void CheckRate(int rate)
        {
            if (rate < 8000 || rate > 96000) throw new ArgumentOutOfRangeException(nameof(rate));
        }

        /// <summary>Générateur à congruence (même suite pour la même graine).</summary>
        private sealed class Gen
        {
            private uint _state;

            public Gen(uint seed) => _state = seed | 1;

            public double Unit()
            {
                _state = unchecked(_state * 1664525u + 1013904223u);
                return (_state >> 8) / 16777216.0;
            }

            public double Noise() => Unit() * 2 - 1;
        }

        /// <summary>Passe-bas à un pôle.</summary>
        private sealed class Lp
        {
            private readonly double _a;
            private double _y;

            public Lp(double cut, int rate) => _a = 1 - Math.Exp(-2 * Math.PI * cut / rate);

            public double Run(double x) => _y += (x - _y) * _a;
        }

        /// <summary>Passe-bande à deux pôles (biquad, gain constant au sommet).</summary>
        private sealed class Bp
        {
            private double _b0, _b2, _a1, _a2, _x1, _x2, _y1, _y2;

            public Bp(double f, double q, int rate) => Tune(f, q, rate);

            public void Tune(double f, double q, int rate)
            {
                f = Math.Max(20, Math.Min(rate * 0.45, f));
                double w = 2 * Math.PI * f / rate, alpha = Math.Sin(w) / (2 * q), a0 = 1 + alpha;
                _b0 = alpha / a0;
                _b2 = -alpha / a0;
                _a1 = -2 * Math.Cos(w) / a0;
                _a2 = (1 - alpha) / a0;
            }

            public double Run(double x)
            {
                double y = _b0 * x + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
                _x2 = _x1;
                _x1 = x;
                _y2 = _y1;
                _y1 = y;
                return y;
            }
        }
    }
}

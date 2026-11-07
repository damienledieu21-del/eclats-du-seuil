using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EdS.Data.Items;
using EdS.Data.Saves;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Numerics;
using EdS.Domain.Space;

namespace EdS.Data.Zones
{
    public static partial class ZoneValidator
    {
        /// <summary>Sous-lot V2.5 : événements d'une zone, au plus.</summary>
        public const int MaxEvents = 16;

        /// <summary>Sous-lot V2.5 : variantes d'un événement, créatures d'une variante, vagues d'une faille, objets d'une récompense, au plus.</summary>
        public const int MaxEventGroups = 8, MaxEventCreatures = 8, MaxEventWaves = 5, MaxEventRewardItems = 4, MaxEventVillagers = 8;

        /// <summary>Sous-lot V2.5 : bornes des durées d'un événement, en minutes du monde (une journée du monde : 1 440).</summary>
        public const int MaxEventSpread = 720, MaxEventAnnounce = 720, MinEventDuration = 10, MaxEventDuration = 720, MaxEventAftermath = 4320;

        /// <summary>Sous-lot V2.5 : fenêtre d'un événement de 1 à 30 jours du monde ; premier jour au plus 1 000.</summary>
        public const int MaxEventEveryDays = 30, MaxEventFirstDay = 1000;

        /// <summary>Sous-lot V2.5 : écart entre deux vagues d'une faille, en ms (de 1 s à 10 min de jeu).</summary>
        public const long MinWaveMs = 1000, MaxWaveMs = 600_000;

        /// <summary>Sous-lot V2.5 : expérience d'une récompense d'événement, au plus.</summary>
        public const long MaxEventXp = 100_000;

        /// <summary>
        /// Sous-lot V2.5 (schéma 10) : <c>events</c>, de 1 à <see cref="MaxEvents"/> événements : <c>id</c> (local, unique), <c>kind</c>
        /// (« raid » : attaque d'un lieu ; « rift » : faille du Seuil ; « hunt » : chasse de bêtes entre elles), <c>everyDays</c> (fenêtre de 1 à
        /// 30 jours, une occurrence au plus par fenêtre), <c>chancePercent</c> (1 à 100), <c>firstDay</c> (0 à 1 000), <c>start</c> (« HH:MM »),
        /// <c>spreadMinutes</c> (0 à 720), <c>announceMinutes</c> (0 à 720), <c>durationMinutes</c> (10 à 720), <c>aftermathMinutes</c> (0 à
        /// 4 320, facultatif, pas pour une chasse), <c>weather</c> facultatif (même forme que sur une population, sans <c>sign</c>), <c>place</c>
        /// facultatif (lieu de la zone), <c>refuge</c> et <c>villagers</c> (attaque seulement, ensemble : où se réfugient ces PNJ),
        /// <c>groups</c> (de 1 à 8 variantes : <c>id</c>, <c>weight</c> 1 à 100, <c>position</c> obligatoire pour une faille et refusée ailleurs,
        /// <c>creatures</c> de 1 à 8 : <c>like</c> une population de la zone, <c>position</c> dans l'aire et hors des obstacles), <c>waves</c>
        /// et <c>waveMs</c> (faille seulement : 1 à 5 vagues, 1 s à 10 min), <c>reward</c> (obligatoire pour une attaque ou une faille, refusé
        /// pour une chasse : <c>xp</c>, <c>money</c>, <c>items</c> facultatif, <c>fact</c> facultatif) et <c>lostFact</c> facultatif (pas pour
        /// une chasse), <c>minLevel</c> facultatif (niveau minimum du personnage, de 1 au niveau maximal ; choix de Damien du 7 novembre 2026).
        /// Une chasse ne prend que des bêtes (populations des équipes de bêtes, jamais des bandits).
        /// </summary>
        /// <remarks>Relecture de la PR #107 : la borne du registre sur toutes les zones reliées est vérifiée par
        /// <see cref="CheckEventRegistry"/> (au chargement des zones reliées).</remarks>
        private static void CheckEvents(List<WorldEventData?> data, CombatSpace? space, List<Place> places, List<Population> populations, ItemCatalog items,
            long tick, ValidationReport report, List<WorldEventDefinition> events, int maxLevel, bool schema11 = true, string? zoneId = null, bool schema12 = true)
        {
            const string root = "$.data.events";
            if (data.Count < 1 || data.Count > MaxEvents)
            {
                report.Add(root, data.Count < 1 ? ValidationCode.OutOfRange : ValidationCode.LimitExceeded, $"de 1 à {MaxEvents} événements (absent quand il n'y en a aucun).");
                return;
            }
            var ids = new List<string>();
            for (int i = 0; i < data.Count; i++)
            {
                string path = $"{root}[{i}]";
                if (data[i] is not WorldEventData e) { Missing(report, path, "événement du monde"); continue; }
                bool ok = CheckId(e.Id, path + ".id", ids, report);
                if (ok) ids.Add(e.Id!);
                WorldEventKind kind = WorldEventKind.Raid;
                bool known = false;
                if (e.Kind is null) { Missing(report, path + ".kind", "« raid », « rift » ou « hunt »"); ok = false; }
                else if (WorldEventDefinition.KindNames.ToList().IndexOf(e.Kind) is int k && k >= 0) { kind = (WorldEventKind)k; known = true; }
                else { report.Add(path + ".kind", ValidationCode.InvalidValue, $"sorte d'événement « {e.Kind} » inconnue : « raid », « rift » ou « hunt »."); ok = false; }
                int? Range(int? value, string field, string what, int min, int max)
                {
                    if (value is null) { Missing(report, path + "." + field, what); ok = false; return null; }
                    if (value < min || value > max) { report.Add(path + "." + field, ValidationCode.OutOfRange, $"{what} hors de [{min}, {max}]."); ok = false; return null; }
                    return value;
                }
                int? every = Range(e.EveryDays, "everyDays", "fenêtre en jours du monde", 1, MaxEventEveryDays);
                int? chance = Range(e.ChancePercent, "chancePercent", "chance par fenêtre, en pour cent", 1, 100);
                int? first = Range(e.FirstDay, "firstDay", "premier jour du monde", 0, MaxEventFirstDay);
                int? start = null;
                if (e.Start is null) { Missing(report, path + ".start", "heure de départ « HH:MM »"); ok = false; }
                else if (!(ParseClock(e.Start) is int m)) { report.Add(path + ".start", ValidationCode.InvalidValue, $"heure « {e.Start} » mal formée : « HH:MM » de 00:00 à 23:59."); ok = false; }
                else start = m;
                int? spread = Range(e.SpreadMinutes, "spreadMinutes", "écart du départ en minutes du monde", 0, MaxEventSpread);
                int? announce = Range(e.AnnounceMinutes, "announceMinutes", "annonce en minutes du monde avant le départ", 0, MaxEventAnnounce);
                int? duration = Range(e.DurationMinutes, "durationMinutes", "durée en minutes du monde", MinEventDuration, MaxEventDuration);
                int aftermath = 0;
                if (e.AftermathMinutes is int am)
                {
                    if (known && kind == WorldEventKind.Hunt) { report.Add(path + ".aftermathMinutes", ValidationCode.InvalidValue, "une chasse n'a pas de conséquences à montrer."); ok = false; }
                    else if (am < 0 || am > MaxEventAftermath) { report.Add(path + ".aftermathMinutes", ValidationCode.OutOfRange, $"conséquences hors de [0, {MaxEventAftermath}] minutes du monde."); ok = false; }
                    else aftermath = am;
                }
                PopulationWeather? weather = null;
                if (e.Weather is WeatherData w)
                {
                    if (w.Sign != null) { report.Add(path + ".weather.sign", ValidationCode.InvalidValue, "un événement a ses propres annonces : pas de « sign »."); ok = false; }
                    else if ((weather = CheckWeather(w, path + ".weather", report)) is null) ok = false;
                }
                if (e.Place != null && places.All(p => p.Id != e.Place)) { report.Add(path + ".place", ValidationCode.UnknownReference, $"lieu « {e.Place} » absent de la zone."); ok = false; }
                GroundVector? refuge = null;
                var villagers = new List<string>();
                if (e.Refuge != null || e.Villagers != null)
                {
                    if (known && kind != WorldEventKind.Raid) { report.Add(path + ".refuge", ValidationCode.InvalidValue, "seule une attaque fait se réfugier des villageois."); ok = false; }
                    else if (e.Refuge is null || e.Villagers is null) { Missing(report, path + (e.Refuge is null ? ".refuge" : ".villagers"), "refuge [x, z] et villageois qui s'y abritent, ensemble"); ok = false; }
                    else
                    {
                        if (!CheckPoint(e.Refuge, path + ".refuge", "refuge [x, z]", report)) ok = false;
                        else if (space != null && Clear(Point(e.Refuge), CharacterRadius, space, "d'un PNJ") is string why) { report.Add(path + ".refuge", ValidationCode.InvalidValue, why); ok = false; }
                        else refuge = Point(e.Refuge);
                        if (e.Villagers.Count < 1 || e.Villagers.Count > MaxEventVillagers) { report.Add(path + ".villagers", ValidationCode.OutOfRange, $"de 1 à {MaxEventVillagers} villageois."); ok = false; }
                        else
                            for (int v = 0; v < e.Villagers.Count; v++)
                            {
                                string? npc = e.Villagers[v];
                                if (npc is null || npc.Length > 64 || !NpcId.IsMatch(npc)) { report.Add($"{path}.villagers[{v}]", ValidationCode.InvalidValue, $"PNJ « {npc} » mal formé : « npc.<nom> » attendu."); ok = false; }
                                else if (villagers.Contains(npc)) { report.Add($"{path}.villagers[{v}]", ValidationCode.DuplicateId, $"PNJ « {npc} » déjà donné."); ok = false; }
                                else villagers.Add(npc);
                            }
                    }
                }
                int waves = 1;
                long waveMs = 0;
                if (known && kind == WorldEventKind.Rift)
                {
                    if (Range(e.Waves, "waves", "nombre de vagues", 1, MaxEventWaves) is int wv) waves = wv;
                    if (e.WaveMs is null) { Missing(report, path + ".waveMs", "écart entre deux vagues, en ms"); ok = false; }
                    else if (e.WaveMs < MinWaveMs || e.WaveMs > MaxWaveMs) { report.Add(path + ".waveMs", ValidationCode.OutOfRange, $"écart hors de [{MinWaveMs}, {MaxWaveMs}] ms."); ok = false; }
                    else if (e.WaveMs % tick != 0) { report.Add(path + ".waveMs", ValidationCode.InvalidValue, $"{e.WaveMs} ms n'est pas un multiple de la durée du tick ({tick} ms)."); ok = false; }
                    else waveMs = e.WaveMs.Value;
                }
                else if (e.Waves != null || e.WaveMs != null)
                { report.Add(path + (e.Waves != null ? ".waves" : ".waveMs"), ValidationCode.InvalidValue, "des vagues seulement pour une faille (« rift »)."); ok = false; }
                // Sous-lot V2.6 (schéma 11) : cicatrice d'une faille restée ouverte.
                WorldEventScar? scar = null;
                if (e.Scar != null)
                {
                    if (!schema11) { report.Add(path + ".scar", ValidationCode.InvalidValue, $"cicatrice d'une faille : schéma {ScarsSchemaVersion} attendu."); ok = false; }
                    else if (known && kind != WorldEventKind.Rift) { report.Add(path + ".scar", ValidationCode.InvalidValue, "seule une faille (« rift ») laisse une cicatrice."); ok = false; }
                    else if ((scar = CheckScar(e.Scar, path + ".scar", items, report, schema12)) is null) ok = false;
                }
                var groups = new List<WorldEventGroup>();
                if (e.Groups is null) { Missing(report, path + ".groups", "variantes de l'événement"); ok = false; }
                else if (e.Groups.Count < 1 || e.Groups.Count > MaxEventGroups) { report.Add(path + ".groups", ValidationCode.OutOfRange, $"de 1 à {MaxEventGroups} variantes."); ok = false; }
                else
                    for (int g = 0; g < e.Groups.Count; g++)
                        if (CheckGroup(e.Groups[g], $"{path}.groups[{g}]", known ? kind : (WorldEventKind?)null, groups, space, populations, report, e.Scar != null) is WorldEventGroup group)
                            groups.Add(group);
                        else ok = false;
                // Sous-lot V2.6.1 (schéma 12) : voisines où la cicatrice d'une variante se propage, jugées une fois toutes les variantes lues.
                if (e.Groups != null && !CheckSpreadTo(e.Groups, path, e.Scar, scar, schema12, groups, report)) ok = false;
                WorldEventReward? reward = null;
                if (known && kind == WorldEventKind.Hunt)
                {
                    if (e.Reward != null) { report.Add(path + ".reward", ValidationCode.InvalidValue, "une chasse ne se récompense pas."); ok = false; }
                    if (e.LostFact != null) { report.Add(path + ".lostFact", ValidationCode.InvalidValue, "une chasse n'a pas d'échec."); ok = false; }
                }
                else if (e.Reward is null) { if (known) { Missing(report, path + ".reward", "récompense de la première réussite"); ok = false; } }
                else if ((reward = CheckEventReward(e.Reward, path + ".reward", items, report)) is null) ok = false;
                if (e.LostFact != null && !CheckDeclaredFact(e.LostFact, path + ".lostFact", report)) ok = false;   // revue Codex de la PR #107 : « fact.met.… » réservé
                // Choix de Damien du 7 novembre 2026 : niveau minimum du personnage, de 1 au niveau maximal des règles.
                int minLevel = 1;
                if (e.MinLevel is int ml)
                {
                    if (ml < 1 || ml > maxLevel) { report.Add(path + ".minLevel", ValidationCode.OutOfRange, $"niveau minimum hors de [1, {maxLevel}]."); ok = false; }
                    else minLevel = ml;
                }
                // Revue Codex de la PR #110 (tour 3) : les clés composées que la partie écrira dans la sauvegarde (registre, cicatrices)
                // tiennent dans sa borne, avec la même formule que le cœur.
                if (zoneId != null && known && kind != WorldEventKind.Hunt && e.Id != null && EventRegistryKey(zoneId, e.Id).Length > SavedEvent.MaxKeyLength)
                { report.Add(path + ".id", ValidationCode.LimitExceeded, $"clé du registre « {EventRegistryKey(zoneId, e.Id)} » au-delà de {SavedEvent.MaxKeyLength} caractères (sauvegarde)."); ok = false; }
                if (zoneId != null && scar != null && e.Id != null && e.Groups != null)
                    for (int g = 0; g < e.Groups.Count; g++)
                        if (e.Groups[g]?.Id is string gid && ScarKey(zoneId, e.Id, gid).Length > SavedEvent.MaxKeyLength)
                        { report.Add($"{path}.groups[{g}].id", ValidationCode.LimitExceeded, $"clé de cicatrice « {ScarKey(zoneId, e.Id, gid)} » au-delà de {SavedEvent.MaxKeyLength} caractères (sauvegarde)."); ok = false; }
                if (ok)
                    events.Add(new WorldEventDefinition(e.Id!, kind, every!.Value, chance!.Value, first!.Value, start!.Value, spread!.Value, announce!.Value,
                        duration!.Value, aftermath, weather, e.Place, refuge, villagers, groups, waves, waveMs, reward, e.LostFact, minLevel, scar));
            }
        }

        private static WorldEventGroup? CheckGroup(WorldEventGroupData? g, string path, WorldEventKind? kind, List<WorldEventGroup> previous, CombatSpace? space,
            List<Population> populations, ValidationReport report, bool scarred = false)
        {
            if (g is null) { Missing(report, path, "variante"); return null; }
            bool ok = CheckId(g.Id, path + ".id", previous.Select(x => x.Id), report);
            if (g.Weight is null) { Missing(report, path + ".weight", "poids de la variante (1 à 100)"); ok = false; }
            else if (g.Weight < 1 || g.Weight > 100) { report.Add(path + ".weight", ValidationCode.OutOfRange, "poids hors de [1, 100]."); ok = false; }
            GroundVector? position = null;
            if (kind == WorldEventKind.Rift)
            {
                if (!CheckPoint(g.Position, path + ".position", "place de la faille [x, z]", report)) ok = false;
                else if (space != null && Clear(Point(g.Position!), CharacterRadius, space, "de la faille") is string why) { report.Add(path + ".position", ValidationCode.InvalidValue, why); ok = false; }
                else position = Point(g.Position!);
            }
            else if (g.Position != null && kind != null) { report.Add(path + ".position", ValidationCode.InvalidValue, "une place seulement pour une faille (« rift »)."); ok = false; }
            var creatures = new List<WorldEventCreature>();
            if (g.Creatures is null) { Missing(report, path + ".creatures", "créatures de la variante"); return null; }
            if (g.Creatures.Count < 1 || g.Creatures.Count > MaxEventCreatures) { report.Add(path + ".creatures", ValidationCode.OutOfRange, $"de 1 à {MaxEventCreatures} créatures."); return null; }
            if (!CheckEventCreatures(g.Creatures, path + ".creatures", kind, space, populations, report, creatures)) ok = false;
            // Sous-lot V2.6 (schéma 11) : échos qui rôdent autour de la cicatrice (une faille à cicatrice seulement), de 1 à MaxScarHaunts.
            var haunts = new List<WorldEventCreature>();
            if (g.Haunts != null)
            {
                if (!scarred) { report.Add(path + ".haunts", ValidationCode.InvalidValue, "des échos rôdeurs seulement pour une faille à cicatrice (« scar »)."); ok = false; }
                else if (g.Haunts.Count < 1 || g.Haunts.Count > MaxScarHaunts) { report.Add(path + ".haunts", ValidationCode.OutOfRange, $"de 1 à {MaxScarHaunts} échos rôdeurs."); ok = false; }
                else if (!CheckEventCreatures(g.Haunts, path + ".haunts", kind, space, populations, report, haunts)) ok = false;
                else if (haunts.FirstOrDefault(h => creatures.Any(c => c.Position.X == h.Position.X && c.Position.Z == h.Position.Z)) is WorldEventCreature same)
                { report.Add($"{path}.haunts[{haunts.IndexOf(same)}].position", ValidationCode.DuplicateId, "un écho rôdeur à la place d'une créature de la faille."); ok = false; }
            }
            return ok ? new WorldEventGroup(g.Id!, g.Weight!.Value, position, creatures, haunts) : null;
        }

        /// <summary>Créatures d'une variante (ou ses échos rôdeurs, sous-lot V2.6) : modèle connu et qui se bat, place libre, une par place.</summary>
        private static bool CheckEventCreatures(List<WorldEventCreatureData?> list, string root, WorldEventKind? kind, CombatSpace? space, List<Population> populations,
            ValidationReport report, List<WorldEventCreature> creatures)
        {
            bool ok = true;
            for (int c = 0; c < list.Count; c++)
            {
                string cp = $"{root}[{c}]";
                if (list[c] is not WorldEventCreatureData d) { Missing(report, cp, "créature"); ok = false; continue; }
                Population? like = d.Like is null ? null : populations.FirstOrDefault(p => p.Creature == d.Like);
                if (d.Like is null) { Missing(report, cp + ".like", "population de la zone dont elle prend le modèle"); ok = false; }
                else if (like is null) { report.Add(cp + ".like", ValidationCode.UnknownReference, $"population « {d.Like} » absente de la zone."); ok = false; }
                else if (like.Night != null) { report.Add(cp + ".like", ValidationCode.InvalidValue, "une créature rare de la nuit ne sert pas de modèle."); ok = false; }
                else if (like.Template.Behavior?.Flee == true) { report.Add(cp + ".like", ValidationCode.InvalidValue, "une bête qui fuit ne sert pas de modèle (elle ne se bat pas)."); ok = false; }
                else if (kind == WorldEventKind.Hunt && like.Template.Team != "meute")
                { report.Add(cp + ".like", ValidationCode.InvalidValue, "une chasse ne prend que des bêtes de la meute (une bête n'en tue pas une autre ; jamais de bandits)."); ok = false; }
                if (!CheckPoint(d.Position, cp + ".position", "place [x, z]", report)) ok = false;
                else if (space != null && like != null && like.Template.Placement is CombatantPlacement at
                    && Clear(Point(d.Position!), at.Radius, space, "de la créature") is string why) { report.Add(cp + ".position", ValidationCode.InvalidValue, why); ok = false; }
                else if (creatures.Any(x => x.Position.X == Point(d.Position!).X && x.Position.Z == Point(d.Position!).Z))
                { report.Add(cp + ".position", ValidationCode.DuplicateId, "deux créatures d'une variante à la même place."); ok = false; }
                if (ok) creatures.Add(new WorldEventCreature(d.Like!, Point(d.Position!)));
            }
            return ok;
        }

        /// <summary>Sous-lot V2.6 : bornes d'une cicatrice (provisoires).</summary>
        public const int MaxScarStage = SavedScar.MaxStage, MaxScarGrowDays = 30, MaxScarStrengthPercent = 100, MinHauntRespawn = 10, MaxHauntRespawn = 1440;

        /// <summary>Sous-lot V2.6 : échos rôdeurs d'une variante, au plus.</summary>
        public const int MaxScarHaunts = 4;

        /// <summary>Sous-lot V2.6 : rayon d'une cicatrice au plus (palier maximal compris), en mètres ; expérience d'une purification au plus.</summary>
        public static readonly Fixed MaxScarRadius = Fixed.FromInt(60);
        public const long MaxPurifyXp = 100_000;

        /// <summary>
        /// Sous-lot V2.6 (schéma 11) : <c>scar</c> d'une faille : <c>growDays</c> (1 à 30 jours du monde par palier), <c>maxStage</c> (1 à
        /// <see cref="MaxScarStage"/>), <c>radius</c> (rayon au palier 1, plus de 0) et <c>radiusPerStage</c> (0 ou plus ; au palier maximal, au
        /// plus <see cref="MaxScarRadius"/> m), <c>strengthPercent</c> (0 à 100 % de PV et de dégâts par palier au-delà du premier),
        /// <c>hauntStage</c> (1 à <c>maxStage</c>) et <c>hauntRespawnMinutes</c> (10 à 1 440), <c>purifyItem</c> (objet du catalogue, un par
        /// purification), <c>purifyXp</c> (0 à 100 000), <c>bornFact</c>, <c>deepFact</c>, <c>purifiedFact</c> facultatifs (« fact.met.… »
        /// réservé).
        /// </summary>
        private static WorldEventScar? CheckScar(WorldEventScarData s, string path, ItemCatalog items, ValidationReport report, bool schema12 = true)
        {
            bool ok = true;
            int? Range(int? value, string field, string what, int min, int max)
            {
                if (value is null) { Missing(report, path + "." + field, what); ok = false; return null; }
                if (value < min || value > max) { report.Add(path + "." + field, ValidationCode.OutOfRange, $"{what} hors de [{min}, {max}]."); ok = false; return null; }
                return value;
            }
            int? grow = Range(s.GrowDays, "growDays", "jours du monde par palier", 1, MaxScarGrowDays);
            int? max = Range(s.MaxStage, "maxStage", "palier maximal", 1, MaxScarStage);
            int? strength = Range(s.StrengthPercent, "strengthPercent", "force des échos par palier, en pour cent", 0, MaxScarStrengthPercent);
            int? haunt = Range(s.HauntStage, "hauntStage", "palier des échos rôdeurs", 1, max ?? MaxScarStage);
            int? respawn = Range(s.HauntRespawnMinutes, "hauntRespawnMinutes", "retour d'un écho rôdeur, en minutes du monde", MinHauntRespawn, MaxHauntRespawn);
            // Revue Codex de la PR #110 (tour 2) : le rayon au palier maximal n'est calculé qu'avec des valeurs déjà dans leurs bornes
            // (rayon et rayon par palier au plus 60 m, palier au plus 8) : aucun débordement possible, même pour une valeur représentable extrême.
            Fixed? radius = null, perStage = null;
            if (s.Radius is null) { Missing(report, path + ".radius", "rayon au palier 1, en mètres"); ok = false; }
            else if (s.Radius.Value <= Fixed.Zero || s.Radius.Value > MaxScarRadius) { report.Add(path + ".radius", ValidationCode.OutOfRange, $"rayon hors de ]0, {MaxScarRadius}] m."); ok = false; }
            else radius = s.Radius.Value;
            if (s.RadiusPerStage is null) { Missing(report, path + ".radiusPerStage", "rayon gagné par palier, en mètres (0 pour aucun)"); ok = false; }
            else if (s.RadiusPerStage.Value < Fixed.Zero || s.RadiusPerStage.Value > MaxScarRadius) { report.Add(path + ".radiusPerStage", ValidationCode.OutOfRange, $"rayon par palier hors de [0, {MaxScarRadius}] m."); ok = false; }
            else perStage = s.RadiusPerStage.Value;
            if (radius is Fixed r && perStage is Fixed ps && max is int m && r + ps * Fixed.FromInt(m - 1) > MaxScarRadius)
            { report.Add(path + ".radiusPerStage", ValidationCode.OutOfRange, $"rayon au palier {m} au-delà de {MaxScarRadius} m."); ok = false; }
            if (s.PurifyItem is null) { Missing(report, path + ".purifyItem", "objet qui purifie la cicatrice"); ok = false; }
            else if (!items.TryGetItem(s.PurifyItem, out _)) { report.Add(path + ".purifyItem", ValidationCode.UnknownReference, $"objet inconnu « {s.PurifyItem} »."); ok = false; }
            if (s.PurifyXp is null) { Missing(report, path + ".purifyXp", "expérience d'une purification (0 pour aucune)"); ok = false; }
            else if (s.PurifyXp < 0 || s.PurifyXp > MaxPurifyXp) { report.Add(path + ".purifyXp", ValidationCode.OutOfRange, $"expérience hors de [0, {MaxPurifyXp}]."); ok = false; }
            if (s.BornFact != null && !CheckDeclaredFact(s.BornFact, path + ".bornFact", report)) ok = false;
            if (s.DeepFact != null && !CheckDeclaredFact(s.DeepFact, path + ".deepFact", report)) ok = false;
            if (s.PurifiedFact != null && !CheckDeclaredFact(s.PurifiedFact, path + ".purifiedFact", report)) ok = false;
            // Sous-lot V2.6.1 (schéma 12) : propagation au lieu voisin, facultative (palier et délai ensemble ; le fait seulement avec eux).
            int spreadStage = 0, spreadDays = 0;
            if (s.SpreadStage != null || s.SpreadDays != null || s.SpreadFact != null)
            {
                string first = s.SpreadStage != null ? ".spreadStage" : s.SpreadDays != null ? ".spreadDays" : ".spreadFact";
                if (!schema12) { report.Add(path + first, ValidationCode.InvalidValue, $"propagation d'une cicatrice : schéma {CurrentSchemaVersion} attendu."); ok = false; }
                else
                {
                    if (Range(s.SpreadStage, "spreadStage", "palier de la propagation", 1, max ?? MaxScarStage) is int ss) spreadStage = ss;
                    if (Range(s.SpreadDays, "spreadDays", "jours du monde avant la propagation", 1, MaxScarSpreadDays) is int sd) spreadDays = sd;
                    if (s.SpreadFact != null && !CheckDeclaredFact(s.SpreadFact, path + ".spreadFact", report)) ok = false;
                }
            }
            return ok ? new WorldEventScar(grow!.Value, max!.Value, s.Radius!.Value, s.RadiusPerStage!.Value, strength!.Value, haunt!.Value, respawn!.Value,
                s.PurifyItem!, s.PurifyXp!.Value, s.BornFact, s.DeepFact, s.PurifiedFact, spreadStage, spreadDays, s.SpreadFact) : null;
        }

        /// <summary>Sous-lot V2.6.1 : délai d'une propagation au plus, en jours du monde.</summary>
        public const int MaxScarSpreadDays = 30;

        /// <summary>
        /// Sous-lot V2.6.1 (schéma 12) : <c>groups[g].spreadTo</c>, facultatif, seulement sur une faille dont la cicatrice se propage
        /// (<c>scar.spreadStage</c>) : de 1 à <see cref="MaxEventGroups"/> variantes de la même faille, connues, ni elle-même, ni deux fois. Une
        /// propagation ne fait que des cicatrices de la faille, au plus une par variante : le nombre de cicatrices reste borné (vérifié au
        /// chargement des zones reliées) et aucune boucle n'est possible.
        /// </summary>
        private static bool CheckSpreadTo(List<WorldEventGroupData?> data, string path, WorldEventScarData? scarData, WorldEventScar? scar, bool schema12,
            List<WorldEventGroup> groups, ValidationReport report)
        {
            bool ok = true;
            var known = new HashSet<string>(data.Where(g => g?.Id != null).Select(g => g!.Id!), StringComparer.Ordinal);
            for (int g = 0; g < data.Count; g++)
            {
                List<string?>? to = data[g]?.SpreadTo;
                if (to is null) continue;
                string p = $"{path}.groups[{g}].spreadTo";
                if (!schema12) { report.Add(p, ValidationCode.InvalidValue, $"propagation d'une cicatrice : schéma {CurrentSchemaVersion} attendu."); ok = false; continue; }
                if (scarData is null || scarData.SpreadStage is null) { report.Add(p, ValidationCode.InvalidValue, "des voisines seulement pour une faille dont la cicatrice se propage (« scar.spreadStage »)."); ok = false; continue; }
                if (to.Count < 1 || to.Count > MaxEventGroups) { report.Add(p, ValidationCode.OutOfRange, $"de 1 à {MaxEventGroups} variantes voisines."); ok = false; continue; }
                var seen = new HashSet<string>(StringComparer.Ordinal);
                bool here = true;
                for (int k = 0; k < to.Count; k++)
                {
                    string? n = to[k];
                    if (n is null || !known.Contains(n)) { report.Add($"{p}[{k}]", ValidationCode.UnknownReference, $"variante « {n} » absente de la faille."); here = false; }
                    else if (n == data[g]!.Id) { report.Add($"{p}[{k}]", ValidationCode.InvalidValue, "une cicatrice ne se propage pas à sa propre place."); here = false; }
                    else if (!seen.Add(n)) { report.Add($"{p}[{k}]", ValidationCode.DuplicateId, $"variante « {n} » déjà donnée."); here = false; }
                }
                if (!here) { ok = false; continue; }
                if (scar != null && groups.FirstOrDefault(x => x.Id == data[g]!.Id) is WorldEventGroup group)
                    groups[groups.IndexOf(group)] = group.WithSpreadTo(to!);
            }
            return ok;
        }

        private static WorldEventReward? CheckEventReward(WorldEventRewardData r, string path, ItemCatalog items, ValidationReport report)
        {
            bool ok = true;
            if (r.Xp is null) { Missing(report, path + ".xp", "expérience (0 pour aucune)"); ok = false; }
            else if (r.Xp < 0 || r.Xp > MaxEventXp) { report.Add(path + ".xp", ValidationCode.OutOfRange, $"expérience hors de [0, {MaxEventXp}]."); ok = false; }
            if (r.Money is null) { Missing(report, path + ".money", "pièces de cuivre (0 pour aucune)"); ok = false; }
            else if (!Coins.IsValid(r.Money.Value)) { report.Add(path + ".money", ValidationCode.OutOfRange, $"pièces hors de [0, {Coins.Max}] cuivre."); ok = false; }
            var given = new List<(string, int)>();
            if (r.Items != null)
            {
                if (r.Items.Count > MaxEventRewardItems) { report.Add(path + ".items", ValidationCode.LimitExceeded, $"{r.Items.Count} objets, au plus {MaxEventRewardItems}."); ok = false; }
                else
                    for (int i = 0; i < r.Items.Count; i++)
                    {
                        string p = $"{path}.items[{i}]";
                        if (r.Items[i]?.Item is not string item || !items.TryGetItem(item, out ItemDefinition def)) { report.Add(p + ".item", ValidationCode.UnknownReference, $"objet inconnu « {r.Items[i]?.Item} »."); ok = false; }
                        else if (r.Items[i]!.Count is not int count || count < 1 || count > def.Stack) { report.Add(p + ".count", ValidationCode.OutOfRange, $"quantité hors de [1, {def.Stack}]."); ok = false; }
                        else if (given.Any(x => x.Item1 == item)) { report.Add(p + ".item", ValidationCode.DuplicateId, $"objet « {item} » déjà donné."); ok = false; }
                        else given.Add((item, count));
                    }
            }
            if (r.Fact != null && !CheckDeclaredFact(r.Fact, path + ".fact", report)) ok = false;   // revue Codex de la PR #107 : « fact.met.… » réservé
            return ok ? new WorldEventReward(r.Xp!.Value, r.Money!.Value, given, r.Fact) : null;
        }

        /// <summary>Sous-lot V2.5 : clé du registre d'un personnage pour un événement (<c>zone.vallons/attaque_hameau</c>) ; la seule formule (cœur et validateur).</summary>
        public static string EventRegistryKey(string zone, string eventId) => zone + "/" + eventId;

        /// <summary>Sous-lot V2.6 : clé d'une cicatrice dans la sauvegarde (<c>zone.vallons/faille_du_seuil/ruines</c>) ; la seule formule (cœur et validateur).</summary>
        public static string ScarKey(string zone, string eventId, string group) => zone + "/" + eventId + "/" + group;

        /// <summary>Sous-lot V2.5 : minute du jour d'une heure « HH:MM » (00:00 à 23:59), ou null.</summary>
        public static int? ParseClock(string? text)
        {
            if (text is null || text.Length != 5 || text[2] != ':') return null;
            if (!int.TryParse(text.Substring(0, 2), NumberStyles.None, CultureInfo.InvariantCulture, out int h)
                || !int.TryParse(text.Substring(3, 2), NumberStyles.None, CultureInfo.InvariantCulture, out int m)) return null;
            return h < 24 && m < 60 ? h * 60 + m : (int?)null;
        }
    }
}

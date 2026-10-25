using System.Collections.Generic;
using System.Linq;
using EdS.Data.Items;
using EdS.Data.Saves;
using EdS.Runtime;
using EdS.World;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot 12.8 : écrans de l'aventure (IMGUI, provisoire) : inventaire et équipement (I), journal de quête et découvertes (J),
    /// atelier et métiers (K), et le dialogue ouvert avec un PNJ. Tous les textes viennent des fichiers de textes par clés (cadrage Q37) ou
    /// des données ; toute action passe par <see cref="AdventureSession"/>, donc par le cœur : un refus ne change rien et sa raison s'affiche.
    /// </summary>
    public sealed class AdventureScreens
    {
        public enum Panel { None, Inventory, Journal, Crafting, Map }

        /// <summary>Sous-lot 12.9d : la carte (petite en haut à droite, grande dans cet écran).</summary>
        public MiniMap Map { get; } = new MiniMap();

        /// <summary>Ennemis en vue à montrer sur la carte (mis à jour par le jeu à chaque image).</summary>
        public IReadOnlyList<MapEnemy> Enemies { get; set; } = new MapEnemy[0];

        private const float PanelWidth = 1200f, PanelHeight = 820f, Row = 34f;

        public Panel Open { get; private set; }

        public void Toggle(Panel panel) => Open = Open == panel ? Panel.None : panel;

        public void Close() => Open = Panel.None;

        private static Rect Frame(out float x, out float y)
        {
            x = Mathf.Max(10f, (Hud.Width - PanelWidth) / 2f);
            y = 90f;
            var frame = new Rect(x, y, PanelWidth, PanelHeight);
            Hud.Fill(frame, new Color(0.05f, 0.05f, 0.08f, 0.93f));
            return frame;
        }

        /// <summary>Dessine l'écran ouvert ; un bouton « Fermer » le ferme.</summary>
        public void Draw(AdventureSession a)
        {
            if (Open == Panel.None) return;
            Frame(out float x, out float y);
            if (Open == Panel.Inventory) Inventory(a, x, y);
            else if (Open == Panel.Journal) Journal(a, x, y);
            else if (Open == Panel.Map)
            {
                Hud.Title(new Rect(x + 20f, y + 12f, 600f, 40f), a.Text("text.ui.title.map"));
                Map.DrawLarge(a, new Rect(x + 20f, y + 60f, PanelWidth - 40f, PanelHeight - 130f), Enemies);
            }
            else Crafting(a, x, y);
            if (Hud.Button(new Rect(x + PanelWidth - 260f, y + PanelHeight - 56f, 240f, 42f), a.Text("text.ui.close"), true)) Close();
        }

        private static void Inventory(AdventureSession a, float x, float y)
        {
            WorldSession w = a.World;
            Hud.Title(new Rect(x + 20f, y + 12f, 600f, 40f), a.Text("text.ui.title.inventory"));
            Hud.Small(new Rect(x + 20f, y + 54f, PanelWidth - 40f, 28f),
                string.Format(a.Text("text.ui.character"), w.Level, w.Character.Xp, w.EnteredLevel));
            // Équipement porté, une ligne par emplacement.
            Hud.Text(new Rect(x + 20f, y + 92f, 560f, 30f), "<b>" + a.Text("text.ui.title.equipment") + "</b>");
            float row = y + 126f;
            foreach (string slot in w.Inventory.Catalog.Slots)
            {
                w.Inventory.Equipped.TryGetValue(slot, out string worn);
                Hud.Small(new Rect(x + 20f, row, 400f, Row), a.Text("text.ui.slot." + slot) + " : " + Hud.Plain(worn == null ? a.Text("text.ui.empty_slot") : a.Item(worn).Name));
                if (worn != null && Hud.Button(new Rect(x + 430f, row, 130f, Row - 4f), a.Text("text.ui.unequip"), true)) a.Unequip(slot);
                row += Row;
            }
            Hud.Small(new Rect(x + 20f, row + 10f, 560f, 60f), a.Text("text.ui.next_rest"));
            // Métiers sous l'équipement.
            row += 80f;
            Hud.Text(new Rect(x + 20f, row, 560f, 30f), "<b>" + a.Text("text.ui.title.professions") + "</b>");
            row += 34f;
            foreach ((string name, int level, long xp) in a.Professions())
            {
                Hud.Small(new Rect(x + 20f, row, 560f, Row), string.Format(a.Text("text.ui.profession_line"), name, level, xp));
                row += Row;
            }
            // Sac : une ligne par place occupée.
            float bx = x + 600f;
            IReadOnlyList<ItemStack> stacks = w.Inventory.Stacks;
            Hud.Text(new Rect(bx, y + 92f, 580f, 30f), "<b>" + string.Format(a.Text("text.ui.places"), w.Inventory.FreePlaces, w.Inventory.Catalog.InventorySize) + "</b>");
            for (int i = 0; i < stacks.Count; i++)
            {
                float ry = y + 126f + i * 26f;
                if (ry > y + PanelHeight - 90f) break;
                ItemDefinition item = a.Item(stacks[i].Item);
                string label = Hud.Plain(item.Name) + (stacks[i].Count > 1 ? " ×" + stacks[i].Count : "")
                    + (item.Type == ItemType.Equipment ? "  (" + string.Format(a.Text("text.ui.level_required"), item.RequiredLevel) + ")" : "");
                Hud.Small(new Rect(bx, ry, 440f, 26f), label);
                if (item.Type == ItemType.Equipment && Hud.Button(new Rect(bx + 450f, ry, 120f, 24f), a.Text("text.ui.equip"), w.Level >= item.RequiredLevel))
                    a.Equip(i);
            }
        }

        private static void Journal(AdventureSession a, float x, float y)
        {
            Hud.Title(new Rect(x + 20f, y + 12f, 600f, 40f), a.Text("text.ui.title.journal"));
            IReadOnlyList<JournalEntry> quests = a.Journal();
            float row = y + 60f;
            if (quests.Count == 0) Hud.Text(new Rect(x + 20f, row, PanelWidth - 40f, 60f), a.Text("text.ui.journal_empty"));
            foreach (JournalEntry q in quests)
            {
                Hud.Text(new Rect(x + 20f, row, PanelWidth - 40f, 30f), "<b>" + Hud.Plain(q.Name) + "</b>" + (q.Done ? " — " + a.Text("text.ui.quest_done_mark") : ""));
                row += 32f;
                if (q.Step != null)
                {
                    Hud.Small(new Rect(x + 40f, row, PanelWidth - 60f, 60f), Hud.Plain(q.Step) + (q.Progress != null ? "  (" + q.Progress + ")" : ""));
                    row += 60f;
                }
            }
            row += 20f;
            Hud.Text(new Rect(x + 20f, row, PanelWidth - 40f, 30f), "<b>" + a.Text("text.ui.title.discoveries") + "</b>");
            row += 34f;
            foreach (string d in a.Discoveries())
            {
                if (row > y + PanelHeight - 110f) break;
                Hud.Small(new Rect(x + 40f, row, PanelWidth - 60f, 52f), "• " + Hud.Plain(d));
                row += 52f;
            }
        }

        private static void Crafting(AdventureSession a, float x, float y)
        {
            Hud.Title(new Rect(x + 20f, y + 12f, 600f, 40f), a.Text("text.ui.title.crafting"));
            float row = y + 64f;
            foreach (RecipeEntry r in a.Recipes())
            {
                bool possible = r.Refusal == CraftRefusal.None;
                Hud.Text(new Rect(x + 20f, row, 900f, 30f), "<b>" + Hud.Plain(r.Name) + "</b>  — " + Hud.Plain(r.Profession) + ", "
                    + string.Format(a.Text("text.ui.level_required"), r.RequiredLevel));
                if (Hud.Button(new Rect(x + PanelWidth - 260f, row, 240f, 40f), a.Text("text.ui.craft"), possible)) a.Craft(r.Id);
                Hud.Small(new Rect(x + 40f, row + 32f, 900f, 26f), Hud.Plain(r.Inputs) + "  →  " + Hud.Plain(r.Output));
                if (!possible) Hud.Small(new Rect(x + 40f, row + 58f, 900f, 26f), "<color=#ffb070>" + a.Text("text.ui.craft." + r.Refusal.ToString().ToLowerInvariant()) + "</color>");
                row += 100f;
            }
            row += 10f;
            Hud.Text(new Rect(x + 20f, row, 560f, 30f), "<b>" + a.Text("text.ui.title.professions") + "</b>");
            row += 34f;
            foreach ((string name, int level, long xp) in a.Professions())
            {
                Hud.Small(new Rect(x + 20f, row, 560f, Row), string.Format(a.Text("text.ui.profession_line"), name, level, xp));
                row += Row;
            }
        }

        /// <summary>Rang rendu par <see cref="DrawDialogue"/> quand le bouton Fermer est touché.</summary>
        public const int CloseDialogue = -2;

        /// <summary>
        /// Dialogue ouvert, en bas de l'écran : nom du PNJ, son texte, les réponses (boutons, ou touches 1 à 9) et un bouton Fermer (revue Codex
        /// de la PR #49 : au doigt, sans clavier, même un dialogue sans réponse se ferme). Rend le rang de la réponse choisie au bouton,
        /// <see cref="CloseDialogue"/>, ou -1.
        /// </summary>
        public static int DrawDialogue(AdventureSession a)
        {
            if (a.Dialogue == null) return -1;
            IReadOnlyList<string> answers = a.DialogueAnswers;
            float height = 200f + answers.Count * 48f;
            float x = Mathf.Max(10f, (Hud.Width - 1300f) / 2f), y = Hud.VirtualHeight - height - 30f;
            Hud.Fill(new Rect(x, y, 1300f, height), new Color(0.06f, 0.05f, 0.04f, 0.93f));
            Hud.Title(new Rect(x + 20f, y + 10f, 1260f, 36f), Hud.Plain(a.DialogueNpc));
            Hud.Text(new Rect(x + 20f, y + 50f, 1260f, 90f), Hud.Plain(a.DialogueText));
            int chosen = -1;
            for (int i = 0; i < answers.Count; i++)
                if (Hud.Button(new Rect(x + 40f, y + 140f + i * 48f, 1220f, 42f), Hud.Plain(string.Format(a.Text("text.ui.answer"), i + 1, answers[i])), true))
                    chosen = i;
            if (Hud.Button(new Rect(x + 1300f - 280f, y + height - 50f, 260f, 42f), a.Text("text.ui.close"), true)) chosen = CloseDialogue;
            return chosen;
        }
    }
}

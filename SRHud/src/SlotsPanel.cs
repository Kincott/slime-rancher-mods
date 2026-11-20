using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SRHud
{
    // Bottom-center vacpack slots:
    //            Carrot  12 / 50              <- selected item, hidden when the slot is empty
    //   ┌──┐ ┌──┐ ╔══╗ ┌──┐ ┌──┐
    //   │🥕│ │  │ ║🍓║ │  │ │💧│               <- icon, count, thin fill gauge in the item's color
    //   └──┘ └──┘ ╚══╝ └──┘ └──┘                 empty slots are just a dim number, no big X
    // Icons, colors and names come from VanillaLook, so slime appearances and translations stay exactly as the
    // game shows them.
    internal class SlotsPanel
    {
        private const float SlotSize = 64f;
        private const float Gap = 8f;
        private const int MaxSlots = 5;

        private readonly RectTransform root;
        private readonly TextMeshProUGUI selectedLabel;
        private readonly Slot[] slots = new Slot[MaxSlots];
        private readonly PlayerState player;
        private readonly TimeDirector time;

        private readonly VanillaLook look;

        private int lastSelected = -2, lastUsable = -1;
        // What the selected-item label currently shows; rebuilt only when one of these changes (no per-frame
        // string allocation, which would feed the garbage collector).
        private int labelSlot = -2, labelCount = -1, labelMax = -1;
        private Identifiable.Id labelId = (Identifiable.Id)(-1);
        private int lastMagicMins = -1;

        private class Slot
        {
            internal RectTransform Root;
            internal Image Back, Ring;
            internal IconOrBadge Icon;
            internal TextMeshProUGUI Count, Index;
            internal Bar Gauge;
            internal Identifiable.Id LastId = (Identifiable.Id)(-1);
            internal int LastCount = -1, LastMax = -1;
        }

        internal SlotsPanel(Transform canvas, VanillaLook look, PlayerState player, TimeDirector time)
        {
            this.player = player;
            this.time = time;
            this.look = look;

            root = Theme.Rect("Slots", canvas);
            Theme.Place(root, new Vector2(0.5f, 0f), new Vector2(0f, 28f),
                new Vector2(MaxSlots * SlotSize + (MaxSlots - 1) * Gap, SlotSize + 34f));

            selectedLabel = Theme.Text("Selected", root, 19f, Theme.TextPrimary, TextAlignmentOptions.Bottom);
            Theme.Place(selectedLabel.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(420f, 28f));
            selectedLabel.fontStyle = FontStyles.Bold;

            for (int i = 0; i < MaxSlots; i++)
            {
                slots[i] = MakeSlot(i);
            }
        }

        private Slot MakeSlot(int i)
        {
            var s = new Slot();
            s.Root = Theme.Rect("Slot" + (i + 1), root);
            Theme.Place(s.Root, Vector2.zero, new Vector2(i * (SlotSize + Gap), 0f), new Vector2(SlotSize, SlotSize));

            s.Ring = Theme.Box("Ring", s.Root, Theme.SelectedRing, 1f);
            Theme.Fill(s.Ring.rectTransform, -2f);
            s.Back = Theme.Box("Back", s.Root, Theme.SlotIdle, 1f);
            Theme.Fill(s.Back.rectTransform, 0f);

            // Fixed-size, centered (slightly raised to clear the gauge): stretching to the slot left the sprite
            // small and off-center in game.
            s.Icon = new IconOrBadge("Icon", s.Root);
            Theme.Place(s.Icon.Root, new Vector2(0.5f, 0.5f), new Vector2(0f, 3f), new Vector2(44f, 44f));

            s.Index = Theme.Text("Index", s.Root, 12f, Theme.TextSecondary, TextAlignmentOptions.TopLeft);
            Theme.Fill(s.Index.rectTransform, 5f);
            s.Index.text = (i + 1).ToString();

            s.Count = Theme.Text("Count", s.Root, 14f, Theme.TextPrimary, TextAlignmentOptions.BottomRight);
            Theme.Fill(s.Count.rectTransform, 6f);
            s.Count.rectTransform.offsetMin = new Vector2(6f, 9f);
            s.Count.fontStyle = FontStyles.Bold;

            s.Gauge = new Bar(s.Root, Color.white);
            s.Gauge.Root.anchorMin = new Vector2(0f, 0f);
            s.Gauge.Root.anchorMax = new Vector2(1f, 0f);
            s.Gauge.Root.offsetMin = new Vector2(7f, 5f);
            s.Gauge.Root.offsetMax = new Vector2(-7f, 10f);
            return s;
        }

        internal void Update()
        {
            Ammo ammo = player.Ammo;
            int usable = Math.Min(ammo.GetUsableSlotCount(), MaxSlots);
            int selected = ammo.GetSelectedAmmoIdx();

            if (usable != lastUsable)
            {
                lastUsable = usable;
                float width = usable * SlotSize + Math.Max(0, usable - 1) * Gap;
                root.sizeDelta = new Vector2(width, root.sizeDelta.y);
                for (int i = 0; i < MaxSlots; i++)
                {
                    slots[i].Root.gameObject.SetActive(i < usable);
                }
            }

            for (int i = 0; i < usable; i++)
            {
                UpdateSlot(slots[i], i, ammo, i == selected);
            }

            if (selected != lastSelected)
            {
                lastSelected = selected;
                for (int i = 0; i < usable; i++)
                {
                    bool on = i == selected;
                    slots[i].Ring.enabled = on;
                    slots[i].Back.color = on ? Theme.SlotSelected : Theme.SlotIdle;
                    slots[i].Root.localScale = on ? new Vector3(1.08f, 1.08f, 1f) : Vector3.one;
                    slots[i].Index.color = on ? Theme.TextPrimary : Theme.TextSecondary;
                }
            }

            UpdateSelectedLabel(ammo, selected, usable);
        }

        private void UpdateSlot(Slot s, int i, Ammo ammo, bool isSelected)
        {
            Identifiable.Id id = ammo.GetSlotName(i);
            int count = id == Identifiable.Id.NONE ? 0 : ammo.GetSlotCount(i);
            int max = ammo.GetSlotMaxCount(i);

            if (id != s.LastId)
            {
                s.LastId = id;
                // The item's own color (as the vanilla slot uses); a neutral accent if the game gives none.
                Color c = id == Identifiable.Id.NONE ? Theme.Energy : look.Color(id);
                if (id == Identifiable.Id.NONE)
                {
                    s.Icon.Clear();
                }
                else
                {
                    Sprite first, second;
                    if (look.LargoPair(id, out first, out second))
                    {
                        s.Icon.SetPair(first, second);
                    }
                    else
                    {
                        Sprite sprite = look.Icon(id);
                        if (VanillaLook.IsPlaceholder(sprite)) sprite = null;
                        s.Icon.Set(sprite, look.Name(id), c); // letter badge if the item has no image
                    }
                }
                if (c.r + c.g + c.b < 0.15f) c = Theme.Energy;
                c.a = 1f;
                s.Gauge.Color = c;
                s.LastCount = -1; // force count refresh
            }

            if (count != s.LastCount || max != s.LastMax)
            {
                s.LastCount = count;
                s.LastMax = max;
                s.Gauge.Set(count, max);
                s.Count.text = count > 0 ? count.ToString() : "";
                // A full slot is worth noticing: the count turns gold.
                s.Count.color = count > 0 && count >= max ? Theme.Gold : Theme.TextPrimary;
            }

            // The last slot is the liquid slot; show the "magic water" timer on it while active.
            if (i == MaxSlots - 1)
            {
                double magic = ammo.GetRemainingWaterIsMagicMins();
                int mins = magic > 0.0 ? (int)Math.Floor(magic) : -1;
                if (mins != lastMagicMins)
                {
                    lastMagicMins = mins;
                    if (mins >= 0)
                    {
                        s.Count.text = time.FormatTime(mins);
                        s.Count.color = Theme.Energy;
                    }
                    s.LastCount = -1; // back to the plain count once the timer ends
                }
            }
        }

        private void UpdateSelectedLabel(Ammo ammo, int selected, int usable)
        {
            bool valid = selected >= 0 && selected < usable;
            Identifiable.Id id = valid ? ammo.GetSlotName(selected) : Identifiable.Id.NONE;
            int count = id != Identifiable.Id.NONE ? ammo.GetSlotCount(selected) : 0;
            int max = valid ? ammo.GetSlotMaxCount(selected) : 0;
            if (selected == labelSlot && id == labelId && count == labelCount && max == labelMax)
            {
                return;
            }
            labelSlot = selected;
            labelId = id;
            labelCount = count;
            labelMax = max;

            if (id == Identifiable.Id.NONE)
            {
                selectedLabel.text = "";
                return;
            }
            selectedLabel.text = look.Name(id) + "  <alpha=#99>" + count + " / " + max;
        }
    }
}

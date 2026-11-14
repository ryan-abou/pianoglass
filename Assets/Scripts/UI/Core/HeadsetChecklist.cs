#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Collections.Generic;

namespace PianoGlass.UI
{
    /// <summary>
    /// A post-build headset test checklist, read from a small text file that
    /// is pushed to the headset with adb, so it changes without a rebuild.
    /// Pure: the development-build panel reads the file and draws this.
    ///
    /// The format is plain Markdown:
    ///
    ///   # Batch G round
    ///   - Launch, don't recenter for 15 s | keys on the piano?
    ///   - Middle C fix with the bare tip | keys stay put
    ///
    /// The first "#" line is the title. Every "- ", "* ", "1. " or "- [ ] "
    /// line is an item; text after " | " is its detail (what pass looks
    /// like), drawn smaller. Anything else is ignored. "**" and "`" are
    /// dropped, since the panel shows text, not Markdown.
    ///
    /// Ticks are keyed by title and item text, so pushing the same list again
    /// keeps them and an edited item starts unticked.
    /// </summary>
    public sealed class HeadsetChecklist
    {
        /// <summary>Rows the panel has room for. Checklists are meant to be
        /// 5 to 8 items; the rest are counted in <see cref="Overflow"/>.</summary>
        public const int MaxItems = 10;

        public sealed class Item
        {
            public string Text { get; }
            public string Detail { get; }
            public string Key { get; }
            public bool Ticked { get; internal set; }

            internal Item(string text, string detail, string key)
            {
                Text = text;
                Detail = detail;
                Key = key;
            }
        }

        private readonly List<Item> _items = new List<Item>();

        public string Title { get; }
        public IReadOnlyList<Item> Items => _items;

        /// <summary>Items past <see cref="MaxItems"/>, left out.</summary>
        public int Overflow { get; private set; }

        public int TickedCount
        {
            get
            {
                int n = 0;
                foreach (Item i in _items) if (i.Ticked) n++;
                return n;
            }
        }

        private HeadsetChecklist(string title) => Title = title;

        /// <summary>Never null: an empty or missing file gives an empty list.</summary>
        public static HeadsetChecklist Parse(string text)
        {
            string title = null;
            var items = new List<(string text, string detail)>();

            foreach (string raw in (text ?? "").Replace("\r", "").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;

                if (line.StartsWith("# "))
                {
                    if (title == null) title = Clean(line.Substring(2));
                    continue;
                }

                string body = ItemBody(line);
                if (body == null) continue;

                string detail = "";
                int bar = body.IndexOf(" | ");
                if (bar >= 0)
                {
                    detail = Clean(body.Substring(bar + 3));
                    body = body.Substring(0, bar);
                }
                body = Clean(body);
                if (body.Length > 0) items.Add((body, detail));
            }

            var list = new HeadsetChecklist(string.IsNullOrEmpty(title) ? "Checklist" : title);
            for (int i = 0; i < items.Count; i++)
            {
                if (i >= MaxItems) { list.Overflow++; continue; }
                list._items.Add(new Item(items[i].text, items[i].detail, list.Title + "\n" + items[i].text));
            }
            return list;
        }

        /// <summary>Flips one item; returns its new state.</summary>
        public bool Toggle(int index)
        {
            Item item = _items[index];
            item.Ticked = !item.Ticked;
            return item.Ticked;
        }

        /// <summary>Ticks every item whose key is saved. Keys from another
        /// list or an edited item match nothing.</summary>
        public void ApplyTicks(IEnumerable<string> keys)
        {
            if (keys == null) return;
            var saved = new HashSet<string>(keys);
            foreach (Item i in _items) i.Ticked = saved.Contains(i.Key);
        }

        public List<string> TickedKeys()
        {
            var keys = new List<string>();
            foreach (Item i in _items) if (i.Ticked) keys.Add(i.Key);
            return keys;
        }

        /// <summary>The item text of a list line, or null if it is not one.</summary>
        private static string ItemBody(string line)
        {
            if (line.StartsWith("- [ ] ") || line.StartsWith("- [x] ") || line.StartsWith("- [X] "))
                return line.Substring(6);
            if (line.StartsWith("- ") || line.StartsWith("* "))
                return line.Substring(2);

            int digits = 0;
            while (digits < line.Length && char.IsDigit(line[digits])) digits++;
            if (digits > 0 && digits + 1 < line.Length && (line[digits] == '.' || line[digits] == ')') &&
                line[digits + 1] == ' ')
                return line.Substring(digits + 2);
            return null;
        }

        private static string Clean(string s) => s.Replace("**", "").Replace("`", "").Trim();
    }
}
#endif

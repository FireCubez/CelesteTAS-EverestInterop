﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CelesteStudio.RichText {
    /// <summary>
    /// Popup menu for autocomplete
    /// </summary>
    [Browsable(false)]
    public class AutocompleteMenu : ToolStripDropDown {
        readonly AutocompleteListView listView;
        public ToolStripControlHost host;

        public AutocompleteMenu(RichText tb) {
            // create a new popup and add the list view to it 
            AutoClose = false;
            AutoSize = false;
            Margin = Padding.Empty;
            Padding = Padding.Empty;
            listView = new AutocompleteListView(tb);
            host = new ToolStripControlHost(listView);
            host.Margin = new Padding(2, 2, 2, 2);
            host.Padding = Padding.Empty;
            host.AutoSize = false;
            host.AutoToolTip = false;
            CalcSize();
            base.Items.Add(host);
            listView.Parent = this;
            SearchPattern = @"[\w\.]";
            MinFragmentLength = 2;
        }

        public Range Fragment { get; internal set; }

        /// <summary>
        /// Regex pattern for serach fragment around caret
        /// </summary>
        public string SearchPattern { get; set; }

        /// <summary>
        /// Minimum fragment length for popup
        /// </summary>
        public int MinFragmentLength { get; set; }

        /// <summary>
        /// Allow TAB for select menu item
        /// </summary>
        public bool AllowTabKey {
            get => listView.AllowTabKey;
            set => listView.AllowTabKey = value;
        }

        /// <summary>
        /// Interval of menu appear (ms)
        /// </summary>
        public int AppearInterval {
            get => listView.AppearInterval;
            set => listView.AppearInterval = value;
        }

        public new AutocompleteListView Items => listView;

        /// <summary>
        /// User selects item
        /// </summary>
        public event EventHandler<SelectingEventArgs> Selecting;

        /// <summary>
        /// It fires after item inserting
        /// </summary>
        public event EventHandler<SelectedEventArgs> Selected;

        /// <summary>
        /// Occurs when popup menu is opening
        /// </summary>
        public new event EventHandler<CancelEventArgs> Opening;

        internal new void OnOpening(CancelEventArgs args) {
            if (Opening != null) {
                Opening(this, args);
            }
        }

        public new void Close() {
            listView.toolTip.Hide(listView);
            base.Close();
        }

        internal void CalcSize() {
            host.Size = listView.Size;
            Size = new Size(listView.Size.Width + 4, listView.Size.Height + 4);
        }

        public virtual void OnSelecting() {
            listView.OnSelecting();
        }

        public void SelectNext(int shift) {
            listView.SelectNext(shift);
        }

        internal void OnSelecting(SelectingEventArgs args) {
            if (Selecting != null) {
                Selecting(this, args);
            }
        }

        public void OnSelected(SelectedEventArgs args) {
            if (Selected != null) {
                Selected(this, args);
            }
        }

        /// <summary>
        /// Shows popup menu immediately
        /// </summary>
        /// <param name="forced">If True - MinFragmentLength will be ignored</param>
        public void Show(bool forced) {
            Items.DoAutocomplete(forced);
        }
    }

    public class AutocompleteListView : UserControl {
        readonly int hoveredItemIndex = -1;
        readonly int itemHeight;
        readonly RichText tb;
        readonly Timer timer = new();

        int oldItemCount = 0;
        int selectedItemIndex = 0;
        IEnumerable<AutocompleteItem> sourceItems = new List<AutocompleteItem>();
        internal ToolTip toolTip = new();
        internal List<AutocompleteItem> visibleItems;

        internal AutocompleteListView(RichText tb) {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            base.Font = new Font(FontFamily.GenericSansSerif, 9);
            visibleItems = new List<AutocompleteItem>();
            itemHeight = Font.Height + 2;
            VerticalScroll.SmallChange = itemHeight;
            BackColor = Color.White;
            MaximumSize = new Size(Size.Width, 180);
            toolTip.ShowAlways = false;
            AppearInterval = 500;
            timer.Tick += new EventHandler(timer_Tick);

            this.tb = tb;

            tb.KeyDown += new KeyEventHandler(tb_KeyDown);
            tb.SelectionChanged += new EventHandler(tb_SelectionChanged);
            tb.KeyPressed += new KeyPressEventHandler(tb_KeyPressed);

            Form form = tb.FindForm();
            if (form != null) {
                form.LocationChanged += (o, e) => Menu.Close();
                form.ResizeBegin += (o, e) => Menu.Close();
                form.FormClosing += (o, e) => Menu.Close();
                form.LostFocus += (o, e) => Menu.Close();
            }

            tb.LostFocus += (o, e) => {
                if (!Menu.Focused) {
                    Menu.Close();
                }
            };

            tb.Scroll += (o, e) => Menu.Close();
        }

        AutocompleteMenu Menu => Parent as AutocompleteMenu;

        internal bool AllowTabKey { get; set; }
        public ImageList ImageList { get; set; }

        internal int AppearInterval {
            get => timer.Interval;
            set => timer.Interval = value;
        }

        public int Count => visibleItems.Count;

        void tb_KeyPressed(object sender, KeyPressEventArgs e) {
            bool backspaceORdel = e.KeyChar == '\b' || e.KeyChar == 0xff;

            /*
            if (backspaceORdel)
                prevSelection = tb.Selection.Start;*/

            if (Menu.Visible && !backspaceORdel) {
                DoAutocomplete(false);
            } else if (Menu.Visible || (!backspaceORdel && e.KeyChar != ' ' && e.KeyChar != '\t')) {
                ResetTimer(timer);
            }
        }

        void timer_Tick(object sender, EventArgs e) {
            timer.Stop();
            DoAutocomplete(false);
        }

        void ResetTimer(Timer timer) {
            timer.Stop();
            timer.Start();
        }

        internal void DoAutocomplete() {
            DoAutocomplete(false);
        }

        internal void DoAutocomplete(bool forced) {
            if (!Menu.Enabled) {
                Menu.Close();
                return;
            }

            visibleItems.Clear();
            selectedItemIndex = 0;
            VerticalScroll.Value = 0;
            //get fragment around caret
            Range fragment = tb.Selection.GetFragment(Menu.SearchPattern);
            string text = fragment.Text;
            //calc screen point for popup menu
            Point point = tb.PlaceToPoint(fragment.End);
            point.Offset(2, tb.CharHeight);
            if (forced || (text.Length >= Menu.MinFragmentLength && tb.Selection.IsEmpty)) {
                Menu.Fragment = fragment;
                bool foundSelected = false;
                //build popup menu
                foreach (var item in sourceItems) {
                    item.Parent = Menu;
                    CompareResult res = item.Compare(text);
                    if (res == CompareResult.ExactAndReplace) {
                        tb.TextSource.Manager.BeginAutoUndoCommands();
                        try {
                            DoAutocomplete(item, fragment);
                            return;
                        } finally {
                            tb.TextSource.Manager.EndAutoUndoCommands();
                        }
                    } else if (res != CompareResult.Hidden) {
                        visibleItems.Add(item);
                    }

                    if (res == CompareResult.VisibleAndSelected && !foundSelected) {
                        foundSelected = true;
                        selectedItemIndex = visibleItems.Count - 1;
                    }
                }

                if (foundSelected) {
                    AdjustScroll();
                    DoSelectedVisible();
                }
            }

            //show popup menu
            if (Count > 0) {
                if (!Menu.Visible) {
                    CancelEventArgs args = new();
                    Menu.OnOpening(args);
                    if (!args.Cancel) {
                        Menu.Show(tb, point);
                    }
                } else {
                    Invalidate();
                }
            } else {
                Menu.Close();
            }
        }

        public IEnumerable<AutocompleteItem> AutoCompleteItems() {
            return sourceItems;
        }

        public AutocompleteItem GetItem(string text, Type itemType = null) {
            itemType = itemType == null ? typeof(AutocompleteItem) : itemType;
            if (!typeof(AutocompleteItem).IsAssignableFrom(itemType)) {
                throw new Exception("Type must be of AutocompleteItem");
            }

            foreach (AutocompleteItem item in sourceItems) {
                if (itemType.IsInstanceOfType(item) && item.Text.Equals(text, StringComparison.OrdinalIgnoreCase)) {
                    return item;
                }
            }

            return null;
        }

        void tb_SelectionChanged(object sender, EventArgs e) {
            /*
            FastColoredTextBox tb = sender as FastColoredTextBox;
            
            if (Math.Abs(prevSelection.iChar - tb.Selection.Start.iChar) > 1 ||
                        prevSelection.iLine != tb.Selection.Start.iLine)
                Menu.Close();
            prevSelection = tb.Selection.Start;*/
            if (Menu.Visible) {
                bool needClose = false;

                if (!tb.Selection.IsEmpty) {
                    needClose = true;
                } else if (!Menu.Fragment.Contains(tb.Selection.Start)) {
                    if (tb.Selection.Start.iLine == Menu.Fragment.End.iLine && tb.Selection.Start.iChar == Menu.Fragment.End.iChar + 1) {
                        //user press key at end of fragment
                        char c = tb.Selection.CharBeforeStart;
                        if (!Regex.IsMatch(c.ToString(), Menu.SearchPattern)) //check char
                        {
                            needClose = true;
                        }
                    } else {
                        needClose = true;
                    }
                }

                if (needClose) {
                    Menu.Close();
                }
            }
        }

        void tb_KeyDown(object sender, KeyEventArgs e) {
            if (Menu.Visible) {
                if (ProcessKey(e.KeyCode, e.Modifiers)) {
                    e.Handled = true;
                }
            }

            if (!Menu.Visible) {
                if (e.Modifiers == Keys.Control && e.KeyCode == Keys.Space) {
                    DoAutocomplete();
                    e.Handled = true;
                }
            }
        }

        void AdjustScroll() {
            if (oldItemCount == visibleItems.Count) {
                return;
            }

            int needHeight = itemHeight * visibleItems.Count + 1;
            Height = Math.Min(needHeight, MaximumSize.Height);
            Menu.CalcSize();

            AutoScrollMinSize = new Size(0, needHeight);
            oldItemCount = visibleItems.Count;
        }

        protected override void OnPaint(PaintEventArgs e) {
            AdjustScroll();
            int startI = VerticalScroll.Value / itemHeight - 1;
            int finishI = (VerticalScroll.Value + ClientSize.Height) / itemHeight + 1;
            startI = Math.Max(startI, 0);
            finishI = Math.Min(finishI, visibleItems.Count);
            int y = 0;
            int leftPadding = 18;
            for (int i = startI; i < finishI; i++) {
                y = i * itemHeight - VerticalScroll.Value;

                if (ImageList != null && visibleItems[i].ImageIndex >= 0) {
                    e.Graphics.DrawImage(ImageList.Images[visibleItems[i].ImageIndex], 1, y);
                }

                if (i == selectedItemIndex) {
                    Brush selectedBrush = new LinearGradientBrush(new Point(0, y - 3), new Point(0, y + itemHeight), Color.White, Color.Orange);
                    e.Graphics.FillRectangle(selectedBrush, leftPadding, y, ClientSize.Width - 1 - leftPadding, itemHeight - 1);
                    e.Graphics.DrawRectangle(Pens.Orange, leftPadding, y, ClientSize.Width - 1 - leftPadding, itemHeight - 1);
                }

                if (i == hoveredItemIndex) {
                    e.Graphics.DrawRectangle(Pens.Red, leftPadding, y, ClientSize.Width - 1 - leftPadding, itemHeight - 1);
                }

                e.Graphics.DrawString(visibleItems[i].ToString(), Font, Brushes.Black, leftPadding, y);
            }
        }

        protected override void OnScroll(ScrollEventArgs se) {
            base.OnScroll(se);
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e) {
            base.OnMouseClick(e);

            if (e.Button == MouseButtons.Left) {
                selectedItemIndex = PointToItemIndex(e.Location);
                DoSelectedVisible();
                Invalidate();
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e) {
            base.OnMouseDoubleClick(e);
            selectedItemIndex = PointToItemIndex(e.Location);
            Invalidate();
            OnSelecting();
        }

        internal virtual void OnSelecting() {
            if (selectedItemIndex < 0 || selectedItemIndex >= visibleItems.Count) {
                return;
            }

            tb.TextSource.Manager.BeginAutoUndoCommands();
            try {
                AutocompleteItem item = visibleItems[selectedItemIndex];
                SelectingEventArgs args = new() {Item = item, SelectedIndex = selectedItemIndex};

                Menu.OnSelecting(args);

                if (args.Cancel) {
                    selectedItemIndex = args.SelectedIndex;
                    Invalidate();
                    return;
                }

                if (!args.Handled) {
                    var fragment = Menu.Fragment;
                    DoAutocomplete(item, fragment);
                }

                Menu.Close();
                //
                SelectedEventArgs args2 = new() {Item = item, Tb = Menu.Fragment.tb};
                item.OnSelected(Menu, args2);
                Menu.OnSelected(args2);
            } finally {
                tb.TextSource.Manager.EndAutoUndoCommands();
            }
        }

        private void DoAutocomplete(AutocompleteItem item, Range fragment) {
            string newText = item.GetTextForReplace();
            //replace text of fragment
            var tb = fragment.tb;
            if (tb.Selection.ColumnSelectionMode) {
                var start = tb.Selection.Start;
                var end = tb.Selection.End;
                start.iChar = fragment.Start.iChar;
                end.iChar = fragment.End.iChar;
                tb.Selection.Start = start;
                tb.Selection.End = end;
            } else {
                tb.Selection.Start = fragment.Start;
                tb.Selection.End = fragment.End;
            }

            tb.InsertText(newText);
            tb.Focus();
        }

        int PointToItemIndex(Point p) {
            return (p.Y + VerticalScroll.Value) / itemHeight;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData) {
            ProcessKey(keyData, Keys.None);

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private bool ProcessKey(Keys keyData, Keys keyModifiers) {
            if (keyModifiers == Keys.None) {
                switch (keyData) {
                    case Keys.Down:
                        SelectNext(+1);
                        return true;
                    case Keys.PageDown:
                        SelectNext(+10);
                        return true;
                    case Keys.Up:
                        SelectNext(-1);
                        return true;
                    case Keys.PageUp:
                        SelectNext(-10);
                        return true;
                    case Keys.Enter:
                        OnSelecting();
                        return true;
                    case Keys.Tab:
                        if (!AllowTabKey) {
                            break;
                        }

                        OnSelecting();
                        return true;
                    case Keys.Escape:
                        Menu.Close();
                        return true;
                }
            }

            return false;
        }

        public void SelectNext(int shift) {
            selectedItemIndex = Math.Max(0, Math.Min(selectedItemIndex + shift, visibleItems.Count - 1));
            DoSelectedVisible();
            //
            Invalidate();
        }

        private void DoSelectedVisible() {
            if (selectedItemIndex >= 0 && selectedItemIndex < visibleItems.Count) {
                SetToolTip(visibleItems[selectedItemIndex]);
            }

            int y = selectedItemIndex * itemHeight - VerticalScroll.Value;
            if (y < 0) {
                VerticalScroll.Value = selectedItemIndex * itemHeight;
            }

            if (y > ClientSize.Height - itemHeight) {
                VerticalScroll.Value = Math.Min(VerticalScroll.Maximum, selectedItemIndex * itemHeight - ClientSize.Height + itemHeight);
            }

            //some magic for update scrolls
            AutoScrollMinSize -= new Size(1, 0);
            AutoScrollMinSize += new Size(1, 0);
        }

        private void SetToolTip(AutocompleteItem autocompleteItem) {
            string title = visibleItems[selectedItemIndex].ToolTipTitle;
            string text = visibleItems[selectedItemIndex].ToolTipText;

            if (string.IsNullOrEmpty(title)) {
                toolTip.ToolTipTitle = null;
                toolTip.SetToolTip(this, null);
                return;
            }

            if (string.IsNullOrEmpty(text)) {
                toolTip.ToolTipTitle = null;
                toolTip.Show(title, this, Width + 3, 0, 3000);
            } else {
                toolTip.ToolTipTitle = title;
                toolTip.Show(text, this, Width + 3, 0, 3000);
            }
        }

        public void SetAutocompleteItems(ICollection<string> items) {
            List<AutocompleteItem> list = new(items.Count);
            foreach (string item in items) {
                list.Add(new AutocompleteItem(item));
            }

            SetAutocompleteItems(list);
        }

        public void SetAutocompleteItems(ICollection<AutocompleteItem> items) {
            sourceItems = items;
        }
    }

    public class SelectingEventArgs : EventArgs {
        public AutocompleteItem Item { get; internal set; }
        public bool Cancel { get; set; }
        public int SelectedIndex { get; set; }
        public bool Handled { get; set; }
    }

    public class SelectedEventArgs : EventArgs {
        public AutocompleteItem Item { get; internal set; }
        public RichText Tb { get; set; }
    }
}
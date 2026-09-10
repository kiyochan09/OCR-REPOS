using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using OCR_Translator.Models;
using OCR_Translator.Services;

namespace OCR_Translator
{
    public partial class Form1
    {
        private void InitializeTableRuleLineControls()
        {
            var grpTableLines = new GroupBox
            {
                Text = "表の罫線設定",
                Location = new Point(10, 285),
                Size = new Size(255, 185),
                ForeColor = Color.DarkSlateGray
            };

            btnTableAddHLine = new Button
            {
                Text = "＋ 横罫線追加",
                Location = new Point(8, 24),
                Size = new Size(116, 32),
                UseVisualStyleBackColor = true
            };
            btnTableAddHLine.Click += (s, e) =>
            {
                if (activeLineAddMode == ImageCoordinateHelper.RuleLineType.Horizontal)
                {
                    activeLineAddMode = ImageCoordinateHelper.RuleLineType.None;
                }
                else
                {
                    activeLineAddMode = ImageCoordinateHelper.RuleLineType.Horizontal;
                    isLineDeleteMode = false;
                }
                UpdateTableLineControlsState();
            };

            btnTableAddVLine = new Button
            {
                Text = "＋ 縦罫線追加",
                Location = new Point(130, 24),
                Size = new Size(116, 32),
                UseVisualStyleBackColor = true
            };
            btnTableAddVLine.Click += (s, e) =>
            {
                if (activeLineAddMode == ImageCoordinateHelper.RuleLineType.Vertical)
                {
                    activeLineAddMode = ImageCoordinateHelper.RuleLineType.None;
                }
                else
                {
                    activeLineAddMode = ImageCoordinateHelper.RuleLineType.Vertical;
                    isLineDeleteMode = false;
                }
                UpdateTableLineControlsState();
            };

            btnTableDeleteLine = new Button
            {
                Text = "－ 罫線削除",
                Location = new Point(8, 62),
                Size = new Size(116, 32),
                UseVisualStyleBackColor = true
            };
            btnTableDeleteLine.Click += (s, e) =>
            {
                int index = lstRegions.SelectedIndex;
                if (index >= 0 && index < regions.Count && regions[index].Type == "table")
                {
                    if (selectedRuleLineIndices.Count > 0)
                    {
                        var table = regions[index];
                        foreach (int delIdx in selectedRuleLineIndices.OrderByDescending(x => x))
                        {
                            if (delIdx >= 0 && delIdx < table.RuleLines.Count)
                                table.RuleLines.RemoveAt(delIdx);
                        }
                        selectedRuleLineIndices.Clear();
                        pageRegions[currentPage] = _layoutStorage.CloneRegions(regions);
                        _layoutStorage.ForceSavePageRegions(currentPage, regions, pageRegions);
                        UpdateTableLineControlsState();
                        pictureBox1.Invalidate();
                        return;
                    }
                }

                isLineDeleteMode = !isLineDeleteMode;
                if (isLineDeleteMode)
                    activeLineAddMode = ImageCoordinateHelper.RuleLineType.None;
                UpdateTableLineControlsState();
            };

            btnTableClearLines = new Button
            {
                Text = "罫線全消去",
                Location = new Point(130, 62),
                Size = new Size(116, 32),
                UseVisualStyleBackColor = true
            };
            btnTableClearLines.Click += (s, e) =>
            {
                int index = lstRegions.SelectedIndex;
                if (index >= 0 && index < regions.Count && regions[index].Type == "table")
                {
                    if (MessageBox.Show("この表のすべての罫線を消去しますか？", "罫線全消去",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        regions[index].RuleLines.Clear();
                        selectedRuleLineIndices.Clear();
                        pageRegions[currentPage] = _layoutStorage.CloneRegions(regions);
                        _layoutStorage.ForceSavePageRegions(currentPage, regions, pageRegions);
                        UpdateTableLineControlsState();
                        pictureBox1.Invalidate();
                    }
                }
            };

            lblTableLineStatus = new Label
            {
                Text = "※Shift+クリック: 複数選択\n※端点ドラッグ: 一括長さ変更\n※Ctrl+ドラッグ: 罫線コピー",
                Location = new Point(8, 100),
                Size = new Size(238, 75),
                ForeColor = Color.DimGray,
                Font = new Font(Font.FontFamily, 8.5f)
            };

            grpTableLines.Controls.Add(btnTableAddHLine);
            grpTableLines.Controls.Add(btnTableAddVLine);
            grpTableLines.Controls.Add(btnTableDeleteLine);
            grpTableLines.Controls.Add(btnTableClearLines);
            grpTableLines.Controls.Add(lblTableLineStatus);

            pnlRegionSettings.Controls.Add(grpTableLines);
            UpdateTableLineControlsState();
        }

        private void UpdateTableLineControlsState()
        {
            if (btnTableAddHLine == null) return;

            int index = lstRegions.SelectedIndex;
            bool isTableSelected = index >= 0 && index < regions.Count && regions[index].Type == "table";

            btnTableAddHLine.Enabled = isTableSelected;
            btnTableAddVLine.Enabled = isTableSelected;
            btnTableDeleteLine.Enabled = isTableSelected;
            btnTableClearLines.Enabled = isTableSelected;

            if (activeLineAddMode == ImageCoordinateHelper.RuleLineType.Horizontal)
            {
                btnTableAddHLine.BackColor = Color.LightSkyBlue;
                btnTableAddVLine.BackColor = SystemColors.Control;
                btnTableDeleteLine.BackColor = SystemColors.Control;
                lblTableLineStatus.Text = "【横罫線追加】表内の位置をクリックしてください";
                lblTableLineStatus.ForeColor = Color.DarkBlue;
            }
            else if (activeLineAddMode == ImageCoordinateHelper.RuleLineType.Vertical)
            {
                btnTableAddHLine.BackColor = SystemColors.Control;
                btnTableAddVLine.BackColor = Color.LightSkyBlue;
                btnTableDeleteLine.BackColor = SystemColors.Control;
                lblTableLineStatus.Text = "【縦罫線追加】表内の位置をクリックしてください";
                lblTableLineStatus.ForeColor = Color.DarkBlue;
            }
            else if (isLineDeleteMode)
            {
                btnTableAddHLine.BackColor = SystemColors.Control;
                btnTableAddVLine.BackColor = SystemColors.Control;
                btnTableDeleteLine.BackColor = Color.LightCoral;
                lblTableLineStatus.Text = "【罫線削除】削除する罫線をクリックしてください";
                lblTableLineStatus.ForeColor = Color.DarkRed;
            }
            else if (isTableSelected && selectedRuleLineIndices.Count > 1)
            {
                btnTableAddHLine.BackColor = SystemColors.Control;
                btnTableAddVLine.BackColor = SystemColors.Control;
                btnTableDeleteLine.BackColor = SystemColors.Control;
                lblTableLineStatus.Text = $"【罫線複数選択中 ({selectedRuleLineIndices.Count}本)】\n・端点(■)ドラッグ: 一括長さ変更\n・ドラッグ: 一括移動\n・Ctrl+ドラッグ: 一括コピー";
                lblTableLineStatus.ForeColor = Color.DarkRed;
            }
            else if (isTableSelected && selectedRuleLineIndices.Count == 1)
            {
                btnTableAddHLine.BackColor = SystemColors.Control;
                btnTableAddVLine.BackColor = SystemColors.Control;
                btnTableDeleteLine.BackColor = SystemColors.Control;
                lblTableLineStatus.Text = "【罫線選択中】\n・Shift+クリック: 複数選択\n・端点(■)ドラッグ: 長さ変更\n・Ctrl+ドラッグ: 罫線コピー";
                lblTableLineStatus.ForeColor = Color.DarkGoldenrod;
            }
            else
            {
                btnTableAddHLine.BackColor = SystemColors.Control;
                btnTableAddVLine.BackColor = SystemColors.Control;
                btnTableDeleteLine.BackColor = SystemColors.Control;
                lblTableLineStatus.Text = isTableSelected
                    ? "※Shift+クリック: 複数選択\n※端点ドラッグ: 一括長さ変更\n※Ctrl+ドラッグ: 罫線コピー"
                    : "※表領域を選択すると罫線を編集できます";
                lblTableLineStatus.ForeColor = Color.DimGray;
            }
        }

        private void DrawTableRuleLines(Graphics g, OcrRegion region, int regionIndex, bool isRegionSelected)
        {
            region.EnsureRuleLines();

            for (int lineIdx = 0; lineIdx < region.RuleLines.Count; lineIdx++)
            {
                var line = region.RuleLines[lineIdx];
                bool isLineSelected = isRegionSelected && selectedRuleLineIndices.Contains(lineIdx);
                bool isLineHovered = (hoveringRuleRegionIndex == regionIndex && hoveringRuleLineIndex == lineIdx);
                bool isLineDragging = (draggingRuleRegionIndex == regionIndex && (draggingRuleLineIndex == lineIdx || (ruleLineDragMode != RuleLineDragMode.None && selectedRuleLineIndices.Contains(lineIdx))));

                Point p1Img = line.IsVertical ? new Point(line.Pos, line.Start) : new Point(line.Start, line.Pos);
                Point p2Img = line.IsVertical ? new Point(line.Pos, line.End) : new Point(line.End, line.Pos);

                Point p1 = ImageCoordinateHelper.ImageToScreenPoint(p1Img, pictureBox1);
                Point p2 = ImageCoordinateHelper.ImageToScreenPoint(p2Img, pictureBox1);

                Color lineColor;
                float lineThick;
                DashStyle dashStyle = DashStyle.Solid;

                if (isLineSelected || isLineDragging)
                {
                    lineColor = Color.Gold;
                    lineThick = 3f;
                }
                else if (isLineHovered)
                {
                    lineColor = Color.Yellow;
                    lineThick = 2.5f;
                }
                else if (isRegionSelected)
                {
                    lineColor = Color.Cyan;
                    lineThick = 1.8f;
                }
                else
                {
                    lineColor = Color.DeepSkyBlue;
                    lineThick = 1.2f;
                    dashStyle = DashStyle.Dash;
                }

                using Pen linePen = new Pen(lineColor, lineThick) { DashStyle = dashStyle };
                g.DrawLine(linePen, p1, p2);

                // 端点ハンドルの描画
                if (isLineSelected || isLineHovered || isLineDragging)
                {
                    int hs = 8;
                    using Brush hBrush = new SolidBrush(Color.White);
                    using Pen hPen = new Pen(Color.Red, 1.5f);

                    Rectangle r1 = new Rectangle(p1.X - hs / 2, p1.Y - hs / 2, hs, hs);
                    Rectangle r2 = new Rectangle(p2.X - hs / 2, p2.Y - hs / 2, hs, hs);

                    g.FillRectangle(hBrush, r1);
                    g.DrawRectangle(hPen, r1);

                    g.FillRectangle(hBrush, r2);
                    g.DrawRectangle(hPen, r2);
                }
                else if (isRegionSelected)
                {
                    int hs = 5;
                    using Brush hBrush = new SolidBrush(Color.Cyan);
                    g.FillRectangle(hBrush, p1.X - hs / 2, p1.Y - hs / 2, hs, hs);
                    g.FillRectangle(hBrush, p2.X - hs / 2, p2.Y - hs / 2, hs, hs);
                }
            }
        }

        private void DrawRuleLinePreview(Graphics g)
        {
            if (activeLineAddMode != ImageCoordinateHelper.RuleLineType.None && lstRegions.SelectedIndex >= 0)
            {
                OcrRegion selRegion = regions[lstRegions.SelectedIndex];
                if (selRegion.Type == "table")
                {
                    Point mousePos = pictureBox1.PointToClient(Cursor.Position);
                    Rectangle tableScreenRect = ImageCoordinateHelper.ImageToScreen(
                        new Rectangle(selRegion.X, selRegion.Y, selRegion.Width, selRegion.Height),
                        pictureBox1);

                    if (tableScreenRect.Contains(mousePos))
                    {
                        using Pen previewLinePen = new Pen(Color.Gold, 2f) { DashStyle = DashStyle.DashDot };
                        if (activeLineAddMode == ImageCoordinateHelper.RuleLineType.Horizontal)
                        {
                            g.DrawLine(previewLinePen, tableScreenRect.Left, mousePos.Y, tableScreenRect.Right, mousePos.Y);
                        }
                        else if (activeLineAddMode == ImageCoordinateHelper.RuleLineType.Vertical)
                        {
                            g.DrawLine(previewLinePen, mousePos.X, tableScreenRect.Top, mousePos.X, tableScreenRect.Bottom);
                        }
                    }
                }
            }
        }

        private bool HandleRuleLineRightClick(Point screenLoc)
        {
            int curIdx = lstRegions.SelectedIndex;
            if (curIdx >= 0 && curIdx < regions.Count && regions[curIdx].Type == "table")
            {
                if (ImageCoordinateHelper.HitTestTableRuleLines(screenLoc, 8, 6, regions[curIdx], selectedRuleLineIndices, pictureBox1, out int hitIdx, out _))
                {
                    var table = regions[curIdx];
                    if (selectedRuleLineIndices.Contains(hitIdx) && selectedRuleLineIndices.Count > 1)
                    {
                        foreach (int delIdx in selectedRuleLineIndices.OrderByDescending(x => x))
                        {
                            if (delIdx >= 0 && delIdx < table.RuleLines.Count)
                                table.RuleLines.RemoveAt(delIdx);
                        }
                    }
                    else
                    {
                        table.RuleLines.RemoveAt(hitIdx);
                    }
                    selectedRuleLineIndices.Clear();
                    pageRegions[currentPage] = _layoutStorage.CloneRegions(regions);
                    _layoutStorage.ForceSavePageRegions(currentPage, regions, pageRegions);
                    UpdateTableLineControlsState();
                    pictureBox1.Invalidate();
                    return true;
                }
            }

            for (int i = 0; i < regions.Count; i++)
            {
                if (i == curIdx) continue;
                if (regions[i].Type == "table")
                {
                    if (ImageCoordinateHelper.HitTestTableRuleLines(screenLoc, 8, 6, regions[i], null, pictureBox1, out int hitIdx, out _))
                    {
                        regions[i].RuleLines.RemoveAt(hitIdx);
                        selectedRuleLineIndices.Clear();
                        pageRegions[currentPage] = _layoutStorage.CloneRegions(regions);
                        _layoutStorage.ForceSavePageRegions(currentPage, regions, pageRegions);
                        UpdateTableLineControlsState();
                        pictureBox1.Invalidate();
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HandleRuleLineMouseDown(MouseEventArgs e)
        {
            // 1. 横罫線追加モード
            if (activeLineAddMode == ImageCoordinateHelper.RuleLineType.Horizontal)
            {
                int selIdx = lstRegions.SelectedIndex;
                if (selIdx >= 0 && selIdx < regions.Count && regions[selIdx].Type == "table")
                {
                    OcrRegion table = regions[selIdx];
                    table.EnsureRuleLines();
                    Point imgPt = ImageCoordinateHelper.ScreenToImagePoint(e.Location, pictureBox1);
                    if (imgPt.Y > table.Y + 2 && imgPt.Y < table.Y + table.Height - 2)
                    {
                        var newLine = new TableRuleLine(false, imgPt.Y, table.X, table.X + table.Width);
                        table.RuleLines.Add(newLine);
                        selectedRuleLineIndices.Clear();
                        selectedRuleLineIndices.Add(table.RuleLines.Count - 1);
                        pageRegions[currentPage] = _layoutStorage.CloneRegions(regions);
                        _layoutStorage.ForceSavePageRegions(currentPage, regions, pageRegions);
                    }
                }
                activeLineAddMode = ImageCoordinateHelper.RuleLineType.None;
                UpdateTableLineControlsState();
                pictureBox1.Invalidate();
                return true;
            }

            // 2. 縦罫線追加モード
            if (activeLineAddMode == ImageCoordinateHelper.RuleLineType.Vertical)
            {
                int selIdx = lstRegions.SelectedIndex;
                if (selIdx >= 0 && selIdx < regions.Count && regions[selIdx].Type == "table")
                {
                    OcrRegion table = regions[selIdx];
                    table.EnsureRuleLines();
                    Point imgPt = ImageCoordinateHelper.ScreenToImagePoint(e.Location, pictureBox1);
                    if (imgPt.X > table.X + 2 && imgPt.X < table.X + table.Width - 2)
                    {
                        var newLine = new TableRuleLine(true, imgPt.X, table.Y, table.Y + table.Height);
                        table.RuleLines.Add(newLine);
                        selectedRuleLineIndices.Clear();
                        selectedRuleLineIndices.Add(table.RuleLines.Count - 1);
                        pageRegions[currentPage] = _layoutStorage.CloneRegions(regions);
                        _layoutStorage.ForceSavePageRegions(currentPage, regions, pageRegions);
                    }
                }
                activeLineAddMode = ImageCoordinateHelper.RuleLineType.None;
                UpdateTableLineControlsState();
                pictureBox1.Invalidate();
                return true;
            }

            // 3. 罫線削除モード
            if (isLineDeleteMode)
            {
                for (int i = 0; i < regions.Count; i++)
                {
                    if (regions[i].Type == "table")
                    {
                        if (ImageCoordinateHelper.HitTestTableRuleLines(e.Location, 8, 8, regions[i], selectedRuleLineIndices, pictureBox1, out int hitIdx, out _))
                        {
                            regions[i].RuleLines.RemoveAt(hitIdx);
                            selectedRuleLineIndices.Clear();
                            pageRegions[currentPage] = _layoutStorage.CloneRegions(regions);
                            _layoutStorage.ForceSavePageRegions(currentPage, regions, pageRegions);
                            break;
                        }
                    }
                }
                isLineDeleteMode = false;
                UpdateTableLineControlsState();
                pictureBox1.Invalidate();
                return true;
            }

            // 4. 通常モードでの罫線選択・端点ドラッグ・Ctrlコピー移動
            int curTableIdx = lstRegions.SelectedIndex;
            if (curTableIdx >= 0 && curTableIdx < regions.Count && regions[curTableIdx].Type == "table")
            {
                var table = regions[curTableIdx];
                table.EnsureRuleLines();

                if (ImageCoordinateHelper.HitTestTableRuleLines(e.Location, 8, 6, table, selectedRuleLineIndices, pictureBox1, out int hitIdx, out var hitPart))
                {
                    bool isShift = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;
                    bool isCtrl = (Control.ModifierKeys & Keys.Control) == Keys.Control;

                    if (hitPart == ImageCoordinateHelper.RuleLineHitPart.LineBody)
                    {
                        if (isShift)
                        {
                            if (selectedRuleLineIndices.Contains(hitIdx))
                                selectedRuleLineIndices.Remove(hitIdx);
                            else
                                selectedRuleLineIndices.Add(hitIdx);
                        }
                        else if (!selectedRuleLineIndices.Contains(hitIdx))
                        {
                            selectedRuleLineIndices.Clear();
                            selectedRuleLineIndices.Add(hitIdx);
                        }
                    }
                    else
                    {
                        if (!selectedRuleLineIndices.Contains(hitIdx))
                        {
                            if (!isShift) selectedRuleLineIndices.Clear();
                            selectedRuleLineIndices.Add(hitIdx);
                        }
                    }

                    if (selectedRuleLineIndices.Count == 0)
                    {
                        UpdateTableLineControlsState();
                        pictureBox1.Invalidate();
                        return true;
                    }

                    draggingRuleLineIndex = hitIdx;
                    draggingRuleRegionIndex = curTableIdx;
                    dragStartImagePoint = ImageCoordinateHelper.ScreenToImagePoint(e.Location, pictureBox1);

                    var line = table.RuleLines[hitIdx];

                    if (hitPart == ImageCoordinateHelper.RuleLineHitPart.StartHandle)
                    {
                        ruleLineDragMode = RuleLineDragMode.AdjustStart;
                        isCtrlCopyDragging = false;
                        pictureBox1.Cursor = line.IsVertical ? Cursors.SizeNS : Cursors.SizeWE;
                    }
                    else if (hitPart == ImageCoordinateHelper.RuleLineHitPart.EndHandle)
                    {
                        ruleLineDragMode = RuleLineDragMode.AdjustEnd;
                        isCtrlCopyDragging = false;
                        pictureBox1.Cursor = line.IsVertical ? Cursors.SizeNS : Cursors.SizeWE;
                    }
                    else
                    {
                        ruleLineDragMode = RuleLineDragMode.MoveLine;
                        if (isCtrl)
                        {
                            isCtrlCopyDragging = true;
                            var newSelected = new HashSet<int>();
                            foreach (int idx in selectedRuleLineIndices.OrderBy(x => x))
                            {
                                var copyLine = table.RuleLines[idx].Clone();
                                table.RuleLines.Add(copyLine);
                                newSelected.Add(table.RuleLines.Count - 1);
                            }
                            selectedRuleLineIndices.Clear();
                            foreach (int n in newSelected) selectedRuleLineIndices.Add(n);
                            draggingRuleLineIndex = selectedRuleLineIndices.Last();
                            pictureBox1.Cursor = Cursors.Cross;
                        }
                        else
                        {
                            isCtrlCopyDragging = false;
                            pictureBox1.Cursor = line.IsVertical ? Cursors.VSplit : Cursors.HSplit;
                        }
                    }

                    dragInitialRuleLines.Clear();
                    foreach (int idx in selectedRuleLineIndices)
                    {
                        if (idx >= 0 && idx < table.RuleLines.Count)
                            dragInitialRuleLines[idx] = table.RuleLines[idx].Clone();
                    }

                    UpdateTableLineControlsState();
                    pictureBox1.Invalidate();
                    return true;
                }
                else if (!((Control.ModifierKeys & Keys.Shift) == Keys.Shift))
                {
                    selectedRuleLineIndices.Clear();
                    UpdateTableLineControlsState();
                    pictureBox1.Invalidate();
                }
            }

            // 他の表領域内の罫線ヒット判定
            for (int i = 0; i < regions.Count; i++)
            {
                if (i == curTableIdx) continue;
                if (regions[i].Type == "table")
                {
                    var table = regions[i];
                    table.EnsureRuleLines();
                    if (ImageCoordinateHelper.HitTestTableRuleLines(e.Location, 8, 6, table, null, pictureBox1, out int hitIdx, out var hitPart))
                    {
                        lstRegions.SelectedIndex = i;
                        selectedRuleLineIndices.Clear();
                        selectedRuleLineIndices.Add(hitIdx);
                        draggingRuleLineIndex = hitIdx;
                        draggingRuleRegionIndex = i;
                        dragStartImagePoint = ImageCoordinateHelper.ScreenToImagePoint(e.Location, pictureBox1);

                        var line = table.RuleLines[hitIdx];

                        if (hitPart == ImageCoordinateHelper.RuleLineHitPart.StartHandle)
                        {
                            ruleLineDragMode = RuleLineDragMode.AdjustStart;
                            isCtrlCopyDragging = false;
                            pictureBox1.Cursor = line.IsVertical ? Cursors.SizeNS : Cursors.SizeWE;
                        }
                        else if (hitPart == ImageCoordinateHelper.RuleLineHitPart.EndHandle)
                        {
                            ruleLineDragMode = RuleLineDragMode.AdjustEnd;
                            isCtrlCopyDragging = false;
                            pictureBox1.Cursor = line.IsVertical ? Cursors.SizeNS : Cursors.SizeWE;
                        }
                        else
                        {
                            bool isCtrl = (Control.ModifierKeys & Keys.Control) == Keys.Control;
                            if (isCtrl)
                            {
                                var copyLine = line.Clone();
                                table.RuleLines.Add(copyLine);
                                selectedRuleLineIndices.Clear();
                                selectedRuleLineIndices.Add(table.RuleLines.Count - 1);
                                draggingRuleLineIndex = table.RuleLines.Count - 1;
                                ruleLineDragMode = RuleLineDragMode.MoveLine;
                                isCtrlCopyDragging = true;
                                pictureBox1.Cursor = Cursors.Cross;
                            }
                            else
                            {
                                ruleLineDragMode = RuleLineDragMode.MoveLine;
                                isCtrlCopyDragging = false;
                                pictureBox1.Cursor = line.IsVertical ? Cursors.VSplit : Cursors.HSplit;
                            }
                        }

                        dragInitialRuleLines.Clear();
                        foreach (int idx in selectedRuleLineIndices)
                        {
                            if (idx >= 0 && idx < table.RuleLines.Count)
                                dragInitialRuleLines[idx] = table.RuleLines[idx].Clone();
                        }

                        UpdateTableLineControlsState();
                        pictureBox1.Invalidate();
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HandleRuleLineMouseMove(MouseEventArgs e)
        {
            // 1. 罫線ドラッグ中
            if (ruleLineDragMode != RuleLineDragMode.None &&
                draggingRuleRegionIndex >= 0 && draggingRuleRegionIndex < regions.Count)
            {
                OcrRegion table = regions[draggingRuleRegionIndex];
                Point imgPt = ImageCoordinateHelper.ScreenToImagePoint(e.Location, pictureBox1);
                int deltaX = imgPt.X - dragStartImagePoint.X;
                int deltaY = imgPt.Y - dragStartImagePoint.Y;

                TableRuleLine? primaryLine = draggingRuleLineIndex >= 0 && draggingRuleLineIndex < table.RuleLines.Count
                    ? table.RuleLines[draggingRuleLineIndex]
                    : null;

                if (ruleLineDragMode == RuleLineDragMode.AdjustStart)
                {
                    foreach (var kvp in dragInitialRuleLines)
                    {
                        int idx = kvp.Key;
                        var initLine = kvp.Value;
                        if (idx >= 0 && idx < table.RuleLines.Count)
                        {
                            var line = table.RuleLines[idx];
                            if (line.IsVertical)
                            {
                                int newStart = Math.Max(table.Y, Math.Min(initLine.Start + deltaY, line.End - 1));
                                line.Start = newStart;
                            }
                            else
                            {
                                int newStart = Math.Max(table.X, Math.Min(initLine.Start + deltaX, line.End - 1));
                                line.Start = newStart;
                            }
                        }
                    }
                    pictureBox1.Cursor = (primaryLine?.IsVertical ?? false) ? Cursors.SizeNS : Cursors.SizeWE;
                    pictureBox1.Invalidate();
                    return true;
                }

                if (ruleLineDragMode == RuleLineDragMode.AdjustEnd)
                {
                    foreach (var kvp in dragInitialRuleLines)
                    {
                        int idx = kvp.Key;
                        var initLine = kvp.Value;
                        if (idx >= 0 && idx < table.RuleLines.Count)
                        {
                            var line = table.RuleLines[idx];
                            if (line.IsVertical)
                            {
                                int newEnd = Math.Max(line.Start + 1, Math.Min(initLine.End + deltaY, table.Y + table.Height));
                                line.End = newEnd;
                            }
                            else
                            {
                                int newEnd = Math.Max(line.Start + 1, Math.Min(initLine.End + deltaX, table.X + table.Width));
                                line.End = newEnd;
                            }
                        }
                    }
                    pictureBox1.Cursor = (primaryLine?.IsVertical ?? false) ? Cursors.SizeNS : Cursors.SizeWE;
                    pictureBox1.Invalidate();
                    return true;
                }

                if (ruleLineDragMode == RuleLineDragMode.MoveLine)
                {
                    foreach (var kvp in dragInitialRuleLines)
                    {
                        int idx = kvp.Key;
                        var initLine = kvp.Value;
                        if (idx >= 0 && idx < table.RuleLines.Count)
                        {
                            var line = table.RuleLines[idx];
                            if (line.IsVertical)
                            {
                                line.Pos = Math.Max(table.X + 2, Math.Min(initLine.Pos + deltaX, table.X + table.Width - 2));
                            }
                            else
                            {
                                line.Pos = Math.Max(table.Y + 2, Math.Min(initLine.Pos + deltaY, table.Y + table.Height - 2));
                            }
                        }
                    }
                    pictureBox1.Cursor = isCtrlCopyDragging ? Cursors.Cross : ((primaryLine?.IsVertical ?? false) ? Cursors.VSplit : Cursors.HSplit);
                    pictureBox1.Invalidate();
                    return true;
                }
            }

            // 2. 罫線追加・削除モードのカーソル・プレビュー
            if (activeLineAddMode == ImageCoordinateHelper.RuleLineType.Horizontal)
            {
                pictureBox1.Cursor = Cursors.HSplit;
                pictureBox1.Invalidate();
                return true;
            }
            if (activeLineAddMode == ImageCoordinateHelper.RuleLineType.Vertical)
            {
                pictureBox1.Cursor = Cursors.VSplit;
                pictureBox1.Invalidate();
                return true;
            }
            if (isLineDeleteMode)
            {
                pictureBox1.Cursor = Cursors.Hand;
                return true;
            }

            // 3. 通常モードでの罫線ホバー判定
            if (resizeMode == ImageCoordinateHelper.ResizeMode.None && movingRegionIndex < 0 && !isDrawingRegion)
            {
                int curTableIdx = lstRegions.SelectedIndex;
                if (curTableIdx >= 0 && curTableIdx < regions.Count && regions[curTableIdx].Type == "table")
                {
                    var table = regions[curTableIdx];
                    table.EnsureRuleLines();

                    if (ImageCoordinateHelper.HitTestTableRuleLines(e.Location, 8, 6, table, selectedRuleLineIndices, pictureBox1, out int hitIdx, out var hitPart))
                    {
                        hoveringRuleLineIndex = hitIdx;
                        hoveringRuleRegionIndex = curTableIdx;
                        hoveringRuleLinePart = hitPart;

                        var line = table.RuleLines[hitIdx];
                        if (hitPart == ImageCoordinateHelper.RuleLineHitPart.StartHandle || hitPart == ImageCoordinateHelper.RuleLineHitPart.EndHandle)
                        {
                            pictureBox1.Cursor = line.IsVertical ? Cursors.SizeNS : Cursors.SizeWE;
                        }
                        else
                        {
                            bool isCtrl = (Control.ModifierKeys & Keys.Control) == Keys.Control;
                            pictureBox1.Cursor = isCtrl ? Cursors.Hand : (line.IsVertical ? Cursors.VSplit : Cursors.HSplit);
                        }

                        pictureBox1.Invalidate();
                        return true;
                    }
                }

                if (hoveringRuleLineIndex != -1)
                {
                    hoveringRuleLineIndex = -1;
                    hoveringRuleRegionIndex = -1;
                    hoveringRuleLinePart = ImageCoordinateHelper.RuleLineHitPart.None;
                    pictureBox1.Invalidate();
                }
            }

            return false;
        }

        private bool HandleRuleLineMouseUp(MouseEventArgs e)
        {
            if (ruleLineDragMode != RuleLineDragMode.None)
            {
                if (draggingRuleRegionIndex >= 0 && draggingRuleRegionIndex < regions.Count)
                {
                    OcrRegion table = regions[draggingRuleRegionIndex];
                    foreach (var line in table.RuleLines)
                    {
                        if (line.Start > line.End)
                        {
                            int tmp = line.Start;
                            line.Start = line.End;
                            line.End = tmp;
                        }
                    }
                    pageRegions[currentPage] = _layoutStorage.CloneRegions(regions);
                    _layoutStorage.ForceSavePageRegions(currentPage, regions, pageRegions);
                }
                ruleLineDragMode = RuleLineDragMode.None;
                draggingRuleLineIndex = -1;
                draggingRuleRegionIndex = -1;
                dragInitialRuleLines.Clear();
                isCtrlCopyDragging = false;
                pictureBox1.Cursor = Cursors.Default;
                pictureBox1.Invalidate();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 表タブ（dgvOcrTable）で選択された行のページおよび表名を特定し、
        /// キャンバス上で該当する表領域（外枠・縦横罫線）を復元・選択して即座に編集可能状態にします。
        /// </summary>
        public void ActivateTableRegionFromGridRow(int rowIndex)
        {
            if (dgvOcrTable == null || rowIndex < 0 || rowIndex >= dgvOcrTable.RowCount) return;

            var row = dgvOcrTable.Rows[rowIndex];
            int pageNum = 0;
            string tableName = "";

            if (row.Tag is TableRowMeta meta)
            {
                pageNum = meta.OriginalPage;
                tableName = meta.OriginalTable;
            }

            if (pageNum <= 0)
            {
                var pageCell = dgvOcrTable.Columns.Contains("Page") ? row.Cells["Page"] : (dgvOcrTable.Columns.Count > 0 ? row.Cells[0] : null);
                if (pageCell?.Value != null && int.TryParse(pageCell.Value.ToString(), out int p))
                {
                    pageNum = p;
                }
            }

            if (pageNum <= 0) return;

            if (string.IsNullOrWhiteSpace(tableName))
            {
                var tableCell = dgvOcrTable.Columns.Contains("Table") ? row.Cells["Table"] : (dgvOcrTable.Columns.Count > 1 ? row.Cells[1] : null);
                if (tableCell?.Value != null)
                {
                    tableName = tableCell.Value.ToString() ?? "";
                }
            }

            RestoreAndSelectTableRegion(pageNum, tableName);
        }

        /// <summary>
        /// 現在選択されているページに存在する表領域、またはグリッドの現在の行に対応する表領域をキャンバス上で復元・選択します。
        /// </summary>
        public void ActivateCurrentPageTableRegion()
        {
            if (pdfDocument == null) return;

            // 1. dgvOcrTable のカレント行が現在のページのものであれば、その行を対象にする
            if (dgvOcrTable != null && dgvOcrTable.CurrentRow != null && dgvOcrTable.CurrentRow.Index >= 0)
            {
                var curRow = dgvOcrTable.CurrentRow;
                var pageCell = dgvOcrTable.Columns.Contains("Page") ? curRow.Cells["Page"] : (dgvOcrTable.Columns.Count > 0 ? curRow.Cells[0] : null);
                if (pageCell?.Value != null && int.TryParse(pageCell.Value.ToString(), out int curPageNum) && curPageNum == currentPage + 1)
                {
                    ActivateTableRegionFromGridRow(curRow.Index);
                    return;
                }
            }

            // 2. dgvOcrTable 内で現在のページ (currentPage + 1) に合致する最初の行を探す
            if (dgvOcrTable != null && dgvOcrTable.RowCount > 0)
            {
                foreach (DataGridViewRow row in dgvOcrTable.Rows)
                {
                    var pageCell = dgvOcrTable.Columns.Contains("Page") ? row.Cells["Page"] : (dgvOcrTable.Columns.Count > 0 ? row.Cells[0] : null);
                    if (pageCell?.Value != null && int.TryParse(pageCell.Value.ToString(), out int pNum) && pNum == currentPage + 1)
                    {
                        ActivateTableRegionFromGridRow(row.Index);
                        return;
                    }
                }
            }

            // 3. dgvOcrTable に該当行がない場合でも、現在ページ上の最初の表領域を復元・選択
            RestoreAndSelectTableRegion(currentPage + 1, "");
        }

        /// <summary>
        /// 指定されたページ・表名の表領域および罫線を復元（復活）し、キャンバスおよび領域リストで選択状態にします。
        /// </summary>
        public void RestoreAndSelectTableRegion(int pageNum, string tableName)
        {
            if (pdfDocument == null) return;
            int targetPageIndex = pageNum - 1;
            if (targetPageIndex < 0 || targetPageIndex >= pdfDocument.PageCount) return;

            // 1. 必要に応じて該当ページへ切り替え
            if (currentPage != targetPageIndex)
            {
                SwitchToPage(targetPageIndex);
            }

            // 2. 現在の regions 内に該当する表領域が存在するか検索
            var tableRegions = regions.Where(r => OcrProcessor.NormalizeRegionType(r.Type) == "table").ToList();
            OcrRegion? targetRegion = null;

            if (!string.IsNullOrWhiteSpace(tableName))
            {
                targetRegion = tableRegions.FirstOrDefault(r => string.Equals(r.Name, tableName, StringComparison.OrdinalIgnoreCase));
            }

            if (targetRegion == null && tableRegions.Count > 0)
            {
                var match = System.Text.RegularExpressions.Regex.Match(tableName, @"\d+");
                if (match.Success && int.TryParse(match.Value, out int tNum) && tNum >= 1 && tNum <= tableRegions.Count)
                {
                    targetRegion = tableRegions[tNum - 1];
                }
                else
                {
                    targetRegion = tableRegions[0];
                }
            }

            // 3. 領域が見つからない場合、自動レイアウト情報 (autoPageRegions / auto_layout.json / result.json) から表領域を復元
            if (targetRegion == null)
            {
                List<OcrRegion>? autoCandidates = null;
                if (autoPageRegions.TryGetValue(targetPageIndex, out var autoList) && autoList != null && autoList.Count > 0)
                {
                    autoCandidates = autoList;
                }
                else
                {
                    string projectDir = OcrProcessor.FindOcrEngineDirectory();
                    string pdfName = string.IsNullOrEmpty(currentPdfPath) ? "" : Path.GetFileNameWithoutExtension(currentPdfPath);
                    string pageDir = System.IO.Path.Combine(projectDir, "ocr_results", pdfName, $"page_{pageNum:D4}");
                    string autoLayoutPath = System.IO.Path.Combine(pageDir, "auto_layout.json");
                    if (!System.IO.File.Exists(autoLayoutPath))
                        autoLayoutPath = System.IO.Path.Combine(pageDir, "result.json");

                    if (System.IO.File.Exists(autoLayoutPath))
                    {
                        try
                        {
                            var detected = OcrJsonParser.LoadAutoLayoutJson(autoLayoutPath);
                            var converted = detected.Select(OcrProcessor.ConvertAutoLayoutRegion).ToList();
                            autoCandidates = ApplyDeckDivision(converted, appSettings.DeckCount, appSettings.DocumentType, appSettings.TextOrientation);
                            autoPageRegions[targetPageIndex] = autoCandidates;
                        }
                        catch { }
                    }
                }

                if (autoCandidates != null && autoCandidates.Count > 0)
                {
                    var autoTables = autoCandidates.Where(r => OcrProcessor.NormalizeRegionType(r.Type) == "table").ToList();
                    OcrRegion? autoMatch = null;

                    if (!string.IsNullOrWhiteSpace(tableName))
                    {
                        autoMatch = autoTables.FirstOrDefault(r => string.Equals(r.Name, tableName, StringComparison.OrdinalIgnoreCase));
                    }

                    if (autoMatch == null && autoTables.Count > 0)
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(tableName, @"\d+");
                        if (match.Success && int.TryParse(match.Value, out int tNum) && tNum >= 1 && tNum <= autoTables.Count)
                        {
                            autoMatch = autoTables[tNum - 1];
                        }
                        else
                        {
                            autoMatch = autoTables[0];
                        }
                    }

                    if (autoMatch != null)
                    {
                        targetRegion = new OcrRegion
                        {
                            Name = !string.IsNullOrWhiteSpace(tableName) ? tableName : autoMatch.Name,
                            Type = "table",
                            Orientation = autoMatch.Orientation,
                            X = autoMatch.X,
                            Y = autoMatch.Y,
                            Width = autoMatch.Width,
                            Height = autoMatch.Height,
                            RuleLines = autoMatch.RuleLines?.Select(l => l.Clone()).ToList() ?? new List<TableRuleLine>()
                        };
                        targetRegion.EnsureRuleLines();
                        regions.Add(targetRegion);
                        pageRegions[targetPageIndex] = _layoutStorage.CloneRegions(regions);
                        _layoutStorage.ForceSavePageRegions(targetPageIndex, regions, pageRegions);
                        RefreshRegionList();
                    }
                }
            }

            // 4. 表領域の罫線（縦線・横線）を確実に復元・確認
            if (targetRegion != null)
            {
                targetRegion.EnsureRuleLines();

                // 内部罫線が存在しない場合（外枠のみ等の場合）、自動レイアウト情報から罫線を取り込む
                if (targetRegion.RuleLines.Count <= 4)
                {
                    OcrRegion? autoForLines = null;
                    if (autoPageRegions.TryGetValue(targetPageIndex, out var autoList2))
                    {
                        autoForLines = autoList2.FirstOrDefault(a => OcrProcessor.NormalizeRegionType(a.Type) == "table" &&
                            Math.Abs(a.X - targetRegion.X) < 80 && Math.Abs(a.Y - targetRegion.Y) < 80 &&
                            a.RuleLines != null && a.RuleLines.Count > 0);
                    }

                    if (autoForLines == null)
                    {
                        string projectDir = OcrProcessor.FindOcrEngineDirectory();
                        string pdfName = string.IsNullOrEmpty(currentPdfPath) ? "" : Path.GetFileNameWithoutExtension(currentPdfPath);
                        string pageDir = System.IO.Path.Combine(projectDir, "ocr_results", pdfName, $"page_{pageNum:D4}");
                        string autoLayoutPath = System.IO.Path.Combine(pageDir, "auto_layout.json");
                        if (!System.IO.File.Exists(autoLayoutPath))
                            autoLayoutPath = System.IO.Path.Combine(pageDir, "result.json");

                        if (System.IO.File.Exists(autoLayoutPath))
                        {
                            try
                            {
                                var detected = OcrJsonParser.LoadAutoLayoutJson(autoLayoutPath);
                                var converted = detected.Select(OcrProcessor.ConvertAutoLayoutRegion).ToList();
                                autoForLines = converted.FirstOrDefault(a => OcrProcessor.NormalizeRegionType(a.Type) == "table" &&
                                    Math.Abs(a.X - targetRegion.X) < 80 && Math.Abs(a.Y - targetRegion.Y) < 80 &&
                                    a.RuleLines != null && a.RuleLines.Count > 0);
                            }
                            catch { }
                        }
                    }

                    if (autoForLines != null && autoForLines.RuleLines != null && autoForLines.RuleLines.Count > 0)
                    {
                        targetRegion.RuleLines = autoForLines.RuleLines.Select(l => l.Clone()).ToList();
                        targetRegion.EnsureRuleLines();
                        pageRegions[targetPageIndex] = _layoutStorage.CloneRegions(regions);
                        _layoutStorage.ForceSavePageRegions(targetPageIndex, regions, pageRegions);
                    }
                }

                // 5. 領域リストおよびキャンバス上で選択状態にし、罫線編集ツールバーを有効化
                int regIdx = regions.IndexOf(targetRegion);
                if (regIdx >= 0)
                {
                    if (lstRegions.SelectedIndex != regIdx)
                    {
                        lstRegions.SelectedIndex = regIdx;
                    }
                    else
                    {
                        UpdateTableLineControlsState();
                        pictureBox1.Invalidate();
                    }

                    // ズーム・スクロール表示中であれば、該当領域が見える位置へスクロール
                    if (currentZoomFactor > 0f && pnlCanvasContainer != null && pnlCanvasContainer.AutoScroll)
                    {
                        Rectangle screenRect = ImageCoordinateHelper.ImageToScreen(
                            new Rectangle(targetRegion.X, targetRegion.Y, targetRegion.Width, targetRegion.Height),
                            pictureBox1);
                        int scrollX = Math.Max(0, screenRect.X + screenRect.Width / 2 - pnlCanvasContainer.ClientSize.Width / 2);
                        int scrollY = Math.Max(0, screenRect.Y + screenRect.Height / 2 - pnlCanvasContainer.ClientSize.Height / 2);
                        pnlCanvasContainer.AutoScrollPosition = new Point(scrollX, scrollY);
                    }
                }
            }
        }
    }
}

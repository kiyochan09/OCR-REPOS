using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using OCR_Translator.Forms;
using OCR_Translator.Models;
using OCR_Translator.Services;
using ResizeMode = OCR_Translator.Services.ImageCoordinateHelper.ResizeMode;

namespace OCR_Translator
{
    public partial class Form1
    {
        public static string GetRegionTypeName(string type)
        {
            return type switch
            {
                "body" => "本文",
                "heading" => "見出し",
                "footnote" => "注釈文",
                "table" => "表",
                "image" => "図",
                _ => "本文"
            };
        }

        private void ChangeSelectedRegionType(string newType, string newName)
        {
            int index = lstRegions.SelectedIndex;
            if (index < 0 || index >= regions.Count) return;

            OcrRegion region = regions[index];
            region.Type = newType;
            region.Name = newName;
            if (newType == "table")
            {
                region.EnsureRuleLines();
                if (region.RuleLines.Count == 0 && autoPageRegions.TryGetValue(currentPage, out var autoList))
                {
                    var match = autoList.FirstOrDefault(a => a.Type == "table" &&
                        Math.Abs(a.X - region.X) < 40 && Math.Abs(a.Y - region.Y) < 40);
                    if (match != null && match.RuleLines.Count > 0)
                    {
                        foreach (var l in match.RuleLines)
                            region.RuleLines.Add(new TableRuleLine(l.IsVertical, l.Pos, l.Start, l.End));
                    }
                }
            }

            lstRegions.Items[index] = newName;
            pageRegions[currentPage] = _layoutStorage.CloneRegions(regions);
            _layoutStorage.ForceSavePageRegions(currentPage, regions, pageRegions);
            UpdateTableLineControlsState();
            pictureBox1.Invalidate();

            if (newType == "image")
            {
                ExtractCurrentPageFigures();
            }
        }

        private void CreateNewRegionWithBounds(Rectangle imageRect, string type, string name)
        {
            try
            {
                OcrRegion region = new OcrRegion
                {
                    Name = name,
                    Type = type,
                    X = imageRect.X,
                    Y = imageRect.Y,
                    Width = imageRect.Width,
                    Height = imageRect.Height
                };

                if (type == "table")
                {
                    region.EnsureRuleLines();
                }

                regions.Add(region);
                int index = lstRegions.Items.Add(region.Name);
                lstRegions.SelectedIndex = index;

                isUpdatingNumericValues = true;
                try
                {
                    numX.Value = Math.Min(numX.Maximum, Math.Max(numX.Minimum, region.X));
                    numY.Value = Math.Min(numY.Maximum, Math.Max(numY.Minimum, region.Y));
                    numWidth.Value = Math.Min(numWidth.Maximum, Math.Max(numWidth.Minimum, region.Width));
                    numHeight.Value = Math.Min(numHeight.Maximum, Math.Max(numHeight.Minimum, region.Height));
                }
                finally
                {
                    isUpdatingNumericValues = false;
                }

                pageRegions[currentPage] = _layoutStorage.CloneRegions(regions);
                _layoutStorage.ForceSavePageRegions(currentPage, regions, pageRegions);
                UpdateTableLineControlsState();
                pictureBox1.Invalidate();

                if (type == "image")
                {
                    ExtractCurrentPageFigures();
                }
            }
            finally
            {
                CancelAllInteractiveDrawingModes();
            }
        }

        private bool isReadingOrderMode = false;
        private List<OcrRegion> reorderedRegions = new();
        private HashSet<OcrRegion> assignedRegions = new();

        private void RefreshRegionList()
        {
            lstRegions.Items.Clear();
            for (int i = 0; i < regions.Count; i++)
            {
                OcrRegion region = regions[i];
                lstRegions.Items.Add($"{i + 1}. {region.Name}");
            }

            if (regions.Count > 0 && lstRegions.SelectedIndex < 0)
                lstRegions.SelectedIndex = 0;

            pictureBox1.Invalidate();
        }

        private void btnReorderMode_Click(object? sender, EventArgs e)
        {
            if (pdfDocument == null || pictureBox1.Image == null)
            {
                MessageBox.Show("先にPDFを開いてください。", "読み順変更",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (regions.Count == 0)
            {
                MessageBox.Show("このページには領域がありません。", "読み順変更",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (isReadingOrderMode)
            {
                FinishReadingOrderMode(true);
                return;
            }

            isReadingOrderMode = true;
            reorderedRegions.Clear();
            assignedRegions.Clear();

            btnReorderMode.BackColor = Color.FromArgb(234, 88, 12);
            btnReorderMode.ForeColor = Color.White;

            txtLog.AppendText("【読み順変更モード開始】領域をクリックした順に ①, ②, ③... と番号（読み順）が割り当てられます。再度ボタンを押すか全領域クリックで完了します。" + Environment.NewLine);

            pictureBox1.Focus();
            pictureBox1.Invalidate();
        }

        private void FinishReadingOrderMode(bool applyChanges)
        {
            if (!isReadingOrderMode) return;

            if (applyChanges && reorderedRegions.Count > 0)
            {
                // 未割り当ての残りの領域があれば末尾に追加
                foreach (var r in regions)
                {
                    if (!assignedRegions.Contains(r))
                    {
                        reorderedRegions.Add(r);
                    }
                }

                regions.Clear();
                regions.AddRange(reorderedRegions);

                pageRegions[currentPage] = _layoutStorage.CloneRegions(regions);
                _layoutStorage.ForceSavePageRegions(currentPage, regions, pageRegions);

                txtLog.AppendText($"【読み順変更完了】{regions.Count}件の領域の読み順を再設定しました。" + Environment.NewLine);
            }

            isReadingOrderMode = false;
            reorderedRegions.Clear();
            assignedRegions.Clear();

            btnReorderMode.BackColor = SystemColors.Control;
            btnReorderMode.ForeColor = SystemColors.ControlText;

            RefreshRegionList();
            pictureBox1.Invalidate();
        }

        public void CancelAllInteractiveDrawingModes()
        {
            isDrawingRegion = false;
            regionPreviewRectangle = Rectangle.Empty;
            resizeMode = ResizeMode.None;
            movingRegionIndex = -1;
            pictureBox1.Cursor = Cursors.Default;
            Cursor = Cursors.Default;
            btnRegionSettings.BackColor = SystemColors.Control;
            pictureBox1.Invalidate();
        }

        private void pictureBox1_MouseLeave(object? sender, EventArgs e)
        {
            if (!isDrawingRegion && resizeMode == ResizeMode.None && movingRegionIndex < 0)
            {
                pictureBox1.Cursor = Cursors.Default;
            }
        }

        private void btnRegionSettings_Click(object? sender, EventArgs e)
        {
            if (pdfDocument == null || pictureBox1.Image == null)
            {
                MessageBox.Show("先にPDFを開いてください。", "領域設定",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (isReadingOrderMode)
            {
                FinishReadingOrderMode(false);
            }

            // 既に描画モードの場合はキャンセル（トグル動作）
            if (isDrawingRegion)
            {
                CancelAllInteractiveDrawingModes();
                return;
            }

            isDrawingRegion = true;
            regionPreviewRectangle = Rectangle.Empty;
            btnRegionSettings.BackColor = Color.LightSkyBlue;
            pictureBox1.Focus();
            pictureBox1.Cursor = Cursors.Cross;
            Cursor = Cursors.Default;
            pictureBox1.Invalidate();
        }

        private void btnDeleteRegion_Click(object sender, EventArgs e)
        {
            int index = lstRegions.SelectedIndex;
            if (index < 0 || index >= regions.Count)
            {
                MessageBox.Show("削除する領域を選択してください。", "領域未選択",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            regions.RemoveAt(index);
            lstRegions.Items.RemoveAt(index);
            pageRegions[currentPage] = _layoutStorage.CloneRegions(regions);
            lstRegions.ClearSelected();

            isUpdatingNumericValues = true;
            try
            {
                numX.Value = 0;
                numY.Value = 0;
                numWidth.Value = 0;
                numHeight.Value = 0;
            }
            finally
            {
                isUpdatingNumericValues = false;
            }

            UpdateTableLineControlsState();
            pictureBox1.Invalidate();
        }

        private void ApplyNumericBoundsToSelectedRegion()
        {
            if (isUpdatingNumericValues) return;
            int index = lstRegions.SelectedIndex;
            if (index < 0 || index >= regions.Count) return;

            OcrRegion region = regions[index];
            region.X = (int)numX.Value;
            region.Y = (int)numY.Value;
            region.Width = (int)numWidth.Value;
            region.Height = (int)numHeight.Value;

            pageRegions[currentPage] = _layoutStorage.CloneRegions(regions);
            _layoutStorage.ForceSavePageRegions(currentPage, regions, pageRegions);
            pictureBox1.Invalidate();
        }

        private void lstRegions_SelectedIndexChanged(object sender, EventArgs e)
        {
            int index = lstRegions.SelectedIndex;
            if (index < 0 || index >= regions.Count || regions[index].Type != "table")
            {
                selectedRuleLineIndices.Clear();
            }
            else
            {
                selectedRuleLineIndices.RemoveWhere(idx => idx >= regions[index].RuleLines.Count);
            }

            UpdateTableLineControlsState();
            if (index < 0 || index >= regions.Count) return;

            OcrRegion region = regions[index];
            isUpdatingNumericValues = true;
            try
            {
                numX.Value = Math.Min(numX.Maximum, Math.Max(numX.Minimum, region.X));
                numY.Value = Math.Min(numY.Maximum, Math.Max(numY.Minimum, region.Y));
                numWidth.Value = Math.Min(numWidth.Maximum, Math.Max(numWidth.Minimum, region.Width));
                numHeight.Value = Math.Min(numHeight.Maximum, Math.Max(numHeight.Minimum, region.Height));
            }
            finally
            {
                isUpdatingNumericValues = false;
            }
        }

        private void ShowRegionContextMenu(Point screenLocation, OcrRegion region)
        {
            var menu = new ContextMenuStrip();

            var titleItem = new ToolStripMenuItem($"【{region.Name}】 領域メニュー")
            {
                Enabled = false,
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold)
            };
            menu.Items.Add(titleItem);

            var reOcrItem = new ToolStripMenuItem("🎯 この領域のみ再OCR (既存編集を保護)")
            {
                Font = new Font(Font.FontFamily, 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(20, 60, 140)
            };
            reOcrItem.Click += async (s, ev) => await RunSingleRegionOcrAsync(region);
            menu.Items.Add(reOcrItem);
            menu.Items.Add(new ToolStripSeparator());

            var types = new (string type, string name)[]
            {
                ("body", "本文"),
                ("heading", "見出し"),
                ("table", "表"),
                ("footnote", "注釈文"),
                ("image", "図")
            };

            foreach (var (t, n) in types)
            {
                var item = new ToolStripMenuItem(n)
                {
                    Checked = (region.Type == t)
                };
                string targetType = t;
                string targetName = n;
                item.Click += (s, ev) => ChangeSelectedRegionType(targetType, targetName);
                menu.Items.Add(item);
            }

            menu.Items.Add(new ToolStripSeparator());
            var delItem = new ToolStripMenuItem("🗑 この領域を削除")
            {
                ForeColor = Color.DarkRed
            };
            delItem.Click += (s, ev) => btnDeleteRegion_Click(this, EventArgs.Empty);
            menu.Items.Add(delItem);

            menu.Show(pictureBox1, screenLocation);
        }

        private void ShowEmptyCanvasContextMenu(Point screenLocation, Point imgPoint)
        {
            var menu = new ContextMenuStrip();
            var titleItem = new ToolStripMenuItem("新規領域を作成 (クリック位置):")
            {
                Enabled = false,
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold)
            };
            menu.Items.Add(titleItem);
            menu.Items.Add(new ToolStripSeparator());

            int w = 250;
            int h = 100;
            int x = Math.Max(0, imgPoint.X - w / 2);
            int y = Math.Max(0, imgPoint.Y - h / 2);
            if (pictureBox1.Image != null)
            {
                w = Math.Min(w, pictureBox1.Image.Width - x);
                h = Math.Min(h, pictureBox1.Image.Height - y);
            }
            Rectangle newRect = new Rectangle(x, y, w, h);

            var types = new (string type, string name)[]
            {
                ("body", "＋ 本文 領域を作成"),
                ("heading", "＋ 見出し 領域を作成"),
                ("table", "＋ 表 領域を作成"),
                ("footnote", "＋ 注釈文 領域を作成"),
                ("image", "＋ 図 領域を作成")
            };

            foreach (var (t, title) in types)
            {
                string targetType = t;
                string regionName = GetRegionTypeName(t);
                var item = new ToolStripMenuItem(title);
                item.Click += (s, ev) => CreateNewRegionWithBounds(newRect, targetType, regionName);
                menu.Items.Add(item);
            }

            menu.Show(pictureBox1, screenLocation);
        }

        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (pictureBox1.Image == null) return;

            // 読み順設定モード中のクリック処理
            if (isReadingOrderMode)
            {
                if (e.Button == MouseButtons.Right)
                {
                    FinishReadingOrderMode(true);
                    return;
                }

                if (e.Button == MouseButtons.Left)
                {
                    int clickedIdx = -1;
                    for (int i = regions.Count - 1; i >= 0; i--)
                    {
                        Rectangle screenRect = ImageCoordinateHelper.ImageToScreen(
                            new Rectangle(regions[i].X, regions[i].Y, regions[i].Width, regions[i].Height),
                            pictureBox1);
                        if (screenRect.Contains(e.Location))
                        {
                            clickedIdx = i;
                            break;
                        }
                    }

                    if (clickedIdx >= 0)
                    {
                        OcrRegion clicked = regions[clickedIdx];
                        if (!assignedRegions.Contains(clicked))
                        {
                            reorderedRegions.Add(clicked);
                            assignedRegions.Add(clicked);

                            int currentOrder = reorderedRegions.Count;
                            txtLog.AppendText($"  -> 【{currentOrder}番目】: {clicked.Name}" + Environment.NewLine);

                            if (reorderedRegions.Count == regions.Count)
                            {
                                FinishReadingOrderMode(true);
                                return;
                            }
                        }
                        pictureBox1.Invalidate();
                    }
                    return;
                }
            }

            // 右クリック：領域描画モード中の場合はキャンセル、または罫線削除 / 領域区分変更 / 新規領域作成
            if (e.Button == MouseButtons.Right)
            {
                if (isDrawingRegion)
                {
                    CancelAllInteractiveDrawingModes();
                    return;
                }

                if (HandleRuleLineRightClick(e.Location))
                    return;

                // 領域上の右クリック判定（区分変更メニュー）
                int hitRegionIdx = -1;
                for (int i = regions.Count - 1; i >= 0; i--)
                {
                    Rectangle screenRect = ImageCoordinateHelper.ImageToScreen(
                        new Rectangle(regions[i].X, regions[i].Y, regions[i].Width, regions[i].Height),
                        pictureBox1);
                    if (screenRect.Contains(e.Location))
                    {
                        hitRegionIdx = i;
                        break;
                    }
                }

                if (hitRegionIdx >= 0)
                {
                    lstRegions.SelectedIndex = hitRegionIdx;
                    pictureBox1.Invalidate();
                    ShowRegionContextMenu(e.Location, regions[hitRegionIdx]);
                    return;
                }
                else
                {
                    // 空白キャンバス上の右クリック：新規領域作成メニュー
                    Point imgPoint = ImageCoordinateHelper.ScreenToImagePoint(e.Location, pictureBox1);
                    ShowEmptyCanvasContextMenu(e.Location, imgPoint);
                    return;
                }
            }

            if (e.Button != MouseButtons.Left) return;

            // 罫線操作のマウスダウン処理
            if (HandleRuleLineMouseDown(e))
                return;

            // 既存領域のリサイズ・移動判定
            int hitIndex = hoverRegionIndex;
            if (hitIndex >= 0 && hitIndex < regions.Count)
            {
                selectedRuleLineIndices.Clear();
                lstRegions.SelectedIndex = hitIndex;
                OcrRegion region = regions[hitIndex];

                if (hoverResizeMode != ResizeMode.None)
                {
                    resizeMode = hoverResizeMode;
                    movingRegionIndex = -1;
                    resizeStartPoint = e.Location;
                    resizeOriginalRectangle = new Rectangle(
                        region.X, region.Y, region.Width, region.Height);
                    return;
                }

                movingRegionIndex = hitIndex;
                moveStartPoint = e.Location;
                moveOriginalRectangle = new Rectangle(
                    region.X, region.Y, region.Width, region.Height);
                pictureBox1.Cursor = Cursors.SizeAll;
                return;
            }

            // 新規領域描画
            selectedRuleLineIndices.Clear();
            isDrawingRegion = true;
            regionStartPoint = e.Location;
            regionPreviewRectangle = new Rectangle(e.X, e.Y, 0, 0);
        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (pictureBox1.Image == null) return;

            // 罫線操作のマウス移動処理
            if (HandleRuleLineMouseMove(e))
                return;

            // 領域ホバー・リサイズ判定
            if (resizeMode == ResizeMode.None && movingRegionIndex < 0 && !isDrawingRegion)
            {
                hoverRegionIndex = -1;
                hoverResizeMode = ResizeMode.None;

                int nearIndex = ImageCoordinateHelper.HitTestRegionNear(
                    e.Location, 20, regions, pictureBox1);

                if (nearIndex >= 0)
                {
                    hoverRegionIndex = nearIndex;

                    if (lstRegions.SelectedIndex != nearIndex)
                    {
                        lstRegions.SelectedIndex = nearIndex;
                        pictureBox1.Invalidate();
                    }

                    OcrRegion hoverRegion = regions[nearIndex];
                    Rectangle hoverRect = ImageCoordinateHelper.ImageToScreen(
                        new Rectangle(hoverRegion.X, hoverRegion.Y,
                            hoverRegion.Width, hoverRegion.Height),
                        pictureBox1);

                    ResizeMode mode = ImageCoordinateHelper.GetResizeMode(e.Location, hoverRect);
                    hoverResizeMode = mode;

                    pictureBox1.Cursor = mode switch
                    {
                        ResizeMode.Left or ResizeMode.Right => Cursors.SizeWE,
                        ResizeMode.Top or ResizeMode.Bottom => Cursors.SizeNS,
                        ResizeMode.TopLeft or ResizeMode.BottomRight => Cursors.SizeNWSE,
                        ResizeMode.TopRight or ResizeMode.BottomLeft => Cursors.SizeNESW,
                        _ => Cursors.SizeAll
                    };
                }
                else
                {
                    pictureBox1.Cursor = Cursors.Default;
                }
            }

            // 領域リサイズ中
            if (resizeMode != ResizeMode.None)
            {
                if (lstRegions.SelectedIndex < 0) return;
                OcrRegion region = regions[lstRegions.SelectedIndex];

                var (scale, _, _) = ImageCoordinateHelper.GetScaleAndOffset(pictureBox1);

                int deltaX = (int)Math.Round((e.X - resizeStartPoint.X) / scale);
                int deltaY = (int)Math.Round((e.Y - resizeStartPoint.Y) / scale);

                int newX = resizeOriginalRectangle.X;
                int newY = resizeOriginalRectangle.Y;
                int newWidth = resizeOriginalRectangle.Width;
                int newHeight = resizeOriginalRectangle.Height;

                switch (resizeMode)
                {
                    case ResizeMode.Left:
                        newX += deltaX; newWidth -= deltaX; break;
                    case ResizeMode.Right:
                        newWidth += deltaX; break;
                    case ResizeMode.Top:
                        newY += deltaY; newHeight -= deltaY; break;
                    case ResizeMode.Bottom:
                        newHeight += deltaY; break;
                    case ResizeMode.TopLeft:
                        newX += deltaX; newWidth -= deltaX;
                        newY += deltaY; newHeight -= deltaY; break;
                    case ResizeMode.TopRight:
                        newWidth += deltaX;
                        newY += deltaY; newHeight -= deltaY; break;
                    case ResizeMode.BottomLeft:
                        newX += deltaX; newWidth -= deltaX;
                        newHeight += deltaY; break;
                    case ResizeMode.BottomRight:
                        newWidth += deltaX; newHeight += deltaY; break;
                }

                const int minSize = 20;
                if (newWidth < minSize)
                {
                    newWidth = minSize;
                    if (resizeMode is ResizeMode.Left or ResizeMode.TopLeft or ResizeMode.BottomLeft)
                        newX = resizeOriginalRectangle.Right - minSize;
                }
                if (newHeight < minSize)
                {
                    newHeight = minSize;
                    if (resizeMode is ResizeMode.Top or ResizeMode.TopLeft or ResizeMode.TopRight)
                        newY = resizeOriginalRectangle.Bottom - minSize;
                }

                newX = Math.Max(0, newX);
                newY = Math.Max(0, newY);
                newWidth = Math.Min(newWidth, pictureBox1.Image.Width - newX);
                newHeight = Math.Min(newHeight, pictureBox1.Image.Height - newY);

                region.X = newX; region.Y = newY;
                region.Width = newWidth; region.Height = newHeight;

                isUpdatingNumericValues = true;
                try
                {
                    numX.Value = Math.Min(numX.Maximum, Math.Max(numX.Minimum, region.X));
                    numY.Value = Math.Min(numY.Maximum, Math.Max(numY.Minimum, region.Y));
                    numWidth.Value = Math.Min(numWidth.Maximum, Math.Max(numWidth.Minimum, region.Width));
                    numHeight.Value = Math.Min(numHeight.Maximum, Math.Max(numHeight.Minimum, region.Height));
                }
                finally
                {
                    isUpdatingNumericValues = false;
                }

                pictureBox1.Invalidate();
                return;
            }

            // 領域移動中
            if (movingRegionIndex >= 0)
            {
                OcrRegion region = regions[movingRegionIndex];
                var (scale, _, _) = ImageCoordinateHelper.GetScaleAndOffset(pictureBox1);

                int newX = moveOriginalRectangle.X + (int)Math.Round((e.X - moveStartPoint.X) / scale);
                int newY = moveOriginalRectangle.Y + (int)Math.Round((e.Y - moveStartPoint.Y) / scale);

                newX = Math.Max(0, Math.Min(newX, pictureBox1.Image.Width - region.Width));
                newY = Math.Max(0, Math.Min(newY, pictureBox1.Image.Height - region.Height));

                region.X = newX; region.Y = newY;

                isUpdatingNumericValues = true;
                try
                {
                    numX.Value = Math.Min(numX.Maximum, Math.Max(numX.Minimum, region.X));
                    numY.Value = Math.Min(numY.Maximum, Math.Max(numY.Minimum, region.Y));
                }
                finally
                {
                    isUpdatingNumericValues = false;
                }

                pictureBox1.Invalidate();
                return;
            }

            // 新規領域描画中
            if (isDrawingRegion)
            {
                int drawX = Math.Min(regionStartPoint.X, e.X);
                int drawY = Math.Min(regionStartPoint.Y, e.Y);
                int drawWidth = Math.Abs(e.X - regionStartPoint.X);
                int drawHeight = Math.Abs(e.Y - regionStartPoint.Y);
                regionPreviewRectangle = new Rectangle(drawX, drawY, drawWidth, drawHeight);
                pictureBox1.Invalidate();
            }
        }

        private void pictureBox1_MouseUp(object sender, MouseEventArgs e)
        {
            if (HandleRuleLineMouseUp(e))
                return;

            if (resizeMode != ResizeMode.None)
            {
                resizeMode = ResizeMode.None;
                pictureBox1.Cursor = Cursors.Default;
                pictureBox1.Invalidate();
                return;
            }

            if (movingRegionIndex >= 0)
            {
                movingRegionIndex = -1;
                pictureBox1.Cursor = Cursors.Default;
                pictureBox1.Invalidate();
                return;
            }

            if (!isDrawingRegion) return;

            if (regionPreviewRectangle.Width < 5 || regionPreviewRectangle.Height < 5)
            {
                CancelAllInteractiveDrawingModes();
                return;
            }

            Rectangle imageRect = ImageCoordinateHelper.ScreenToImage(
                regionPreviewRectangle, pictureBox1);

            var menu = new ContextMenuStrip();
            var titleItem = new ToolStripMenuItem("新規領域の種別を選択:")
            {
                Enabled = false,
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold)
            };
            menu.Items.Add(titleItem);
            menu.Items.Add(new ToolStripSeparator());

            var types = new (string type, string name)[]
            {
                ("body", "＋ 本文"),
                ("heading", "＋ 見出し"),
                ("table", "＋ 表"),
                ("footnote", "＋ 注釈文"),
                ("image", "＋ 図")
            };

            foreach (var (t, n) in types)
            {
                string targetType = t;
                string regionName = n;
                var item = new ToolStripMenuItem(n);
                item.Click += (s, ev) => CreateNewRegionWithBounds(imageRect, targetType, regionName);
                menu.Items.Add(item);
            }

            menu.Items.Add(new ToolStripSeparator());
            var cancelItem = new ToolStripMenuItem("✕ キャンセル");
            cancelItem.Click += (s, ev) => CancelAllInteractiveDrawingModes();
            menu.Items.Add(cancelItem);

            menu.Closed += (s, ev) => CancelAllInteractiveDrawingModes();

            menu.Show(pictureBox1, e.Location);
        }

        private void btnCopyRegionsToPages_Click(object? sender, EventArgs e)
        {
            if (regions == null || regions.Count == 0)
            {
                MessageBox.Show(
                    "コピーする領域設定がありません。先に領域を設定または自動判定してください。",
                    "確認",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            // 現在開いているバッチのページ範囲 (1-based)
            int minP = (int)numPageStart.Value;
            int maxP = (int)numPageEnd.Value;
            if (maxP < minP)
            {
                maxP = minP;
            }

            using var form = new CopyRegionsForm(currentPage, minP, maxP, regions);
            if (form.ShowDialog(this) == DialogResult.OK && form.SelectedTargetPages.Count > 0)
            {
                int copiedCount = _layoutStorage.CopyRegionsToPages(
                    pageRegions,
                    currentPage,
                    form.SelectedTargetPages,
                    regions);

                string targetSummary = string.Join(", ", form.SelectedTargetPages.Select(p => $"P.{p + 1}"));
                txtLog.AppendText($"✔ 第 {currentPage + 1} ページの領域設定（{regions.Count}件）を {copiedCount} ページ（{targetSummary}）に一括コピー・保存しました。\n");
                txtLog.ScrollToCaret();

                MessageBox.Show(
                    $"第 {currentPage + 1} ページの領域設定（{regions.Count}件）を {copiedCount} ページに一括適用しました。\n\n対象ページ: {targetSummary}",
                    "領域コピー完了",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private async void btnReOcrRegion_Click(object? sender, EventArgs e)
        {
            int index = lstRegions.SelectedIndex;
            if (index < 0 || index >= regions.Count)
            {
                MessageBox.Show("先に領域一覧またはプレビュー画面で再OCRしたい領域を選択してください。", "領域再OCR",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            await RunSingleRegionOcrAsync(regions[index]);
        }
    }
}

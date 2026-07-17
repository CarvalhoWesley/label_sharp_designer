using System.ComponentModel;
using System.Globalization;
using LabelSharpDesigner.Core.Document;
using LabelSharpDesigner.Core.Elements;
using LabelSharpDesigner.Core.Styles;
using LabelSharpDesigner.UI.WinForms.Canvas;

namespace LabelSharpDesigner.UI.WinForms.Panels;

/// <summary>
/// The editor's property panel: reactive editing of the selected element(s)' fields, mirroring
/// the original Flutter <c>label_property_panel</c> package's layout (General + Geometry +
/// a type-specific section, or a reduced multi-selection view). Every field edit is dispatched
/// through <see cref="LabelCanvasControl.ApplyPropertyChange"/>, so this panel never mutates a
/// <see cref="LabelDocument"/> directly — every edit is undoable by construction.
/// </summary>
public sealed class PropertyPanel : UserControl
{
    private readonly Panel _content;
    private LabelCanvasControl? _canvas;
    private int _top;
    private int _contentWidth;

    public PropertyPanel()
    {
        _content = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        _content.Resize += (_, _) => Rebuild();
        Controls.Add(_content);
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public LabelCanvasControl? Canvas
    {
        get => _canvas;
        set
        {
            if (_canvas is not null)
            {
                _canvas.SelectionChanged -= OnCanvasChanged;
                _canvas.DocumentChanged -= OnCanvasChanged;
            }

            _canvas = value;
            if (_canvas is not null)
            {
                _canvas.SelectionChanged += OnCanvasChanged;
                _canvas.DocumentChanged += OnCanvasChanged;
            }

            Rebuild();
        }
    }

    /// <summary>Deferred via <see cref="Control.BeginInvoke(Delegate)"/>: <see cref="Rebuild"/> disposes
    /// every field control, and this handler can be running inside one of those controls' own event
    /// (e.g. a checkbox's <c>CheckedChanged</c> triggering <c>ApplyPropertyChange</c>) — rebuilding
    /// synchronously would dispose a control while its own handler is still on the call stack.</summary>
    private void OnCanvasChanged(object? sender, EventArgs e)
    {
        if (IsHandleCreated)
        {
            BeginInvoke(new MethodInvoker(Rebuild));
        }
        else
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        _content.SuspendLayout();
        foreach (Control control in _content.Controls)
        {
            control.Dispose();
        }

        _content.Controls.Clear();
        _top = 0;
        _contentWidth = Math.Max(_content.ClientSize.Width - 20, 140);

        var document = _canvas?.Document;
        var selectedIds = _canvas?.SelectedElementIds ?? Array.Empty<string>();
        var selected = document?.Elements.Where(e => selectedIds.Contains(e.Id)).ToList() ?? [];

        if (document is null || selected.Count == 0)
        {
            AddControl(new Label { Text = "Nenhuma seleção", ForeColor = SystemColors.GrayText }, 24);
        }
        else if (selected.Count == 1)
        {
            BuildGeneralSection(document, selected[0]);
            BuildGeometrySection(selected[0]);
            BuildTypeSpecificSection(document, selected[0]);
        }
        else
        {
            BuildMultiSelection(selected);
        }

        _content.ResumeLayout();
    }

    // ---- layout plumbing ----

    private void AddControl(Control control, int height)
    {
        control.Left = 4;
        control.Top = _top;
        control.Width = _contentWidth - 8;
        control.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
        _content.Controls.Add(control);
        _top += height;
    }

    private void AddSectionTitle(string text) =>
        AddControl(new Label { Text = text, Font = new Font(Font, FontStyle.Bold), Padding = new Padding(0, 6, 0, 2) }, 26);

    private void AddLabel(string text) =>
        AddControl(new Label { Text = text, ForeColor = SystemColors.GrayText, AutoSize = false, Height = 16 }, 18);

    private void AddField(Control field, int height = 22)
    {
        field.Height = height;
        AddControl(field, height + 6);
    }

    private NumericUpDown CreateNumeric(decimal value, decimal min, decimal max, int decimals, Action<double> onChange)
    {
        var numeric = new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            DecimalPlaces = decimals,
            Increment = decimals == 0 ? 1 : (decimal)Math.Pow(10, -decimals),
            Value = Math.Clamp(value, min, max),
        };
        numeric.ValueChanged += (_, _) => onChange((double)numeric.Value);
        return numeric;
    }

    private static Color ToColor(ArgbColor color) => Color.FromArgb(color.A, color.R, color.G, color.B);

    private static ArgbColor ToArgbColor(Color color) => new(color.A, color.R, color.G, color.B);

    private Button CreateColorButton(ArgbColor color, Action<ArgbColor> onChange)
    {
        // FlatStyle.Flat (not .System) here: .System hands rendering off to native theming, which
        // ignores BackColor — this button's whole point is showing the color as its own background.
        // Flat still fixes the .NET 9 dark-mode bug (FlatStyle.Standard renders no button text) while
        // keeping the swatch.
        var button = new Button { Text = color.ToHex(), BackColor = ToColor(color), FlatStyle = FlatStyle.Flat };
        button.Click += (_, _) =>
        {
            using var dialog = new ColorDialog { Color = ToColor(color) };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                onChange(ToArgbColor(dialog.Color));
            }
        };
        return button;
    }

    private TextBox CreateTextField(string initialValue, Action<string> commit)
    {
        var box = new TextBox { Text = initialValue };
        box.Leave += (_, _) => commit(box.Text);
        box.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                commit(box.Text);
                e.SuppressKeyPress = true;
            }
        };
        return box;
    }

    /// <summary>Adds a small button right below <paramref name="field"/> that inserts <c>{{ name }}</c>
    /// for whichever declared <see cref="LabelVariable"/> the user picks from a dropdown menu, at the
    /// field's own last known caret position — so building a placeholder never requires remembering the
    /// <c>{{ }}</c> syntax by hand. For any free-text field that goes through <c>TemplateResolver</c> at
    /// resolve time (<see cref="TextElement.Content"/>, <see cref="BarcodeElement.Data"/>,
    /// <see cref="QrCodeElement.Data"/>) — never for <see cref="VariableElement.Expression"/>, which is
    /// a bare expression, not a template (see <see cref="BuildVariableSection"/>).</summary>
    private void AddInsertVariableButton(LabelDocument document, TextBox field, Action<string> commit)
    {
        var lastSelectionStart = field.Text.Length;
        var lastSelectionLength = 0;

        void RememberSelection(object? sender, EventArgs e)
        {
            lastSelectionStart = field.SelectionStart;
            lastSelectionLength = field.SelectionLength;
        }

        field.KeyUp += RememberSelection;
        field.MouseUp += RememberSelection;
        field.Enter += RememberSelection;

        // FlatStyle.System works around a .NET 9 dark-mode bug where the default FlatStyle.Standard
        // renders no button text.
        var button = new Button { Text = "{{ }} Inserir variável...", AutoSize = true, FlatStyle = FlatStyle.System };
        button.Click += (_, _) =>
        {
            // Deliberately never disposed here: ToolStripDropDown keeps running its own
            // close/click-processing after Closed fires, and disposing the menu from inside its own
            // Closed handler races that — the framework then touches the already-disposed menu and
            // throws "Cannot access a disposed object." Not added to any Controls collection (context
            // menus never are), so it's never in PropertyPanel.Rebuild()'s "dispose every field
            // control" pass either — left for the GC/finalizer, same as any other short-lived,
            // unparented WinForms component.
            var menu = new ContextMenuStrip();
            if (document.Variables.Count == 0)
            {
                menu.Items.Add(new ToolStripMenuItem("Nenhuma variável cadastrada — veja \"Variáveis...\"") { Enabled = false });
            }
            else
            {
                foreach (var variable in document.Variables)
                {
                    var name = variable.Name;
                    var item = new ToolStripMenuItem($"{name} ({variable.Type})");
                    item.Click += (_, _) =>
                    {
                        var placeholder = "{{" + name + "}}";
                        var text = field.Text;
                        var start = Math.Clamp(lastSelectionStart, 0, text.Length);
                        var length = Math.Clamp(lastSelectionLength, 0, text.Length - start);
                        var updated = text[..start] + placeholder + text[(start + length)..];

                        field.Text = updated;
                        var caret = start + placeholder.Length;
                        field.Focus();
                        field.SelectionStart = caret;
                        field.SelectionLength = 0;
                        lastSelectionStart = caret;
                        lastSelectionLength = 0;

                        commit(updated);
                    };
                    menu.Items.Add(item);
                }
            }

            menu.Show(button, new Point(0, button.Height));
        };

        AddField(button, 24);
    }

    // ---- common sections ----

    private void BuildGeneralSection(LabelDocument document, LabelElement element)
    {
        AddSectionTitle("Geral");

        AddLabel("Nome");
        AddField(CreateTextField(element.Name ?? string.Empty, text =>
            _canvas!.ApplyPropertyChange(e => e with { Name = string.IsNullOrWhiteSpace(text) ? null : text }, "Nome")));

        AddLabel("Camada");
        var layers = document.Layers.OrderBy(l => l.Order).ToList();
        var layerCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        layerCombo.Items.Add("(nenhuma)");
        foreach (var layer in layers)
        {
            layerCombo.Items.Add(layer.Name);
        }

        var selectedLayerIndex = element.LayerId is null ? 0 : layers.FindIndex(l => l.Id == element.LayerId) + 1;
        layerCombo.SelectedIndex = Math.Max(selectedLayerIndex, 0);
        layerCombo.SelectedIndexChanged += (_, _) =>
        {
            var newLayerId = layerCombo.SelectedIndex <= 0 ? null : layers[layerCombo.SelectedIndex - 1].Id;
            _canvas!.ApplyPropertyChange(e => e with { LayerId = newLayerId }, "Camada");
        };
        AddField(layerCombo);

        AddLabel($"Opacidade ({Math.Round(element.Opacity * 100)}%)");
        AddField(CreateNumeric((decimal)Math.Round(element.Opacity * 100), 0, 100, 0, value =>
            _canvas!.ApplyPropertyChange(e => e with { Opacity = value / 100.0 }, "Opacidade")));

        var lockedCheck = new CheckBox { Text = "Bloqueado", Checked = element.Locked };
        lockedCheck.CheckedChanged += (_, _) => _canvas!.ApplyPropertyChange(e => e with { Locked = lockedCheck.Checked }, "Bloqueio");
        AddField(lockedCheck);
    }

    private void BuildGeometrySection(LabelElement element)
    {
        AddSectionTitle("Geometria");

        // X/Y live in the status bar (EditorForm), not here — they need to reflect every mouse-move
        // of a drag, and this panel only rebuilds on a committed change (see PropertyPanel's
        // DocumentChanged/LiveChanged split), so it can't show live position without adding back the
        // per-frame rebuild cost we're avoiding.
        AddLabel("Largura (mm)");
        AddField(CreateNumeric((decimal)element.Size.Width, 0.1m, 2000, 2, value =>
            _canvas!.ApplyPropertyChange(e => e with { Size = e.Size with { Width = Math.Max(value, 0.1) } }, "Largura")));

        AddLabel("Altura (mm)");
        AddField(CreateNumeric((decimal)element.Size.Height, 0.1m, 2000, 2, value =>
            _canvas!.ApplyPropertyChange(e => e with { Size = e.Size with { Height = Math.Max(value, 0.1) } }, "Altura")));

        AddLabel("Rotação (°)");
        AddField(CreateNumeric((decimal)element.RotationDegrees, -3600, 3600, 1, value =>
            _canvas!.ApplyPropertyChange(e => e with { RotationDegrees = value }, "Rotação")));
    }

    private void BuildMultiSelection(List<LabelElement> elements)
    {
        AddSectionTitle($"{elements.Count} elementos selecionados");

        var allVisible = elements.All(e => e.Visible);
        var allLocked = elements.All(e => e.Locked);
        var averageOpacity = elements.Average(e => e.Opacity);

        var visibleCheck = new CheckBox { Text = allVisible ? "Ocultar todos" : "Mostrar todos", Checked = allVisible };
        visibleCheck.Click += (_, _) => _canvas!.ApplyPropertyChange(e => e with { Visible = !allVisible }, "Visibilidade");
        AddField(visibleCheck);

        var lockedCheck = new CheckBox { Text = allLocked ? "Desbloquear todos" : "Bloquear todos", Checked = allLocked };
        lockedCheck.Click += (_, _) => _canvas!.ApplyPropertyChange(e => e with { Locked = !allLocked }, "Bloqueio");
        AddField(lockedCheck);

        AddLabel($"Opacidade média ({Math.Round(averageOpacity * 100)}%)");
        AddField(CreateNumeric((decimal)Math.Round(averageOpacity * 100), 0, 100, 0, value =>
            _canvas!.ApplyPropertyChange(e => e with { Opacity = value / 100.0 }, "Opacidade")));
    }

    // ---- shared style field groups ----

    private void AddStyleIdDropdown(List<LabelStyle> styles, string? styleId, Action<string?> onChange)
    {
        AddLabel("Estilo nomeado");
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        combo.Items.Add("(nenhum)");
        foreach (var style in styles)
        {
            combo.Items.Add(style.Name);
        }

        var index = styleId is null ? 0 : styles.FindIndex(s => s.Id == styleId) + 1;
        combo.SelectedIndex = Math.Max(index, 0);
        combo.SelectedIndexChanged += (_, _) => onChange(combo.SelectedIndex <= 0 ? null : styles[combo.SelectedIndex - 1].Id);
        AddField(combo);
    }

    private void AddTextStyleFields(TextStyleSpec style, Action<TextStyleSpec> onChange)
    {
        AddLabel("Fonte");
        AddField(CreateTextField(style.FontFamily, text =>
            onChange(style with { FontFamily = string.IsNullOrWhiteSpace(text) ? style.FontFamily : text })));

        AddLabel("Tamanho (pt)");
        AddField(CreateNumeric((decimal)style.FontSizePt, 1, 500, 1, value => onChange(style with { FontSizePt = value })));

        // Panel's default Height (100px) was never overridden here — it painted over "Cor do texto"/
        // "Alinhamento" below, since AddControl only reserves the 36px passed in, not whatever height
        // the control itself happens to have.
        var stylesRow = new Panel { Height = 24 };
        var boldCheck = new CheckBox { Text = "Negrito", Checked = style.Bold, Left = 0, Top = 0, Width = 70 };
        var italicCheck = new CheckBox { Text = "Itálico", Checked = style.Italic, Left = 74, Top = 0, Width = 70 };
        var underlineCheck = new CheckBox { Text = "Sublinhado", Checked = style.Underline, Left = 148, Top = 0, Width = 96 };
        boldCheck.CheckedChanged += (_, _) => onChange(style with { Bold = boldCheck.Checked });
        italicCheck.CheckedChanged += (_, _) => onChange(style with { Italic = italicCheck.Checked });
        underlineCheck.CheckedChanged += (_, _) => onChange(style with { Underline = underlineCheck.Checked });
        stylesRow.Controls.Add(boldCheck);
        stylesRow.Controls.Add(italicCheck);
        stylesRow.Controls.Add(underlineCheck);
        AddControl(stylesRow, 36);

        AddLabel("Cor do texto");
        AddField(CreateColorButton(style.Color, color => onChange(style with { Color = color })));

        AddLabel("Alinhamento");
        var alignCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var value in Enum.GetValues<TextAlign>())
        {
            alignCombo.Items.Add(value);
        }

        alignCombo.SelectedItem = style.Align;
        alignCombo.SelectedIndexChanged += (_, _) => onChange(style with { Align = (TextAlign)alignCombo.SelectedItem! });
        AddField(alignCombo);
    }

    private void AddShapeStyleFields(ShapeStyleSpec style, Action<ShapeStyleSpec> onChange)
    {
        AddLabel("Cor da borda");
        AddField(CreateColorButton(style.BorderColor, color => onChange(style with { BorderColor = color })));

        AddLabel("Espessura da borda (mm)");
        AddField(CreateNumeric((decimal)style.BorderWidthMm, 0, 20, 2, value => onChange(style with { BorderWidthMm = value })));

        var fillColorButton = CreateColorButton(style.FillColor ?? ArgbColor.White, color => onChange(style with { FillColor = color }));
        fillColorButton.Enabled = style.FillColor is not null;

        var fillCheck = new CheckBox { Text = "Preencher", Checked = style.FillColor is not null };
        fillCheck.CheckedChanged += (_, _) =>
        {
            fillColorButton.Enabled = fillCheck.Checked;
            onChange(style with { FillColor = fillCheck.Checked ? style.FillColor ?? ArgbColor.White : null });
        };
        AddField(fillCheck);
        AddField(fillColorButton);
    }

    private void AddDateTimeSourceFields(
        DateTimeValueSource source,
        string? variableName,
        Action<DateTimeValueSource> onSourceChange,
        Action<string?> onVariableNameChange)
    {
        AddLabel("Origem");
        var sourceCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var value in Enum.GetValues<DateTimeValueSource>())
        {
            sourceCombo.Items.Add(value);
        }

        sourceCombo.SelectedItem = source;
        sourceCombo.SelectedIndexChanged += (_, _) => onSourceChange((DateTimeValueSource)sourceCombo.SelectedItem!);
        AddField(sourceCombo);

        AddLabel("Nome da variável (quando origem = Variable)");
        AddField(CreateTextField(variableName ?? string.Empty, text => onVariableNameChange(string.IsNullOrWhiteSpace(text) ? null : text)));
    }

    // ---- type-specific sections ----

    private void BuildTypeSpecificSection(LabelDocument document, LabelElement element)
    {
        switch (element)
        {
            case TextElement text:
                BuildTextSection(document, text);
                break;
            case BarcodeElement barcode:
                BuildBarcodeSection(document, barcode);
                break;
            case QrCodeElement qr:
                BuildQrCodeSection(document, qr);
                break;
            case ImageElement image:
                BuildImageSection(image);
                break;
            case RectangleElement rectangle:
                BuildRectangleSection(rectangle);
                break;
            case EllipseElement ellipse:
                AddSectionTitle("Elipse");
                AddShapeStyleFields(ellipse.Style, s => _canvas!.ApplyPropertyChange(e => e is EllipseElement el ? el with { Style = s } : e, "Estilo"));
                break;
            case CircleElement circle:
                AddSectionTitle("Círculo");
                AddShapeStyleFields(circle.Style, s => _canvas!.ApplyPropertyChange(e => e is CircleElement el ? el with { Style = s } : e, "Estilo"));
                break;
            case LineElement line:
                BuildLineSection(line);
                break;
            case DateElement date:
                BuildDateSection(date);
                break;
            case TimeElement time:
                BuildTimeSection(time);
                break;
            case VariableElement variable:
                BuildVariableSection(document, variable);
                break;
            case TableElement table:
                BuildTableSection(table);
                break;
            case GroupElement group:
                AddSectionTitle("Grupo");
                AddControl(new Label { Text = $"{group.Children.Count} elemento(s) agrupado(s)" }, 22);
                break;
        }
    }

    private void BuildTextSection(LabelDocument document, TextElement text)
    {
        AddSectionTitle("Texto");

        AddLabel("Conteúdo");
        void CommitContent(string value) => _canvas!.ApplyPropertyChange(e => e is TextElement t ? t with { Content = value } : e, "Conteúdo");
        var contentBox = new TextBox { Text = text.Content, Multiline = true };
        contentBox.Leave += (_, _) => CommitContent(contentBox.Text);
        AddField(contentBox, 60);
        AddInsertVariableButton(document, contentBox, CommitContent);

        AddStyleIdDropdown(
            document.Styles.Where(s => s.Text is not null).ToList(),
            text.StyleId,
            id => _canvas!.ApplyPropertyChange(e => e is TextElement t ? t with { StyleId = id } : e, "Estilo nomeado"));

        AddTextStyleFields(text.Style, newStyle => _canvas!.ApplyPropertyChange(e => e is TextElement t ? t with { Style = newStyle } : e, "Estilo de texto"));
    }

    private void BuildBarcodeSection(LabelDocument document, BarcodeElement barcode)
    {
        AddSectionTitle("Código de barras");

        AddLabel("Dados");
        void CommitData(string value) => _canvas!.ApplyPropertyChange(e => e is BarcodeElement b ? b with { Data = value } : e, "Dados do código de barras");
        var dataField = CreateTextField(barcode.Data, CommitData);
        AddField(dataField);
        AddInsertVariableButton(document, dataField, CommitData);

        AddLabel("Simbologia");
        var symbologyCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var value in Enum.GetValues<BarcodeSymbology>())
        {
            symbologyCombo.Items.Add(value);
        }

        symbologyCombo.SelectedItem = barcode.Symbology;
        symbologyCombo.SelectedIndexChanged += (_, _) =>
            _canvas!.ApplyPropertyChange(e => e is BarcodeElement b ? b with { Symbology = (BarcodeSymbology)symbologyCombo.SelectedItem! } : e, "Simbologia");
        AddField(symbologyCombo);

        var showTextCheck = new CheckBox { Text = "Mostrar texto", Checked = barcode.ShowText };
        showTextCheck.CheckedChanged += (_, _) =>
            _canvas!.ApplyPropertyChange(e => e is BarcodeElement b ? b with { ShowText = showTextCheck.Checked } : e, "Mostrar texto");
        AddField(showTextCheck);

        AddLabel("Largura do módulo (mm)");
        AddField(CreateNumeric((decimal)barcode.ModuleWidth, 0.05m, 5, 2, value =>
            _canvas!.ApplyPropertyChange(e => e is BarcodeElement b ? b with { ModuleWidth = value } : e, "Largura do módulo")));

        AddLabel("Tamanho do texto (mm)");
        AddField(CreateNumeric((decimal)barcode.TextSize, 0.5m, 20, 2, value =>
            _canvas!.ApplyPropertyChange(e => e is BarcodeElement b ? b with { TextSize = value } : e, "Tamanho do texto")));
    }

    private void BuildQrCodeSection(LabelDocument document, QrCodeElement qr)
    {
        AddSectionTitle("QR Code");

        AddLabel("Dados");
        void CommitData(string value) => _canvas!.ApplyPropertyChange(e => e is QrCodeElement q ? q with { Data = value } : e, "Dados do QR");
        var dataField = CreateTextField(qr.Data, CommitData);
        AddField(dataField);
        AddInsertVariableButton(document, dataField, CommitData);

        AddLabel("Nível de correção de erro");
        var eccCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var value in Enum.GetValues<QrErrorCorrectionLevel>())
        {
            eccCombo.Items.Add(value);
        }

        eccCombo.SelectedItem = qr.ErrorCorrectionLevel;
        eccCombo.SelectedIndexChanged += (_, _) =>
            _canvas!.ApplyPropertyChange(e => e is QrCodeElement q ? q with { ErrorCorrectionLevel = (QrErrorCorrectionLevel)eccCombo.SelectedItem! } : e, "Nível de correção");
        AddField(eccCombo);
    }

    private void BuildImageSection(ImageElement image)
    {
        AddSectionTitle("Imagem");

        AddLabel("Origem (caminho ou URL)");
        var sourceField = CreateTextField(image.Source, text => _canvas!.ApplyPropertyChange(e => e is ImageElement i ? i with { Source = text } : e, "Origem da imagem"));
        AddField(sourceField);

        // FlatStyle.System works around a .NET 9 dark-mode bug where the default FlatStyle.Standard
        // renders no button text.
        var browseButton = new Button { Text = "Procurar arquivo...", FlatStyle = FlatStyle.System };
        browseButton.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = "Imagens (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|Todos os arquivos (*.*)|*.*" };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                sourceField.Text = dialog.FileName;
                _canvas!.ApplyPropertyChange(e => e is ImageElement i ? i with { Source = dialog.FileName } : e, "Origem da imagem");
            }
        };
        AddField(browseButton);

        AddLabel("Ajuste");
        var fitCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var value in Enum.GetValues<ImageFit>())
        {
            fitCombo.Items.Add(value);
        }

        fitCombo.SelectedItem = image.Fit;
        fitCombo.SelectedIndexChanged += (_, _) =>
            _canvas!.ApplyPropertyChange(e => e is ImageElement i ? i with { Fit = (ImageFit)fitCombo.SelectedItem! } : e, "Ajuste da imagem");
        AddField(fitCombo);
    }

    private void BuildRectangleSection(RectangleElement rectangle)
    {
        AddSectionTitle("Retângulo");

        AddLabel("Raio do canto (mm)");
        AddField(CreateNumeric((decimal)rectangle.CornerRadius, 0, 50, 2, value =>
            _canvas!.ApplyPropertyChange(e => e is RectangleElement r ? r with { CornerRadius = value } : e, "Raio do canto")));

        AddShapeStyleFields(rectangle.Style, s => _canvas!.ApplyPropertyChange(e => e is RectangleElement r ? r with { Style = s } : e, "Estilo"));
    }

    private void BuildLineSection(LineElement line)
    {
        AddSectionTitle("Linha");

        AddLabel("Cor do traço");
        AddField(CreateColorButton(line.StrokeColor, color => _canvas!.ApplyPropertyChange(e => e is LineElement l ? l with { StrokeColor = color } : e, "Cor do traço")));

        AddLabel("Espessura do traço (mm)");
        AddField(CreateNumeric((decimal)line.StrokeWidth, 0.05m, 20, 2, value =>
            _canvas!.ApplyPropertyChange(e => e is LineElement l ? l with { StrokeWidth = value } : e, "Espessura do traço")));
    }

    private void BuildDateSection(DateElement date)
    {
        AddSectionTitle("Data");

        AddLabel("Formato (.NET)");
        AddField(CreateTextField(date.Format, text => _canvas!.ApplyPropertyChange(e => e is DateElement d ? d with { Format = text } : e, "Formato")));

        AddDateTimeSourceFields(
            date.Source,
            date.VariableName,
            source => _canvas!.ApplyPropertyChange(e => e is DateElement d ? d with { Source = source } : e, "Origem"),
            variableName => _canvas!.ApplyPropertyChange(e => e is DateElement d ? d with { VariableName = variableName } : e, "Nome da variável"));

        AddTextStyleFields(date.Style, newStyle => _canvas!.ApplyPropertyChange(e => e is DateElement d ? d with { Style = newStyle } : e, "Estilo de texto"));
    }

    private void BuildTimeSection(TimeElement time)
    {
        AddSectionTitle("Hora");

        AddLabel("Formato (.NET)");
        AddField(CreateTextField(time.Format, text => _canvas!.ApplyPropertyChange(e => e is TimeElement t ? t with { Format = text } : e, "Formato")));

        AddDateTimeSourceFields(
            time.Source,
            time.VariableName,
            source => _canvas!.ApplyPropertyChange(e => e is TimeElement t ? t with { Source = source } : e, "Origem"),
            variableName => _canvas!.ApplyPropertyChange(e => e is TimeElement t ? t with { VariableName = variableName } : e, "Nome da variável"));

        AddTextStyleFields(time.Style, newStyle => _canvas!.ApplyPropertyChange(e => e is TimeElement t ? t with { Style = newStyle } : e, "Estilo de texto"));
    }

    private void BuildVariableSection(LabelDocument document, VariableElement variable)
    {
        AddSectionTitle("Variável");

        AddLabel("Expressão");
        AddField(CreateTextField(variable.Expression, text =>
            _canvas!.ApplyPropertyChange(e => e is VariableElement v ? v with { Expression = text } : e, "Expressão")));

        AddStyleIdDropdown(
            document.Styles.Where(s => s.Text is not null).ToList(),
            variable.StyleId,
            id => _canvas!.ApplyPropertyChange(e => e is VariableElement v ? v with { StyleId = id } : e, "Estilo nomeado"));

        AddTextStyleFields(variable.Style, newStyle => _canvas!.ApplyPropertyChange(e => e is VariableElement v ? v with { Style = newStyle } : e, "Estilo de texto"));
    }

    private void BuildTableSection(TableElement table)
    {
        AddSectionTitle("Tabela");

        AddLabel("Altura da linha (mm)");
        AddField(CreateNumeric((decimal)table.RowHeightMm, 1, 100, 2, value =>
            _canvas!.ApplyPropertyChange(e => e is TableElement t ? t with { RowHeightMm = value } : e, "Altura da linha")));

        AddLabel("Colunas (cabeçalho / campo / largura mm)");
        var grid = new DataGridView
        {
            AllowUserToAddRows = true,
            AllowUserToDeleteRows = true,
            RowHeadersVisible = false,
            AutoGenerateColumns = false,
        };
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Header", HeaderText = "Cabeçalho", FillWeight = 40 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "DataField", HeaderText = "Campo", FillWeight = 40 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "WidthMm", HeaderText = "Largura", FillWeight = 20 });
        foreach (var column in table.Columns)
        {
            grid.Rows.Add(column.Header, column.DataField, column.WidthMm.ToString(CultureInfo.InvariantCulture));
        }

        AddField(grid, 140);

        // FlatStyle.System works around a .NET 9 dark-mode bug where the default FlatStyle.Standard
        // renders no button text.
        var applyButton = new Button { Text = "Aplicar colunas", FlatStyle = FlatStyle.System };
        applyButton.Click += (_, _) =>
        {
            var columns = new List<TableColumn>();
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                var header = row.Cells["Header"].Value?.ToString();
                var field = row.Cells["DataField"].Value?.ToString();
                if (string.IsNullOrWhiteSpace(header) || string.IsNullOrWhiteSpace(field))
                {
                    continue;
                }

                var widthText = row.Cells["WidthMm"].Value?.ToString();
                var width = double.TryParse(widthText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedWidth) ? parsedWidth : 20;
                columns.Add(new TableColumn { Header = header, DataField = field, WidthMm = width });
            }

            if (columns.Count == 0)
            {
                return;
            }

            _canvas!.ApplyPropertyChange(e => e is TableElement t ? t with { Columns = columns } : e, "Colunas da tabela");
        };
        AddField(applyButton);
    }
}

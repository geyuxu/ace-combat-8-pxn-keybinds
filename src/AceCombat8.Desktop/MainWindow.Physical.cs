using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AceCombat8.Core;
using Microsoft.Win32;
using static AceCombat8.Core.Lang;

namespace AceCombat8.Desktop;

public partial class MainWindow
{
    private FlightInput? selectedInput;
    private string? selectedAction;
    private bool placePoint;
    private bool onlyMissing;
    private ControllerCapabilities? capabilities;
    private StackPanel? inputEditor;
    private ComboBox? inputPicker;
    private readonly Dictionary<FlightInput, Button> hotspots = new();
    internal IReadOnlyDictionary<FlightInput, Button> VisualHotspots => hotspots;
    internal ComboBox? AddActionPicker { get; private set; }
    internal Button? AddMappingButton { get; private set; }

    private sealed record ActionChoice(string Key, string Label);

    private void PhysicalPage()
    {
        ContentHost.Children.Add(PageTitle(T("实体按键", "Physical controls", "物理コントロール"),
            T("点示意图上的标记，或从列表选择输入，再添加它要执行的操作。", "Choose a marker or an input from the list, then add the actions it should trigger.", "図のマーカーまたは一覧から入力を選び、実行する操作を追加します。")));
        var session = Session!;
        var device = session.Document.Device;
        var inputs = ControllerInputs.List(session, capabilities);
        selectedInput = inputs.FirstOrDefault(i => i == selectedInput) ?? inputs.FirstOrDefault();
        hotspots.Clear();
        var photo = !testing ? ControllerPhotos.Load(device.Id) : null;
        var card = Card(device.Label, out var body);
        BuildControllerPicture(body, photo, inputs);
        BuildPhotoCommands(body, photo);
        ContentHost.Children.Add(card);
        inputEditor = new StackPanel();
        var options = inputs.Select(i => new InputChoice(ControllerInputs.Short(i, device.IsPxn), i)).ToArray();
        inputPicker = new ComboBox
        {
            ItemsSource = options, DisplayMemberPath = "Label", SelectedItem = options.FirstOrDefault(c => c.Input == selectedInput),
            Margin = new Thickness(0, 0, 0, 12)
        };
        System.Windows.Automation.AutomationProperties.SetName(inputPicker, T("选择实体输入", "Choose a physical input", "物理入力を選択"));
        inputPicker.SelectionChanged += (_, _) =>
        {
            if (inputPicker.SelectedItem is InputChoice { Input: { } input })
            {
                selectedInput = input;
                DrawEditor();
            }
        };
        var editor = Card(T("所选输入的操作", "Actions for the selected input", "選択した入力の操作"), out var fields);
        fields.Children.Add(inputPicker);
        fields.Children.Add(inputEditor);
        ContentHost.Children.Add(editor);
        DrawEditor();
    }

    private void DrawEditor()
    {
        if (inputEditor == null || Session == null || selectedInput == null) return;
        var input = selectedInput;
        foreach (var (key, marker) in hotspots)
            marker.Background = Brush(key == input ? "#246C72" : "#14283F");
        inputEditor.Children.Clear();
        var matches = Session.Current.SelectMany(pair => pair.Value.Inputs.Select((i, n) => (pair.Key, Input: i, Index: n)))
            .Where(row => row.Input.Type == input.Type && row.Input.Button == input.Button).ToArray();
        if (matches.Length == 0)
            inputEditor.Children.Add(Text(T("这个输入还没有操作。", "This input has no actions yet.", "この入力には操作が割り当てられていません。"), 12, "#A2B1C6"));
        foreach (var match in matches)
            AddExistingMapping(match.Key, match.Input, match.Index);
        AddMappingControls(input);
    }

    private void AddExistingMapping(string key, FlightInput input, int index)
    {
        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 7) };
        var button = ActionButton(T("移除此键", "Remove this input", "この入力を解除"), () =>
        {
            Session!.RemoveInput(key, index);
            DrawEditor();
            RefreshChanges();
        });
        button.Margin = new Thickness(10, 0, 0, 0);
        DockPanel.SetDock(button, Dock.Right);
        row.Children.Add(button);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(Text(ActionCatalog.Name(key), 13));
        string? detail = ActionCatalog.Hold(key);
        if (input.Direction != "Standard" || input.Enable != 0 || input.Disable != 0)
            detail = ControllerInputs.Describe(input, Session!.Document.Device.IsPxn) + " · " + detail;
        if (!string.IsNullOrWhiteSpace(detail)) text.Children.Add(Text(detail, 11, "#A2B1C6"));
        row.Children.Add(text);
        inputEditor!.Children.Add(row);
    }

    private void AddMappingControls(FlightInput input)
    {
        var session = Session!;
        var choices = session.Current.Keys.Where(key => ActionCatalog.CanAssign(key, input))
            .Select(key => new ActionChoice(key, ActionCatalog.Name(key))).OrderBy(c => c.Label).ToArray();
        if (choices.Length == 0) return;
        var row = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
        inputEditor!.Children.Add(Text(T("选择要添加的游戏操作", "Choose an action to add", "追加する操作を選択"), 12, "#A2B1C6"));
        var picker = new ComboBox
        {
            ItemsSource = choices, DisplayMemberPath = "Label", SelectedValuePath = "Key",
            SelectedItem = choices.FirstOrDefault(c => c.Key == selectedAction)
        };
        System.Windows.Automation.AutomationProperties.SetName(picker, T("选择游戏操作", "Choose a game action", "ゲーム内操作を選択"));
        var reverse = new CheckBox { Content = T("反向", "Reverse", "反転"), Margin = new Thickness(8, 0, 0, 0) };
        var hint = Text("", 11, "#A2B1C6", new Thickness(0, 7, 0, 0));
        var apply = ActionButton(T("添加绑定", "Add binding", "割り当てを追加"), () =>
        {
            if (picker.SelectedItem is not ActionChoice action) return;
            string direction = reverse.IsChecked == true ? "Reverse" : "Standard";
            session.AddInput(action.Key, input with { Direction = direction });
            DrawEditor();
            RefreshChanges();
        });
        apply.Margin = new Thickness(9, 0, 0, 0);
        DockPanel.SetDock(apply, Dock.Right);
        DockPanel.SetDock(reverse, Dock.Right);
        row.Children.Add(apply);
        row.Children.Add(reverse);
        row.Children.Add(picker);
        void Update()
        {
            selectedAction = (picker.SelectedItem as ActionChoice)?.Key ?? selectedAction;
            bool canReverse = ActionCatalog.IsAxisInput(input)
                || picker.SelectedItem is ActionChoice item && ActionCatalog.IsCamera(item.Key);
            reverse.Visibility = canReverse ? Visibility.Visible : Visibility.Collapsed;
            if (!canReverse) reverse.IsChecked = false;
            apply.IsEnabled = picker.SelectedItem != null;
            hint.Text = picker.SelectedItem is ActionChoice option ? ActionCatalog.Hold(option.Key) ?? "" : "";
            hint.Visibility = hint.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        picker.SelectionChanged += (_, _) => Update();
        Update();
        inputEditor!.Children.Add(row);
        inputEditor.Children.Add(hint);
        AddActionPicker = picker;
        AddMappingButton = apply;
    }

    private void SelectInput(FlightInput input)
    {
        selectedInput = input;
        if (inputPicker?.ItemsSource is IEnumerable<InputChoice> choices)
            inputPicker.SelectedItem = choices.FirstOrDefault(c => c.Input == input);
        DrawEditor();
        inputEditor?.BringIntoView();
    }

    private void BuildControllerPicture(StackPanel parent, ControllerPhoto? photo, IReadOnlyList<FlightInput> inputs)
    {
        var device = Session!.Document.Device;
        Canvas canvas;
        IEnumerable<Hotspot> points;
        if (photo != null)
        {
            if (!ControllerPhotos.TryReadImage(photo.File, out var image))
            {
                BuildControllerPicture(parent, null, inputs);
                return;
            }
            double height = ControllerDiagram.Width * image.PixelHeight / image.PixelWidth;
            canvas = new Canvas { Width = ControllerDiagram.Width, Height = height };
            canvas.Children.Add(new Image { Source = image, Width = canvas.Width, Height = canvas.Height });
            points = photo.Points.Select(point => new Hotspot(
                ControllerInputs.Short(point.Input, device.IsPxn), point.Input, point.X * canvas.Width, point.Y * height));
            canvas.MouseLeftButtonDown += (_, e) => PlacePhotoPoint(photo, canvas, e.GetPosition(canvas));
        }
        else if (device.IsPxn)
        {
            canvas = ControllerDiagram.Pxn();
            points = ControllerDiagram.PxnHotspots();
        }
        else
        {
            var buttons = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
            foreach (var input in inputs.Where(i => i.Type == "Button" || ControllerInputs.HatTypes.Contains(i.Type)))
            {
                var marker = MappingButton(input, input.Type == "Button" ? input.Button.ToString() : ControllerInputs.Short(input, false));
                marker.Margin = new Thickness(0, 0, 5, 5);
                marker.MinWidth = 32;
                buttons.Children.Add(marker);
            }
            parent.Children.Add(new ScrollViewer { MaxHeight = 148, Content = buttons });
            parent.Children.Add(Text(T("此设备可按 DirectInput 编号编辑。导入一张正面照片，即可为任意按键放置标记。",
                "Edit this device by DirectInput number, or import a front-facing photo and place markers for individual controls.",
                "DirectInput 番号で編集できます。正面の写真を読み込むと、各入力のマーカーを配置できます。"), 12, "#A2B1C6"));
            return;
        }
        foreach (var point in points)
        {
            var marker = MappingButton(point.Input, point.Label);
            canvas.Children.Add(marker);
            marker.Measure(new Size(double.PositiveInfinity, 25));
            Canvas.SetLeft(marker, point.X - marker.DesiredSize.Width / 2);
            Canvas.SetTop(marker, point.Y - 12.5);
            hotspots[point.Input] = marker;
        }
        parent.Children.Add(new Viewbox { Child = canvas, Stretch = Stretch.Uniform, MaxHeight = 335, HorizontalAlignment = HorizontalAlignment.Center });
    }

    private Button MappingButton(FlightInput input, string label)
    {
        var marker = new Button
        {
            Content = label, Padding = new Thickness(5, 0, 5, 0), MinWidth = 25, Height = 25,
            FontSize = 11, ToolTip = ControllerInputs.Short(input, Session!.Document.Device.IsPxn), Tag = input,
            BorderBrush = Brush("#72CAD1")
        };
        marker.Click += (_, _) => SelectInput(input);
        System.Windows.Automation.AutomationProperties.SetName(marker, (string)marker.ToolTip);
        hotspots[input] = marker;
        return marker;
    }

    private void BuildPhotoCommands(StackPanel parent, ControllerPhoto? photo)
    {
        var row = new WrapPanel { Margin = new Thickness(0, 11, 0, 0) };
        void Add(string label, Action action)
        {
            var button = ActionButton(label, action);
            button.Margin = new Thickness(0, 0, 8, 4);
            row.Children.Add(button);
        }
        Add(T("导入设备图片…", "Import device photo…", "機器の写真を読み込む…"), ImportPhoto);
        if (photo != null)
        {
            Add(T("在图上放置所选输入", "Place the selected input", "選択した入力を配置"), () =>
            {
                placePoint = true;
                SetStatus(T("点击照片中的对应位置。", "Click the location on the photo.", "写真上の位置をクリックしてください。"));
            });
            Add(T("还原默认图", "Reset to default", "既定の表示に戻す"), () =>
            {
                ControllerPhotos.Reset(Session!.Document.Device.Id);
                placePoint = false;
                RefreshAll();
            });
        }
        parent.Children.Add(row);
        if (photo == null && Session!.Document.Device.IsPxn)
            parent.Children.Add(Text(T("PXN 2119 Pro 示意图 · 遮挡的按键用标记引线表示。", "PXN 2119 Pro schematic · Callouts identify controls hidden from this angle.", "PXN 2119 Pro の模式図。見えにくい入力は引き出し線で示します。"), 11, "#8DA2BE"));
    }

    private void PlacePhotoPoint(ControllerPhoto photo, Canvas canvas, Point position)
    {
        if (!placePoint || selectedInput == null) return;
        TryAction(() =>
        {
            var input = selectedInput;
            var points = photo.Points.Where(point => point.Input != input).ToList();
            points.Add(new(input.Type, input.Button, position.X / canvas.Width, position.Y / canvas.Height));
            ControllerPhotos.Save(Session!.Document.Device.Id, photo with { Points = points });
            placePoint = false;
            RefreshAll();
        });
    }

    private void ImportPhoto()
    {
        var dialog = new OpenFileDialog { Filter = T("图片", "Images", "画像") + " (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        _ = ControllerPhotos.ReadImage(dialog.FileName);
        ControllerPhotos.Save(Session!.Document.Device.Id, new(dialog.FileName, []));
        placePoint = false;
        RefreshAll();
    }
}

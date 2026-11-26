namespace AceCombat8.Core;

public sealed record ControllerCapabilities(int Buttons, int Axes, int Hats, IReadOnlyList<FlightInput>? ReportedInputs = null);

public static class ControllerInputs
{
    public static readonly string[] AxisTypes = ["X", "Y", "Z", "Rx", "Ry", "Rz", "Slider"];
    public static readonly string[] HatTypes = ["POV_U", "POV_D", "POV_L", "POV_R"];
    private static readonly string[] Arrows = ["↑", "↓", "←", "→"];

    public static IReadOnlyList<FlightInput> List(EditSession session, ControllerCapabilities? caps)
    {
        var existing = session.Current.Values.SelectMany(b => b.Inputs).Where(i => i.Type != "None").ToList();
        var inputs = new List<FlightInput>();
        int maxButton = Math.Max(existing.Where(i => i.Type == "Button").Select(i => i.Button).DefaultIfEmpty().Max(),
            session.Document.Device.IsPxn ? Math.Max(23, caps?.Buttons ?? 0) : caps?.Buttons ?? 128);
        for (int i = 1; i <= Math.Clamp(maxButton, 0, 128); i++) inputs.Add(new("Button", i));
        if (session.Document.Device.IsPxn || caps == null || caps.Hats > 0)
            inputs.AddRange(HatTypes.Select(type => new FlightInput(type)));
        if (caps?.ReportedInputs is { Count: > 0 } reported)
        {
            if (!session.Document.Device.IsPxn && reported.Any(i => i.Type == "Button"))
                inputs.RemoveAll(i => i.Type == "Button");
            inputs.AddRange(reported);
        }
        else
            inputs.AddRange(AxisTypes.Select(type => new FlightInput(type)));
        inputs.AddRange(existing.Select(i => new FlightInput(i.Type, i.Button)));
        return inputs.Distinct().ToList();
    }

    public static string Short(FlightInput input, bool pxn) => input.Type switch
    {
        "Button" when pxn && input.Button is >= 17 and <= 20 => "POV2 " + Arrows[input.Button - 17],
        "Button" => Lang.T($"{input.Button} 号键", $"Button {input.Button}", $"ボタン {input.Button}"),
        _ when Array.IndexOf(HatTypes, input.Type) is int d && d >= 0 => "POV1 " + Arrows[d],
        _ => input.Type + (input.Button != 0 ? $" {input.Button}" : "")
    };

    public static string Describe(FlightInput input, bool pxn)
    {
        var label = Short(input, pxn);
        if (pxn && input.Type == "Button")
        {
            if (input.Button <= 12) label = Lang.T("右手 · ", "Stick · ", "スティック · ") + label;
            else if (input.Button <= 20) label = Lang.T("左手 · ", "Throttle · ", "スロットル · ") + label;
        }
        var direction = input.Direction switch
        {
            "Standard" => "",
            "Reverse" => Lang.T(" · 反向", " · reversed", " · 反転"),
            _ => " · " + input.Direction
        };
        string modifiers = input.Enable == 0 && input.Disable == 0 ? "" : $" (+{input.Enable}/−{input.Disable})";
        return label + direction + modifiers;
    }
}

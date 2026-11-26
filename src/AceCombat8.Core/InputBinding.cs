using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Encodings.Web;

namespace AceCombat8.Core;

public sealed record FlightInput(string Type, int Button = 0, string Direction = "Standard", int Enable = 0, int Disable = 0)
{
    public static FlightInput None => new("None");
    public bool IsReverse => Direction == "Reverse";
    public string Token => $"{Type}:{Button}:{Direction}:{Enable}:{Disable}";
}

public sealed class InputBinding
{
    private static readonly JsonSerializerOptions JsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private readonly JsonObject root;
    public IReadOnlyList<FlightInput> Inputs { get; }
    public string Fingerprint => root.ToJsonString();

    private InputBinding(JsonObject root)
    {
        this.root = root;
        if (root["inputSettings"] is not JsonArray array)
            throw new FormatException("Missing inputSettings array.");
        Inputs = array.Select(ReadInput).ToList();
    }

    public static InputBinding Parse(string encoded)
    {
        try
        {
            var inner = JsonSerializer.Deserialize<string>(encoded);
            return new InputBinding(JsonNode.Parse(inner ?? "") as JsonObject
                ?? throw new FormatException("The binding is not a JSON object."));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            throw new FormatException(Lang.T("无法读取绑定，请选择有效的 FlightStick.ini。", "Could not read the bindings. Choose a valid FlightStick.ini.", "割り当てを読み込めません。正しい FlightStick.ini を選択してください。"), ex);
        }
    }

    private static FlightInput ReadInput(JsonNode? node)
    {
        if (node is not JsonObject value || value["inputType"]?.GetValue<string>() is not { } type)
            throw new FormatException("The input entry is missing inputType.");
        return new(type, value["buttonIndex"]?.GetValue<int>() ?? 0,
            value["axisInputOption"]?.GetValue<string>() ?? "Standard",
            value["enableButtonIndex"]?.GetValue<int>() ?? 0,
            value["disableButtonIndex"]?.GetValue<int>() ?? 0);
    }

    public InputBinding WithInputs(IEnumerable<FlightInput> inputs)
    {
        var clone = (JsonObject)root.DeepClone();
        var old = (JsonArray)clone["inputSettings"]!;
        var replacement = new JsonArray();
        int index = 0;
        foreach (var input in inputs)
        {
            var item = index < old.Count && old[index] is JsonObject obj
                ? (JsonObject)obj.DeepClone() : new JsonObject();
            item["inputType"] = input.Type;
            item["buttonIndex"] = input.Button;
            item["axisInputOption"] = input.Direction;
            item["enableButtonIndex"] = input.Enable;
            item["disableButtonIndex"] = input.Disable;
            replacement.Add(item);
            index++;
        }
        clone["inputSettings"] = replacement;
        return new InputBinding(clone);
    }

    public string Encode() => JsonSerializer.Serialize(root.ToJsonString(JsonOptions), JsonOptions);
    public InputBinding AddInput(FlightInput input)
    {
        if (Inputs.Contains(input)) return this;
        var clone = (JsonObject)root.DeepClone();
        var settings = (JsonArray)clone["inputSettings"]!;
        for (int i = Inputs.Count - 1; i >= 0; i--)
            if (Inputs[i].Type == "None") settings.RemoveAt(i);
        settings.Add(new JsonObject
        {
            ["inputType"] = input.Type, ["buttonIndex"] = input.Button, ["axisInputOption"] = input.Direction,
            ["enableButtonIndex"] = input.Enable, ["disableButtonIndex"] = input.Disable
        });
        return new InputBinding(clone);
    }

    public InputBinding RemoveInput(int index)
    {
        var clone = (JsonObject)root.DeepClone();
        var settings = (JsonArray)clone["inputSettings"]!;
        settings.RemoveAt(index);
        return settings.Count == 0 ? new InputBinding(clone).WithInputs([FlightInput.None]) : new InputBinding(clone);
    }

    public bool SameAs(InputBinding other) => JsonNode.DeepEquals(root, other.root);
    public bool HasInputs(params FlightInput[] inputs) => Inputs.SequenceEqual(inputs);
    public string Describe(DeviceSection? device = null)
    {
        var active = Inputs.Where(i => i.Type != "None").ToArray();
        return active.Length == 0 ? Lang.T("未绑定", "Unbound", "未割り当て")
            : string.Join(" / ", active.Select(i => ControllerInputs.Describe(i, device?.IsPxn ?? true)));
    }
}

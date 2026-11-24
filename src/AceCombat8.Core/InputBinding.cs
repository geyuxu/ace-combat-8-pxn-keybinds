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
            throw new FormatException("缺少 inputSettings 数组。");
        Inputs = array.Select(ReadInput).ToList();
    }

    public static InputBinding Parse(string encoded)
    {
        try
        {
            var inner = JsonSerializer.Deserialize<string>(encoded);
            return new InputBinding(JsonNode.Parse(inner ?? "") as JsonObject
                ?? throw new FormatException("绑定内容不是 JSON 对象。"));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            throw new FormatException("无法读取游戏按键格式，请选择有效的 FlightStick.ini。", ex);
        }
    }

    private static FlightInput ReadInput(JsonNode? node)
    {
        if (node is not JsonObject value || value["inputType"]?.GetValue<string>() is not { } type)
            throw new FormatException("输入条目缺少 inputType。");
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
    public bool SameAs(InputBinding other) => JsonNode.DeepEquals(root, other.root);
    public bool HasInputs(params FlightInput[] inputs) => Inputs.SequenceEqual(inputs);
    public string Describe() => Inputs.Count == 0 ? "未绑定" : string.Join(" ＋ ", Inputs.Select(PxnDevice.Describe));
}

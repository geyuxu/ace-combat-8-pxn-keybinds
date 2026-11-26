using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AceCombat8.Core;

namespace AceCombat8.Desktop;

internal sealed record Hotspot(string Label, FlightInput Input, double X, double Y);

internal static class ControllerDiagram
{
    public const double Width = 660;
    public const double Height = 270;

    public static Canvas Pxn()
    {
        var canvas = new Canvas { Width = Width, Height = Height, Background = Solid("#0D1929"), ClipToBounds = true };
        for (int x = 0; x < Width; x += 30) Line(canvas, x, 0, x, Height, "#192A3E");
        for (int y = 0; y < Height; y += 30) Line(canvas, 0, y, Width, y, "#192A3E");
        // Original schematic with grouped callouts for controls hidden from this angle.
        Shape(canvas, "M 49,202 L 94,153 L 235,155 L 289,200 L 282,225 L 66,232 Z", "#536982", "#18283D");
        Shape(canvas, "M 49,202 L 96,157 L 235,158 L 289,200 L 245,214 L 70,216 Z", "#526781", "#273C53");
        Shape(canvas, "M 104,190 L 108,102 Q 109,78 139,72 L 217,73 Q 245,76 253,96 L 258,144 Q 259,163 234,172 L 209,191 Z", "#647D96", "#26384B");
        Shape(canvas, "M 108,101 Q 121,87 146,88 L 220,88 Q 246,91 253,103 L 255,136 Q 247,149 225,152 L 119,154 Z", "#526B83", "#273A4E");
        Shape(canvas, "M 73,197 L 86,178 L 111,173 L 100,202 Z", "#24394E", "#15283B");
        Shape(canvas, "M 330,203 L 376,165 L 546,164 L 600,202 L 594,228 L 347,236 Z", "#607793", "#18283D");
        Shape(canvas, "M 330,203 L 378,165 L 546,165 L 600,202 L 565,216 L 346,221 Z", "#5C7089", "#293F55");
        Ellipse(canvas, 418, 159, 105, 37, "#435B74");
        Ellipse(canvas, 433, 153, 78, 32, "#75899E");
        Shape(canvas, "M 449,164 Q 437,147 441,129 L 447,97 Q 433,91 433,77 L 443,41 Q 447,31 460,28 L 491,27 Q 506,28 514,43 L 528,74 Q 533,87 515,101 L 497,107 L 492,153 Q 490,167 476,171 Z", "#7E91A6", "#283B51");
        Shape(canvas, "M 443,41 L 461,33 L 491,33 Q 501,34 507,45 L 521,73 L 510,91 L 452,90 L 438,76 Z", "#6D8197", "#233449");
        Shape(canvas, "M 447,99 L 464,107 L 463,151 L 451,154 L 441,130 Z", "#485E76", "#24364C");
        Shape(canvas, "M 500,103 L 520,96 L 522,113 L 510,127 L 499,121 Z", "#5B748C", "#27374C");
        Line(canvas, 267, 47, 303, 47, "#7292A7");
        Line(canvas, 225, 100, 267, 47, "#7292A7");
        Line(canvas, 516, 112, 542, 115, "#7292A7");
        Line(canvas, 452, 123, 420, 122, "#7292A7");
        Label(canvas, Lang.T("左手油门", "THROTTLE", "左手スロットル"), 56, 239);
        Label(canvas, Lang.T("右手摇杆", "FLIGHT STICK", "右手フライトスティック"), 335, 239);
        Label(canvas, "POV2", 265, 14);
        Label(canvas, "POV1", 559, 13);
        return canvas;
    }

    public static IEnumerable<Hotspot> PxnHotspots()
    {
        yield return new("1", new("Button", 1), 555, 115);
        yield return new("2", new("Button", 2), 406, 122);
        for (int i = 0; i < 4; i++)
        {
            yield return new((3 + i).ToString(), new("Button", 3 + i), 453 + 27 * (i % 2), 48 + 28 * (i / 2));
            yield return new((13 + i).ToString(), new("Button", 13 + i), 134 + 27 * i, 121);
        }
        for (int i = 0; i < 6; i++)
            yield return new((7 + i).ToString(), new("Button", 7 + i), 364 + 36 * i, 199);
        var labels = new[] { "↑", "↓", "←", "→" };
        for (int i = 0; i < 4; i++)
        {
            var offsets = new[] { (0, -19), (0, 19), (-21, 0), (21, 0) };
            yield return new(labels[i], new(ControllerInputs.HatTypes[i]), 582 + offsets[i].Item1, 62 + offsets[i].Item2);
            yield return new(labels[i], new("Button", 17 + i), 284 + offsets[i].Item1, 75 + offsets[i].Item2);
        }
    }

    private static SolidColorBrush Solid(string color) => new((Color)ColorConverter.ConvertFromString(color));
    private static void Shape(Canvas canvas, string data, string light, string dark)
    {
        var fill = new LinearGradientBrush((Color)ColorConverter.ConvertFromString(light),
            (Color)ColorConverter.ConvertFromString(dark), 70);
        canvas.Children.Add(new Path { Data = Geometry.Parse(data), Fill = fill, Stroke = Solid("#7D96B0"), StrokeThickness = 0.9 });
    }
    private static void Line(Canvas canvas, double x1, double y1, double x2, double y2, string color) =>
        canvas.Children.Add(new Line { X1 = x1, X2 = x2, Y1 = y1, Y2 = y2, Stroke = Solid(color), StrokeThickness = 0.8 });
    private static void Ellipse(Canvas canvas, double x, double y, double width, double height, string fill)
    {
        var ellipse = new Ellipse { Width = width, Height = height, Fill = Solid(fill), Stroke = Solid("#7991A8") };
        canvas.Children.Add(ellipse); Canvas.SetLeft(ellipse, x); Canvas.SetTop(ellipse, y);
    }
    private static void Label(Canvas canvas, string label, double x, double y)
    {
        var text = new TextBlock { Text = label, Foreground = Solid("#A3B8D1"), FontSize = 11, FontWeight = FontWeights.SemiBold };
        canvas.Children.Add(text); Canvas.SetLeft(text, x); Canvas.SetTop(text, y);
    }
}

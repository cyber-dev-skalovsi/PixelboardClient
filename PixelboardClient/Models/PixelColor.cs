using System.Text.Json;

namespace PixelboardClient.Models
{
    public class PixelColor
    {
        public int Red { get; set; }
        public int Green { get; set; }
        public int Blue { get; set; }

        public PixelColor() { }

        public PixelColor(int red, int green, int blue)
        {
            Red = red;
            Green = green;
            Blue = blue;
        }

        // Parse aus JSON String Format {"Red":100,"Green":100,"Blue":100}
        public static PixelColor? FromJsonString(string? jsonString)
        {
            if (string.IsNullOrEmpty(jsonString))
                return null;

            try
            {
                // Deserialisiere JSON direkt
                var color = JsonSerializer.Deserialize<PixelColor>(jsonString);
                return color;
            }
            catch
            {
                return null;
            }
        }

        // Fallback: Parse aus rgb() Format falls nötig
        public static PixelColor? FromRgbString(string? rgbString)
        {
            if (string.IsNullOrEmpty(rgbString))
                return null;

            try
            {
                // Versuche zuerst JSON
                if (rgbString.StartsWith("{"))
                {
                    return FromJsonString(rgbString);
                }

                // Entfernt "rgb(" und ")"
                var values = rgbString.Replace("rgb(", "").Replace(")", "").Split(',');

                if (values.Length == 3)
                {
                    return new PixelColor(
                        int.Parse(values[0].Trim()),
                        int.Parse(values[1].Trim()),
                        int.Parse(values[2].Trim())
                    );
                }
            }
            catch
            {
                return null;
            }

            return null;
        }
    }
}

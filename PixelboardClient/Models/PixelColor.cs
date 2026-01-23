namespace PixelboardClient.Models
{
    public class PixelColor
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Red { get; set; }
        public int Green { get; set; }
        public int Blue { get; set; }

        public PixelColor()
        {
            Red = 0;
            Green = 0;
            Blue = 0;
        }

        /// <summary>
        /// Parse RGB string in verschiedenen Formaten:
        /// - "rgb(255, 128, 0)"
        /// - "#FF8000"
        /// - JSON Format wird in Service behandelt
        /// </summary>
        public static PixelColor Parse(string rgbString)
        {
            var color = new PixelColor();

            if (string.IsNullOrEmpty(rgbString))
                return color;

            try
            {
                // rgb(r, g, b) Format
                if (rgbString.StartsWith("rgb"))
                {
                    var values = rgbString
                        .Replace("rgb(", "")
                        .Replace("rgba(", "")
                        .Replace(")", "")
                        .Split(',');

                    if (values.Length >= 3)
                    {
                        color.Red = int.Parse(values[0].Trim());
                        color.Green = int.Parse(values[1].Trim());
                        color.Blue = int.Parse(values[2].Trim());
                    }
                }
                // Hex Format #RRGGBB
                else if (rgbString.StartsWith("#") && rgbString.Length == 7)
                {
                    color.Red = Convert.ToInt32(rgbString.Substring(1, 2), 16);
                    color.Green = Convert.ToInt32(rgbString.Substring(3, 2), 16);
                    color.Blue = Convert.ToInt32(rgbString.Substring(5, 2), 16);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Parsen von '{rgbString}': {ex.Message}");
            }

            return color;
        }

        public string ToRgbString()
        {
            return $"rgb({Red}, {Green}, {Blue})";
        }

        public string ToHexString()
        {
            return $"#{Red:X2}{Green:X2}{Blue:X2}";
        }

        public bool IsBlack()
        {
            return Red == 0 && Green == 0 && Blue == 0;
        }

        public bool IsErrorColor()
        {
            return Red == 255 && Green == 0 && Blue == 255;
        }
    }
}

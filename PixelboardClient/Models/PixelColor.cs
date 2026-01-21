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

        // Parse RGB string wie "rgb(255, 128, 0)" oder "#FF8000"
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
                        .Replace(")", "")
                        .Split(',');

                    if (values.Length == 3)
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
    }
}

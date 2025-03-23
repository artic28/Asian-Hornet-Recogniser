using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace war3.Models
{
    public class BoundingBoxDimensions
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Height { get; set; }
        public float Width { get; set; }
    }

    public class BoundingBox
    {
        public BoundingBoxDimensions Dimensions { get; set; }

        public string Label { get; set; }

        public float Confidence { get; set; }

        public RectangleF Rect
        {
            get { return new RectangleF(Dimensions.X, Dimensions.Y, Dimensions.Width, Dimensions.Height); }
        }

        public Color BoxColour { get; set; }

        public string Description => $"{Label} ({(Confidence * 100).ToString("0")}%)";

        private static readonly Color[] classColours = new Color[]
        {
            Color.Khaki, Color.Fuchsia, Color.Silver, Color.RoyalBlue,
            Color.Green, Color.DarkOrange, Color.Purple, Color.Gold,
            Color.Red, Color.Aquamarine, Color.Lime, Color.AliceBlue,
            Color.Sienna, Color.Orchid, Color.Tan, Color.LightPink,
            Color.Yellow, Color.HotPink, Color.OliveDrab, Color.SandyBrown,
            Color.DarkTurquoise
        };

        public static Color GetColor(int index) => index < classColours.Length ? classColours[index] : classColours[index % classColours.Length];
    }
}

using Grasshopper.GUI.Gradient;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace AssemblerLib.Utils
{
    public static class Constants
    {
        internal const double ObstructionToleranceMultiplier = 5.0;
        internal const double ObstructionRayLength = 1.5;
        internal const double SafeScaleMultiplier = 1.2;
        /// <summary>
        /// Tolerance from Rhino file
        /// </summary>
        internal static readonly double RhinoAbsoluteTolerance = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
        /// <summary>
        /// Tolerance squared - for fast neighbour search
        /// </summary>
        internal static readonly double RhinoAbsoluteToleranceSquared = Math.Pow(Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, 2);
        /// <summary>
        /// Offset for Obstruction check ray
        /// </summary>
        internal static readonly double ObstructionRayOffset = ObstructionToleranceMultiplier * RhinoAbsoluteTolerance;

        public static readonly GH_Gradient HistoryGradient = new GH_Gradient(new double[] { 0.0, 0.5, 0.9, 1.0 },
            new Color[] { Color.Black, Color.FromArgb(56, 136, 150), Color.FromArgb(186, 224, 224), Color.White });
        public static readonly GH_Gradient ZHeightGradient = new GH_Gradient(new double[] { 0.0, 0.33, 0.66, 1.0 },
            new Color[] { Color.Black, Color.FromArgb(150, 66, 114), Color.FromArgb(224, 186, 187), Color.White });
        public static readonly GH_Gradient DensityGradient = new GH_Gradient(new double[] { 0.0, 0.5, 1.0 },
            new Color[] { Color.White, Color.SlateGray, Color.DarkSlateGray });
        public static readonly GH_Gradient ReceiverValuesGradient = new GH_Gradient(new double[] { 0.0, 0.5, 1.0 },
            new Color[] { Color.White, Color.Red, Color.DarkRed });
        public static readonly GH_Gradient DiscoGradient = new GH_Gradient(new double[] { 0.0, 1.0 },
            new Color[] { Color.FromArgb(255, 0, 255), Color.FromArgb(0, 255, 255) });

        /// <summary>
        /// <see cref="AssemblyObject"/> Type palette, with up to 24 Colors
        /// </summary>
        /// <remarks>Which are already WAY TOO MANY!!!</remarks>
        public static readonly Color[] AOTypePalette = new Color[] {
        Color.FromArgb(192,57,43), Color.FromArgb(100,100,100), Color.FromArgb(52,152,219), Color.FromArgb(253,188,75),
        Color.FromArgb(155,89,182), Color.FromArgb(46,204,113), Color.FromArgb(49,54,59), Color.FromArgb(231,76,60),
        Color.FromArgb(189,195,199), Color.FromArgb(201,206,59), Color.FromArgb(142,68,173), Color.FromArgb(52,73,94),
        Color.FromArgb(29,153,19), Color.FromArgb(237,21,21), Color.FromArgb(127,140,141), Color.FromArgb(61,174,233),
        Color.FromArgb(243,156,31), Color.FromArgb(41,128,190), Color.FromArgb(35,38,41), Color.FromArgb(252,252,252),
        Color.FromArgb(218,68,83), Color.FromArgb(22,160,133), Color.FromArgb(149,165,166), Color.FromArgb(44,62,80)};

        /// <summary>
        /// Palette for receiver, sender (in this order)
        /// </summary>
        public static readonly Color[] SRPalette = new Color[] { Color.SlateGray, Color.FromArgb(229, 229, 220) };

        /// <summary>
        /// Known Colors palette as a List
        /// </summary>
        /// <remarks>see: https://www.codeproject.com/Questions/826358/How-to-choose-a-random-color-from-System-Drawing-C</remarks>
        public static readonly List<KnownColor> KnownColorList = Enum.GetValues(typeof(KnownColor)).Cast<KnownColor>().ToList();
    }
}

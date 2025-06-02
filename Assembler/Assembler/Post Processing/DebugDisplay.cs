using Assembler.Properties;
using AssemblerLib;
using AssemblerLib.Utils;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace Assembler
{
    public class DebugDisplay : GH_Component
    {
        const string fontFace = "Lucida Console";
        BoundingBox _boundingBox;
        List<DisplayLine> _curves;
        List<DisplayLine> _dashCurves;
        List<DisplayLine> _dirVectors;
        List<DisplayText> _texts;
        PointCloud _cloud;
        double textSize;

        private struct DisplayText
        {
            public string Text;
            public Color Color;
            public Plane Plane;
            public double Height;
            public string FontFace;
            public TextHorizontalAlignment HorizontalAlignment;
            public TextVerticalAlignment VerticalAlignment;

            public Text3d ToText3d()
            {
                return new Text3d(Text)
                {
                    FontFace = FontFace,
                    TextPlane = Plane,
                    Height = Height,
                    HorizontalAlignment = HorizontalAlignment,
                    VerticalAlignment = VerticalAlignment
                };
            }
        }

        private struct DisplayLine
        {
            public Line Line;
            public Color Color;
            public int Thickness;
        }

        private int _cloudPointsSize;
        // Colors for Handle occupancy status( -1 - occluded, 0 - free, 1 - connected, 2 - secondary connection)
        // use as index the Handle occupancy status +1
        private readonly Color[] OccupancyColors = new Color[] { Color.Red, Color.White, Color.Lime, Color.Blue };

        public override BoundingBox ClippingBox => _boundingBox;

        /// <summary>
        /// Initializes a new instance of the DebugDisplay class.
        /// </summary>
        public DebugDisplay()
          : base("DebugDisplay", "AOaDebugDisp",
              "Display Assemblage - Debug mode\n" +
                "Displays useful in-place data for debugging, such as:\n" +
                "- AO index, name, type, weight, placement rule, receiver index, receiver value and sender value\n" +
                "- Handle type, rotation, weight, occupancy status\n" +
                "See plugin documentation for detailed explanation",
              "Assembler", "Post Processing")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Assemblage", "AOa", "The Assemblage", GH_ParamAccess.item);
            pManager.AddIntegerParameter("AO indexes", "i", "indexes of AssemblyObjects to Display, leave empty to display the entire Assemblage", GH_ParamAccess.list);
            pManager.AddNumberParameter("Text Size", "T", "Text Size Multiplier", GH_ParamAccess.item, 1.0);
            pManager[1].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Assemblage AOa = null;
            if (!DA.GetData("Assemblage", ref AOa) || AOa == null) return;

            List<int> AOindexes = new List<int>();
            DA.GetDataList(1, AOindexes);

            DA.GetData("Text Size", ref textSize);
            if (textSize <= 0) return;

            if (AOindexes.Count == 0)
                SetAllAODisplayData(AOa);
            else
                foreach (int i in AOindexes)
                    SetAODisplayData(AOa.AssemblyObjects[new GH_Path(i), 0], AOa.AssemblageRules[new GH_Path(i), 0], AOa.ReceiverAIndexes[new GH_Path(i), 0], false);
        }

        private void SetAllAODisplayData(Assemblage AOa)
        {
            for (int i = 0; i < AOa.AssemblyObjects.BranchCount; i++)
                SetAODisplayData(AOa.AssemblyObjects.Branches[i][0], AOa.AssemblageRules.Branches[i][0], AOa.ReceiverAIndexes.Branches[i][0], false);
        }

        private Plane AdjustPlane(Plane inputPlane)
        {
            Plane p = inputPlane;
            if (p.ZAxis * Vector3d.ZAxis < -0.5)
                p.Rotate(Math.PI, p.YAxis);
            else
                if (p.YAxis * Vector3d.ZAxis < -0.5 || p.XAxis * Vector3d.ZAxis < -0.5)
                p.Rotate(Math.PI, p.ZAxis);

            return p;
        }

        private void SetAODisplayData(AssemblyObject ao, string rule, int receiverInd, bool horizontalText = false)
        {
            DisplayText dText;
            DisplayLine dCurve;
            Plane referencePlane, basePlane;

            //
            // . . . . . . . AO stuff
            //

            _boundingBox.Union(ao.CollisionMesh.GetBoundingBox(false));
            referencePlane = horizontalText ? AdjustPlane(ao.ReferencePlane) : ao.ReferencePlane;
            basePlane = referencePlane;

            // AO direction vector
            basePlane.Transform(Transform.Translation(referencePlane.ZAxis * textSize * -0.25));
            dCurve = new DisplayLine()
            {
                Line = new Line(basePlane.Origin - ao.Direction * textSize * 1, basePlane.Origin + ao.Direction * textSize * 1),
                Color = Color.MediumPurple,
                Thickness = 3
            };
            _dirVectors.Add(dCurve);

            referencePlane.Transform(Transform.Translation(referencePlane.ZAxis * textSize * 0.25));
            basePlane = referencePlane;

            // AO index
            dText = new DisplayText()
            {
                Text = $"{ao.AInd}",
                Color = Color.Gold,
                Plane = basePlane,
                Height = textSize * 0.7,
                FontFace = fontFace,
                HorizontalAlignment = TextHorizontalAlignment.Center,
                VerticalAlignment = TextVerticalAlignment.Middle
            };
            _texts.Add(dText);


            // AO name, type, weight
            basePlane.Transform(Transform.Translation(referencePlane.YAxis * textSize * 0.75));
            dText = new DisplayText()
            {
                Text = $"{ao.Name} t{ao.Type} w{ao.Weight:0.00}",
                Color = Color.LightGray,
                Plane = basePlane,
                Height = textSize * 0.3,
                FontFace = fontFace,
                HorizontalAlignment = TextHorizontalAlignment.Center,
                VerticalAlignment = TextVerticalAlignment.Bottom
            };
            _texts.Add(dText);


            // AO rule
            basePlane.Transform(Transform.Translation(referencePlane.YAxis * textSize * 0.7));
            dText = new DisplayText()
            {
                Text = rule,
                Color = Color.DimGray,
                Plane = basePlane,
                Height = textSize * 0.2,
                FontFace = fontFace,
                HorizontalAlignment = TextHorizontalAlignment.Center,
                VerticalAlignment = TextVerticalAlignment.Bottom
            };
            _texts.Add(dText);

            basePlane = referencePlane;

            // AO receiver value
            basePlane.Transform(Transform.Translation(referencePlane.YAxis * textSize * -0.8 + referencePlane.XAxis * textSize * -0.2));
            dText = new DisplayText()
            {
                Text = $"{ao.ReceiverValue:0.000}",
                Color = Constants.SRPalette[0],// Color.SlateGray,
                Plane = basePlane,
                Height = textSize * 0.25,
                FontFace = fontFace,
                HorizontalAlignment = TextHorizontalAlignment.Right,
                VerticalAlignment = TextVerticalAlignment.Top
            };
            _texts.Add(dText);

            // AO sender value
            basePlane.Transform(Transform.Translation(referencePlane.XAxis * textSize * 0.4));
            dText = new DisplayText()
            {
                Text = $"{ao.SenderValue:0.000}",
                Color = Constants.SRPalette[1],
                Plane = basePlane,
                Height = textSize * 0.25,
                FontFace = fontFace,
                HorizontalAlignment = TextHorizontalAlignment.Left,
                VerticalAlignment = TextVerticalAlignment.Top
            };
            _texts.Add(dText);

            // AO receiver index
            basePlane.Transform(Transform.Translation(referencePlane.XAxis * textSize * -0.2 + referencePlane.YAxis * textSize * -0.6));
            dText = new DisplayText()
            {
                Text = $"{receiverInd}",
                Color = Constants.SRPalette[0],
                Plane = basePlane,
                Height = textSize * 0.3,
                FontFace = fontFace,
                HorizontalAlignment = TextHorizontalAlignment.Center,
                VerticalAlignment = TextVerticalAlignment.Top
            };
            _texts.Add(dText);

            //
            // . . . . . . . Handle stuff
            //
            int hInd = 0;
            foreach (Handle h in ao.Handles)
            {
                // Line
                dCurve = new DisplayLine()
                {
                    Line = new Line(h.SenderPlane.Origin, h.SenderPlane.Origin + h.SenderPlane.ZAxis * textSize * -0.25),
                    Color = OccupancyColors[h.Occupancy + 1],
                    Thickness = 3
                };
                _curves.Add(dCurve);

                // Handles Occupancy Point Cloud
                _cloud.Add(h.SenderPlane.Origin, OccupancyColors[h.Occupancy + 1]);

                basePlane = h.SenderPlane;
                basePlane.Transform(Transform.Translation(h.SenderPlane.ZAxis * textSize * -0.3));

                // neighbour object/handle text
                dText = new DisplayText()
                {
                    Text = $"{h.NeighbourObject} . {h.NeighbourHandle}",
                    Color = OccupancyColors[h.Occupancy + 1],
                    Plane = basePlane,
                    Height = textSize * 0.3,
                    FontFace = fontFace,
                    HorizontalAlignment = TextHorizontalAlignment.Center,
                    VerticalAlignment = TextVerticalAlignment.Bottom
                };
                _texts.Add(dText);

                basePlane.Transform(Transform.Translation(h.SenderPlane.ZAxis * textSize * -0.25));

                // Handle index
                dText = new DisplayText()
                {
                    Text = $"{hInd}",
                    Color = Color.FromArgb(80, 80, 80),
                    Plane = basePlane,
                    Height = textSize * 0.45,
                    FontFace = fontFace,
                    HorizontalAlignment = TextHorizontalAlignment.Center,
                    VerticalAlignment = TextVerticalAlignment.Middle
                };
                _texts.Add(dText);

                basePlane.Transform(Transform.Translation(h.SenderPlane.YAxis * textSize * 0.5));

                // Handle type
                dText = new DisplayText()
                {
                    Text = $"{h.Type}",
                    Color = Constants.HTypePalette[h.Type],
                    Plane = basePlane,
                    Height = textSize * 0.25,
                    FontFace = fontFace,
                    HorizontalAlignment = TextHorizontalAlignment.Center,
                    VerticalAlignment = TextVerticalAlignment.Bottom
                };
                _texts.Add(dText);

                // Handle Rotation | Weight | IdleWeight
                basePlane.Transform(Transform.Translation(h.SenderPlane.YAxis * textSize * -1));
                string rotation = h.RotationIndex == -1 ? $"" : $"r{h.Rotations[h.RotationIndex]}|";

                dText = new DisplayText()
                {
                    Text = $"{rotation}w{h.Weight}/{h.IdleWeight}",
                    Color = Color.Gray,
                    Plane = basePlane,
                    Height = textSize * 0.2,
                    FontFace = fontFace,
                    HorizontalAlignment = TextHorizontalAlignment.Center,
                    VerticalAlignment = TextVerticalAlignment.Top
                };
                _texts.Add(dText);

                hInd++;
            }

        }

        protected override void BeforeSolveInstance()
        {
            _boundingBox = BoundingBox.Empty;
            _curves = new List<DisplayLine>();
            _dashCurves = new List<DisplayLine>();
            _dirVectors = new List<DisplayLine>();
            _texts = new List<DisplayText>();

            _cloud = new PointCloud();
            _cloudPointsSize = 4;
        }

        protected override void AfterSolveInstance()
        {
            base.AfterSolveInstance();
        }

        public override void ClearData()
        {
            base.ClearData();
        }

        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            for (int i = 0; i < _curves.Count; i++)
                args.Display.DrawLine(_curves[i].Line, _curves[i].Color, _curves[i].Thickness);
            for (int i = 0; i < _dashCurves.Count; i++)
                args.Display.DrawDottedLine(_dashCurves[i].Line, _dashCurves[i].Color);
            for (int i = 0; i < _dirVectors.Count; i++)
                args.Display.DrawArrow(_dirVectors[i].Line, _dirVectors[i].Color, 0, _dirVectors[i].Thickness * textSize * 0.1);

            Text3d drawtext;
            for (int i = 0; i < _texts.Count; i++)
            {
                // draw neighbour AO index
                drawtext = _texts[i].ToText3d();
                args.Display.Draw3dText(drawtext, _texts[i].Color);
                drawtext.Dispose();
            }

            args.Display.DrawPointCloud(_cloud, _cloudPointsSize);

        }

        /// <summary>
        /// Exposure override for position in the Subcategory (options primary to septenary)
        /// https://apidocs.co/apps/grasshopper/6.8.18210/T_Grasshopper_Kernel_GH_Exposure.htm
        /// </summary>
        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.secondary; }
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return Resources.Debug_Display;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("0BEEF27A-9C86-4E54-99EC-EA231CFDDF7E"); }
        }
    }
}
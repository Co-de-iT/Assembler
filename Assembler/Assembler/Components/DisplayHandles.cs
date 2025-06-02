using Assembler.Properties;
using AssemblerLib;
using AssemblerLib.Utils;
using GH_IO.Serialization;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Assembler
{
    public class DisplayHandles : GH_Component
    {
        private bool absoluteTextSize;
        private DataTree<Handle> handlesTree;
        double zOffset, planeOffset, axisLineLength;
        double modelSize;
        private readonly double textProportion = 50;
        private BoundingBox _clip;
        private readonly List<Line> _curve = new List<Line>();
        private readonly List<Line> _dotCurve = new List<Line>();
        private readonly List<Color> _color = new List<Color>();
        private readonly List<int> _width = new List<int>();
        private readonly DataTree<double> _rotations = new DataTree<double>();
        private readonly DataTree<Plane> _textLocations = new DataTree<Plane>();
        private readonly Color xAxis = Color.Red;
        private readonly Color yAxis = Color.Green;
        private readonly Color xGhost = Color.Firebrick;
        private readonly Color yGhost = Color.LimeGreen;
        private readonly Color handleTextColor = Color.Black;
        private Color[] handleTypeColors;
        private readonly Color rotationColor = Color.FromArgb(52, 59, 59);
        private GH_Path currentPath;

        private readonly Hatch triangle;

        // TODO: add a message when absolute text size is used?

        /// <summary>
        /// Initializes a new instance of the DisplayHandle class.
        /// </summary>
        public DisplayHandles()
          : base("DisplayHandles", "HandDisp",
              "Displays Handle Type, weight, sender and receiver planes with rotations for all Handles in the input list",
              "Assembler", "Components")
        {
            triangle = MakeHatchTriangle();
            absoluteTextSize = GetValue("absHTextSize", false);
            ExpireSolution(true);
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Handles", "H", "Handles to display", GH_ParamAccess.list);
            pManager.AddNumberParameter("Display Size", "s", "Handles Display Size", GH_ParamAccess.item);
            pManager[1].Optional = true; // display size is optional
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
            // Data structure
            // {DA.Iterations, i}[j]
            // i - Handle index
            // j - rotations index

            // get data
            handlesTree = new DataTree<Handle>();
            List<Handle> handlesList = new List<Handle>();

            if (!DA.GetDataList(0, handlesList)) return;
            if (handlesList == null || handlesList.Count == 0) return;

            // purge nulls from Handles list
            handlesList = HandleUtils.PurgeNullHandlesFromList(handlesList);

            // This is to display multiple Handles list input as a tree
            currentPath = new GH_Path(DA.Iteration);

            // populate handlesTree + rotations Data Tree
            for (int i = 0; i < handlesList.Count; i++)
            {
                handlesTree.Add(handlesList[i], currentPath.AppendElement(i));
                _rotations.AddRange(handlesList[i].Rotations, currentPath.AppendElement(i));
            }

            double textSize = 1;

            if (!DA.GetData(1, ref textSize) || textSize == 0) textSize = absoluteTextSize ? 1 : 0.1;

            if (absoluteTextSize)
            {
                modelSize = textSize * textProportion;
                zOffset = modelSize * 0.02;
            }
            else
            {
                modelSize = textSize;
                zOffset = textSize * 2;
                planeOffset = textSize * 3;
                axisLineLength = zOffset * 2;
            }

            // set display data
            SetDisplayData();
        }

        void SetDisplayData()
        {
            Transform move;
            Handle handle;
            GH_Path handlePath;
            handleTypeColors = new Color[handlesTree.BranchCount];

            for (int i = 0; i < handlesTree.BranchCount; i++)
            {
                handlePath = currentPath.AppendElement(i);
                handle = handlesTree.Branch(handlePath)[0];
                
                Line rZ = new Line(handle.SenderPlane.Origin, handle.SenderPlane.Origin + (handle.SenderPlane.ZAxis * zOffset * (_rotations.Branches[i].Count + 1)));
                _dotCurve.Add(rZ);
                _clip.Union(rZ.BoundingBox);

                Line rX = new Line(handle.SenderPlane.Origin, handle.SenderPlane.Origin + (handle.SenderPlane.XAxis * axisLineLength));
                Line rY = new Line(handle.SenderPlane.Origin, handle.SenderPlane.Origin + (handle.SenderPlane.YAxis * axisLineLength));
                _curve.Add(rX);
                _color.Add(xAxis);
                _curve.Add(rY);
                _color.Add(yAxis);
                _clip.Union(rX.BoundingBox);
                _clip.Union(rY.BoundingBox);

                Plane textLoc = handle.SenderPlane;
                // prevent Z conflict with eventual shaded geometries in the model
                textLoc.Origin = handle.SenderPlane.Origin + handle.SenderPlane.ZAxis * RhinoDoc.ActiveDoc.ModelAbsoluteTolerance * 2;
                _textLocations.Add(textLoc, handlePath);

                handleTypeColors[i] = Constants.HTypePalette[handle.Type % Constants.HTypePalette.Length];

                for (int j = 0; j < _rotations.Branch(handlePath).Count; j++)
                {
                    double transformAmount = j * planeOffset + planeOffset;
                    move = Transform.Translation(handle.ReceiverPlanes[j].ZAxis * -transformAmount);
                    rX = new Line(handle.ReceiverPlanes[j].Origin, handle.ReceiverPlanes[j].Origin + (handle.ReceiverPlanes[j].XAxis * axisLineLength));
                    rY = new Line(handle.ReceiverPlanes[j].Origin, handle.ReceiverPlanes[j].Origin + (handle.ReceiverPlanes[j].YAxis * axisLineLength));
                    rX.Transform(move);
                    rY.Transform(move);
                    _curve.Add(rX);
                    _color.Add(xGhost);
                    _curve.Add(rY);
                    _color.Add(yGhost);
                    _clip.Union(rX.BoundingBox);
                    _clip.Union(rY.BoundingBox);

                    textLoc.Origin = handle.SenderPlane.Origin + handle.SenderPlane.ZAxis * transformAmount;
                    _textLocations.Add(textLoc, handlePath);
                }
            }
        }

        /// <summary>
        /// This method will be called once every solution, before any calls to RunScript.
        /// </summary>
        protected override void BeforeSolveInstance()
        {
            _clip = BoundingBox.Empty;
            _curve.Clear();
            _dotCurve.Clear();
            _color.Clear();
            _width.Clear();
            _rotations.Clear();
            _textLocations.Clear();
        }

        //Return a BoundingBox that contains all the geometry you are about to draw.
        public override BoundingBox ClippingBox
        {
            get { return _clip; }
        }

        public override void DrawViewportMeshes(IGH_PreviewArgs args)
        {
            Plane textLocation;
            Hatch localTriangle;
            double triSize = axisLineLength * 0.5;
            Transform orient, scale = Transform.Scale(Plane.WorldXY.Origin, triSize);

            for (int i = 0; i < _textLocations.BranchCount; i++)
            {
                textLocation = _textLocations.Branches[i][0];
                localTriangle = (Hatch)triangle.Duplicate();
                orient = Transform.PlaneToPlane(Plane.WorldXY, textLocation);
                localTriangle.Transform(scale);
                localTriangle.Transform(orient);
                args.Display.DrawHatch(localTriangle, handleTypeColors[i], handleTypeColors[i]);
            }
        }

        //Draw all wires and points in this method.
        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            for (int i = 0; i < _dotCurve.Count; i++)
                args.Display.DrawDottedLine(_dotCurve[i], Color.Black);

            for (int i = 0; i < _curve.Count; i++)
                args.Display.DrawLine(_curve[i], _color[i], 2);

            // in case you want the text to be always user-oriented use this plane instead of the Handle's own
            //Plane plane;
            //args.Viewport.GetFrustumFarPlane(out plane);
            double size, pixPerUnit;
            Rhino.Display.RhinoViewport viewport = args.Viewport;
            Plane textLoc;
            double offset = -1;
            int handleIndex;
            int hType;
            double[] textSizes = new double[_textLocations.BranchCount];

            // figure out text sizes
            if (!absoluteTextSize)
            {
                for (int i = 0; i < _textLocations.BranchCount; i++)
                    textSizes[i] = modelSize;
            }
            else
            {
                for (int i = 0; i < _textLocations.BranchCount; i++)
                {
                    viewport.GetWorldToScreenScale(_textLocations.Branches[i][0].Origin, out pixPerUnit);
                    textSizes[i] = modelSize / pixPerUnit;
                }
            }

            for (int i = 0; i < _textLocations.BranchCount; i++)
            {
                handleIndex = _textLocations.Paths[i][1];
                hType = handlesTree.Branches[handleIndex][0].Type;
                size = textSizes[i];
                textLoc = _textLocations.Branches[i][0];

                textLoc.Translate(textLoc.YAxis * size * offset);
                Rhino.Display.Text3d drawText = new Rhino.Display.Text3d(string.Format("H {0} . type {1} . w {2:0.##}", handleIndex, hType, handlesTree.Branches[handleIndex][0].Weight), textLoc, size)
                {
                    HorizontalAlignment = Rhino.DocObjects.TextHorizontalAlignment.Left,
                    VerticalAlignment = Rhino.DocObjects.TextVerticalAlignment.Top
                };
                args.Display.Draw3dText(drawText, handleTextColor);
                drawText.Dispose();
                for (int j = 1; j < _textLocations.Branches[i].Count; j++)
                {
                    textLoc = _textLocations.Branches[i][j];
                    textLoc.Translate(textLoc.YAxis * size * offset);
                    drawText = new Rhino.Display.Text3d(string.Format("r{0} . {1:0.##}°", j - 1, _rotations.Branches[i][j - 1]), textLoc, size * 0.65)
                    {
                        HorizontalAlignment = Rhino.DocObjects.TextHorizontalAlignment.Right,
                        VerticalAlignment = Rhino.DocObjects.TextVerticalAlignment.Middle
                    };
                    args.Display.Draw3dText(drawText, rotationColor);
                    drawText.Dispose();
                }
            }
        }

        private Hatch MakeHatchTriangle()
        {
            Hatch triangle;
            Curve hatchBoundary = new Polyline(new Point3d[] { new Point3d(0, 0, 0), new Point3d(1, 0, 0), new Point3d(0, 1, 0), new Point3d(0, 0, 0) }).ToNurbsCurve();

            triangle = Hatch.Create(hatchBoundary, 0, 0, 1, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance)[0];
            triangle.BasePoint = new Point3d();

            return triangle;
        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            Menu_AppendSeparator(menu);

            ToolStripMenuItem toolStripMenuItem = Menu_AppendItem(menu, "Absolute text size", AbsT_Click, true, absoluteTextSize);
            toolStripMenuItem.ToolTipText = "Displays text in absolute (zoom independent) size";

            Menu_AppendSeparator(menu);
        }

        private void AbsT_Click(object sender, EventArgs e)
        {
            RecordUndoEvent("Absolute text size");
            absoluteTextSize = !GetValue("absHTextSize", false);
            SetValue("absHTextSize", absoluteTextSize);
            ExpireSolution(true);
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetBoolean("absHTextSize", absoluteTextSize);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            reader.TryGetBoolean("absHTextSize", ref absoluteTextSize);
            return base.Read(reader);
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
                return Resources.Display_Handle;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("40714A63-00CE-4FA2-935E-3ABFC605B26A"); }
        }
    }
}
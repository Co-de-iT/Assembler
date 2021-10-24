using System;
using System.Collections.Generic;
using System.Drawing;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using AssemblerLib;
using Assembler.Properties;
using GH_IO.Serialization;
using System.Windows.Forms;

namespace Assembler
{
    public class DisplayHandle : GH_Component
    {
        private bool absoluteTextSize;
        private List<Handle> handles;
        double offset;
        double absSize;
        //bool model;
        private BoundingBox _clip;
        private List<Line> _curve = new List<Line>();
        private List<Line> _dotCurve = new List<Line>();
        private List<Color> _color = new List<Color>();
        private List<int> _width = new List<int>();
        private DataTree<double> _rotations = new DataTree<double>();
        private DataTree<Plane> _textLocations = new DataTree<Plane>();

        /// <summary>
        /// Initializes a new instance of the DisplayHandle class.
        /// </summary>
        public DisplayHandle()
          : base("DisplayHandle", "HandDisp",
              "Displays a Handle Type, weight, sender and receiver planes",
              "Assembler", "Components")
        {
            absoluteTextSize = GetValue("absHTextSize", false);
            //UpdateMessage();
            ExpireSolution(true);
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Handles", "H", "Handles to display", GH_ParamAccess.list);
            pManager.AddNumberParameter("Display Size", "s", "Handles Display Size", GH_ParamAccess.item);
            pManager[1].Optional = true; // display scale is optional
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
            // get data
            handles = new List<Handle>();
            List<Curve> pCurves = new List<Curve>();
            List<Polyline> poly = new List<Polyline>();
            //Polyline p;
            GH_Structure<GH_Number> rot = new GH_Structure<GH_Number>();
            List<int> type = new List<int>();
            List<double> w = new List<double>();
            double size = 1;

            if (!DA.GetDataList(0, handles)) return;
            if (handles == null || handles.Count == 0) return;

            // populate rotations Data Tree
            for (int i = 0; i < handles.Count; i++)
                _rotations.AddRange(handles[i].rRotations, new GH_Path(0, i));

            if (!DA.GetData(1, ref size)) size = absoluteTextSize ? 1 : 0.1;

            if (absoluteTextSize)
            {
                absSize = size == 0 ? 50 : size * 50;
                offset = absSize * 0.02;
            }
            else
            {
                absSize = size;
                offset = size * 2;
            }

            // set display data
            SetDisplayData();
        }

        void SetDisplayData()
        {
            Transform move;
            Handle hand;


            for (int i = 0; i < handles.Count; i++)
            {
                hand = handles[i];
                Line rZ = new Line(hand.sender.Origin, hand.sender.Origin + (hand.sender.ZAxis * offset * (_rotations.Branches[i].Count + 1)));
                _dotCurve.Add(rZ);
                _clip.Union(rZ.BoundingBox);
                //_color.Add(Color.Black);
                Line rX = new Line(hand.sender.Origin, hand.sender.Origin + (hand.sender.XAxis * offset * 2));//1.2
                Line rY = new Line(hand.sender.Origin, hand.sender.Origin + (hand.sender.YAxis * offset * 2));
                _curve.Add(rX);
                _color.Add(Color.Red);
                _curve.Add(rY);
                _color.Add(Color.Green);
                _clip.Union(rX.BoundingBox);
                _clip.Union(rY.BoundingBox);
                Plane textLoc = hand.sender;
                //textLoc.Translate(hand.s.YAxis * absSize * -0.25);
                _textLocations.Add(textLoc, new GH_Path(0, i));

                for (int j = 0; j < _rotations.Branches[i].Count; j++)
                {
                    move = Transform.Translation(hand.receivers[j].ZAxis * -(j * offset + offset));
                    rX = new Line(hand.receivers[j].Origin, hand.receivers[j].Origin + (hand.receivers[j].XAxis * offset * 2));
                    rY = new Line(hand.receivers[j].Origin, hand.receivers[j].Origin + (hand.receivers[j].YAxis * offset * 2));
                    rX.Transform(move);
                    rY.Transform(move);
                    _curve.Add(rX);
                    _color.Add(Color.LightSalmon);
                    _curve.Add(rY);
                    _color.Add(Color.LightGreen);
                    _clip.Union(rX.BoundingBox);
                    _clip.Union(rY.BoundingBox);

                    textLoc.Origin = hand.sender.Origin + hand.sender.ZAxis * (j * offset + offset);
                    _textLocations.Add(textLoc, new GH_Path(0, i));
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

        //Draw all meshes in this method.
        public override void DrawViewportMeshes(IGH_PreviewArgs args)
        {
        }

        //Draw all wires and points in this method.
        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            for (int i = 0; i < _dotCurve.Count; i++)
                args.Display.DrawDottedLine(_dotCurve[i], Color.Black);

            for (int i = 0; i < _curve.Count; i++)
                args.Display.DrawLine(_curve[i], _color[i], 2);

            // in case you want the text to be always user-oriented use this plane instead of the Handle's one
            //Plane plane;
            //args.Viewport.GetFrustumFarPlane(out plane);
            double size;
            double pixPerUnit;
            Rhino.Display.RhinoViewport viewport = args.Viewport;
            Plane textLoc;
            double offset = -1;

            if (!absoluteTextSize)
                for (int i = 0; i < _textLocations.BranchCount; i++)
                {
                    size = absSize;
                    textLoc = _textLocations.Branches[i][0];
                    textLoc.Translate(textLoc.YAxis * size * offset);

                    Rhino.Display.Text3d drawText = new Rhino.Display.Text3d(string.Format("h {0} | type {1} | w {2}", i, handles[i].type, handles[i].weight), textLoc, size);
                    drawText.HorizontalAlignment = Rhino.DocObjects.TextHorizontalAlignment.Left;
                    drawText.VerticalAlignment = Rhino.DocObjects.TextVerticalAlignment.Top;
                    args.Display.Draw3dText(drawText, Color.Black);
                    drawText.Dispose();
                    for (int j = 1; j < _textLocations.Branches[i].Count; j++)
                    {
                        textLoc = _textLocations.Branches[i][j];
                        textLoc.Translate(textLoc.YAxis * size * offset);
                        drawText = new Rhino.Display.Text3d(string.Format("r{0} {1}", j - 1, _rotations.Branches[i][j - 1]), textLoc, size);
                        drawText.HorizontalAlignment = Rhino.DocObjects.TextHorizontalAlignment.Center;
                        drawText.VerticalAlignment = Rhino.DocObjects.TextVerticalAlignment.Top;
                        args.Display.Draw3dText(drawText, Color.SlateGray);
                        drawText.Dispose();
                    }
                }
            else
                for (int i = 0; i < _textLocations.BranchCount; i++)
                {
                    // Figure out the size. This means measuring the visible size in the viewport AT the current location.
                    viewport.GetWorldToScreenScale(_textLocations.Branches[i][0].Origin, out pixPerUnit);
                    size = absSize / pixPerUnit;
                    textLoc = _textLocations.Branches[i][0];
                    textLoc.Translate(textLoc.YAxis * size * offset);
                    Rhino.Display.Text3d drawText = new Rhino.Display.Text3d(string.Format("h {0} | type {1} | w {2}", i, handles[i].type, handles[i].weight), textLoc, size);
                    drawText.HorizontalAlignment = Rhino.DocObjects.TextHorizontalAlignment.Left;
                    drawText.VerticalAlignment = Rhino.DocObjects.TextVerticalAlignment.Top;
                    args.Display.Draw3dText(drawText, Color.Black);
                    drawText.Dispose();
                    for (int j = 1; j < _textLocations.Branches[i].Count; j++)
                    {
                        textLoc = _textLocations.Branches[i][j];
                        textLoc.Translate(textLoc.YAxis * size * offset);
                        viewport.GetWorldToScreenScale(_textLocations.Branches[i][j].Origin, out pixPerUnit);
                        size = absSize / pixPerUnit;
                        drawText = new Rhino.Display.Text3d(string.Format("r{0} {1}", j - 1, _rotations.Branches[i][j - 1]), textLoc, size);
                        drawText.HorizontalAlignment = Rhino.DocObjects.TextHorizontalAlignment.Center;
                        drawText.VerticalAlignment = Rhino.DocObjects.TextVerticalAlignment.Top;
                        args.Display.Draw3dText(drawText, Color.SlateGray);
                        drawText.Dispose();
                    }
                }
        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            Menu_AppendSeparator(menu);
            ToolStripMenuItem toolStripMenuItem = Menu_AppendItem(menu, "Absolute text size", AbsT_Click, true, absoluteTextSize);
            toolStripMenuItem.ToolTipText = "Prevents an AssemblyObject from connecting with itself";
            Menu_AppendSeparator(menu);
        }

        private void AbsT_Click(object sender, EventArgs e)
        {
            RecordUndoEvent("Absolute text size");
            absoluteTextSize = !GetValue("absHTextSize", false);
            SetValue("absHTextSize", absoluteTextSize);
            // set component message
            //UpdateMessage();
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
            //UpdateMessage();
            return base.Read(reader);
        }

        //private void UpdateMessage()
        //{
        //    string message = "";
        //    if (absoluteTextSize)
        //        message += "absolute";

        //    Message = message;
        //}

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
            get { return new Guid("bbfdb124-b265-43a9-9f0b-68704f46a35d"); }
        }
    }
}
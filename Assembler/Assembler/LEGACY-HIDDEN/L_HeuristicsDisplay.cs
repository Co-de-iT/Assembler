using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino.Display;
using AssemblerLib;
using Assembler.Properties;

namespace Assembler
{
    public class L_HeuristicsDisplay : GH_Component
    {
        bool showEdges, haveXData;
        private BoundingBox _clip;
        private List<XData> xDCatalog;
        private List<Mesh> _mesh;
        private List<Mesh> _invalidMesh;
        private List<Color> _color;
        private List<DisplayMaterial> _mat;
        private List<Point3d> eg_Points;
        private List<Curve> eg_Curves;
        private List<Brep> eg_Breps;
        private List<Mesh> eg_Meshes;
        private double textSize;
        private readonly int _width = 1;
        private Color[] colorCatalog;
        private DisplayMaterial[] matCatalog;
        private GH_Line[][] edgeCatalog;

        private Dictionary<string, int> catalog;
        DataTree<AssemblyObject> AOpairs;
        DataTree<XData> xData;
        DataTree<GH_Line> geomEdges;
        DataTree<GH_Line> displayEdges;
        DataTree<GH_Line> invalidDisplayEdges;
        DataTree<Color> edgeColor;
        DataTree<Plane> textLocations;
        DataTree<Plane> numberLocations;
        DataTree<Point3d> bbCenters;
        DataTree<string> rulesText;
        DataTree<bool> coherencePattern;

        private readonly Color[] objPalette = new Color[] { Color.AliceBlue, Color.SlateGray, Color.Goldenrod, Color.YellowGreen, Color.DarkKhaki, Color.CadetBlue,
            Color.Plum, Color.LightSteelBlue, Color.PaleTurquoise, Color.Olive, Color.Violet, Color.DarkGray, Color.DarkSlateGray, Color.DarkGoldenrod, Color.DarkOliveGreen};

        private readonly Color[] srPalette = new Color[] { Color.FromArgb(229, 229, 220), Color.SlateGray };
        private readonly DisplayMaterial[] srMatPalette = new DisplayMaterial[] { new DisplayMaterial(Color.FromArgb(229, 229, 220)), new DisplayMaterial(Color.SlateGray) };

        /// <summary>
        /// Initializes a new instance of the HeuristicDisplay class.
        /// </summary>
        public L_HeuristicsDisplay()
          : base("Heuristic Display", "HeuD",
              "Display Heuristics as visual combination of AssemblyObjects\nLEGACY component",
              "Assembler", "Heuristics")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Origin Point", "P", "Origin Point for Display", GH_ParamAccess.item, new Point3d());
            pManager.AddGenericParameter("AssemblyObjects Set", "AOs", "List of Assembly Objects in the set", GH_ParamAccess.list);
            pManager.AddGenericParameter("XData", "XD", "Xdata associated with the AssemblyObject in the catalog", GH_ParamAccess.list);
            pManager.AddTextParameter("Heuristics String", "HeS", "Heuristics String", GH_ParamAccess.list);
            pManager.AddNumberParameter("X size", "Xs", "Cell size along X direction as % of Bounding Box", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Y size", "Ys", "Cell size along Y direction as % of Bounding Box", GH_ParamAccess.item, 1.0);
            pManager.AddIntegerParameter("n. Rows", "nR", "number of rows", GH_ParamAccess.item, 10);
            pManager.AddColourParameter("Colors", "C", "Color pair for receiver and sender, receiver first", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Show Edges", "E", "Show Mesh edges", GH_ParamAccess.item, false);
            pManager.AddIntegerParameter("Display Mode", "D", "Choose which display mode to use\n0 - by AssemblyObject type\n1 - by Sender/Receiver status\n\nattach a value list component for automatic list generation", GH_ParamAccess.item, 0);
            pManager[2].Optional = true; // XData is optional
            pManager[7].Optional = true; // colors are optional
            pManager[8].Optional = true; // show edges is optional
            pManager[9].Optional = true; // display mode is optional
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
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "This is an obsolete component - replace it with a new version from the Ribbon");

            List<AssemblyObject> AOs = new List<AssemblyObject>();
            List<Color> Cols = new List<Color>();
            bool edges = false;

            // sanity check on inputs
            if (!DA.GetDataList("AssemblyObjects Set", AOs)) return;
            if (!DA.GetDataList("Colors", Cols))
            {
                for (int i = 0; i < AOs.Count; i++)
                    Cols.Add(objPalette[i % AOs.Count]);
            }

            List<string> HeS = new List<string>();
            if (!DA.GetDataList("Heuristics String", HeS)) return;

            // get XData catalog
            xDCatalog = new List<XData>();
            DA.GetDataList(2, xDCatalog);
            // flag for extra geometry existence
            haveXData = (xDCatalog != null) && (xDCatalog.Count > 0);

            // get the rest of inputs
            Point3d P = new Point3d();
            double xS = double.NaN;
            double yS = double.NaN;
            int nR = 1;
            int dMode = 0;
            DA.GetData("Origin Point", ref P);
            DA.GetData("X size", ref xS);
            DA.GetData("Y size", ref yS);
            DA.GetData("n. Rows", ref nR);
            DA.GetData("Show Edges", ref edges);
            DA.GetData("Display Mode", ref dMode);

            // __________________ autoList part __________________

            // variable for the list
            Grasshopper.Kernel.Special.GH_ValueList vList;
            // tries to cast input as list
            try
            {

                // if the list is not the first parameter then change Input[0] to the corresponding value
                vList = (Grasshopper.Kernel.Special.GH_ValueList)Params.Input[9].Sources[0];

                if (!vList.NickName.Equals("Display"))
                {
                    vList.ClearData();
                    vList.ListItems.Clear();
                    vList.NickName = "Display";
                    var item1 = new Grasshopper.Kernel.Special.GH_ValueListItem("by Type", "0");
                    var item2 = new Grasshopper.Kernel.Special.GH_ValueListItem("by s/r status", "1");
                    vList.ListItems.Add(item1);
                    vList.ListItems.Add(item2);

                    vList.ListItems[0].Value.CastTo(out dMode);
                }
            }
            catch
            {
                // handles anything that is not a value list
            }

            // cast input to AssemblyObject type
            AssemblyObject[] components = AOs.ToArray();

            // Build Component catalog
            catalog = Utilities.BuildDictionary(components, true);

            // Build Heuristics Tree
            DataTree<Rule> HeR = HeuristicsRulesFromString(AOs, HeS);//, out HeSTree);

            // build edge catalog
            edgeCatalog = new GH_Line[components.Length][];
            for (int i = 0; i < edgeCatalog.Length; i++)
                edgeCatalog[i] = GetSihouette(components[i].collisionMesh);

            // build colors and materials catalogs
            colorCatalog = Cols.ToArray();
            matCatalog = CompileMatCatalog(colorCatalog);

            showEdges = edges;

            AOpairs = GeneratePairs(components, HeR, P, xS, yS, nR);
            SetPreviewData(AOpairs, xData, dMode != 0);

        }

        public GH_Line[] GetSihouette(Mesh M)
        {
            ConcurrentBag<GH_Line> lines = new ConcurrentBag<GH_Line>();
            double tol = Math.PI * 0.25; // angle tolerance  ignore edges whose faces meet at an angle larger than this

            M.Normals.ComputeNormals();
            Rhino.Geometry.Collections.MeshTopologyEdgeList topologyEdges = M.TopologyEdges;

            Parallel.For(0, topologyEdges.Count, i =>
            //for (int i = 0; i < topologyEdges.Count; i++)
            {
                int[] connectedFaces = topologyEdges.GetConnectedFaces(i);
                if (connectedFaces.Length < 2)
                    lines.Add(new GH_Line(topologyEdges.EdgeLine(i)));

                if (connectedFaces.Length == 2)
                {
                    Vector3f norm1 = M.FaceNormals[connectedFaces[0]];
                    Vector3f norm2 = M.FaceNormals[connectedFaces[1]];
                    double nAng = Vector3d.VectorAngle(new Vector3d((double)norm1.X, (double)norm1.Y, (double)norm1.Z),
                      new Vector3d((double)norm2.X, (double)norm2.Y, (double)norm2.Z));
                    if (nAng > tol)
                        lines.Add(new GH_Line(topologyEdges.EdgeLine(i)));

                }
            });

            return lines.ToArray();
        }

        DisplayMaterial[] CompileMatCatalog(Color[] colorTable)
        {
            DisplayMaterial[] mT = new DisplayMaterial[colorTable.Length];

            for (int i = 0; i < colorTable.Length; i++)
                mT[i] = new DisplayMaterial(colorTable[i]);

            return mT;

        }

        DataTree<Rule> HeuristicsRulesFromString(List<AssemblyObject> AOset, List<string> heu)
        {
            DataTree<Rule> heuT = new DataTree<Rule>();

            string[] rComp = heu.ToArray();

            int rT, rH, rR, sT, sH;
            double rRA;
            int w;
            for (int i = 0; i < rComp.Length; i++)
            {
                string[] rule = rComp[i].Split(new[] { '<', '%' });
                string[] rec = rule[0].Split(new[] { '|' });
                string[] sen = rule[1].Split(new[] { '|' });
                // sender and receiver component types
                sT = catalog[sen[0]];
                rT = catalog[rec[0]];
                // sender handle index
                sH = Convert.ToInt32(sen[1]);
                // weight
                w = Convert.ToInt32(rule[2]);
                string[] rRot = rec[1].Split(new[] { '=' });
                // receiver handle index and rotation
                rH = Convert.ToInt32(rRot[0]);
                rRA = Convert.ToDouble(rRot[1]);
                rR = AOset[rT].handles[rH].rDictionary[rRA]; // using rotations

                heuT.Add(new Rule(rec[0], rT, rH, rR, rRA, sen[0], sT, sH, w), new GH_Path(rT));
            }
            //heS.Graft();
            return heuT;
        }

        public DataTree<AssemblyObject> GeneratePairs(AssemblyObject[] AO, DataTree<Rule> Hr, Point3d O, double padX, double padY, int nR)
        {
            DataTree<AssemblyObject> AOpairs = new DataTree<AssemblyObject>();
            AssemblyObject sender, receiver;

            xData = new DataTree<XData>();
            XData senGeom, recGeom;

            geomEdges = new DataTree<GH_Line>();
            List<GH_Line> senEdges, recEdges;

            textLocations = new DataTree<Plane>();
            numberLocations = new DataTree<Plane>();
            bbCenters = new DataTree<Point3d>();
            rulesText = new DataTree<string>();
            coherencePattern = new DataTree<bool>();

            double sX = 0, sY = 0, sZ = 0;

            int rT, rH, sT, sH, rR;
            Plane loc;

            int countY = 0, countX = 0;
            int total = Hr.AllData().Count;
            int rowElements = (total / nR) + 1;

            // sequence
            // loop 1:
            // record point grid position (countX, countY)
            // create copies of AOs
            // orient sender to receiver according to rule and add to geometries Tree
            // calculate Bounding Box size and compare to sX, sY, sZ (retain maximum)
            // calculate consistency (collisions)
            // if consistent, orient extra geometry (sender to receiver)

            for (int i = 0; i < Hr.BranchCount; i++)
            {

                Rule[] rules = Hr.Branches[i].ToArray();
                rT = rules[i].rT;

                for (int j = 0; j < rules.Length; j++)
                {
                    senGeom = null;
                    recGeom = null;

                    // extract rule parameters
                    rH = rules[j].rH;
                    sT = rules[j].sT;
                    sH = rules[j].sH;
                    rR = rules[j].rR;

                    // define location as point grid position (countX, countY)
                    loc = Plane.WorldXY;
                    loc.Origin = new Point3d(countX, countY, 0);

                    // choose components
                    receiver = Utilities.Clone(AO[rT]);//new AssemblyObject(AO[rT]);
                    sender = Utilities.Clone(AO[sT]);//new AssemblyObject(AO[sT]);

                    recEdges = new List<GH_Line>();
                    senEdges = new List<GH_Line>();
                    // copy edges
                    recEdges = edgeCatalog[rT].Select(l => new GH_Line(l)).ToList();
                    senEdges = edgeCatalog[sT].Select(l => new GH_Line(l)).ToList();

                    // generate transformation orient: sender to receiver
                    Transform orient = Transform.PlaneToPlane(sender.handles[sH].sender, receiver.handles[rH].receivers[rR]);

                    // orient sender AssemblyObject
                    sender.Transform(orient);

                    // orient edges
                    for (int k = 0; k < senEdges.Count; k++)
                        senEdges[k].Transform(orient);

                    // copy and orient XData if present
                    if (haveXData)
                    {
                        for (int k = 0; k < xDCatalog.Count; k++)
                        {
                            // receiver XData
                            if (String.Equals(xDCatalog[k].AOName, receiver.name))
                                recGeom = new XData(xDCatalog[k]);
                            // sender XData
                            if (String.Equals(xDCatalog[k].AOName, sender.name))
                            {
                                senGeom = new XData(xDCatalog[k]);
                                senGeom.Transform(orient);
                            }
                        }
                    }
                    // calculate Bounding Box and compare size to initial parameters
                    BoundingBox bb = receiver.collisionMesh.GetBoundingBox(false);
                    bb.Union(sender.collisionMesh.GetBoundingBox(false));

                    // record center plane of AO combination
                    bbCenters.Add(bb.Center, Hr.Paths[i].AppendElement(j));

                    // retain largest dimensions (for grid final size)
                    sX = Math.Max(sX, bb.Diagonal.X);
                    sY = Math.Max(sY, bb.Diagonal.Y);
                    sZ = Math.Max(sZ, bb.Diagonal.Z);

                    // add AssemblyObjects to DataTree - same path of heuristic + one more index for rotations
                    AOpairs.Add(receiver, Hr.Paths[i].AppendElement(j));
                    AOpairs.Add(sender, Hr.Paths[i].AppendElement(j));
                    // edges need one more index for sender/receiver identification (0 receiver, 1 sender)
                    geomEdges.AddRange(recEdges, Hr.Paths[i].AppendElement(j).AppendElement(0));
                    geomEdges.AddRange(senEdges, Hr.Paths[i].AppendElement(j).AppendElement(1));

                    // add point grid location to tree
                    textLocations.Add(loc, Hr.Paths[i].AppendElement(j));

                    // fill coherence pattern & orient extra geometry only if valid combination
                    bool valid = !Utilities.CollisionCheckPair(receiver, sender);
                    coherencePattern.Add(valid, Hr.Paths[i].AppendElement(j));

                    if (valid && haveXData)
                    {
                        // extra geometries need one more index for sender/receiver identification (0 receiver, 1 sender)
                        if (recGeom != null) xData.Add(recGeom, Hr.Paths[i].AppendElement(j).AppendElement(0));
                        if (senGeom != null) xData.Add(senGeom, Hr.Paths[i].AppendElement(j).AppendElement(1));
                    }

                    // rewrite rule for display and add to the text Tree
                    string rule = rules[j].ToString();
                    rulesText.Add(rule, Hr.Paths[i].AppendElement(j));

                    // calculate next grid position
                    countX++;
                    if (countX % rowElements == 0)
                    {
                        countX = 0;
                        countY++;
                    }
                }
            }

            // calculate textSize
            textSize = sX * 0.025;

            // loop2:
            // scale point grid positions by final sX, sY, sZ
            // move all elements (AO geometries, edges, extra geometries) in position
            for (int i = 0; i < AOpairs.BranchCount; i++)
            {
                Point3d newLoc = textLocations.Branches[i][0].Origin;
                newLoc.X = O.X + (newLoc.X + 0.5) * sX * padX;
                newLoc.Y = O.Y + (newLoc.Y + 0.5) * sY * padY;
                newLoc.Z = O.Z + sZ * 0.5;
                Point3d textLoc = new Point3d(newLoc.X, newLoc.Y - 0.45 * sY, 0);
                Point3d numLoc = new Point3d(newLoc.X, newLoc.Y - 0.35 * sY, 0);
                Plane textPlane = Plane.WorldXY;
                Plane numberPlane = Plane.WorldXY;
                textPlane.Origin = textLoc;
                numberPlane.Origin = numLoc;
                // record final text position
                textLocations.Branches[i][0] = textPlane;
                numberLocations.Add(numberPlane, new GH_Path(i));

                // define transformation
                Transform move = Transform.Translation(newLoc - bbCenters.Branches[i][0]);

                // transfer AssemblyObjects
                foreach (AssemblyObject ao in AOpairs.Branches[i])
                    ao.Transform(move);

                //transfer edges
                for (int k = 0; k < 2; k++)
                    foreach (GH_Line l in geomEdges.Branch(AOpairs.Path(i).AppendElement(k)))
                        l.Transform(move);

                // transfer extra geometries
                if (haveXData && coherencePattern.Branches[i][0])
                    for (int k = 0; k < 2; k++)
                        if (xData.PathExists(AOpairs.Paths[i].AppendElement(k)))
                            xData.Branch(AOpairs.Paths[i].AppendElement(k))[0].Transform(move);
            }
            return AOpairs;
        }

        public void SetPreviewData(DataTree<AssemblyObject> Aopairs, DataTree<XData> xDataTree, bool srMode)
        {
            Mesh m;
            displayEdges = new DataTree<GH_Line>();
            invalidDisplayEdges = new DataTree<GH_Line>();
            edgeColor = new DataTree<Color>();
            Color eColor;
            int typeIndex;

            for (int i = 0; i < Aopairs.BranchCount; i++)
                for (int j = 0; j < Aopairs.Branches[i].Count; j++)
                {
                    m = Aopairs.Branches[i][j].collisionMesh;
                    _clip = BoundingBox.Union(_clip, m.GetBoundingBox(false));

                    if (coherencePattern.Branches[i][0])
                    {
                        _mesh.Add(m);
                        typeIndex = AOpairs.Branches[i][j].type;
                        if (!haveXData) eColor = Color.Black;
                        else eColor = srMode ? srPalette[j] : colorCatalog[typeIndex];
                        edgeColor.Add(eColor, new GH_Path(i));
                        _color.Add(srMode ? srPalette[j] : colorCatalog[typeIndex]);
                        _mat.Add(srMode ? srMatPalette[j] : matCatalog[typeIndex]);
                        displayEdges.AddRange(geomEdges.Branch(Aopairs.Path(i).AppendElement(j)), new GH_Path(i, j));
                    }
                    else
                    {
                        _invalidMesh.Add(m);
                        invalidDisplayEdges.AddRange(geomEdges.Branch(Aopairs.Path(i).AppendElement(j)), new GH_Path(i, j));
                    }

                }
            if (haveXData)
            {
                // Extrusions and Surfaces are detected as Breps
                eg_Breps = new List<Brep>();
                eg_Curves = new List<Curve>();
                eg_Meshes = new List<Mesh>();
                eg_Points = new List<Point3d>();

                for (int i = 0; i < xDataTree.BranchCount; i++)
                    if (xDataTree.Branches[i][0] != null)
                        for (int j = 0; j < xDataTree.Branches[i][0].data.Count; j++)
                        {
                            // Point3d are the exception as they are a struct and a cast to GeometryBase is null
                            /*
                             When they are referenced from Rhino and result as ReferencedPoint in GH, they can be cast
                            as GeometryBase - ObjectType Rhino.DocObjects.ObjectType.Point
                            If they appear as set of coordinates (as is the case of XData), they are Point3d (struct)
                             */
                            if (xDataTree.Branches[i][0].data[j] is Point3d)
                                eg_Points.Add((Point3d)xDataTree.Branches[i][0].data[j]);
                            else
                            {
                                GeometryBase gb = xDataTree.Branches[i][0].data[j] as GeometryBase;

                                if (gb != null)
                                {
                                    _clip = BoundingBox.Union(_clip, gb.GetBoundingBox(false));

                                    //convert GeometryBase in the related object type
                                    switch (gb.ObjectType)
                                    {
                                        case Rhino.DocObjects.ObjectType.Brep:
                                            eg_Breps.Add(gb as Brep);
                                            break;
                                        case Rhino.DocObjects.ObjectType.Curve:
                                            eg_Curves.Add(gb as Curve);
                                            break;
                                        case Rhino.DocObjects.ObjectType.Surface:
                                            eg_Breps.Add((gb as Surface).ToBrep());
                                            break;
                                        case Rhino.DocObjects.ObjectType.Mesh:
                                            eg_Meshes.Add(gb as Mesh);
                                            break;
                                        // for future implementation:
                                        //case Rhino.DocObjects.ObjectType.Extrusion:
                                        //    break;
                                        //case Rhino.DocObjects.ObjectType.PointSet:
                                        //    break;
                                        default:
                                            break;
                                    }

                                }
                            }
                        }
            }
        }


        /// <summary>
        /// This method will be called once every solution, before any calls to RunScript.
        /// </summary>
        protected override void BeforeSolveInstance()
        {
            _clip = BoundingBox.Empty;
            _mesh = new List<Mesh>();
            _invalidMesh = new List<Mesh>();
            _color = new List<Color>();
            _mat = new List<DisplayMaterial>();
        }


        //Return a BoundingBox that contains all the geometry you are about to draw.
        public override BoundingBox ClippingBox
        {
            get { return _clip; }
        }

        //Draw all meshes in this method.
        public override void DrawViewportMeshes(IGH_PreviewArgs args)
        {


            if (haveXData)
            {
                DisplayMaterial wh = new DisplayMaterial(Color.White);
                foreach (Brep b in eg_Breps)
                    args.Display.DrawBrepShaded(b, wh);
                foreach (Mesh m in eg_Meshes)
                    args.Display.DrawMeshShaded(m, wh);
            }
            else
                for (int i = 0; i < _mesh.Count; i++)
                    args.Display.DrawMeshShaded(_mesh[i], _mat[i]);
        }

        //Draw all wires and points in this method.
        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            // text
            Rhino.Display.Text3d drawText;
            for (int i = 0; i < rulesText.BranchCount; i++)
            {
                drawText = new Text3d(rulesText.Branches[i][0], textLocations.Branches[i][0], textSize);
                drawText.FontFace = "Lucida Console";
                drawText.HorizontalAlignment = Rhino.DocObjects.TextHorizontalAlignment.Center;
                drawText.VerticalAlignment = Rhino.DocObjects.TextVerticalAlignment.Top;
                args.Display.Draw3dText(drawText, Color.Black);
                drawText.Dispose();
                //args.Display.Draw3dText(rulesText.Branches[i][0], Color.Black, textLocations.Branches[i][0], textSize, "Lucida Console",
                //    false, false, Rhino.DocObjects.TextHorizontalAlignment.Center, Rhino.DocObjects.TextVerticalAlignment.Top);

                drawText = new Text3d(string.Format("{0}", i), numberLocations.Branches[i][0], textSize * 0.8);
                drawText.FontFace = "Lucida Console";
                drawText.HorizontalAlignment = Rhino.DocObjects.TextHorizontalAlignment.Center;
                drawText.VerticalAlignment = Rhino.DocObjects.TextVerticalAlignment.Bottom;
                args.Display.Draw3dText(drawText, Color.DimGray);
                drawText.Dispose();
                //args.Display.Draw3dText(string.Format("{0}", i), Color.DimGray, numberLocations.Branches[i][0], textSize * 0.8, "Lucida Console",
                //false, false, Rhino.DocObjects.TextHorizontalAlignment.Center, Rhino.DocObjects.TextVerticalAlignment.Bottom);
            }

            if (haveXData || showEdges)
                for (int i = 0; i < edgeColor.BranchCount; i++)
                    for (int j = 0; j < edgeColor.Branches[i].Count; j++)
                        foreach (GH_Line edge in displayEdges.Branch(edgeColor.Path(i).AppendElement(j)))
                            args.Display.DrawLine(edge.Value, edgeColor.Branches[i][j], _width);

            // display invalid cases
            foreach (GH_Line edge in invalidDisplayEdges.AllData())
                args.Display.DrawLine(edge.Value, Color.Red, _width * 2);

            if (haveXData)
            {
                foreach (Curve c in eg_Curves)
                    args.Display.DrawCurve(c, Color.YellowGreen, 3); //DeepPink, Crimson, LimeGreen
                foreach (Point3d p in eg_Points)
                    args.Display.DrawPoint(p, Color.DarkRed);
                if (showEdges)
                {
                    foreach (Brep b in eg_Breps)
                        args.Display.DrawBrepWires(b, Color.DarkSlateGray, 1);
                    foreach (Mesh m in eg_Meshes)
                        args.Display.DrawMeshWires(m, Color.DarkSlateGray, 1);
                }
            }
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
                return Resources.Heuristics_Dispay_OLD;
            }
        }

        /// <summary>
        /// Exposure override for position in the SUbcategory (options primary to septenary)
        /// https://apidocs.co/apps/grasshopper/6.8.18210/T_Grasshopper_Kernel_GH_Exposure.htm
        /// </summary>
        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.hidden; }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("6121d3ac-c714-4b36-aa08-cab168fb7518"); }
        }
    }
}
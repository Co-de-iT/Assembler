using Assembler.Properties;
using Assembler.Utils;
using AssemblerLib;
using GH_IO.Serialization;
using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Components;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Assembler
{
    public class HeuristicsDisplay : GH_Component
    {
        private bool filterkWZLock;
        private string displayType;

        private List<GH_CustomPreviewItem> _items;
        private List<GH_CustomPreviewItem> _XDitems;

        bool showEdges, haveXData;
        private BoundingBox _clip;
        private List<XData> xDCatalog;
        private List<Mesh> _mesh;
        private List<Color> _color;
        private List<DisplayMaterial> _mat;
        private List<Point3d> XD_Points;
        private List<Curve> XD_Curves;
        private List<Brep> XD_Breps;
        private List<Mesh> XD_Meshes;
        private double textSize;
        private readonly int _width = 1;
        private Color[] typeColorCatalog;
        private Color[] srColorCatalog;
        private DisplayMaterial[] typeMatCatalog;
        private DisplayMaterial[] srMatCatalog;
        private GH_Line[][] edgeCatalog;

        private Dictionary<string, int> catalog;
        DataTree<AssemblyObject> AOpairs;
        DataTree<XData> xData;
        DataTree<GH_Line> geomEdges;
        DataTree<GH_Line> displayEdges;
        DataTree<GH_Line> invalidDisplayEdges;
        DataTree<GH_Line> ZincompatibleDisplayEdges;
        DataTree<Color> edgeColor;
        DataTree<Plane> textLocations;
        DataTree<Plane> numberLocations;
        DataTree<Point3d> bbCenters;
        DataTree<string> rulesText;
        DataTree<bool> coherencePattern;
        DataTree<bool> zLockPattern;

        /// <summary>
        /// Initializes a new instance of the HeuristicDisplay class.
        /// </summary>
        public HeuristicsDisplay()
          : base("Heuristic Display", "HeuD",
              "Display Heuristics as visual combination of AssemblyObjects",
              "Assembler", "Heuristics")
        {
            filterkWZLock = GetValue("ZLockFilter", false);
            displayType = GetValue("OutputType", "AO Types");
            UpdateMessage();
            ExpireSolution(true);
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Origin Point", "P", "Origin Point for Display", GH_ParamAccess.item, new Point3d());
            pManager.AddGenericParameter("AssemblyObjects Set", "AOs", "List of Assembly Objects in the set", GH_ParamAccess.list);
            pManager.AddGenericParameter("XData", "XD", "Xdata associated with the AssemblyObject in the catalog", GH_ParamAccess.list);
            pManager.AddTextParameter("Heuristics Set", "HeS", "Heuristics Set", GH_ParamAccess.list);
            pManager.AddNumberParameter("X size", "Xs", "Cell size along X direction as % of Bounding Box", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Y size", "Ys", "Cell size along Y direction as % of Bounding Box", GH_ParamAccess.item, 1.0);
            pManager.AddIntegerParameter("n. Rows", "nR", "number of rows", GH_ParamAccess.item, 10);
            pManager.AddColourParameter("Colors", "C", "Colors (OPTIONAL)\n2 colors for Sender-Receiver display mode (receiver first)\nOne color for component type for Display by type", GH_ParamAccess.list);

            pManager[2].Optional = true; // XData is optional
            pManager[7].Optional = true; // colors are optional
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
            List<AssemblyObjectGoo> GH_AOs = new List<AssemblyObjectGoo>();
            List<AssemblyObject> AOs = new List<AssemblyObject>();
            List<Color> InputColors = new List<Color>();
            List<Color> TypeColors = new List<Color>();
            bool edges = GetValue("ShowEdges", true);

            // sanity check on mandatory inputs
            if (!DA.GetDataList("AssemblyObjects Set", GH_AOs)) return;

            AOs = GH_AOs.Select(ao => ao.Value).ToList();

            List<string> HeS = new List<string>();
            if (!DA.GetDataList("Heuristics Set", HeS)) return;

            // get XData catalog
            xDCatalog = new List<XData>();
            DA.GetDataList(2, xDCatalog);
            // flag for extra geometry existence
            haveXData = (xDCatalog != null) && (xDCatalog.Count > 0);

            // get the remaining inputs
            Point3d P = new Point3d();
            double xS = double.NaN;
            double yS = double.NaN;
            int nR = 1;

            DA.GetData("Origin Point", ref P);
            DA.GetData("X size", ref xS);
            DA.GetData("Y size", ref yS);
            DA.GetData("n. Rows", ref nR);

            if (!DA.GetDataList("Colors", InputColors))
            {
                // if there's no Color input build TypeColor from default palette
                if (AOs.Count <= Utilities.AOTypePalette.Length)
                    for (int i = 0; i < AOs.Count; i++)
                        TypeColors.Add(Utilities.AOTypePalette[i]);
                else
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "You are using an exceptionally large set of AssemblyObjects - " +
                        "Switching to random palette\n" +
                        "Have you considered reducing the number of objects in your set?");
                    Random rand = new Random(0);
                    for (int i = 0; i < AOs.Count; i++)
                        TypeColors.Add(Color.FromKnownColor(Utilities.colorlist[rand.Next(0, Utilities.colorlist.Count - 1)]));
                }
            }

            if (InputColors.Count > 0)
            {
                // check input colors sanity if in AO Types mode
                if (InputColors.Count < AOs.Count && GetValue("OutputType", "AO Types") == "AO Types")
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please provide one color for each AssemblyObject in the set");
                    return;
                }
                TypeColors = InputColors;
            }


            // cast input to AssemblyObject type
            AssemblyObject[] components = AOs.ToArray();

            // build Component catalog
            catalog = Utilities.BuildDictionary(components);

            // build Rules List
            List<Rule> HeR = Utilities.HeuristicsRulesFromString(AOs, catalog, HeS);//, out HeSTree);

            // build edge catalog
            edgeCatalog = new GH_Line[components.Length][];
            for (int i = 0; i < edgeCatalog.Length; i++)
                edgeCatalog[i] = Utilities.GetSihouette(components[i].collisionMesh);

            // build colors and materials catalogs
            typeColorCatalog = TypeColors.ToArray();
            if (InputColors.Count > 1)
                srColorCatalog = new Color[] { InputColors[0], InputColors[1] };
            else
                srColorCatalog = Utilities.srPalette;

            typeMatCatalog = CompileMatCatalog(typeColorCatalog);
            srMatCatalog = CompileMatCatalog(srColorCatalog);

            showEdges = edges;
            bool srMode;
            switch (GetValue("OutputType", "AO Types"))
            {
                case "AO Types":
                    srMode = false;
                    break;
                case "S-R":
                    srMode = true;
                    break;
                default:
                    srMode = false;
                    break;
            }

            AOpairs = GeneratePairs(components, HeR, P, xS, yS, nR);
            SetPreviewData(AOpairs, xData, srMode);
        }

        DisplayMaterial[] CompileMatCatalog(Color[] colorTable)
        {
            DisplayMaterial[] materialTable = new DisplayMaterial[colorTable.Length];

            for (int i = 0; i < colorTable.Length; i++)
                materialTable[i] = new DisplayMaterial(colorTable[i], (255 - colorTable[i].A) / 255.0);

            return materialTable;

        }

        private DataTree<AssemblyObject> GeneratePairs(AssemblyObject[] AO, List<Rule> Hr, Point3d O, double padX, double padY, int nR)
        {
            DataTree<AssemblyObject> AOpairs = new DataTree<AssemblyObject>();
            AssemblyObject senderAO, receiverAO;
            List<XData> XDsenderGeometry, XDreceiverGeometry;
            List<GH_Line> senEdges, recEdges;

            double sX = 0, sY = 0, sZ = 0;

            int rT, rH, sT, sH, rR;
            Plane loc;

            int countY = 0, countX = 0;
            int total = Hr.Count;
            int rowElements = (total / nR) + 1;

            // sequence
            // loop 1:
            // record point grid position (countX, countY)
            // create copies of AOs
            // orient sender to receiver according to rule and add to geometries Tree
            // calculate Bounding Box size and compare to sX, sY, sZ (retain maximum)
            // calculate consistency (collisions)
            // if consistent, orient extra geometry (sender to receiver)

            for (int i = 0; i < Hr.Count; i++)
            {

                rT = Hr[i].rT;

                XDsenderGeometry = new List<XData>();
                XDreceiverGeometry = new List<XData>();

                // extract rule parameters
                rH = Hr[i].rH;
                sT = Hr[i].sT;
                sH = Hr[i].sH;
                rR = Hr[i].rR;

                // define location as point grid position (countX, countY)
                loc = Plane.WorldXY;
                loc.Origin = new Point3d(countX, countY, 0);

                // choose components
                receiverAO = Utilities.Clone(AO[rT]);
                senderAO = Utilities.Clone(AO[sT]);

                recEdges = new List<GH_Line>();
                senEdges = new List<GH_Line>();
                // copy edges
                recEdges = edgeCatalog[rT].Select(l => new GH_Line(l)).ToList();
                senEdges = edgeCatalog[sT].Select(l => new GH_Line(l)).ToList();

                // generate transformation orient: sender to receiver
                Transform orient = Transform.PlaneToPlane(senderAO.handles[sH].sender, receiverAO.handles[rH].receivers[rR]);

                // orient sender AssemblyObject
                senderAO.Transform(orient);

                // orient edges
                for (int k = 0; k < senEdges.Count; k++)
                    senEdges[k].Transform(orient);

                // copy and orient XData if present
                if (haveXData)
                {
                    for (int k = 0; k < xDCatalog.Count; k++)
                    {
                        // receiver XData
                        if (String.Equals(xDCatalog[k].AOName, receiverAO.name))
                            XDreceiverGeometry.Add(new XData(xDCatalog[k]));
                        // sender XData
                        if (String.Equals(xDCatalog[k].AOName, senderAO.name))
                        {
                            XData XDsenderGeomTemp = new XData(xDCatalog[k]);
                            XDsenderGeomTemp.Transform(orient);
                            XDsenderGeometry.Add(XDsenderGeomTemp);
                        }
                    }
                }
                // calculate Bounding Box and compare size to initial parameters
                BoundingBox bb = receiverAO.collisionMesh.GetBoundingBox(false);
                bb.Union(senderAO.collisionMesh.GetBoundingBox(false));

                // record center plane of AO combination
                bbCenters.Add(bb.Center, new GH_Path(i));

                // retain largest dimensions (for grid final size)
                sX = Math.Max(sX, bb.Diagonal.X);
                sY = Math.Max(sY, bb.Diagonal.Y);
                sZ = Math.Max(sZ, bb.Diagonal.Z);

                // add AssemblyObjects to DataTree - same path of heuristic + one more index for rotations
                AOpairs.Add(receiverAO, new GH_Path(i));
                AOpairs.Add(senderAO, new GH_Path(i));
                // edges need one more index for sender/receiver identification (0 receiver, 1 sender)
                geomEdges.AddRange(recEdges, new GH_Path(i, 0));
                geomEdges.AddRange(senEdges, new GH_Path(i, 1));

                // add point grid location to tree
                textLocations.Add(loc, new GH_Path(i));

                // fill coherence pattern & orient extra geometry only if valid combination
                bool valid = !Utilities.CollisionCheckPair(receiverAO, senderAO);
                // check for Z orientation lock
                // if sender is NOT oriented as World Z rule is considered Z-Lock incompatible (but not invalid)
                bool zLockChecked = true;
                if (filterkWZLock && senderAO.worldZLock)
                    zLockChecked = Utilities.AbsoluteZCheck(senderAO);

                zLockPattern.Add(zLockChecked, new GH_Path(i));
                coherencePattern.Add(valid, new GH_Path(i));

                // display XData only if valid AND zLockChecked
                if (valid && zLockChecked && haveXData)
                {
                    // extra geometries need one more index for sender/receiver identification (0 receiver, 1 sender)
                    if (XDreceiverGeometry.Count > 0) xData.AddRange(XDreceiverGeometry, new GH_Path(i, 0));
                    if (XDsenderGeometry.Count > 0) xData.AddRange(XDsenderGeometry, new GH_Path(i, 1));
                }

                // rewrite rule for display and add to the text Tree
                string rule = Hr[i].ToString();
                rulesText.Add(rule, new GH_Path(i));

                // calculate next grid position
                countX++;
                if (countX % rowElements == 0)
                {
                    countX = 0;
                    countY++;
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
                Point3d textLoc = new Point3d(newLoc.X, newLoc.Y - 0.45 * sY * padY, 0);
                Point3d numLoc = new Point3d(newLoc.X, textLoc.Y + textSize * 2, 0);
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
                if (haveXData && coherencePattern.Branches[i][0] && zLockPattern.Branches[i][0])
                    for (int k = 0; k < 2; k++)
                        if (xData.PathExists(AOpairs.Paths[i].AppendElement(k)))
                            for (int j = 0; j < xData.Branch(AOpairs.Paths[i].AppendElement(k)).Count; j++)
                                xData.Branch(AOpairs.Paths[i].AppendElement(k))[j].Transform(move);
            }
            return AOpairs;
        }

        private void SetPreviewData(DataTree<AssemblyObject> Aopairs, DataTree<XData> xDataTree, bool srMode)
        {
            Mesh m;
            Color edgeColor, typeColor;
            DisplayMaterial typeMaterial;
            int typeIndex;

            for (int i = 0; i < Aopairs.BranchCount; i++)
                for (int j = 0; j < Aopairs.Branches[i].Count; j++)
                {
                    m = Aopairs.Branches[i][j].collisionMesh;
                    // improve preview
                    m.Unweld(0, true);
                    _clip = BoundingBox.Union(_clip, m.GetBoundingBox(false));

                    if (coherencePattern.Branches[i][0] && zLockPattern.Branches[i][0])
                    {
                        _mesh.Add(m);
                        typeIndex = AOpairs.Branches[i][j].type;
                        if (!haveXData) edgeColor = Color.Black;
                        else edgeColor = srMode ? srColorCatalog[j] : typeColorCatalog[typeIndex];
                        this.edgeColor.Add(edgeColor, new GH_Path(i));
                        typeColor = srMode ? srColorCatalog[j] : typeColorCatalog[typeIndex];
                        typeMaterial = srMode ? srMatCatalog[j] : typeMatCatalog[typeIndex];
                        _color.Add(typeColor);
                        _mat.Add(typeMaterial);
                        displayEdges.AddRange(geomEdges.Branch(Aopairs.Path(i).AppendElement(j)), new GH_Path(i, j));

                        // create item for rendered preview
                        GH_CustomPreviewItem item = default(GH_CustomPreviewItem);
                        item.Geometry = new GH_Mesh(m);
                        item.Shader = typeMaterial;
                        item.Colour = typeColor;
                        item.Material = new GH_Material(typeMaterial);
                        _items.Add(item);
                    }
                    else
                    {
                        if (!coherencePattern.Branches[i][0])
                            invalidDisplayEdges.AddRange(geomEdges.Branch(Aopairs.Path(i).AppendElement(j)), new GH_Path(i, j));
                        else
                            ZincompatibleDisplayEdges.AddRange(geomEdges.Branch(Aopairs.Path(i).AppendElement(j)), new GH_Path(i, j));
                    }

                }
            if (haveXData)
            {
                // Extrusions and Surfaces are detected as Breps
                XD_Breps = new List<Brep>();
                XD_Curves = new List<Curve>();
                XD_Meshes = new List<Mesh>();
                XD_Points = new List<Point3d>();

                for (int i = 0; i < xDataTree.BranchCount; i++)
                    if (xDataTree.Branches[i][0] != null)
                        for (int k = 0; k < xDataTree.Branches[i].Count; k++)
                            for (int j = 0; j < xDataTree.Branches[i][k].data.Count; j++)
                            {
                                // Point3d are the exception as they are a struct and a cast to GeometryBase is null
                                /*
                                 When they are referenced from Rhino and result as ReferencedPoint in GH, they can be cast
                                as GeometryBase - ObjectType Rhino.DocObjects.ObjectType.Point
                                If they appear as set of coordinates (as is the case of XData), they are Point3d (struct)
                                 */
                                if (xDataTree.Branches[i][k].data[j] is Point3d)
                                    XD_Points.Add((Point3d)xDataTree.Branches[i][k].data[j]);
                                else
                                {
                                    GeometryBase gb = xDataTree.Branches[i][k].data[j] as GeometryBase;

                                    if (gb != null)
                                    {
                                        _clip = BoundingBox.Union(_clip, gb.GetBoundingBox(false));

                                        // create item for rendered preview
                                        GH_CustomPreviewItem item = default(GH_CustomPreviewItem);

                                        //convert GeometryBase in the related object type
                                        switch (gb.ObjectType)
                                        {
                                            case Rhino.DocObjects.ObjectType.Brep:
                                                XD_Breps.Add(gb as Brep);
                                                item.Geometry = new GH_Brep(gb as Brep);
                                                break;
                                            case Rhino.DocObjects.ObjectType.Curve:
                                                XD_Curves.Add(gb as Curve);
                                                //item.Geometry = new GH_Curve(gb as Curve);
                                                break;
                                            case Rhino.DocObjects.ObjectType.Surface:
                                                XD_Breps.Add((gb as Surface).ToBrep());
                                                item.Geometry = new GH_Surface(gb as Surface);
                                                break;
                                            case Rhino.DocObjects.ObjectType.Mesh:
                                                XD_Meshes.Add(gb as Mesh);
                                                item.Geometry = new GH_Mesh(gb as Mesh);
                                                break;
                                            // for future implementation:
                                            //case Rhino.DocObjects.ObjectType.Extrusion:
                                            //    break;
                                            //case Rhino.DocObjects.ObjectType.PointSet:
                                            //    break;
                                            //case Rhino.DocObjects.ObjectType.SubD:
                                            //    XD_SubD.Add(gb as SubD); // prepare a list
                                            //    item.Geometry = new GH_SubD(gb as SubD);
                                            //    break;
                                            default:
                                                item.Geometry = null;
                                                break;
                                        }

                                        if (item.Geometry != null)
                                        {
                                            //                         AOPairs branch index    AOPairs object index
                                            typeIndex = AOpairs.Branch(xDataTree.Paths[i][0])[xDataTree.Paths[i][1]].type;
                                            typeColor = srMode ? srColorCatalog[xDataTree.Paths[i][1]] : typeColorCatalog[typeIndex];
                                            typeMaterial = srMode ? srMatCatalog[xDataTree.Paths[i][1]] : typeMatCatalog[typeIndex];
                                            item.Shader = typeMaterial;
                                            item.Colour = typeColor;
                                            item.Material = new GH_Material(typeMaterial);
                                            _XDitems.Add(item);
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
            _color = new List<Color>();
            _mat = new List<DisplayMaterial>();
            _items = new List<GH_CustomPreviewItem>();
            _XDitems = new List<GH_CustomPreviewItem>();
            // initialize global variables
            xData = new DataTree<XData>();
            geomEdges = new DataTree<GH_Line>();
            textLocations = new DataTree<Plane>();
            numberLocations = new DataTree<Plane>();
            bbCenters = new DataTree<Point3d>();
            rulesText = new DataTree<string>();
            coherencePattern = new DataTree<bool>();
            zLockPattern = new DataTree<bool>();
            displayEdges = new DataTree<GH_Line>();
            ZincompatibleDisplayEdges = new DataTree<GH_Line>();
            invalidDisplayEdges = new DataTree<GH_Line>();
            edgeColor = new DataTree<Color>();
        }

        public override void ClearData()
        {
            base.ClearData();
            _items = null;
            _XDitems = null;
        }

        public override bool IsBakeCapable
        {
            get { return _items.Count > 0; }
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
                if (_XDitems != null && !args.Document.IsRenderMeshPipelineViewport(args.Display))
                {
                    if (base.Attributes.Selected)
                    {
                        GH_PreviewMeshArgs args2 = new GH_PreviewMeshArgs(args.Viewport, args.Display, args.ShadeMaterial_Selected, args.MeshingParameters);
                        foreach (GH_CustomPreviewItem item in _XDitems)
                        {
                            item.Geometry.DrawViewportMeshes(args2);
                        }
                    }
                    else
                    {
                        foreach (GH_CustomPreviewItem item2 in _XDitems)
                        {
                            GH_PreviewMeshArgs args3 = new GH_PreviewMeshArgs(args.Viewport, args.Display, item2.Shader, args.MeshingParameters);
                            item2.Geometry.DrawViewportMeshes(args3);
                        }
                    }
                }
                else
                {
                    DisplayMaterial wh = new DisplayMaterial(Color.White);
                    foreach (Brep b in XD_Breps)
                        args.Display.DrawBrepShaded(b, wh);
                    foreach (Mesh m in XD_Meshes)
                        args.Display.DrawMeshShaded(m, wh);
                }
            }
            else
            {
                if (_items != null && !args.Document.IsRenderMeshPipelineViewport(args.Display))
                {
                    if (base.Attributes.Selected)
                    {
                        GH_PreviewMeshArgs args2 = new GH_PreviewMeshArgs(args.Viewport, args.Display, args.ShadeMaterial_Selected, args.MeshingParameters);
                        foreach (GH_CustomPreviewItem item in _items)
                        {
                            item.Geometry.DrawViewportMeshes(args2);
                        }
                    }
                    else
                    {
                        foreach (GH_CustomPreviewItem item2 in _items)
                        {
                            GH_PreviewMeshArgs args3 = new GH_PreviewMeshArgs(args.Viewport, args.Display, item2.Shader, args.MeshingParameters);
                            item2.Geometry.DrawViewportMeshes(args3);
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < _mesh.Count; i++)
                        args.Display.DrawMeshShaded(_mesh[i], _mat[i]);
                }
            }
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

                drawText = new Text3d(string.Format("{0}", i), numberLocations.Branches[i][0], textSize * 0.8);
                drawText.FontFace = "Lucida Console";
                drawText.HorizontalAlignment = Rhino.DocObjects.TextHorizontalAlignment.Center;
                drawText.VerticalAlignment = Rhino.DocObjects.TextVerticalAlignment.Bottom;
                args.Display.Draw3dText(drawText, Color.DimGray);
                drawText.Dispose();
            }

            if (haveXData || showEdges)
                for (int i = 0; i < edgeColor.BranchCount; i++)
                    for (int j = 0; j < edgeColor.Branches[i].Count; j++)
                        foreach (GH_Line edge in displayEdges.Branch(edgeColor.Path(i).AppendElement(j)))
                            args.Display.DrawLine(edge.Value, edgeColor.Branches[i][j], _width);

            // display invalid cases
            foreach (GH_Line edge in invalidDisplayEdges.AllData())
                args.Display.DrawLine(edge.Value, Color.FromArgb(50, 50, 50), _width * 2);
            // display Z-incompatible cases
            foreach (GH_Line edge in ZincompatibleDisplayEdges.AllData())
                args.Display.DrawLine(edge.Value, Color.FromArgb(50, 50, 250), _width * 2);

            if (haveXData)
            {
                foreach (Curve c in XD_Curves)
                    args.Display.DrawCurve(c, Color.YellowGreen, 3); //Alternatives: DeepPink, Crimson, LimeGreen
                foreach (Point3d p in XD_Points)
                    args.Display.DrawPoint(p, Color.DarkRed);
                if (showEdges)
                {
                    foreach (Brep b in XD_Breps)
                        args.Display.DrawBrepWires(b, Color.DarkSlateGray, 1);
                    foreach (Mesh m in XD_Meshes)
                        args.Display.DrawMeshWires(m, Color.DarkSlateGray, 1);
                }
            }
        }

        public override void BakeGeometry(Rhino.RhinoDoc doc, ObjectAttributes att, List<Guid> objectIds)
        {
            if (_items != null && _items.Count != 0)
            {
                if (att == null)
                {
                    att = doc.CreateDefaultAttributes();
                }
                foreach (GH_CustomPreviewItem item in _items)
                {
                    Guid guid = item.PushToRhinoDocument(doc, att);
                    if (guid != Guid.Empty)
                    {
                        objectIds.Add(guid);
                    }
                }
            }
        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            Menu_AppendSeparator(menu);
            ToolStripMenuItem toolStripMenuItem = Menu_AppendItem(menu, "AO Types", AOTypes_Click, true, GetValue("OutputType", "AO Types") == "AO Types");
            toolStripMenuItem.ToolTipText = "Color by AO type";
            ToolStripMenuItem toolStripMenuItem2 = Menu_AppendItem(menu, "S-R Status", SR_Click, true, GetValue("OutputType", "AO Types") == "S-R");
            toolStripMenuItem2.ToolTipText = "Color by Sender-Receiver status - receiver in blue";
            Menu_AppendSeparator(menu);
            ToolStripMenuItem toolStripMenuItem3 = Menu_AppendItem(menu, "Show Edges", Edges_Click, true, GetValue("ShowEdges", true));
            toolStripMenuItem3.ToolTipText = "Show or hide geometry edges";
            //if (GetValue("ShowEdges", true))
            //{
            //    Menu_AppendItem(menu, "Edge thickness in pixels:");
            //    Menu_AppendDigitScrollerItem(menu, 1, 10, 1, 0);
            //}
            Menu_AppendSeparator(menu);
            ToolStripMenuItem toolStripMenuItem4 = Menu_AppendItem(menu, "Check World Z lock", ZLock_Click, true, filterkWZLock);
            toolStripMenuItem4.ToolTipText = "Consider absolute Z-Lock (if active for AssemblyObjects)";
            Menu_AppendSeparator(menu);
        }

        private void AOTypes_Click(object sender, EventArgs e)
        {
            RecordUndoEvent("AO Types");
            SetValue("OutputType", "AO Types");
            displayType = GetValue("OutputType", "AO Types");
            UpdateMessage();
            ExpireSolution(true);
        }
        private void SR_Click(object sender, EventArgs e)
        {
            RecordUndoEvent("S-R Status");
            SetValue("OutputType", "S-R");
            displayType = GetValue("OutputType", "AO Types");
            UpdateMessage();
            ExpireSolution(true);
        }

        private void Edges_Click(object sender, EventArgs e)
        {
            RecordUndoEvent("Show Edges");
            bool newValue = !GetValue("ShowEdges", true);
            SetValue("ShowEdges", newValue);
            ExpireSolution(true);
        }

        private void ZLock_Click(object sender, EventArgs e)
        {
            RecordUndoEvent("Check World Z lock");
            filterkWZLock = !GetValue("ZLockFilter", false);
            SetValue("ZLockFilter", filterkWZLock);
            // set component message
            UpdateMessage();
            ExpireSolution(true);
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetBoolean("ZLockFilter", filterkWZLock);
            writer.SetString("DisplayType", displayType);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            reader.TryGetBoolean("ZLockFilter", ref filterkWZLock);
            reader.TryGetString("DisplayType", ref displayType);
            UpdateMessage();
            return base.Read(reader);
        }

        private void UpdateMessage()
        {
            Message = filterkWZLock ? displayType + "\nZ Lock" : displayType;
        }

        /// <summary>
        /// Exposure override for position in the Subcategory (options primary to septenary)
        /// https://apidocs.co/apps/grasshopper/6.8.18210/T_Grasshopper_Kernel_GH_Exposure.htm
        /// </summary>
        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.primary; }
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
                return Resources.Heuristics_Dispay;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("f653df39-e14a-42a6-b9dd-fbc83cb95e61"); }
        }
    }
}
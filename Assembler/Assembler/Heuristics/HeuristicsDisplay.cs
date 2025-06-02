using Assembler.Properties;
using Assembler.Utils;
using AssemblerLib;
using AssemblerLib.Utils;
using GH_IO.Serialization;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Components;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Assembler
{
    // TODO: this one needs some refactoring (pushing it to future versions)
    public class HeuristicsDisplay : GH_CustomPreviewComponent
    {
        private bool filterkWZLock, _showEdges, _showHandles, _haveXData;
        private string displayType;
        private double _textSize;
        private readonly double _textRatio = 0.025;
        private readonly double _textShift = 0.45;
        private readonly int _width = 1;
        private BoundingBox _boundingBox;

        private List<GH_CustomPreviewItem> _items, _XDitems;
        private List<XData> _xDCatalog;
        private List<Color> _color;
        private List<DisplayMaterial> _mat;
        private List<Point3d> _XDPoints;
        private List<Curve> _XDCurves;
        private List<Brep> _XDBreps;
        private List<Mesh> _XDMeshes;
        private Color[] _typeColorCatalog, _srColorCatalog;
        private DisplayMaterial[] _typeMatCatalog, _srMatCatalog;
        private GH_Line[][] _edgeCatalog;

        private DataTree<AssemblyObject> AOpairs;
        private DataTree<XData> xData;
        private DataTree<GH_Line> geomEdges;
        private DataTree<GH_Line> displayEdges;
        private DataTree<GH_Line> invalidDisplayEdges;
        private DataTree<GH_Line> ZincompatibleDisplayEdges;
        private DataTree<Color> edgeColor;
        private DataTree<Plane> gridPlanes;
        private DataTree<Plane> textLocations;
        private DataTree<Plane> numberLocations;
        private List<ConstructionPlane> handleLocations;
        private DataTree<Point3d> bbCenters;
        private DataTree<string> rulesText;
        private DataTree<bool> coherencePattern;
        private DataTree<bool> zLockPattern;

        public override BoundingBox ClippingBox => _boundingBox;
        public override bool IsBakeCapable => _items.Count > 0;

        /// <summary>
        /// Initializes a new instance of the HeuristicDisplay class.
        /// </summary>
        public HeuristicsDisplay()
          : base()
        {
            Name = "Heuristic Display";
            NickName = "HeuD";
            Description = "Display Heuristics as visual combination of AssemblyObjects";
            Category = "Assembler";
            SubCategory = "Heuristics";

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
            pManager.AddPlaneParameter("Origin Plane", "P", "Origin Plane for Display", GH_ParamAccess.item, Plane.WorldXY);
            pManager.AddPlaneParameter("Orientation Plane", "Op", "Orientation Plane for AO Display\nLeave empty to use the origin plane", GH_ParamAccess.item, Plane.Unset);
            pManager.AddGenericParameter("AssemblyObjects Set", "AOs", "List of Assembly Objects in the set", GH_ParamAccess.list);
            pManager.AddGenericParameter("XData", "XD", "Xdata associated with the AssemblyObject in the catalog", GH_ParamAccess.list);
            pManager.AddTextParameter("Heuristics Set", "HeS", "Heuristics Set", GH_ParamAccess.list);
            pManager.AddNumberParameter("X size", "Xs", "Cell size along Origin Plane X direction as % of Bounding Box", GH_ParamAccess.item, 1.2);
            pManager.AddNumberParameter("Y size", "Ys", "Cell size along Origin Plane Y direction as % of Bounding Box", GH_ParamAccess.item, 1.2);
            pManager.AddNumberParameter("Text size", "Ts", "Text size as % of default size\nDefault size is computed proportionally to geometry size", GH_ParamAccess.item, 1.0);
            pManager.AddIntegerParameter("n. Rows", "nR", "number of rows", GH_ParamAccess.item, 10);
            pManager.AddColourParameter("Colors", "C", "Colors (OPTIONAL)\n2 colors for Sender-Receiver display mode (receiver first)\nOne color for component type for Display by type", GH_ParamAccess.list);

            pManager[3].Optional = true; // XData is optional
            pManager[9].Optional = true; // Colors are optional
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Coherence Pattern", "cP", "Pattern of valid/invalid combinations", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<AssemblyObjectGoo> GH_AOs = new List<AssemblyObjectGoo>();
            List<AssemblyObject> AOSet = new List<AssemblyObject>();
            List<Color> InputColors = new List<Color>();
            List<Color> TypeColors = new List<Color>();

            // sanity check on mandatory inputs
            if (!DA.GetDataList("AssemblyObjects Set", GH_AOs)) return;

            AOSet = GH_AOs.Where(a => a != null).Select(ao => ao.Value).ToList();

            if (AOSet.Count == 0)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please provide at least one valid AssemblyObject");

            List<string> HeS = new List<string>();
            if (!DA.GetDataList("Heuristics Set", HeS)) return;

            // get XData catalog
            _xDCatalog = new List<XData>();
            DA.GetDataList("XData", _xDCatalog);

            // flag for XData existence
            _haveXData = (_xDCatalog != null) && (_xDCatalog.Count > 0);

            // get the remaining inputs
            Plane P = new Plane();
            Plane OP = Plane.Unset;
            double xS = double.NaN;
            double yS = double.NaN;
            double tS = double.NaN;
            int nR = 1;

            DA.GetData("Origin Plane", ref P);
            DA.GetData("Orientation Plane", ref OP);
            DA.GetData("X size", ref xS);
            DA.GetData("Y size", ref yS);
            DA.GetData("Text size", ref tS);
            DA.GetData("n. Rows", ref nR);

            // If the OP input is invalid use Origin Plane
            if (!OP.IsValid) OP = P;

            if (!DA.GetDataList("Colors", InputColors))
            {
                // if there's no Color input build TypeColor from default palette
                if (AOSet.Count <= Constants.AOTypePalette.Length)
                    for (int i = 0; i < AOSet.Count; i++)
                        TypeColors.Add(Constants.AOTypePalette[i]);
                else
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "The number of AssemblyObjects exceeds the internal palette length - " +
                        "Switching to random colors\n");
                    Random rand = new Random(0);
                    for (int i = 0; i < AOSet.Count; i++)
                        TypeColors.Add(Color.FromKnownColor(Constants.KnownColorList[rand.Next(0, Constants.KnownColorList.Count - 1)]));
                }
            }

            if (InputColors.Count > 0)
            {
                // check input Colors sanity if in AO Types mode
                if (InputColors.Count < AOSet.Count && GetValue("OutputType", "AO Types") == "AO Types")
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please provide one color for each AssemblyObject in the set");
                    return;
                }
                TypeColors = InputColors;
            }

            // update message (otherwise component does not show message when loading a file containing it)
            UpdateMessage();

            // build Component catalog
            Dictionary<string, int> _AOcatalog = AssemblageUtils.BuildDictionary(AOSet.ToArray());

            // build Rules List
            List<Rule> HeR = RuleUtils.HeuristicsRulesFromString(AOSet, _AOcatalog, HeS);

            // build edge catalog
            _edgeCatalog = new GH_Line[AOSet.Count][];
            for (int i = 0; i < _edgeCatalog.Length; i++)
                _edgeCatalog[i] = MeshUtils.GetSilhouette(AOSet[i].CollisionMesh);

            // build Colors and materials catalogs
            _typeColorCatalog = TypeColors.ToArray();
            if (InputColors.Count > 1)
                _srColorCatalog = new Color[] { InputColors[0], InputColors[1] };
            else
                _srColorCatalog = Constants.SRPalette;

            _typeMatCatalog = CompileMatCatalog(_typeColorCatalog);
            _srMatCatalog = CompileMatCatalog(_srColorCatalog);

            _showEdges = GetValue("ShowEdges", true);
            _showHandles = GetValue("ShowHandles", false);
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

            AOpairs = GeneratePairs(AOSet, HeR, P, OP, xS, yS, tS, nR);
            SetPreviewData(AOpairs, xData, srMode);
            GH_Structure<GH_Boolean> coherencePattern_GH = DataUtils.ToGHBooleanTree(coherencePattern);

            DA.SetDataTree(0, coherencePattern_GH);
        }

        DisplayMaterial[] CompileMatCatalog(Color[] colorTable)
        {
            DisplayMaterial[] materialTable = new DisplayMaterial[colorTable.Length];

            for (int i = 0; i < colorTable.Length; i++)
                materialTable[i] = new DisplayMaterial(colorTable[i], (255 - colorTable[i].A) / 255.0);

            return materialTable;

        }

        private DataTree<AssemblyObject> GeneratePairs(List<AssemblyObject> AOSet, List<Rule> heuristicsRules, Plane basePlane, Plane orientPlane, double padX, double padY, double textSizeMult, int nRows)
        {
            DataTree<AssemblyObject> AOpairs = new DataTree<AssemblyObject>();

            AssemblyObject senderAO, receiverAO;
            List<XData> XDsenderGeometry, XDreceiverGeometry;
            List<GH_Line> senderEdges, receiverEdges;

            double sizeX = 0, sizeY = 0, sizeZ = 0, maxSize = 0;

            Rule rule;
            Plane locationPlane;
            Transform orientS_to_R;

            int countY = 0, countX = 0;
            int totalRules = heuristicsRules.Count;
            int rowElements = (int)Math.Ceiling(totalRules / (double)nRows);
            bool valid, zLockChecked;

            GH_Path rulePath;
            DataTree<(int, int)> HandleRSIndexes = new DataTree<(int, int)>();


            // 1st loop
            // . orient S/R AOs, establish rule coherence & compute max dimensions
            // . fill grid indexes as grid (countX, countY)

            for (int i = 0; i < heuristicsRules.Count; i++)
            {
                XDsenderGeometry = new List<XData>();
                XDreceiverGeometry = new List<XData>();
                rulePath = new GH_Path(i);

                // extract rule
                rule = heuristicsRules[i];

                // record R & S Handle indexes
                HandleRSIndexes.Add((rule.rH, rule.sH), rulePath);

                // define point grid locations (countX, countY)
                locationPlane = basePlane;
                locationPlane.Origin = basePlane.PointAt(countX, countY, 0);

                // choose sender & receiver AO from catalog
                receiverAO = AssemblyObjectUtils.Clone(AOSet[rule.rT]);
                senderAO = AssemblyObjectUtils.Clone(AOSet[rule.sT]);

                // copy edges from catalog
                receiverEdges = _edgeCatalog[rule.rT].Select(l => new GH_Line(l)).ToList();
                senderEdges = _edgeCatalog[rule.sT].Select(l => new GH_Line(l)).ToList();

                // generate transformation orient: sender to receiver
                orientS_to_R = Transform.PlaneToPlane(senderAO.Handles[rule.sH].SenderPlane, receiverAO.Handles[rule.rH].ReceiverPlanes[rule.rR]);

                // orient sender AssemblyObject
                senderAO.Transform(orientS_to_R);

                // orient edges
                for (int j = 0; j < senderEdges.Count; j++)
                    senderEdges[j].Transform(orientS_to_R);

                // calculate Bounding Box and compare size to initial parameters
                BoundingBox bb = receiverAO.CollisionMesh.GetBoundingBox(false);
                bb.Union(senderAO.CollisionMesh.GetBoundingBox(false));

                // record center point of AO combination
                bbCenters.Add(bb.Center, rulePath);

                // retain largest dimensions (for grid final size)
                sizeX = Math.Max(sizeX, bb.Diagonal.X);
                sizeY = Math.Max(sizeY, bb.Diagonal.Y);
                sizeZ = Math.Max(sizeZ, bb.Diagonal.Z);

                maxSize = Math.Max(sizeZ, Math.Max(sizeX, sizeY));

                // add AssemblyObjects to DataTree - same path of heuristic rule
                AOpairs.Add(receiverAO, rulePath);
                AOpairs.Add(senderAO, rulePath);

                // edges need one more index for sender/receiver identification (0 receiver, 1 sender)
                geomEdges.AddRange(receiverEdges, rulePath.AppendElement(0));// new GH_Path(i, 0));
                geomEdges.AddRange(senderEdges, rulePath.AppendElement(1));//new GH_Path(i, 1));

                // add point grid location to tree
                gridPlanes.Add(locationPlane, rulePath);

                // fill coherence pattern &, if valid, orient extra geometry
                valid = !AssemblageUtils.IsAOCollidingWithAnother(receiverAO, senderAO);
                // check for Z orientation lock
                // if sender is NOT oriented as World Z rule is considered Z-Lock incompatible (but not invalid)
                zLockChecked = true;
                if (filterkWZLock && senderAO.WorldZLock)
                    zLockChecked = AssemblyObjectUtils.AbsoluteZCheck(senderAO, Constants.RhinoAbsoluteTolerance);

                zLockPattern.Add(zLockChecked, rulePath);
                coherencePattern.Add(valid, rulePath);

                // rewrite rule for display and add to the text Tree
                string ruleText = heuristicsRules[i].ToString();
                rulesText.Add(ruleText, rulePath);

                // calculate next grid position
                countX++;
                if (countX % rowElements == 0)
                {
                    countX = 0;
                    countY++;
                }
            } // end of 1st loop

            // calculate _textSize
            _textSize = sizeX * _textRatio * textSizeMult;

            // define origin point transformation
            Transform translateAndScale = Transform.Multiply(Transform.Translation(0, 0, sizeZ * 0.5), Transform.Scale(basePlane, sizeX * padX, sizeY * padY, 0));

            // define local variables
            Plane AOPlane, textPlane, numberPlane;
            ConstructionPlane cp;
            Point3d AOLoc, textLoc, numLoc, originLoc;
            int rHindex, sHindex;

            // TODO: this could be parallelized (moving the local variables above inside the for loop - requires no DataTrees!!)
            // 2nd loop
            // . orient on final planes
            // . if coherent, orient XData using XDataUtils.AssociateXDataToAO (does also orientation)
            for (int i = 0; i < AOpairs.BranchCount; i++)
            {
                // compute final plane position
                originLoc = gridPlanes.Branches[i][0].Origin;
                originLoc.Transform(translateAndScale);

                AOPlane = orientPlane;
                textPlane = basePlane;
                numberPlane = basePlane;
                AOPlane.Origin = originLoc;

                AOLoc = AOPlane.Origin;
                textLoc = AOLoc - basePlane.YAxis * _textShift * sizeY * padY;
                textLoc -= basePlane.ZAxis * sizeZ * 0.5;
                numLoc = textLoc + basePlane.YAxis * _textSize * 2;
                textPlane.Origin = textLoc;
                numberPlane.Origin = numLoc;

                // record final text position
                textLocations.Add(textPlane, AOpairs.Path(i));
                numberLocations.Add(numberPlane, AOpairs.Path(i));

                // define AO transformation
                Plane from = Plane.WorldXY;
                from.Origin = bbCenters.Branches[i][0];
                Transform orientAO = Transform.PlaneToPlane(from, AOPlane);

                // transfer AssemblyObjects
                foreach (AssemblyObject ao in AOpairs.Branches[i])
                    ao.Transform(orientAO);

                //transfer edges
                for (int k = 0; k < 2; k++)
                    foreach (GH_Line l in geomEdges.Branch(AOpairs.Path(i).AppendElement(k)))
                        l.Transform(orientAO);

                // get SR Handle planes
                (rHindex, sHindex) = HandleRSIndexes.Branches[i][0];

                //receiver
                cp = new ConstructionPlane
                {
                    Plane = AOpairs.Branches[i][0].Handles[rHindex].SenderPlane,
                    ShowGrid = false,
                    ShowZAxis = true,
                    GridSpacing = maxSize * 0.3,
                    GridLineCount = 1
                };
                handleLocations.Add(cp);
                // sender
                cp = new ConstructionPlane
                {
                    Plane = AOpairs.Branches[i][1].Handles[sHindex].SenderPlane,
                    ShowGrid = true,
                    ShowZAxis = false,
                    GridSpacing = maxSize * 0.15,
                    GridLineCount = 1,
                    ThickLineColor = Constants.SRPalette[1],
                    ThickLineFrequency = 1
                };
                handleLocations.Add(cp);

                // associate extra geometries
                if (_haveXData && coherencePattern.Branches[i][0] && zLockPattern.Branches[i][0])
                {
                    // receiver XData
                    List<XData> recXData = XDataUtils.AssociateXDataToAO(AOpairs.Branches[i][0], _xDCatalog);
                    if (recXData.Count > 0)
                        xData.AddRange(recXData, AOpairs.Paths[i].AppendElement(0));
                    // sender XData
                    List<XData> senXData = XDataUtils.AssociateXDataToAO(AOpairs.Branches[i][1], _xDCatalog);
                    if (senXData.Count > 0)
                        xData.AddRange(senXData, AOpairs.Paths[i].AppendElement(1));
                }
            }

            return AOpairs;
        }


        private void SetPreviewData(DataTree<AssemblyObject> Aopairs, DataTree<XData> xDataTree, bool srMode)
        {
            Mesh mesh;
            Color edgeColor, typeColor;
            DisplayMaterial typeMaterial;
            int typeIndex;

            for (int i = 0; i < Aopairs.BranchCount; i++)
                for (int j = 0; j < Aopairs.Branches[i].Count; j++)
                {
                    mesh = Aopairs.Branches[i][j].CollisionMesh;
                    // improve preview
                    mesh.Unweld(0, true);

                    _boundingBox.Union(mesh.GetBoundingBox(false));

                    if (coherencePattern.Branches[i][0] && zLockPattern.Branches[i][0])
                    {
                        typeIndex = AOpairs.Branches[i][j].Type;
                        if (!_haveXData) edgeColor = Color.Black;
                        else edgeColor = srMode ? _srColorCatalog[j] : _typeColorCatalog[typeIndex];
                        this.edgeColor.Add(edgeColor, new GH_Path(i));
                        typeColor = srMode ? _srColorCatalog[j] : _typeColorCatalog[typeIndex];
                        typeMaterial = srMode ? _srMatCatalog[j] : _typeMatCatalog[typeIndex];
                        _color.Add(typeColor);
                        _mat.Add(typeMaterial);
                        displayEdges.AddRange(geomEdges.Branch(Aopairs.Path(i).AppendElement(j)), new GH_Path(i, j));

                        // create XDitem for rendered preview
                        GH_CustomPreviewItem item = default(GH_CustomPreviewItem);
                        GH_Mesh gMesh = new GH_Mesh(mesh);
                        GH_Material GHmat = new GH_Material(typeMaterial);
                        item.Geometry = gMesh;
                        item.Shader = GHmat.Value;
                        item.Colour = typeColor;
                        item.Material = GHmat;
                        _items.Add(item);
                        _boundingBox.Union(gMesh.Boundingbox);
                    }
                    else
                    {
                        if (!coherencePattern.Branches[i][0])
                            invalidDisplayEdges.AddRange(geomEdges.Branch(Aopairs.Path(i).AppendElement(j)), new GH_Path(i, j));
                        else
                            ZincompatibleDisplayEdges.AddRange(geomEdges.Branch(Aopairs.Path(i).AppendElement(j)), new GH_Path(i, j));
                    }

                }
            if (_haveXData)
            {
                // Extrusions and Surfaces are detected as Breps
                for (int i = 0; i < xDataTree.BranchCount; i++)
                {
                    if (xDataTree.Branches[i][0] == null) continue;
                    for (int j = 0; j < xDataTree.Branches[i].Count; j++)
                        for (int k = 0; k < xDataTree.Branches[i][j].Data.Count; k++)
                        {
                            // Point3d Vector3d, Line & Plane are the exception as they are a struct and a cast to GeometryBase is null
                            /*
                             When they are referenced from Rhino and result as ReferencedPoint in GH, they can be cast
                            as GeometryBase - ObjectType Rhino.DocObjects.ObjectType.Point
                            If they appear as set of coordinates (as is the case of XData), they are Point3d (struct)
                             */
                            if (xDataTree.Branches[i][j].Data[k] is Point3d pd)
                                _XDPoints.Add(pd);
                            //_XDPoints.Add((Point3d)xDataTree.Branches[i][k].Data[j]);
                            else if (xDataTree.Branches[i][j].Data[k] is Line ld)
                                _XDCurves.Add(ld.ToNurbsCurve());
                            else if (xDataTree.Branches[i][j].Data[k] is GeometryBase gb)
                            {
                                _boundingBox = BoundingBox.Union(_boundingBox, gb.GetBoundingBox(false));

                                // create XDitem for rendered preview
                                GH_CustomPreviewItem XDitem = default(GH_CustomPreviewItem);

                                //convert GeometryBase in the related object type
                                switch (gb.ObjectType)
                                {
                                    case ObjectType.Brep:
                                        _XDBreps.Add(gb as Brep);
                                        XDitem.Geometry = new GH_Brep(gb as Brep);
                                        break;
                                    case ObjectType.Curve:
                                        _XDCurves.Add(gb as Curve);
                                        //XDitem.Geometry = new GH_Curve(gb as Curve);
                                        break;
                                    case ObjectType.Surface:
                                        _XDBreps.Add((gb as Surface).ToBrep());
                                        XDitem.Geometry = new GH_Surface(gb as Surface);
                                        break;
                                    case ObjectType.Mesh:
                                        _XDMeshes.Add(gb as Mesh);
                                        XDitem.Geometry = new GH_Mesh(gb as Mesh);
                                        break;
                                    // for future implementation:
                                    //case Rhino.DocObjects.ObjectType.Extrusion:
                                    //    break;
                                    //case Rhino.DocObjects.ObjectType.PointSet:
                                    //    break;
                                    //case Rhino.DocObjects.ObjectType.SubD:
                                    //    XD_SubD.Add(gb as SubD); // prepare a list
                                    //    XDitem.Geometry = new GH_SubD(gb as SubD);
                                    //    break;
                                    default:
                                        XDitem.Geometry = null;
                                        break;
                                }

                                if (XDitem.Geometry != null)
                                {
                                    //                         AOPairs branch index    AOPairs object index
                                    typeIndex = AOpairs.Branch(xDataTree.Paths[i][0])[xDataTree.Paths[i][1]].Type;
                                    typeColor = srMode ? _srColorCatalog[xDataTree.Paths[i][1]] : _typeColorCatalog[typeIndex];
                                    typeMaterial = srMode ? _srMatCatalog[xDataTree.Paths[i][1]] : _typeMatCatalog[typeIndex];
                                    XDitem.Shader = typeMaterial;
                                    XDitem.Colour = typeColor;
                                    XDitem.Material = new GH_Material(typeMaterial);
                                    _XDitems.Add(XDitem);
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
            _boundingBox = BoundingBox.Empty;
            _color = new List<Color>();
            _mat = new List<DisplayMaterial>();
            _items = new List<GH_CustomPreviewItem>();
            _XDitems = new List<GH_CustomPreviewItem>();
            _XDBreps = new List<Brep>();
            _XDCurves = new List<Curve>();
            _XDMeshes = new List<Mesh>();
            _XDPoints = new List<Point3d>();
            // initialize global variables
            xData = new DataTree<XData>();
            geomEdges = new DataTree<GH_Line>();
            gridPlanes = new DataTree<Plane>();
            textLocations = new DataTree<Plane>();
            numberLocations = new DataTree<Plane>();
            handleLocations = new List<ConstructionPlane>();
            bbCenters = new DataTree<Point3d>();
            rulesText = new DataTree<string>();
            coherencePattern = new DataTree<bool>();
            zLockPattern = new DataTree<bool>();
            displayEdges = new DataTree<GH_Line>();
            ZincompatibleDisplayEdges = new DataTree<GH_Line>();
            invalidDisplayEdges = new DataTree<GH_Line>();
            edgeColor = new DataTree<Color>();
            // BUG: this prevents an error on Rhino 8
            base.BeforeSolveInstance();
        }

        protected override void AfterSolveInstance()
        {
            base.AfterSolveInstance();
        }

        public override void ClearData()
        {
            base.ClearData();
        }

        [Obsolete]
        public override void AppendRenderGeometry(GH_RenderArgs args)
        {

            if (!_haveXData)
            {
                if (_items != null && _items.Count != 0)
                    foreach (GH_CustomPreviewItem item in _items)
                        item.PushToRenderPipeline(args);
            }
            else if (_XDitems != null && _XDitems.Count != 0)
                foreach (GH_CustomPreviewItem XDitem in _XDitems)
                    XDitem.PushToRenderPipeline(args);
        }

        //Draw all meshes in this method.
        public override void DrawViewportMeshes(IGH_PreviewArgs args)
        {
            // in theory DrawViewportMeshes should not run in Rendered mode when AppendRenderGeometry exists...
            // BUT: no preview in rendered Rhino 8 with this statement without version check
            // wait for Rhino 8 SR7 and test what happens when removing version check on this statement
            if (RhinoApp.ExeVersion < 8 && args.Document.IsRenderMeshPipelineViewport(args.Display)) return;

            if (_haveXData)
            {
                if (_XDitems != null)
                {
                    if (this.Attributes.Selected)
                    {
                        GH_PreviewMeshArgs args2 = new GH_PreviewMeshArgs(args.Viewport, args.Display, args.ShadeMaterial_Selected, args.MeshingParameters);
                        foreach (GH_CustomPreviewItem item in _XDitems)
                            item.Geometry.DrawViewportMeshes(args2);
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
            }
            else
            {
                if (_items != null)
                {
                    if (this.Attributes.Selected)
                    {
                        GH_PreviewMeshArgs args2 = new GH_PreviewMeshArgs(args.Viewport, args.Display, args.ShadeMaterial_Selected, args.MeshingParameters);
                        foreach (GH_CustomPreviewItem item in _items)
                            item.Geometry.DrawViewportMeshes(args2);
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
            }
        }

        //Draw all wires and points in this method.
        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            // handles planes
            if(_showHandles)
            foreach (ConstructionPlane hp in handleLocations)
                args.Display.DrawConstructionPlane(hp);

            // text
            Text3d drawText;
            for (int i = 0; i < rulesText.BranchCount; i++)
            {
                drawText = new Text3d(rulesText.Branches[i][0], textLocations.Branches[i][0], _textSize)
                {
                    FontFace = "Lucida Console",
                    HorizontalAlignment = TextHorizontalAlignment.Center,
                    VerticalAlignment = TextVerticalAlignment.Top
                };
                args.Display.Draw3dText(drawText, Color.Black);
                drawText.Dispose();

                drawText = new Text3d(string.Format("{0}", i), numberLocations.Branches[i][0], _textSize * 0.8)
                {
                    FontFace = "Lucida Console",
                    HorizontalAlignment = TextHorizontalAlignment.Center,
                    VerticalAlignment = TextVerticalAlignment.Bottom
                };
                args.Display.Draw3dText(drawText, Color.DimGray);
                drawText.Dispose();
            }

            if (_haveXData || _showEdges)
                for (int i = 0; i < edgeColor.BranchCount; i++)
                    for (int j = 0; j < edgeColor.Branches[i].Count; j++)
                        foreach (GH_Line edge in displayEdges.Branch(edgeColor.Path(i).AppendElement(j)))
                            args.Display.DrawLine(edge.Value, edgeColor.Branches[i][j], _width);

            // display invalid cases
            foreach (GH_Line edge in invalidDisplayEdges.AllData())
                args.Display.DrawLine(edge.Value, Color.FromArgb(50, 50, 50), _width * 3);
            // display Z-incompatible cases
            foreach (GH_Line edge in ZincompatibleDisplayEdges.AllData())
                args.Display.DrawLine(edge.Value, Color.FromArgb(50, 50, 250), _width * 2);

            if (_haveXData)
            {
                foreach (Curve c in _XDCurves)
                    args.Display.DrawCurve(c, Color.YellowGreen, 3); //Alternatives: DeepPink, Crimson, LimeGreen
                foreach (Point3d p in _XDPoints)
                    args.Display.DrawPoint(p, Color.DarkRed);
                if (_showEdges)
                {
                    foreach (Brep b in _XDBreps)
                        args.Display.DrawBrepWires(b, Color.DarkSlateGray, 1);
                    foreach (Mesh m in _XDMeshes)
                        args.Display.DrawMeshWires(m, Color.DarkSlateGray, 1);
                }
            }
        }

        public override void BakeGeometry(Rhino.RhinoDoc doc, ObjectAttributes att, List<Guid> objectIds)
        {
            string HeuristicDisplayLayerName = "HD Geometry";
            string CollisionLayerName = "HD_CollisionVolumes", XDataLayerName = "HD_XDataGeometry",
                TextLayerName = "HD_Rules", InvalidLayerName = "HD_Invalid",
                CurvesLayerName = "HD_XDCurves", PointsLayerName = "HD_XDPoints";
            Layer HDparent;

            HDparent = new Layer
            {
                Name = HeuristicDisplayLayerName,
                Color = Color.Black
            };

            if (doc.Layers.FindByFullPath(HDparent.Name, -1) == -1)
            {
                doc.Layers.Add(HDparent);
            }

            HDparent = doc.Layers.FindName(HDparent.Name, RhinoMath.UnsetIntIndex);

            if (_items == null || _items.Count == 0) return;

            if (att == null)
            {
                att = doc.CreateDefaultAttributes();
            }

            if (_XDitems != null && _XDitems.Count != 0)
                att.SetDisplayModeOverride(DisplayModeDescription.FindByName("Wireframe"));

            att.LayerIndex = CreateChildLayer(doc, HDparent, CollisionLayerName, Color.FromArgb(0, 255, 0));

            foreach (GH_CustomPreviewItem item in _items)
            {
                att.ColorSource = ObjectColorSource.ColorFromObject;
                att.ObjectColor = item.Material.Value.Diffuse;
                Guid guid = item.PushToRhinoDocument(doc, att);
                if (guid != Guid.Empty)
                    objectIds.Add(guid);
            }

            // text
            Text3d drawText;
            att = doc.CreateDefaultAttributes();
            att.LayerIndex = CreateChildLayer(doc, HDparent, TextLayerName, Color.DimGray);

            for (int i = 0; i < rulesText.BranchCount; i++)
            {
                drawText = new Text3d(rulesText.Branches[i][0], textLocations.Branches[i][0], _textSize)
                {
                    FontFace = "Lucida Console",
                    HorizontalAlignment = TextHorizontalAlignment.Center,
                    VerticalAlignment = TextVerticalAlignment.Top
                };
                doc.Objects.AddText(drawText, att);
                drawText.Dispose();

                drawText = new Text3d(string.Format("{0}", i), numberLocations.Branches[i][0], _textSize * 0.8)
                {
                    FontFace = "Lucida Console",
                    HorizontalAlignment = TextHorizontalAlignment.Center,
                    VerticalAlignment = TextVerticalAlignment.Bottom
                };
                doc.Objects.AddText(drawText, att);
                drawText.Dispose();
            }

            // invalid cases
            if (invalidDisplayEdges != null && invalidDisplayEdges.BranchCount > 0)
            {
                att.LayerIndex = CreateChildLayer(doc, HDparent, InvalidLayerName, Color.Black);

                foreach (GH_Line edge in invalidDisplayEdges.AllData())
                    doc.Objects.AddLine(edge.Value, att);
            }

            // XData
            if (_XDitems == null || _XDitems.Count == 0) return;

            att.LayerIndex = CreateChildLayer(doc, HDparent, XDataLayerName, Color.Black);

            foreach (GH_CustomPreviewItem XDitem in _XDitems)
            {
                att.ColorSource = ObjectColorSource.ColorFromObject;
                att.ObjectColor = XDitem.Material.Value.Diffuse;
                Guid guid = XDitem.PushToRhinoDocument(doc, att);
                if (guid != Guid.Empty)
                    objectIds.Add(guid);
            }

            // curves & points
            att.ColorSource = ObjectColorSource.ColorFromLayer;
            att.LayerIndex = CreateChildLayer(doc, HDparent, CurvesLayerName, Color.YellowGreen);
            foreach (Curve c in _XDCurves)
                doc.Objects.AddCurve(c, att);

            att.LayerIndex = CreateChildLayer(doc, HDparent, PointsLayerName, Color.DarkRed);
            foreach (Point3d p in _XDPoints)
                doc.Objects.AddPoint(p, att);
        }

        private int CreateChildLayer(RhinoDoc doc, Layer parent, string childName, Color childColor)
        {
            Layer child;

            int childIndex = doc.Layers.FindByFullPath(childName, -1);

            if (childIndex == -1)
            {
                child = new Layer
                {
                    Name = childName,
                    Color = childColor, // Color.Black;
                    ParentLayerId = parent.Id
                };
                doc.Layers.Add(child);
            }

            child = doc.Layers.FindName(childName, RhinoMath.UnsetIntIndex);
            childName = parent.Name + "::" + child.Name;
            return doc.Layers.FindByFullPath(childName, -1);
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            Menu_AppendSeparator(menu);
            ToolStripMenuItem toolStripMenuItem = Menu_AppendItem(menu, "AO Types", AOTypes_Click, true, GetValue("OutputType", "AO Types") == "AO Types");
            toolStripMenuItem.ToolTipText = "Color by AO type";
            ToolStripMenuItem toolStripMenuItem2 = Menu_AppendItem(menu, "S-R Status", SR_Click, true, GetValue("OutputType", "AO Types") == "S-R");
            toolStripMenuItem2.ToolTipText = "Color by Sender-Receiver status - receiver in blue";
            Menu_AppendSeparator(menu);
            ToolStripMenuItem toolStripMenuItem3 = Menu_AppendItem(menu, "Show Edges", Edges_Click, true, GetValue("ShowEdges", true));
            toolStripMenuItem3.ToolTipText = "Show or hide geometry edges";
            ToolStripMenuItem toolStripMenuItem4 = Menu_AppendItem(menu, "Show Handles", Handles_Click, true, GetValue("ShowHandles", false));
            toolStripMenuItem4.ToolTipText = "Show or hide connection Handles";
            // BUG: I can't seem to make this work, no matter what
            //if (GetValue("ShowEdges", true))
            //{
            //    Menu_AppendItem(menu, "Edge thickness in pixels:");
            //    Menu_AppendDigitScrollerItem(menu, 1m, 10m, 1m, 0);
            //}
            Menu_AppendSeparator(menu);
            ToolStripMenuItem toolStripMenuItem5 = Menu_AppendItem(menu, "Check World Z lock", ZLock_Click, true, filterkWZLock);
            toolStripMenuItem5.ToolTipText = "Consider absolute Z-Lock (if active for AssemblyObjects)";
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
            UpdateMessage();
            ExpireSolution(true);
        }
        private void Handles_Click(object sender, EventArgs e)
        {
            RecordUndoEvent("Show Handles");
            bool newValue = !GetValue("ShowHandles", false);
            SetValue("ShowHandles", newValue);
            UpdateMessage();
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
                return Resources.Heuristics_Display;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("FE2DB723-A9DB-42DA-8EE2-0F253D4A67C6"); }
        }
    }
}
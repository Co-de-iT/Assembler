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
    [Obsolete]
    public class L_HeuristicsDisplay : GH_CustomPreviewComponent
    {
        private bool filterkWZLock, _showEdges, _haveXData;
        private string displayType;
        private double _textSize;
        private readonly double _textRatio = 0.025;
        private readonly double _textShift = 0.45;
        private readonly int _width = 1;
        private BoundingBox _boundingBox;

        private List<GH_CustomPreviewItem> _items, _XDitems;
        private List<XData> _xDCatalog;
        //private List<Mesh> _mesh;
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
        private DataTree<Plane> gridLocations;
        private DataTree<Plane> textLocations;
        private DataTree<Plane> numberLocations;
        private DataTree<Point3d> bbCenters;
        private DataTree<string> rulesText;
        private DataTree<bool> coherencePattern;
        private DataTree<bool> zLockPattern;

        public override BoundingBox ClippingBox => _boundingBox;
        public override bool IsBakeCapable => _items.Count > 0;

        /// <summary>
        /// Initializes a new instance of the HeuristicDisplay class.
        /// </summary>
        public L_HeuristicsDisplay()
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
            pManager.AddPointParameter("Origin Point", "P", "Origin Point for Display", GH_ParamAccess.item, new Point3d());
            pManager.AddGenericParameter("AssemblyObjects Set", "AOs", "List of Assembly Objects in the set", GH_ParamAccess.list);
            pManager.AddGenericParameter("XData", "XD", "Xdata associated with the AssemblyObject in the catalog", GH_ParamAccess.list);
            pManager.AddTextParameter("Heuristics Set", "HeS", "Heuristics Set", GH_ParamAccess.list);
            pManager.AddNumberParameter("X size", "Xs", "Cell size along X direction as % of Bounding Box", GH_ParamAccess.item, 1.2);
            pManager.AddNumberParameter("Y size", "Ys", "Cell size along Y direction as % of Bounding Box", GH_ParamAccess.item, 1.2);
            pManager.AddNumberParameter("Text size", "Ts", "Text size as % of default size\nDefault size is computed proportionally to geometry size", GH_ParamAccess.item, 1.0);
            pManager.AddIntegerParameter("n. Rows", "nR", "number of rows", GH_ParamAccess.item, 10);
            pManager.AddColourParameter("Colors", "C", "Colors (OPTIONAL)\n2 colors for Sender-Receiver display mode (receiver first)\nOne color for component type for Display by type", GH_ParamAccess.list);

            pManager[2].Optional = true; // XData is optional
            pManager[8].Optional = true; // Colors are optional
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
            List<AssemblyObject> AOSet = new List<AssemblyObject>();
            List<Color> InputColors = new List<Color>();
            List<Color> TypeColors = new List<Color>();
            bool edges = GetValue("ShowEdges", true);

            // sanity check on mandatory inputs
            if (!DA.GetDataList("AssemblyObjects Set", GH_AOs)) return;

            AOSet = GH_AOs.Where(a => a != null).Select(ao => ao.Value).ToList();

            if (AOSet.Count == 0)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please provide at least one valid AssemblyObject");

            List<string> HeS = new List<string>();
            if (!DA.GetDataList("Heuristics Set", HeS)) return;

            // get XData catalog
            _xDCatalog = new List<XData>();
            DA.GetDataList(2, _xDCatalog);
            // flag for XData existence
            _haveXData = (_xDCatalog != null) && (_xDCatalog.Count > 0);

            // get the remaining inputs
            Point3d P = new Point3d();
            double xS = double.NaN;
            double yS = double.NaN;
            double tS = double.NaN;
            int nR = 1;

            DA.GetData("Origin Point", ref P);
            DA.GetData("X size", ref xS);
            DA.GetData("Y size", ref yS);
            DA.GetData("Text size", ref tS);
            DA.GetData("n. Rows", ref nR);

            if (!DA.GetDataList("Colors", InputColors))
            {
                // if there's no Color input build TypeColor from default palette
                if (AOSet.Count <= Constants.AOTypePalette.Length)
                    for (int i = 0; i < AOSet.Count; i++)
                        TypeColors.Add(Constants.AOTypePalette[i]);
                else
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "You are using an exceptionally large set of AssemblyObjects - " +
                        "Switching to random palette\n" +
                        "Have you considered reducing the number of objects in your set?");
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
            Dictionary <string, int>_AOcatalog = AssemblageUtils.BuildDictionary(AOSet.ToArray());

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

            _showEdges = edges;
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

            AOpairs = GeneratePairs(AOSet, HeR, P, xS, yS, tS, nR);
            SetPreviewData(AOpairs, xData, srMode);
        }

        DisplayMaterial[] CompileMatCatalog(Color[] colorTable)
        {
            DisplayMaterial[] materialTable = new DisplayMaterial[colorTable.Length];

            for (int i = 0; i < colorTable.Length; i++)
                materialTable[i] = new DisplayMaterial(colorTable[i], (255 - colorTable[i].A) / 255.0);

            return materialTable;

        }

        private DataTree<AssemblyObject> GeneratePairs(List<AssemblyObject> AOSet, List<Rule> heuristicsRules, Point3d origin, double padX, double padY, double textSizeMult, int nRows)
        {
            DataTree<AssemblyObject> AOpairs = new DataTree<AssemblyObject>();
            AssemblyObject senderAO, receiverAO;
            List<XData> XDsenderGeometry, XDreceiverGeometry;
            List<GH_Line> senderEdges, receiverEdges;

            double sizeX = 0, sizeY = 0, sizeZ = 0;

            Rule rule;
            Plane locationPlane;

            int countY = 0, countX = 0;
            int totalRules = heuristicsRules.Count;
            int rowElements = (int)Math.Ceiling(totalRules / (double)nRows);

            /*
             TO DO:
            - use a base plane instead of point
            1.1 record plane origin grid positions
            1.4 Bounding Box might have to be plane-oriented (maybe not), or use the Receiver's reference plane
            Do not orient now, wait for later (second loop)
             */

            // sequence
            // / / / loop 1:
            // 1.1 record point grid position (countX, countY)
            // 1.2 create copies of AOs
            // 1.3 orient sender to receiver according to rule and add to geometries Tree
            // 1.4 calculate Bounding Box size and compare to sX, sY, sZ (retain maximum)
            // 1.5 calculate consistency (collisions)
            // 1.5.1 if consistent, orient extra geometry (sender to receiver)

            for (int i = 0; i < heuristicsRules.Count; i++)
            {

                XDsenderGeometry = new List<XData>();
                XDreceiverGeometry = new List<XData>();

                // extract rule
                rule = heuristicsRules[i];

                // define location as point grid position (countX, countY)
                locationPlane = Plane.WorldXY;
                locationPlane.Origin = new Point3d(countX, countY, 0);

                // choose sender & receiver AO from catalog
                receiverAO = AssemblyObjectUtils.Clone(AOSet[rule.rT]);
                senderAO = AssemblyObjectUtils.Clone(AOSet[rule.sT]);

                // copy edges
                receiverEdges = new List<GH_Line>();
                senderEdges = new List<GH_Line>();
                receiverEdges = _edgeCatalog[rule.rT].Select(l => new GH_Line(l)).ToList();
                senderEdges = _edgeCatalog[rule.sT].Select(l => new GH_Line(l)).ToList();

                // generate transformation orient: sender to receiver
                Transform orientStoR = Transform.PlaneToPlane(senderAO.Handles[rule.sH].SenderPlane, receiverAO.Handles[rule.rH].ReceiverPlanes[rule.rR]);

                // orient sender AssemblyObject
                senderAO.Transform(orientStoR);

                // orient edges
                for (int j = 0; j < senderEdges.Count; j++)
                    senderEdges[j].Transform(orientStoR);

                // copy and orient XData if present
                if (_haveXData)
                {
                    for (int j = 0; j < _xDCatalog.Count; j++)
                    {
                        // receiver XData
                        if (String.Equals(_xDCatalog[j].AOName, receiverAO.Name))
                            XDreceiverGeometry.Add(new XData(_xDCatalog[j]));
                        // sender XData
                        if (String.Equals(_xDCatalog[j].AOName, senderAO.Name))
                        {
                            XData XDsenderGeomTemp = new XData(_xDCatalog[j]);
                            XDsenderGeomTemp.Transform(orientStoR);
                            XDsenderGeometry.Add(XDsenderGeomTemp);
                        }
                    }
                }
                // calculate Bounding Box and compare size to initial parameters
                BoundingBox bb = receiverAO.CollisionMesh.GetBoundingBox(false);
                bb.Union(senderAO.CollisionMesh.GetBoundingBox(false));

                // record center point of AO combination
                bbCenters.Add(bb.Center, new GH_Path(i));

                // retain largest dimensions (for grid final size)
                sizeX = Math.Max(sizeX, bb.Diagonal.X);
                sizeY = Math.Max(sizeY, bb.Diagonal.Y);
                sizeZ = Math.Max(sizeZ, bb.Diagonal.Z);

                // add AssemblyObjects to DataTree - same path of heuristic rule
                AOpairs.Add(receiverAO, new GH_Path(i));
                AOpairs.Add(senderAO, new GH_Path(i));
                // edges need one more index for sender/receiver identification (0 receiver, 1 sender)
                geomEdges.AddRange(receiverEdges, new GH_Path(i, 0));
                geomEdges.AddRange(senderEdges, new GH_Path(i, 1));

                // add point grid location to tree
                gridLocations.Add(locationPlane, new GH_Path(i));
                //textLocations.Add(locationPlane, new GH_Path(i));

                // fill coherence pattern & orient extra geometry only if valid combination
                bool valid = !AssemblageUtils.IsAOCollidingWithAnother(receiverAO, senderAO);
                // check for Z orientation lock
                // if sender is NOT oriented as World Z rule is considered Z-Lock incompatible (but not invalid)
                bool zLockChecked = true;
                if (filterkWZLock && senderAO.WorldZLock)
                    zLockChecked = AssemblyObjectUtils.AbsoluteZCheck(senderAO, Constants.RhinoAbsoluteTolerance);

                zLockPattern.Add(zLockChecked, new GH_Path(i));
                coherencePattern.Add(valid, new GH_Path(i));

                // display XData only if valid AND zLockChecked
                if (valid && zLockChecked && _haveXData)
                {
                    // extra geometries need one more index for sender/receiver identification (0 receiver, 1 sender)
                    if (XDreceiverGeometry.Count > 0) xData.AddRange(XDreceiverGeometry, new GH_Path(i, 0));
                    if (XDsenderGeometry.Count > 0) xData.AddRange(XDsenderGeometry, new GH_Path(i, 1));
                }

                // rewrite rule for display and add to the text Tree
                string ruleText = heuristicsRules[i].ToString();
                rulesText.Add(ruleText, new GH_Path(i));

                // calculate next grid position
                countX++;
                if (countX % rowElements == 0)
                {
                    countX = 0;
                    countY++;
                }
            }

            // calculate _textSize
            _textSize = sizeX * _textRatio * textSizeMult;

            /*
             TO-DO
            2.1 scale along ref plane axes
            2.2 orient instead of moving, using a custom provided orientation plane (or the input reference plane if no custom plane is provided)
             */

            // / / / loop 2:
            // 2.1 scale point grid positions by final sizeX, sizeY, sizeZ
            // 2.2 move all elements (AO geometries, edges, extra geometries) in position
            for (int i = 0; i < AOpairs.BranchCount; i++)
            {
                Point3d AOLoc = gridLocations.Branches[i][0].Origin;
                //Point3d AOLoc = textLocations.Branches[i][0].Origin;
                AOLoc.X = origin.X + (AOLoc.X + 0.5) * sizeX * padX;
                AOLoc.Y = origin.Y + (AOLoc.Y + 0.5) * sizeY * padY;
                AOLoc.Z = origin.Z + sizeZ * 0.5;
                Point3d textLoc = new Point3d(AOLoc.X, AOLoc.Y - _textShift * sizeY * padY, origin.Z);
                Point3d numLoc = new Point3d(AOLoc.X, textLoc.Y + _textSize * 2, origin.Z);
                Plane textPlane = Plane.WorldXY;
                Plane numberPlane = Plane.WorldXY;
                textPlane.Origin = textLoc;
                numberPlane.Origin = numLoc;

                // record final text position
                //textLocations.Branches[i][0] = textPlane;
                textLocations.Add(textPlane, new GH_Path(i));
                numberLocations.Add(numberPlane, new GH_Path(i));

                // define transformation
                Transform moveToAOLoc = Transform.Translation(AOLoc - bbCenters.Branches[i][0]);

                // transfer AssemblyObjects
                foreach (AssemblyObject ao in AOpairs.Branches[i])
                    ao.Transform(moveToAOLoc);

                //transfer edges
                for (int k = 0; k < 2; k++)
                    foreach (GH_Line l in geomEdges.Branch(AOpairs.Path(i).AppendElement(k)))
                        l.Transform(moveToAOLoc);

                // transfer extra geometries
                if (_haveXData && coherencePattern.Branches[i][0] && zLockPattern.Branches[i][0])
                    for (int k = 0; k < 2; k++)
                        if (xData.PathExists(AOpairs.Paths[i].AppendElement(k)))
                            for (int j = 0; j < xData.Branch(AOpairs.Paths[i].AppendElement(k)).Count; j++)
                                xData.Branch(AOpairs.Paths[i].AppendElement(k))[j].Transform(moveToAOLoc);
            }
            return AOpairs;
        }

        private DataTree<AssemblyObject> MoveToGridPosition(DataTree<AssemblyObject> AOpairs, Point3d origin,
            double sizeX, double sizeY, double sizeZ, double padX, double padY)
        {
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
                    m = Aopairs.Branches[i][j].CollisionMesh;
                    // improve preview
                    m.Unweld(0, true);

                    _boundingBox.Union(m.GetBoundingBox(false));

                    if (coherencePattern.Branches[i][0] && zLockPattern.Branches[i][0])
                    {
                        //_mesh.Add(m);
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
                        GH_Mesh gm = new GH_Mesh(m);
                        GH_Material GHmat = new GH_Material(typeMaterial);
                        item.Geometry = gm;
                        item.Shader = GHmat.Value;// typeMaterial;
                        item.Colour = typeColor;
                        item.Material = GHmat;// new GH_Material(typeMaterial);
                        _items.Add(item);
                        _boundingBox.Union(gm.Boundingbox);
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
            //_mesh = new List<Mesh>();
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
            gridLocations = new DataTree<Plane>();
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
            // this prevents an error on Rhino 8 - NOT ANYMORE!!!
            base.BeforeSolveInstance();
        }

        protected override void AfterSolveInstance()
        {
            base.AfterSolveInstance();
        }

        public override void ClearData()
        {
            base.ClearData();
            //_items = null;
            //_XDitems = null;
        }

        [Obsolete]
        public override void AppendRenderGeometry(GH_RenderArgs args)
        {
            //GH_Document gH_Document = OnPingDocument();
            //if (gH_Document != null && (gH_Document.PreviewMode == GH_PreviewMode.Disabled || gH_Document.PreviewMode == GH_PreviewMode.Wireframe))
            //{
            //    return;
            //}

            //List<GH_CustomPreviewItem> XDitems = _XDitems;

            if (!_haveXData)
            {
                if (_items != null && _items.Count != 0)
                {
                    foreach (GH_CustomPreviewItem item in _items)
                        item.PushToRenderPipeline(args);
                }
            }
            else if (_XDitems != null && _XDitems.Count != 0)
            {
                foreach (GH_CustomPreviewItem XDitem in _XDitems)
                    XDitem.PushToRenderPipeline(args);

            }

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
                //else
                //{
                //    DisplayMaterial wh = new DisplayMaterial(Color.White);
                //    foreach (Brep b in _XDBreps)
                //        args.Display.DrawBrepShaded(b, wh);
                //    foreach (Mesh m in _XDMeshes)
                //        args.Display.DrawMeshShaded(m, wh);
                //}
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
            // text
            Text3d drawText;
            for (int i = 0; i < rulesText.BranchCount; i++)
            {
                drawText = new Text3d(rulesText.Branches[i][0], textLocations.Branches[i][0], _textSize);
                drawText.FontFace = "Lucida Console";
                drawText.HorizontalAlignment = TextHorizontalAlignment.Center;
                drawText.VerticalAlignment = TextVerticalAlignment.Top;
                args.Display.Draw3dText(drawText, Color.Black);
                drawText.Dispose();

                drawText = new Text3d(string.Format("{0}", i), numberLocations.Branches[i][0], _textSize * 0.8);
                drawText.FontFace = "Lucida Console";
                drawText.HorizontalAlignment = TextHorizontalAlignment.Center;
                drawText.VerticalAlignment = TextVerticalAlignment.Bottom;
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

            HDparent = new Layer();
            HDparent.Name = HeuristicDisplayLayerName;
            HDparent.Color = Color.Black;

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
                {
                    objectIds.Add(guid);
                }
            }

            // text
            Text3d drawText;
            att = doc.CreateDefaultAttributes();
            att.LayerIndex = CreateChildLayer(doc, HDparent, TextLayerName, Color.DimGray);

            for (int i = 0; i < rulesText.BranchCount; i++)
            {
                drawText = new Text3d(rulesText.Branches[i][0], textLocations.Branches[i][0], _textSize);
                drawText.FontFace = "Lucida Console";
                drawText.HorizontalAlignment = TextHorizontalAlignment.Center;
                drawText.VerticalAlignment = TextVerticalAlignment.Top;
                doc.Objects.AddText(drawText, att);
                drawText.Dispose();

                drawText = new Text3d(string.Format("{0}", i), numberLocations.Branches[i][0], _textSize * 0.8);
                drawText.FontFace = "Lucida Console";
                drawText.HorizontalAlignment = TextHorizontalAlignment.Center;
                drawText.VerticalAlignment = TextVerticalAlignment.Bottom;
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
                {
                    objectIds.Add(guid);
                }
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
                child = new Layer();
                child.Name = childName;
                child.Color = childColor; // Color.Black;
                child.ParentLayerId = parent.Id;
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
            //if (GetValue("ShowEdges", true))
            //{
            //    Menu_AppendItem(menu, "Edge thickness in pixels:");
            //    Menu_AppendDigitScrollerItem(menu, 1m, 10m, 1m, 0);
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
            get { return GH_Exposure.hidden; }
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
                return Resources.L_Heuristics_Display;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("55875EBA-05FA-4F28-915B-F83C8B1A67ED"); }
        }
    }
}
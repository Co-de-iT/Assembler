using Assembler.Properties;
using Assembler.Utils;
using AssemblerLib;
using AssemblerLib.Utils;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Assembler
{
    public class HeuristicsDisplayEx : GH_Component
    {

        private Dictionary<string, int> AOcatalog;
        DataTree<AssemblyObject> AOpairs;
        DataTree<AssemblyObjectGoo> AOoutput;
        private DataTree<Plane> gridPlanes;
        DataTree<Plane> textLocations;
        //DataTree<Plane> numberLocations;
        DataTree<Point3d> bbCenters;
        DataTree<bool> coherencePattern;
        private readonly double _textShift = 0.45;

        /// <summary>
        /// Initializes a new instance of the HeuristicsDisplayEx class.
        /// </summary>
        public HeuristicsDisplayEx()
          : base("Heuristics Display Ex", "HeuDeX",
              "Display Heuristics as visual combination of AssemblyObjects - extended version",
              "Assembler", "Heuristics")
        {
            // this hides the component preview when placed onto the canvas
            // source: http://frasergreenroyd.com/how-to-stop-components-from-automatically-displaying-results-in-grasshopper/
            IGH_PreviewObject prevObj = (IGH_PreviewObject)this;
            prevObj.Hidden = true;
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddPlaneParameter("Origin Plane", "P", "Origin Plane for Display", GH_ParamAccess.item, Plane.WorldXY);
            pManager.AddGenericParameter("AssemblyObjects Set", "AOs", "List of Assembly Objects in the set", GH_ParamAccess.list);
            pManager.AddTextParameter("Heuristics Set", "HeS", "Heuristics Set", GH_ParamAccess.list);
            pManager.AddNumberParameter("X size", "Xs", "Cell size along X direction as % of Bounding Box", GH_ParamAccess.item, 1.2);
            pManager.AddNumberParameter("Y size", "Ys", "Cell size along Y direction as % of Bounding Box", GH_ParamAccess.item, 1.2);
            pManager.AddIntegerParameter("n. Rows", "nR", "number of rows", GH_ParamAccess.item, 10);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("AssemblyObject pairs", "AO", "Receiver/Sender pairs", GH_ParamAccess.tree);
            pManager.AddTextParameter("Heuristics Set Tree", "HeT", "Heuristics rules Set Tree", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Coherence Pattern", "cP", "Pattern of valid/invalid combinations", GH_ParamAccess.tree);
            pManager.AddPlaneParameter("Text base plane", "tP", "Locations for text placement", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<AssemblyObjectGoo> GH_AOs = new List<AssemblyObjectGoo>();
            List<AssemblyObject> AOset = new List<AssemblyObject>();

            // sanity check on mandatory inputs
            if (!DA.GetDataList("AssemblyObjects Set", GH_AOs)) return;

            AOset = GH_AOs.Where(a => a != null).Select(ao => ao.Value).ToList();

            if (AOset.Count == 0)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please provide at least one valid AssemblyObject");

            List<string> HeS = new List<string>();
            if (!DA.GetDataList("Heuristics Set", HeS)) return;

            // get the rest of inputs
            Plane P = new Plane();
            double xS = double.NaN;
            double yS = double.NaN;
            int nR = 1;
            DA.GetData("Origin Plane", ref P);
            DA.GetData("X size", ref xS);
            DA.GetData("Y size", ref yS);
            DA.GetData("n. Rows", ref nR);

            // cast input to AssemblyObject type
            AssemblyObject[] components = AOset.ToArray();

            // build Component catalog
            AOcatalog = AssemblageUtils.BuildDictionary(components);

            // build Heuristics Tree
            // heuristics string in tree format for manual selection in GH
            DataTree<string> HeSTree = new DataTree<string>();
            for (int i = 0; i < HeS.Count; i++)
                HeSTree.Add(HeS[i], new GH_Path(DA.Iteration, i));

            // build Rules list
            List<Rule> HeR = RuleUtils.HeuristicsRulesFromString(AOset, AOcatalog, HeS);

            AOpairs = GeneratePairs(components, HeR, P, xS, yS, nR, DA.Iteration);
            AOoutput = AOsToGoo(AOpairs);
            GH_Structure<GH_Boolean> coherencePattern_GH = DataUtils.ToGHBooleanTree(coherencePattern);
            GH_Structure<GH_String> HeSTree_GH = DataUtils.ToGHStringTree(HeSTree);
            GH_Structure<GH_Plane> textLocations_GH = DataUtils.ToGHPlaneTree(textLocations);

            // output data
            DA.SetDataTree(0, AOoutput);
            DA.SetDataTree(1, HeSTree_GH);
            DA.SetDataTree(2, coherencePattern_GH);
            DA.SetDataTree(3, textLocations_GH);
        }

        private DataTree<AssemblyObject> GeneratePairs(AssemblyObject[] AOSet, List<Rule> heuristicsRules, Plane basePlane, double padX, double padY, int nRows, int iteration)
        {
            DataTree<AssemblyObject> AOpairs = new DataTree<AssemblyObject>();
            AssemblyObject senderAO, receiverAO;

            gridPlanes = new DataTree<Plane>();
            textLocations = new DataTree<Plane>();
            //numberLocations = new DataTree<Plane>();
            bbCenters = new DataTree<Point3d>();
            coherencePattern = new DataTree<bool>();

            double sizeX = 0, sizeY = 0, sizeZ = 0;

            Rule rule;
            Plane locationPlane;
            Transform orientS_to_R;

            int countY = 0, countX = 0;
            int total = heuristicsRules.Count;
            int rowElements = (int)Math.Ceiling(total / (double)nRows);
            bool valid;

            // sequence
            // loop 1:
            // record point grid position (countX, countY)
            // create copies of AOs
            // orient sender to receiver according to rule and add to geometries Tree
            // calculate Bounding Box size and compare to sX, sY, sZ (retain maximum)
            // calculate consistency (collisions)
            // if consistent, orient extra geometry (sender to receiver)

            for (int i = 0; i < heuristicsRules.Count; i++)
            {

                // extract rule
                rule = heuristicsRules[i];

                // define point grid locations (countX, countY)
                locationPlane = basePlane;
                locationPlane.Origin = basePlane.PointAt(countX, countY, 0);

                // choose choose sender & receiver AO from catalog
                receiverAO = AssemblyObjectUtils.Clone(AOSet[rule.rT]);
                senderAO = AssemblyObjectUtils.Clone(AOSet[rule.sT]);

                // generate transformation orient: sender to receiver
                orientS_to_R = Transform.PlaneToPlane(senderAO.Handles[rule.sH].SenderPlane, receiverAO.Handles[rule.rH].ReceiverPlanes[rule.rR]);

                // orient sender AssemblyObject
                senderAO.Transform(orientS_to_R);

                // calculate Bounding Box and compare size to initial parameters
                BoundingBox bb = receiverAO.CollisionMesh.GetBoundingBox(false);
                bb.Union(senderAO.CollisionMesh.GetBoundingBox(false));

                // record center plane of AO combination
                bbCenters.Add(bb.Center, new GH_Path(i));

                // retain largest dimensions (for grid final size)
                sizeX = Math.Max(sizeX, bb.Diagonal.X);
                sizeY = Math.Max(sizeY, bb.Diagonal.Y);
                sizeZ = Math.Max(sizeZ, bb.Diagonal.Z);

                // add AssemblyObjects to DataTree - same path of heuristic + one more index for rotations
                AOpairs.Add(receiverAO, new GH_Path(i));
                AOpairs.Add(senderAO, new GH_Path(i));

                // add point grid location to tree
                gridPlanes.Add(locationPlane, new GH_Path(i));

                // fill coherence pattern & orient extra geometry only if valid combination
                valid = !AssemblageUtils.IsAOCollidingWithAnother(receiverAO, senderAO);
                coherencePattern.Add(valid, new GH_Path(iteration, i));

                // calculate next grid position
                countX++;
                if (countX % rowElements == 0)
                {
                    countX = 0;
                    countY++;
                }
            }

            // define origin point transformation
            Transform translateAndScale = Transform.Multiply(Transform.Translation(0, 0, sizeZ * 0.5), Transform.Scale(basePlane, sizeX * padX, sizeY * padY, 0));

            // define local variables
            Plane AOPlane, textPlane;//, numberPlane;
            Point3d AOLoc, textLoc, originLoc; //, numLoc;

            // loop2:
            // scale point grid positions by final sX, sY, sZ
            // move all elements (AO geometries, edges, extra geometries) in position
            for (int i = 0; i < AOpairs.BranchCount; i++)
            {
                // compute final plane position
                originLoc = gridPlanes.Branches[i][0].Origin;
                originLoc.Transform(translateAndScale);

                AOPlane = basePlane;
                textPlane = basePlane;
                //numberPlane = basePlane;
                AOPlane.Origin = originLoc;

                AOLoc = AOPlane.Origin;
                textLoc = AOLoc - basePlane.YAxis * _textShift * sizeY * padY;
                textLoc -= basePlane.ZAxis * sizeZ * 0.5;
                //numLoc = textLoc + basePlane.YAxis * _textSize * 2;
                textPlane.Origin = textLoc;
                //numberPlane.Origin = numLoc;

                // record final text position
                textLocations.Add(textPlane, new GH_Path(i));
                //numberLocations.Add(numberPlane, new GH_Path(i));

                // define AO transformation
                Plane from = Plane.WorldXY;
                from.Origin = bbCenters.Branches[i][0];
                Transform orientAO = Transform.PlaneToPlane(from, AOPlane);

                // transfer AssemblyObjects
                foreach (AssemblyObject ao in AOpairs.Branches[i])
                    ao.Transform(orientAO);

            }
            return AOpairs;
        }

        private DataTree<AssemblyObjectGoo> AOsToGoo(DataTree<AssemblyObject> AOtree)
        {
            DataTree<AssemblyObjectGoo> AOGooTree = new DataTree<AssemblyObjectGoo>();

            for (int i = 0; i < AOtree.BranchCount; i++)
            {
                for (int j = 0; j < AOtree.Branches[i].Count; j++)
                {
                    AOGooTree.Add(new AssemblyObjectGoo(AOtree.Branches[i][j]), AOtree.Paths[i]);
                }
            }

            return AOGooTree;
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
                return Resources.Heuristics_Display_X;
            }
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
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("C42BFDAF-3632-47B1-9910-F5A27711838F"); }
        }
    }
}
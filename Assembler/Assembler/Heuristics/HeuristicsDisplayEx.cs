using Assembler.Properties;
using Assembler.Utils;
using AssemblerLib;
using AssemblerLib.Utils;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Assembler
{
    public class HeuristicsDisplayEx : GH_Component
    {

        private Dictionary<string, int> catalog;
        DataTree<AssemblyObject> AOpairs;
        DataTree<AssemblyObjectGoo> AOoutput;
        DataTree<Plane> textLocations;
        DataTree<Plane> numberLocations;
        DataTree<Point3d> bbCenters;
        DataTree<bool> coherencePattern;

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
            pManager.AddPointParameter("Origin Point", "P", "Origin Point for Display", GH_ParamAccess.item, new Point3d());
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
            pManager.AddPointParameter("Text base points", "tP", "Locations for text placement", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<AssemblyObjectGoo> GH_AOs = new List<AssemblyObjectGoo>();
            List<AssemblyObject> AOs = new List<AssemblyObject>();

            // sanity check on inputs
            if (!DA.GetDataList("AssemblyObjects Set", GH_AOs)) return;

            AOs = GH_AOs.Where(a => a != null).Select(ao => ao.Value).ToList();

            if (AOs.Count == 0)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please provide at least one valid AssemblyObject");

            List<string> HeS = new List<string>();
            if (!DA.GetDataList("Heuristics Set", HeS)) return;

            // get the rest of inputs
            Point3d P = new Point3d();
            double xS = double.NaN;
            double yS = double.NaN;
            int nR = 1;
            DA.GetData("Origin Point", ref P);
            DA.GetData("X size", ref xS);
            DA.GetData("Y size", ref yS);
            DA.GetData("n. Rows", ref nR);

            // cast input to AssemblyObject type
            AssemblyObject[] components = AOs.ToArray();

            // build Component catalog
            catalog = AssemblageUtils.BuildDictionary(components);

            // build Heuristics Tree
            // heuristics string in tree format for manual selection in GH
            DataTree<string> HeSTree = new DataTree<string>();
            for (int i = 0; i < HeS.Count; i++)
                HeSTree.Add(HeS[i], new GH_Path(i));

            // build Rules list
            List<Rule> HeR = RuleUtils.HeuristicsRulesFromString(AOs, catalog, HeS);

            AOpairs = GeneratePairs(components, HeR, P, xS, yS, nR);
            AOoutput = AOsToGoo(AOpairs);

            // output data
            DA.SetDataTree(0, AOoutput);
            DA.SetDataTree(1, HeSTree);
            DA.SetDataTree(2, coherencePattern);
            DA.SetDataTree(3, textLocations);
        }

        private DataTree<AssemblyObject> GeneratePairs(AssemblyObject[] AO, List<Rule> Hr, Point3d O, double padX, double padY, int nR)
        {
            DataTree<AssemblyObject> AOpairs = new DataTree<AssemblyObject>();
            AssemblyObject sender, receiver;

            textLocations = new DataTree<Plane>();
            numberLocations = new DataTree<Plane>();
            bbCenters = new DataTree<Point3d>();
            coherencePattern = new DataTree<bool>();

            double sX = 0, sY = 0, sZ = 0;

            int rT, rH, sT, sH, rR;
            Plane loc;

            int countY = 0, countX = 0;
            int total = Hr.Count;
            // int rowElements = (total / nR) + 1;
            int rowElements = (int)Math.Ceiling(total / (double)nR);

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

                // extract rule parameters
                rH = Hr[i].rH;
                sT = Hr[i].sT;
                sH = Hr[i].sH;
                rR = Hr[i].rR;

                // define location as point grid position (countX, countY)
                loc = Plane.WorldXY;
                loc.Origin = new Point3d(countX, countY, 0);

                // choose components
                receiver = AssemblyObjectUtils.Clone(AO[rT]);
                sender = AssemblyObjectUtils.Clone(AO[sT]);

                // generate transformation orient: sender to receiver
                Transform orient = Transform.PlaneToPlane(sender.Handles[sH].Sender, receiver.Handles[rH].Receivers[rR]);

                // orient sender AssemblyObject
                sender.Transform(orient);

                // calculate Bounding Box and compare size to initial parameters
                BoundingBox bb = receiver.CollisionMesh.GetBoundingBox(false);
                bb.Union(sender.CollisionMesh.GetBoundingBox(false));

                // record center plane of AO combination
                bbCenters.Add(bb.Center, new GH_Path(i));

                // retain largest dimensions (for grid final size)
                sX = Math.Max(sX, bb.Diagonal.X);
                sY = Math.Max(sY, bb.Diagonal.Y);
                sZ = Math.Max(sZ, bb.Diagonal.Z);

                // add AssemblyObjects to DataTree - same path of heuristic + one more index for rotations
                AOpairs.Add(receiver, new GH_Path(i));
                AOpairs.Add(sender, new GH_Path(i));

                // add point grid location to tree
                textLocations.Add(loc, new GH_Path(i));

                // fill coherence pattern & orient extra geometry only if valid combination
                bool valid = !AssemblageUtils.CollisionCheckPair(receiver, sender);
                coherencePattern.Add(valid, new GH_Path(i));

                // calculate next grid position
                countX++;
                if (countX % rowElements == 0)
                {
                    countX = 0;
                    countY++;
                }
            }

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
                return Resources.Heuristics_Dispay_X;
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
            get { return new Guid("617948af-0e27-468b-a60b-4399e49a2fd9"); }
        }
    }
}
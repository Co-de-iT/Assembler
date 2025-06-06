﻿using System;
using System.Collections.Generic;
using System.Linq;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using AssemblerLib;
using Assembler.Properties;
using AssemblerLib.Utils;

namespace Assembler
{
    [Obsolete]
    public class L2_HeuristicsDisplayEx : GH_Component
    {
        bool haveXData;
        private Dictionary<string, int> catalog;
        private List<XData> xDCatalog;
        DataTree<AssemblyObject> AOpairs;
        DataTree<XData> xData;
        DataTree<Plane> textLocations;
        DataTree<Plane> numberLocations;
        DataTree<Point3d> bbCenters;
        //DataTree<string> rulesText;
        DataTree<bool> coherencePattern;

        /// <summary>
        /// Initializes a new instance of the HeuristicsDisplayEx class.
        /// </summary>
        public L2_HeuristicsDisplayEx()
          : base("Heuristics Display Ex", "HeuDeX",
              "Display Heuristics as visual combination of AssemblyObjects - extended version\nLEGACY component",
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
            pManager[2].Optional = true; // XData is optional
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("AssemblyObject pairs", "AO", "Receiver/Sender pairs", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Coherence Pattern", "cP", "Pattern of valid/invalid combinations", GH_ParamAccess.tree);
            pManager.AddTextParameter("Heuristics String Tree", "HeT", "Heuristics rules string tree", GH_ParamAccess.tree);
            pManager.AddPointParameter("Text base points", "tP", "Locations for text placement", GH_ParamAccess.tree);
            pManager.AddGenericParameter("XData", "XD", "Xdata associated with the AssemblyObject in the catalog", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "This is an obsolete component - replace it with a new version from the Ribbon");

            List<AssemblyObject> AOs = new List<AssemblyObject>();

            // sanity check on inputs
            if (!DA.GetDataList("AssemblyObjects Set", AOs)) return;

            List<string> HeS = new List<string>();
            if (!DA.GetDataList("Heuristics String", HeS)) return;

            // get XData catalog
            xDCatalog = new List<XData>();
            DA.GetDataList(2, xDCatalog);

            // flag for XData existence
            haveXData = (xDCatalog != null) && (xDCatalog.Count > 0);
            xData = new DataTree<XData>();

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

            // Build Component catalog
            catalog = AssemblageUtils.BuildDictionary(components);

            // Build Heuristics Tree
            DataTree<string> HeSTree; // heuristics string in tree format for manual selection in GH
            DataTree<Rule> HeR = HeuristicsRulesFromString(AOs, HeS, out HeSTree);

            AOpairs = GeneratePairs(components, HeR, P, xS, yS, nR);

            // output data
            DA.SetDataTree(0, AOpairs);
            DA.SetDataTree(1, coherencePattern);
            DA.SetDataTree(2, HeSTree);
            DA.SetDataTree(3, textLocations);
            DA.SetDataTree(4, xData);
        }

        DataTree<Rule> HeuristicsRulesFromString(List<AssemblyObject> AOset, List<string> heu, out DataTree<string> heS)
        {
            DataTree<Rule> heuT = new DataTree<Rule>();
            heS = new DataTree<string>();

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
                //rR = Convert.ToInt32(rRot[1]);
                rRA = Convert.ToDouble(rRot[1]);
                rR = AOset[rT].Handles[rH].RDictionary[rRA]; // using rotations

                heuT.Add(new Rule(rec[0], rT, rH, rR, rRA, sen[0], sT, sH, w), new GH_Path(rT));
                heS.Add(rComp[i], new GH_Path(rT));
            }
            heS.Graft();
            return heuT;
        }

        public DataTree<AssemblyObject> GeneratePairs(AssemblyObject[] AO, DataTree<Rule> Hr, Point3d O, double padX, double padY, int nR)
        {
            DataTree<AssemblyObject> AOpairs = new DataTree<AssemblyObject>();
            AssemblyObject sender, receiver;

            xData = new DataTree<XData>();
            XData senGeom, recGeom;

            textLocations = new DataTree<Plane>();
            numberLocations = new DataTree<Plane>();
            bbCenters = new DataTree<Point3d>();
            //rulesText = new DataTree<string>();
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
                    receiver = AssemblyObjectUtils.Clone(AO[rT]);//new AssemblyObject(AO[rT]);
                    sender = AssemblyObjectUtils.Clone(AO[sT]);//new AssemblyObject(AO[sT]);

                    // generate transformation orient: sender to receiver
                    Transform orient = Transform.PlaneToPlane(sender.Handles[sH].SenderPlane, receiver.Handles[rH].ReceiverPlanes[rR]);

                    // orient sender AssemblyObject
                    sender.Transform(orient);

                    // copy and orient XData if present
                    if (haveXData)
                    {
                        for (int k = 0; k < xDCatalog.Count; k++)
                        {
                            // receiver XData
                            if (String.Equals(xDCatalog[k].AOName, receiver.Name))
                                recGeom = new XData(xDCatalog[k]);
                            // sender XData
                            if (String.Equals(xDCatalog[k].AOName, sender.Name))
                            {
                                senGeom = new XData(xDCatalog[k]);
                                senGeom.Transform(orient);
                            }
                        }
                    }
                    // calculate Bounding Box and compare size to initial parameters
                    BoundingBox bb = receiver.CollisionMesh.GetBoundingBox(false);
                    bb.Union(sender.CollisionMesh.GetBoundingBox(false));

                    // record center plane of AO combination
                    bbCenters.Add(bb.Center, Hr.Paths[i].AppendElement(j));

                    // retain largest dimensions (for grid final size)
                    sX = Math.Max(sX, bb.Diagonal.X);
                    sY = Math.Max(sY, bb.Diagonal.Y);
                    sZ = Math.Max(sZ, bb.Diagonal.Z);

                    // add AssemblyObjects to DataTree - same path of heuristic + one more index for rotations
                    AOpairs.Add(receiver, Hr.Paths[i].AppendElement(j));
                    AOpairs.Add(sender, Hr.Paths[i].AppendElement(j));

                    // add point grid location to tree
                    textLocations.Add(loc, Hr.Paths[i].AppendElement(j));

                    // fill coherence pattern & orient XData only if valid combination
                    bool valid = !AssemblageUtils.IsAOCollidingWithAnother(receiver, sender);
                    coherencePattern.Add(valid, Hr.Paths[i].AppendElement(j));

                    if (valid && haveXData)
                    {
                        if (recGeom != null) xData.Add(recGeom, Hr.Paths[i].AppendElement(j).AppendElement(0));
                        if (senGeom != null) xData.Add(senGeom, Hr.Paths[i].AppendElement(j).AppendElement(1));
                    }

                    // rewrite rule for display and add to the text Tree (just for Heuristic Display component)
                    //string rule = receiver.Name + "|" + rH + "=" + rR + "(" + receiver.handlesTree[rH].rRotations[rR] + ")" + "<" + sender.Name + "|" + sH;
                    //rulesText.Add(rule, Hr.Paths[i].AppendElement(j));

                    // calculate next grid position
                    countX++;
                    if (countX % rowElements == 0)
                    {
                        countX = 0;
                        countY++;
                    }
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

                // transfer extra geometries
                if (haveXData && coherencePattern.Branches[i][0])
                    for (int k = 0; k < 2; k++)
                        if (xData.PathExists(AOpairs.Paths[i].AppendElement(k)))
                            xData.Branch(AOpairs.Paths[i].AppendElement(k))[0].Transform(move);
            }
            return AOpairs;
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
                return Resources.L_Heuristics_Display_X;
            }
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
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("37c7b5cf-68b2-400b-acfd-69e6fd710bba"); }
        }
    }
}
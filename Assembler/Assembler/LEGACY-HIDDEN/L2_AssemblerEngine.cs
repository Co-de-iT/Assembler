﻿using Assembler.Properties;
using Assembler.Utils;
using AssemblerLib;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Assembler
{
    [Obsolete]
    public class L2_AssemblerEngine : GH_Component
    {
        Assemblage AOa;
        private bool pending = false;

        // DIAGNOSTICS
        //private System.Diagnostics.Stopwatch stopwatch;
        //private long computeTime, otherTime;
        //private string directory;

        /// <summary>
        /// Initializes a new instance of the AssemblerEngine class.
        /// </summary>
        public L2_AssemblerEngine()
          : base("Assembler Engine Simple", "AOEngS",
              "Assembler Engine Simpe - OBSOLETE\nDo not use this anymore",
              "Assembler", "Engine")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            // objects
            pManager.AddGenericParameter("AssemblyObjects Set", "AOs", "List of Assembly Objects in the set", GH_ParamAccess.list);
            pManager.AddGenericParameter("Previous Assemblage", "AOpa", "The list of AssemblyObjects in an existing Assemblage", GH_ParamAccess.list);
            pManager.AddPlaneParameter("Starting Plane", "P", "Starting Plane for the Assemblage\nIgnored if a previous Assemblage is input", GH_ParamAccess.item, Plane.WorldXY);
            pManager.AddIntegerParameter("Starting Object Type", "sO", "Index of starting object from the AO set\nIgnored if a previous Assemblage is input", GH_ParamAccess.item, 0);

            pManager[1].Optional = true; // Previous Assemblage

            // heuristics
            pManager.AddTextParameter("Heuristics String", "HeS", "Heuristics String", GH_ParamAccess.list);

            // criteria selectors

            // exogenous

            // controls
            pManager.AddBooleanParameter("Go", "go", "Run Assemblage continuously until it reaches the desired n. of objects", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Step", "s", "Run an Assemblage step executing the specified n. of iterations", GH_ParamAccess.item, false);
            pManager.AddIntegerParameter("N. Iterations", "nI", "Number of iterations to execute at each step", GH_ParamAccess.item, 10);
            pManager.AddIntegerParameter("Max n. Objects", "maxN", "The max n. of objects allowed in the assemblage", GH_ParamAccess.item, 1000);
            pManager.AddBooleanParameter("Reset", "R", "Reset Assemblage", GH_ParamAccess.item, false);

        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Assemblage", "AOa", "The Assemblage", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Assemblage Count", "c", "The number of objects in the Assemblage", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // DIAGNOSTICS
            //if (stopwatch == null) stopwatch = new System.Diagnostics.Stopwatch();
            //otherTime = stopwatch.ElapsedMilliseconds;
            //stopwatch.Restart();


            //
            // . . . . . . . . . . . . 0. Sanity checks on input data
            //

            // objects
            List<AssemblyObjectGoo> GH_AOs = new List<AssemblyObjectGoo>();
            List<AssemblyObjectGoo> GH_AOpa = new List<AssemblyObjectGoo>();
            List<AssemblyObject> AOs = new List<AssemblyObject>(), AOpa = new List<AssemblyObject>();

            if (!DA.GetDataList("AssemblyObjects Set", GH_AOs)) return;
            if (GH_AOs == null || GH_AOs.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please provide at least one AssemblyObject in the AOset");
                return;
            }

            DA.GetDataList("Previous Assemblage", GH_AOpa);

            Plane P = new Plane();
            DA.GetData("Starting Plane", ref P);
            int sO = 0;
            DA.GetData("Starting Object Type", ref sO);

            // heuristics
            List<string> HeS = new List<string>();
            if (!DA.GetDataList("Heuristics String", HeS)) return;
            if (HeS == null || HeS.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please provide at least one Heuristic rule");
                return;
            }

            // criteria selectors

            // controls
            bool go = false, step = false, reset = false;
            int nInt = 0, maxObj = 0;
            DA.GetData("Go", ref go);
            DA.GetData("Step", ref step);
            DA.GetData("N. Iterations", ref nInt);
            DA.GetData("Max n. Objects", ref maxObj);
            DA.GetData("Reset", ref reset);

            //
            // . . . . . . . . . . . . 1. Reset and initialize protocols
            //

            if (reset || AOa == null)
            {
                AOs = GH_AOs.Select(ao => ao.Value).ToList();
                AOpa = GH_AOpa.Select(ao => ao.Value).ToList();
                // join rules in a single line
                List<string> HeuString = new List<string>();
                HeuString.Add(string.Join(",", HeS));

                // construct heuristics settings
                HeuristicsSettings Heu = new HeuristicsSettings(HeuString, 0, 0, 0, 0);

                // construct exogenous settings
                ExogenousSettings Exo = new ExogenousSettings(new List<Mesh>(), 0, null, 0, Box.Unset, false);

                // construct Assemblage
                AOa = new Assemblage(AOs, AOpa, P, sO, Heu, Exo);

            }

            //
            // . . . . . . . . . . . . 2. Update live variables
            //


            //
            // . . . . . . . . . . . . 3. Update Assemblage & Component
            //

            // sometimes the toggle button gets stuck in the pressed event if the computation is intensive
            // leading to potential locks - this prevents it from happening
            if (step && !pending)
            {
                pending = true;
                return;
            }

            if ((go || (step && pending)) && AOa.AssemblyObjects.BranchCount < maxObj)
            {
                for (int i = 0; i < nInt; i++)
                    AOa.Update();
                ExpireSolution(true);
                if (pending) pending = false;

                // DIAGNOSTICS
                //computeTime = stopwatch.ElapsedMilliseconds;
                //string data = string.Format("{0}, {1}", computeTime, otherTime);
                //Utilities.AppendToFile(directory, "computeTimes.txt", data);
            }

            DA.SetData("Assemblage", AOa);
            DA.SetData("Assemblage Count", AOa.AssemblyObjects.BranchCount);

            // DIAGNOSTICS
            // stopwatch.Restart();

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
                return Resources.L_Assembler_Engine_X;
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
            get { return new Guid("6d146ab7-f360-480b-9ef2-4ba79e0689b2"); }
        }
    }
}
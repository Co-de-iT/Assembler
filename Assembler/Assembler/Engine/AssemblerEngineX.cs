using System;
using System.Collections.Generic;
using System.Linq;

using Grasshopper.Kernel;
using Rhino.Geometry;
using AssemblerLib;
using Assembler.Properties;
using GH_IO.Serialization;
using Assembler;
using System.Windows.Forms;
using Assembler.Utils;

namespace Assembler
{
    public class AssemblerEngineX : GH_Component
    {

        private bool checkWZLock;
        private bool useSupports;

        /// <summary>
        /// Initializes a new instance of the AssemblerEngineX class.
        /// </summary>
        public AssemblerEngineX()
          : base("Assembler Engine X", "AOEngX",
              "Assembler Engine Xpert\nWhere the magic happens... with more control",
              "Assembler", "Engine")
        {
            checkWZLock = GetValue("ZLockCheck", false);
            useSupports = GetValue("UseSupports", false);
            UpdateMessage();
            ExpireSolution(true);
        }

        Assemblage AOa;
        private bool pending = false;

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            // objects
            pManager.AddGenericParameter("AssemblyObjects Set", "AOs", "List of Assembly Objects in the set", GH_ParamAccess.list);
            pManager.AddGenericParameter("Previous Assemblage", "AOpa", "The list of AssemblyObjects in an existing Assemblage\noptional", GH_ParamAccess.list);
            pManager.AddPlaneParameter("Starting Plane", "P", "Starting Plane for the Assemblage\nIgnored if a previous Assemblage is input", GH_ParamAccess.item, Plane.WorldXY);
            pManager.AddIntegerParameter("Starting Object Type", "sO", "Index of starting object from the AO set\nIgnored if a previous Assemblage is input", GH_ParamAccess.item, 0);

            pManager[1].Optional = true; // Previous Assemblage

            // heuristics
            pManager.AddGenericParameter("Heuristics Settings", "HS", "Heuristics Settings for the Assemblage", GH_ParamAccess.item);

            // exogenous
            pManager.AddGenericParameter("Exogenous Settings", "ES", "Exogenous Settings for the Assemblage", GH_ParamAccess.item);

            pManager[5].Optional = true; // Exogenous

            // controls
            pManager.AddBooleanParameter("Go", "go", "Run Assemblage continuously until it reaches the desired n. of objects", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Step", "s", "Run an Assemblage step executing the specified n. of iterations", GH_ParamAccess.item, false);
            pManager.AddIntegerParameter("N. Iterations", "nI", "Number of iterations to execute at each step", GH_ParamAccess.item, 10);
            pManager.AddIntegerParameter("Max n. Objects", "maxN", "The max n. of objects allowed in the assemblage", GH_ParamAccess.item, 1000);
            pManager.AddBooleanParameter("Reset Exogenous", "rE", "Reset Exogenous factors (Field, Environment Objects) preserving the Assemblage", GH_ParamAccess.item, false);
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
            //
            // . . . . . . . . . . . . 0. Sanity checks on input data
            //

            // . . . . objects
            List<AssemblyObjectGoo> GH_AOs = new List<AssemblyObjectGoo>();
            List<AssemblyObjectGoo> GH_AOpa = new List<AssemblyObjectGoo>();
            List<AssemblyObject> AOs = new List<AssemblyObject>(), AOpa = new List<AssemblyObject>();

            if (!DA.GetDataList("AssemblyObjects Set", GH_AOs)) return;
            if (GH_AOs == null || GH_AOs.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please provide at least one AssemblyObject in the AOset");
                return;
            }

            //AOs = GH_AOs.Select(ao => ao.Value).ToList();

            DA.GetDataList("Previous Assemblage", GH_AOpa);

            //AOpa = GH_AOpa.Select(ao => ao.Value).ToList();

            Plane referencePlane = new Plane();
            DA.GetData("Starting Plane", ref referencePlane);
            int startingObjectType = 0;
            DA.GetData("Starting Object Type", ref startingObjectType);

            // . . . . heuristics
            HeuristicsSettings HS = new HeuristicsSettings();
            if (!DA.GetData("Heuristics Settings", ref HS)) return;
            List<string> heuristicsString = HS.heuristicsString;
            int currentHeuristics = HS.currentHeuristics;
            int heuristicsMode = HS.heuristicsMode;

            // . . . . criteria selectors
            int receiverSelectionMode = HS.receiverSelectionMode;
            int ruleSelectionMode = HS.ruleSelectionMode;

            // . . . . exogenous
            ExogenousSettings ES = new ExogenousSettings();
            List<Mesh> environmentMeshes = new List<Mesh>();
            int environmentMode = 0;
            Field field = null;
            double fieldScalarThreshold = 0;
            Box sandbox = Box.Empty;
            if (DA.GetData("Exogenous Settings", ref ES))
            {
                environmentMeshes = ES.environmentMeshes;
                environmentMode = ES.environmentMode;
                field = ES.field;
                fieldScalarThreshold = ES.fieldScalarThreshold;
                sandbox = ES.sandBox;
            }

            // . . . . controls
            bool go = false, step = false, reset = false, resetEx = false;
            int nInt = 0, maxObj = 0;
            DA.GetData("Go", ref go);
            DA.GetData("Step", ref step);
            DA.GetData("N. Iterations", ref nInt);
            DA.GetData("Max n. Objects", ref maxObj);
            DA.GetData("Reset Exogenous", ref resetEx);
            DA.GetData("Reset", ref reset);

            //
            // . . . . . . . . . . . . 1. Reset and initialize protocols
            //

            if (reset || AOa == null)
            {
                AOs = GH_AOs.Select(ao => ao.Value).ToList();
                AOpa = GH_AOpa.Select(ao => ao.Value).ToList();
                AOa = new Assemblage(AOs, AOpa, field, fieldScalarThreshold, heuristicsString, referencePlane, startingObjectType, environmentMeshes, sandbox);
            }

            //
            // . . . . . . . . . . . . 2. Update live variables
            //

            AOa.checkWorldZLock = checkWZLock;  //GetValue("ZLockCheck", false);

            // update environment check method
            AOa.SetEnvCheckMethod(environmentMode);

            // reset exogenous parameters if necessary (without resetting the assemblage)
            if (resetEx)
                AOa.ResetExogenous(environmentMeshes, field, fieldScalarThreshold, sandbox);

            // update current heuristics mode
            AOa.heuristicsMode = heuristicsMode;
            // update current heuristic index in manual mode
            // NOTE: if the assemblage uses the Field heuristics weights mode this is ignored
            if (heuristicsMode == 0 && currentHeuristics != AOa.currentHeuristics)
            {
                AOa.currentHeuristics = currentHeuristics % AOa.heuristicsTree.BranchCount;
                AOa.ResetAvailableObjects();
            }
            // update selection modes
            AOa.selectReceiverMode = receiverSelectionMode;
            AOa.selectRuleMode = ruleSelectionMode;

            // check field-dependent variables
            if (field == null)
            {
                if (heuristicsMode == 1)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Heuristics mode set to Field with no Field provided");
                    return;
                }

                if (receiverSelectionMode == 1)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Receiver selection mode set to Field with no Field provided");
                    return;
                }

                if (ruleSelectionMode == 1 || ruleSelectionMode == 2)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Rule selection mode set to Field with no Field provided");
                    return;
                }
            }

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

            if ((go || (step && pending)) && AOa.assemblyObjects.Count < maxObj)
            {
                for (int i = 0; i < nInt; i++)
                    AOa.Update();
                ExpireSolution(true);
                if (pending) pending = false;
            }

            DA.SetData("Assemblage", AOa);
            DA.SetData("Assemblage Count", AOa.assemblyObjects.Count);

        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            Menu_AppendSeparator(menu);
            ToolStripMenuItem toolStripMenuItem = Menu_AppendItem(menu, "Check World Z lock", ZLock_click, true, checkWZLock);
            toolStripMenuItem.ToolTipText = "Checks World Z axis orientation for AssemblyObjects with World Z lock enabled";
            //ToolStripMenuItem toolStripMenuItem1 = Menu_AppendItem(menu, "Use Supports", Supports_click, true, useSupports);
            //toolStripMenuItem1.ToolTipText = "Use supports (if present in AssemblyObjects) for Assemblage coherence\nNOT YET IMPLEMENTED";
            Menu_AppendSeparator(menu);
        }

        private void ZLock_click(object sender, EventArgs e)
        {
            RecordUndoEvent("Check World Z lock");
            checkWZLock = !GetValue("ZLockCheck", false);
            SetValue("ZLockCheck", checkWZLock);

            // set component message
            UpdateMessage();
            ExpireSolution(true);
        }

        private void Supports_click(object sender, EventArgs e)
        {
            RecordUndoEvent("Use Supports");
            useSupports = !GetValue("UseSupports", false);
            SetValue("UseSupports", useSupports);

            // set component message
            UpdateMessage();
            ExpireSolution(true);
        }

        private void UpdateMessage()
        {
            Message = checkWZLock ? "World Z Lock\n" : "";
            Message += useSupports ? "Supports" : "";
        }

        public override bool Write(GH_IWriter writer)
        {
            // NOTE: the value in between "" is shared AMONG ALL COMPONENTS of a librbary!
            // for instance, ZLockComp is accessible (and modifyable) by other components!
            writer.SetBoolean("ZLockCheck", checkWZLock);
            writer.SetBoolean("UseSupports", useSupports);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            reader.TryGetBoolean("ZLockCheck", ref checkWZLock);
            reader.TryGetBoolean("UseSupports", ref useSupports);
            UpdateMessage();
            return base.Read(reader);
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
                return Resources.Assembler_Engine_X;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("c7b33596-c80e-43c1-a6e5-c90806944083"); }
        }
    }
}
using Assembler.Properties;
using Assembler.Utils;
using AssemblerLib;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Assembler
{
    public class AssemblerEngine : GH_Component
    {
        // see https://developer.rhino3d.com/api/grasshopper/html/5f6a9f31-8838-40e6-ad37-a407be8f2c15.htm
        private bool m_checkWZLock = false;
        public bool CheckWZLock
        {
            get { return m_checkWZLock; }
            set
            {
                m_checkWZLock = value;
                if (m_checkWZLock)
                {
                    Message = "World Z Lock\n";
                }
                else
                {
                    Message = "";
                }
            }
        }

        private bool m_useSupports = false;
        public bool UseSupports
        {
            get { return m_useSupports; }
            set
            {
                m_useSupports = value;
                if (m_useSupports)
                {
                    Message += "Supports";
                }
                else
                {
                    Message += "";
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the AssemblerEngineX class.
        /// </summary>
        public AssemblerEngine()
          : base("Assembler Engine", "AOaEngine",
              "Assembler Engine\nWhere the magic happens...",
              "Assembler", "Engine")
        {
            // this hides the component preview when placed onto the canvas
            // source: http://frasergreenroyd.com/how-to-stop-components-from-automatically-displaying-results-in-grasshopper/
            IGH_PreviewObject prevObj = (IGH_PreviewObject)this;
            prevObj.Hidden = true;
        }

        Assemblage AOa;
        private bool pending = false;

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            // objects
            pManager.AddGenericParameter("AssemblyObjects Set", "AOs", "List of unique AssemblyObjects composing the set", GH_ParamAccess.list);
            pManager.AddGenericParameter("Previous Assemblage", "AOpa", "List of preexisting AssemblyObjects, i.e. from an existing Assemblage (optional)", GH_ParamAccess.list);
            pManager.AddPlaneParameter("Starting Plane", "P", "Starting Plane for the Assemblage\nIgnored if preexisting AssemblyObjects are present", GH_ParamAccess.item, Plane.WorldXY);
            pManager.AddIntegerParameter("Starting Object Type", "sO", "Index of starting object from the AO set\nIgnored if preexisting AssemblyObjects are present", GH_ParamAccess.item, 0);

            pManager[1].Optional = true; // Previous Assemblage

            // heuristics
            pManager.AddGenericParameter("Heuristics Settings", "HS", "Heuristics Settings for the Assemblage", GH_ParamAccess.item);

            // exogenous
            pManager.AddGenericParameter("Exogenous Settings", "ES", "Exogenous Settings for the Assemblage", GH_ParamAccess.item);
            // exogenous settings are optional
            pManager[5].Optional = true;

            // controls
            pManager.AddBooleanParameter("Go", "go", "Run Assemblage continuously until it reaches the targeted max n. of objects", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Step", "s", "Run an Assemblage step executing the specified n. of iterations", GH_ParamAccess.item, false);
            pManager.AddIntegerParameter("N. Iterations", "nI", "Number of iterations to execute at each step", GH_ParamAccess.item, 10);
            pManager.AddIntegerParameter("Target Max n. Objects", "tN", "The target Max n. of objects allowed in the assemblage" +
                "\nThis is an *approximate* target Assembler will try to reach, given the number of starting objects and iterations at each step" +
                "\nWith 1 starting object (default), 100 items per step and a target of 950 you will get 1001 objects", GH_ParamAccess.item, 1000);
            pManager.AddBooleanParameter("Reset Settings", "rS", "Reset Exogenous and Heuristics settings preserving the Assemblage", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Reset", "R", "Resets the Assemblage", GH_ParamAccess.item, false);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Assemblage", "AOa", "The Assemblage", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Assemblage Count", "c", "The number of objects in the Assemblage", GH_ParamAccess.item);
        }

        public override void CreateAttributes()
        {
            m_attributes = new AssemblerEngine_Attributes(this);
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

            DA.GetDataList("Previous Assemblage", GH_AOpa);


            Plane startReferencePlane = new Plane();
            DA.GetData("Starting Plane", ref startReferencePlane);
            int startingObjectType = 0;
            DA.GetData("Starting Object Type", ref startingObjectType);

            // . . . . heuristics
            HeuristicsSettings HS = new HeuristicsSettings();
            if (!DA.GetData("Heuristics Settings", ref HS)) return;

            // . . . . exogenous
            ExogenousSettings ES = new ExogenousSettings();

            Box sandbox = Box.Empty;
            if (!DA.GetData("Exogenous Settings", ref ES))
                ES = new ExogenousSettings(new List<Mesh>(), 0, null, 0, Box.Unset, false);

            // check Field-dependent variables
            if (ES.Field == null && HS.IsFieldDependent)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Heuristics Settings are Field dependent but no Field is provided\nCheck HeM, RsM and SsM parameters");
                return;
            }
            else if (HS.HeuristicsMode == 1)
            {
                if (ES.Field.GetiWeights().BranchCount == 0)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Heuristics mode (HeM) is Field dependent but Field has no iWeights");
                    return;
                }
                else
                {
                    int maxIWeight = ES.Field.GetiWeights().AllData().Max();
                    if (maxIWeight > HS.HeuSetsString.Count)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"The provided Field iWeights require at least {maxIWeight + 1} Heuristics Sets (HeS)");
                        return;
                    }
                }
            }

            // . . . . controls
            bool go = false, step = false, reset = false, resetEx = false;
            int nInt = 0, maxObj = 0;
            DA.GetData("Go", ref go);
            DA.GetData("Step", ref step);
            DA.GetData("N. Iterations", ref nInt);
            DA.GetData("Target Max n. Objects", ref maxObj);
            DA.GetData("Reset Settings", ref resetEx);
            DA.GetData("Reset", ref reset);

            //
            // . . . . . . . . . . . . 1. Reset and initialize protocols
            //

            if (reset || AOa == null)
            {
                AOs = GH_AOs.Where(a => a != null).Select(ao => ao.Value).ToList();
                if (AOs.Count < GH_AOs.Count)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"{GH_AOs.Count - AOs.Count} null AssemblyObjects were removed from the set");
                AOpa = GH_AOpa.Where(a => a != null).Select(ao => ao.Value).ToList();
                if (AOpa.Count < GH_AOpa.Count)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"{GH_AOpa.Count - AOpa.Count} null AssemblyObjects were removed from the set");
                AOa = new Assemblage(AOs, AOpa, startReferencePlane, startingObjectType, HS, ES);
                AOa.ResetSettings(HS, ES);
            }

            //
            // . . . . . . . . . . . . 2. Update live variables
            //

            // reset exogenous parameters if necessary (without resetting the assemblage)
            if (resetEx)
                AOa.ResetSettings(HS, ES);

            // World Z-Lock
            AOa.CheckWorldZLock = CheckWZLock;
            // use supports
            AOa.UseSupports = UseSupports;

            //
            // . . . . . . . . . . . . 3. Update Assemblage & Component
            //

            // sometimes the toggle button gets stuck in the 'pressed' status if the computation is intensive,
            // leading to potential locks - this prevents it from happening
            if (step && !pending)
            {
                pending = true;
                return;
            }

            if ((go || (step && pending)) && AOa.AssemblyObjects.DataCount < maxObj)
            {
                for (int i = 0; i < nInt; i++)
                    AOa.Update();
                ExpireSolution(true);
                if (pending) pending = false;
            }

            DA.SetData("Assemblage", AOa);
            DA.SetData("Assemblage Count", new GH_Integer(AOa.AssemblyObjects.DataCount));
        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            Menu_AppendSeparator(menu);
            ToolStripMenuItem toolStripMenuItem = Menu_AppendItem(menu, "Check World Z lock", ZLock_click, true, CheckWZLock);
            toolStripMenuItem.ToolTipText = "Checks World Z axis orientation for AssemblyObjects with World Z lock enabled";
#if DEBUG
            ToolStripMenuItem toolStripMenuItem1 = Menu_AppendItem(menu, "Use Supports", Supports_click, true, UseSupports);
            toolStripMenuItem1.ToolTipText = "Use supports (if present in AssemblyObjects) for Assemblage coherence\nNOT YET IMPLEMENTED";
#endif
            Menu_AppendSeparator(menu);
        }

        private void ZLock_click(object sender, EventArgs e)
        {
            RecordUndoEvent("Check World Z lock");
            CheckWZLock = !CheckWZLock;
            ExpireSolution(true);
        }

        private void Supports_click(object sender, EventArgs e)
        {
            RecordUndoEvent("Use Supports");
            UseSupports = !UseSupports;
            ExpireSolution(true);
        }

        public override bool Write(GH_IWriter writer)
        {
            // NOTE: the value in between "" is shared AMONG ALL COMPONENTS of a library!
            // for instance, ZLockCheck is accessible (and modifyable) by other components!
            writer.SetBoolean("ZLockCheck", CheckWZLock);
            writer.SetBoolean("UseSupports", UseSupports);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            CheckWZLock = reader.GetBoolean("ZLockCheck");
            UseSupports = reader.GetBoolean("UseSupports");
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
                return Resources.Assembler_Engine;
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
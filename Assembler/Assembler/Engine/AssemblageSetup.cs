using Assembler.Properties;
using Assembler.Utils;
using AssemblerLib;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Assembler
{
    public class AssemblageSetup : GH_Component
    {
        // see https://developer.rhino3d.com/api/grasshopper/html/5f6a9f31-8838-40e6-ad37-a407be8f2c15.htm
        private string WZL = "", supp = "";
        private bool m_checkWZLock = false;
        public bool CheckWZLock
        {
            get { return m_checkWZLock; }
            set
            {
                m_checkWZLock = value;
                WZL = m_checkWZLock ? "World Z Lock\n" : "";
                Message = WZL + supp;
            }
        }

        private bool m_useSupports = false;
        public bool UseSupports
        {
            get { return m_useSupports; }
            set
            {
                m_useSupports = value;
                supp = m_useSupports ? "Supports" : "";
                Message = WZL + supp;
            }
        }

        /// <summary>
        /// Because of their use in the Write method,
        /// the value of these strings is shared AMONG ALL COMPONENTS of a library!
        /// "ZLockCheck" & "UseSupports" are accessible (and modifyable) by other components!
        /// </summary>
        private readonly string WZLockName = "ZLockCheck";
        // TODO: for next major release, call it WZLockCheck - changing the name breaks the engine in the example files!
        private readonly string UseSupportsName = "UseSupports";
        /// <summary>
        /// Initializes a new instance of the AssemblageSetup class.
        /// </summary>
        public AssemblageSetup()
          : base("Assemblage Setup", "AOaSetup",
              "Assembler Engine Setup\nFor use with iterative strategies",
              "Assembler", "Engine")
        {
            // this hides the component preview when placed onto the canvas
            // source: http://frasergreenroyd.com/how-to-stop-components-from-automatically-displaying-results-in-grasshopper/
            IGH_PreviewObject prevObj = (IGH_PreviewObject)this;
            prevObj.Hidden = true;
        }

        Assemblage AOa;

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            // objects
            pManager.AddGenericParameter("AssemblyObjects Set", "AOs", "List of unique AssemblyObjects composing the set", GH_ParamAccess.list);

            pManager.AddGenericParameter("Starting AssemblyObjects", "AO", "List of AssemblyObjects to start from, either new objects or from an existing Assemblage", GH_ParamAccess.list);
            // heuristics
            pManager.AddGenericParameter("Heuristics Settings", "HS", "Heuristics Settings for the Assemblage", GH_ParamAccess.item);

            // exogenous
            pManager.AddGenericParameter("Exogenous Settings", "ES", "Exogenous Settings for the Assemblage", GH_ParamAccess.item);
            // exogenous settings are optional
            pManager[3].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Assemblage", "AOa", "The Assemblage", GH_ParamAccess.item);
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
            List<AssemblyObjectGoo> GH_AOset = new List<AssemblyObjectGoo>();
            List<AssemblyObjectGoo> GH_AOstart = new List<AssemblyObjectGoo>();
            List<AssemblyObject> AOset = new List<AssemblyObject>(), AOstart = new List<AssemblyObject>();

            if (!DA.GetDataList("AssemblyObjects Set", GH_AOset)) return;
            if (GH_AOset == null || GH_AOset.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please provide at least one AssemblyObject in the AOset");
                return;
            }

            if (!DA.GetDataList("Starting AssemblyObjects", GH_AOstart)) return;
            if (GH_AOstart == null || GH_AOstart.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please provide at least one starting AssemblyObject");
                return;
            }

            AOset = GH_AOset.Where(a => a != null).Select(ao => ao.Value).ToList();
            if (AOset.Count < GH_AOset.Count)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"{GH_AOset.Count - AOset.Count} null AssemblyObjects were removed from the set");

            AOstart = GH_AOstart.Where(a => a != null).Select(ao => ao.Value).ToList();
            if (AOstart.Count < GH_AOstart.Count)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"{GH_AOstart.Count - AOstart.Count} null AssemblyObjects were removed from the set");

            // TODO: implement this better, with == and != operators for Handle and Equals() methods for Handle and AO
            //foreach (AssemblyObject ao in AOstart)
            //    foreach (AssemblyObject aoset in AOset)
            //        if (!aoset.Equals(ao))
            //        {
            //            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "One or more starting AssemblyObjects do not belong to the AO Set");
            //            return;
            //        }

            // . . . . heuristics
            HeuristicsSettings HS = new HeuristicsSettings();
            if (!DA.GetData("Heuristics Settings", ref HS)) return;

            // . . . . exogenous
            ExogenousSettings ES = new ExogenousSettings();

            //Box sandbox = Box.Unset;
            if (!DA.GetData("Exogenous Settings", ref ES))
                ES = new ExogenousSettings(new List<Mesh>(), 0, null, 0, Box.Unset, false);

            // check Field-dependent variables
            if (ES.Field == null && HS.IsFieldDependent)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Heuristics Settings are Field dependent but no Field is provided\nCheck HeM, RsM and SsM parameters");
                return;
            }
            else if (HS.HeuristicsMode == HeuristicModes.Field)
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

            //
            // . . . . . . . . . . . . 1. Initialize Assemblage
            //

            AOa = new Assemblage(AOset, AOstart, HS, ES, CheckWZLock);//, UseSupports);

            DA.SetData("Assemblage", AOa);
        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            Menu_AppendSeparator(menu);
            ToolStripMenuItem toolStripMenuItem = Menu_AppendItem(menu, "Check World Z lock", ZLock_click, true, CheckWZLock);
            toolStripMenuItem.ToolTipText = "Checks World Z axis orientation for AssemblyObjects with World Z lock enabled";
#if DEBUG
            ToolStripMenuItem toolStripMenuItem1 = Menu_AppendItem(menu, "Use Supports", Supports_click, true, UseSupports);
            toolStripMenuItem1.ToolTipText = "Use supports (if present in AssemblyObjects) for Assemblage coherence\nDEBUG MODE - NOT YET IMPLEMENTED";
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
            writer.SetBoolean(WZLockName, CheckWZLock);
            writer.SetBoolean(UseSupportsName, UseSupports);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            CheckWZLock = reader.GetBoolean(WZLockName);
            UseSupports = reader.GetBoolean(UseSupportsName);
            return base.Read(reader);
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
                return Resources.Assembler_Setup;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("40EDE9BC-A973-43B5-819D-39837EE0B6D7"); }
        }
    }
}
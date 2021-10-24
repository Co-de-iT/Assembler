using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using AssemblerLib;
using Assembler.Properties;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Data;
using System.Linq;

namespace Assembler
{
    // it's named HeuristicsSettingsComponent to avoid conflict with HeuristicsSettings class in AssemblerLib
    public class HeuristicsSettingsComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the HeuristicsSettings class.
        /// </summary>
        public HeuristicsSettingsComponent()
          : base("Heuristics Settings", "HeuSet",
              "Collects Heuristics related settings",
              "Assembler", "Heuristics")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            // heuristics
            pManager.AddTextParameter("Heuristics String", "HeS", "Heuristics String", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Current Heuristics", "cH", "index of current Heuristics String to use from the list above", GH_ParamAccess.item, 0);
            pManager.AddIntegerParameter("Heuristics Mode", "HeM", "Heuristics Mode selector" +
                "\n0 - manual via cH parameter" +
                "\n1 - Field driven via iWeights",
                GH_ParamAccess.item, 0);
            // criteria selectors
            pManager.AddIntegerParameter("Receiver Selection Mode", "rOS",
                "Receiver Object selection criteria" +
                "\n0 - random" +
                "\n1 - scalar field fast" +
                "\n2 - scalar field accurate" +
                "\n3 - dense packing (SLOW)",
                GH_ParamAccess.item, 0);
            pManager.AddIntegerParameter("Rule Selection Mode", "rRS",
                "Rule selection criteria" +
                "\n0 - random" +
                "\n1 - scalar field fast" +
                "\n2 - scalar field accurate" +
                "\n3 - vector field fast" +
                "\n4 - vector field accurate" +
                "\n5 - minimum local bounding box volume" +
                "\n6 - minimum local bounding box diagonal"+
                "\n7 - weighted random choice",
                GH_ParamAccess.item, 0);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;

        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Heuristics Settings", "HS", "Heuristics Settings for the Assemblage", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // heuristics
            GH_Structure<GH_String> HeuStrings;// = new GH_Structure<GH_String>();
            if (!DA.GetDataTree(0, out HeuStrings)) return;
            if (HeuStrings.IsEmpty || HeuStrings == null || HeuStrings.Branches[0].Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please provide at least one Heuristic string");
                return;
            }
            List<string> HeS = new List<string>();
            for (int i = 0; i < HeuStrings.Branches.Count; i++)
            {
                HeS.Add(string.Join(",", HeuStrings.Branches[i].Select(s => s.Value).ToList()));
            }

            int cH = 0;
            DA.GetData("Current Heuristics", ref cH);
            int HeM = 0;
            DA.GetData("Heuristics Mode", ref HeM);

            // criteria selectors
            int rOS = 0, rRS = 0;
            DA.GetData("Receiver Selection Mode", ref rOS);
            DA.GetData("Rule Selection Mode", ref rRS);

            HeuristicsSettings HS = new HeuristicsSettings(HeS, cH, HeM, rOS, rRS);

            DA.SetData(0, HS);
        }

        /// <summary>
        /// Exposure override for position in the Subcategory (options primary to septenary)
        /// https://apidocs.co/apps/grasshopper/6.8.18210/T_Grasshopper_Kernel_GH_Exposure.htm
        /// </summary>
        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.tertiary; }
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
                return Resources.Heuristics_Settings;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("2a4c7e4d-79e6-4086-980f-5e5eac15aad7"); }
        }
    }
}
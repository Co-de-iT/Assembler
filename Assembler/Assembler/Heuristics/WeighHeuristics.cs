using Assembler.Properties;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace Assembler
{
    public class WeighHeuristics : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the WeighRules class.
        /// </summary>
        public WeighHeuristics()
          : base("Weigh Heuristics", "WHeu",
              "Assigns custom weights to the Heuristics rules",
              "Assembler", "Heuristics")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Heuristics String", "HeS", "Heuristics String", GH_ParamAccess.list);
            pManager.AddIntegerParameter("integer Weights", "iW", "Custom integer Weights", GH_ParamAccess.list);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Heuristics String", "HeS", "Heuristics String", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<string> rules = new List<string>();
            if (!DA.GetDataList(0, rules)) return;
            List<int> iWeights = new List<int>();
            if (!DA.GetDataList(1, iWeights)) return;

            if (rules.Count == 0 || iWeights.Count == 0) return;
            if (rules.Count != iWeights.Count) return;

            List<string> weightedRules = new List<string>();

            for (int i = 0; i < rules.Count; i++)
            {
                string[] rSplit = rules[i].Split('%');
                weightedRules.Add(String.Concat(rSplit[0], "%", Convert.ToString(iWeights[i])));
            }

            DA.SetDataList(0, weightedRules);
        }

        /// <summary>
        /// Exposure override for position in the Subcategory (options primary to septenary)
        /// https://apidocs.co/apps/grasshopper/6.8.18210/T_Grasshopper_Kernel_GH_Exposure.htm
        /// </summary>
        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.secondary; }
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
                return Resources.Weigh_Rules_2;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("bc39912a-1d58-460a-8b9a-2ee7964fb0a0"); }
        }
    }
}
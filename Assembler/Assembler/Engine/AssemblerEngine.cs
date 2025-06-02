using Assembler.Properties;
using AssemblerLib;
using AssemblerLib.Utils;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using System;

namespace Assembler
{
    public class AssemblerEngine : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the AssemblerEngine class.
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

        Assemblage AOa, AOaInput;
        private bool pending = false;

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Assemblage", "AOa", "The Assemblage after Setup", GH_ParamAccess.item);

            // controls
            pManager.AddBooleanParameter("Go", "go", "Run Assemblage continuously until it reaches the targeted max n. of objects", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Step", "s", "Run an Assemblage step executing the specified n. of iterations", GH_ParamAccess.item, false);
            pManager.AddIntegerParameter("N. Iterations", "nI", "Number of iterations to execute at each step", GH_ParamAccess.item, 10);
            pManager.AddIntegerParameter("Target Max n. Objects", "tN", "The target Max n. of objects allowed in the assemblage", GH_ParamAccess.item, 1000);
            pManager.AddBooleanParameter("Reset Settings", "rS", "Reset Exogenous and Heuristics settings preserving the Assemblage", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Reset", "R", "Resets the Assemblage", GH_ParamAccess.item, false);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Assemblage", "AOa", "The Assemblage", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Assemblage Count", "c", "The number of AssemblyObjects in the Assemblage", GH_ParamAccess.item);
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
            if (!DA.GetData("Assemblage", ref AOaInput)) return;
            if (AOaInput == null || AOaInput.AssemblyObjects.DataCount == 0) return;

            // . . . . controls
            bool go = false, step = false, reset = false, resetEx = false;
            int nIterations = 0, maxObj = 0;
            DA.GetData("Go", ref go);
            DA.GetData("Step", ref step);
            DA.GetData("N. Iterations", ref nIterations);
            DA.GetData("Target Max n. Objects", ref maxObj);
            DA.GetData("Reset Settings", ref resetEx);
            DA.GetData("Reset", ref reset);

            //
            // . . . . . . . . . . . . 1. Reset and initialize protocols
            //

            if (reset || AOa == null)
            {
                // clone Assemblage to avoid modifying the original setup
                AOa = AssemblageUtils.Clone(AOaInput);
                AOa.ResetAssemblageStatus(AOaInput.HeuristicsSettings, AOaInput.ExogenousSettings);
            }

            //
            // . . . . . . . . . . . . 2. Reset Settings
            //

            // reset exogenous parameters if necessary (without resetting the assemblage)
            if (resetEx) AOa.ResetAssemblageStatus(AOaInput.HeuristicsSettings, AOaInput.ExogenousSettings);


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

            if (go || (step && pending))
            {
                int iterations, AOcount = AOa.AssemblyObjects.DataCount;
                if (AOcount > maxObj - nIterations)
                    iterations = maxObj - AOcount;
                else iterations = nIterations;

                for (int i = 0; i < iterations; i++)
                    AOa.Update();

                // trim AssemblyObjects excess
                if (AOcount > maxObj)
                {
                    for (int i = AOcount - 1; i >= maxObj; i--)
                        AssemblageUtils.RemoveAssemblyObject(AOa, AOa.AssemblyObjects.Paths[i][0]);

                    AOa.ResetAOsOccupancyStatus();
                }

                ExpireSolution(true);

                if (pending) pending = false;
            }

            DA.SetData("Assemblage", AOa);
            DA.SetData("Assemblage Count", new GH_Integer(AOa.AssemblyObjects.DataCount));
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
            get { return new Guid("A07AAB58-C92E-494F-B040-BD1DC28E93E8"); }
        }
    }
}
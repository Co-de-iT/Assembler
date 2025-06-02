using Assembler.Properties;
using Assembler.Utils;
using AssemblerLib;
using AssemblerLib.Utils;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Assembler
{
    public class AssemblageSelectReceiver : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the AssemblageUpdate class.
        /// </summary>
        public AssemblageSelectReceiver()
          : base("Assemblage Select Receiver", "AOaSelRec",
              "Assembler Engine Select Receiver\nFor use with iterative strategies",
              "Assembler", "Engine")
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
            pManager.AddGenericParameter("Assemblage", "AOa", "The Assemblage", GH_ParamAccess.item);
            pManager.AddNumberParameter("AO Receiver Data", "RD", "Computed data for each AssemblyObject", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Selection Criteria", "S", "0 - select min value\n1 - select MAX value", GH_ParamAccess.item, 0);

            pManager[1].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Assemblage", "AOa", "The updated Assemblage", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Receiver Index", "rI", "Receiver AssemblyObject index", GH_ParamAccess.item);
            pManager.AddGenericParameter("AO candidates", "AOc", "AssemblyObjects Sender candidates", GH_ParamAccess.list);
            pManager.AddNumberParameter("Candidates factors", "Cf", "Sum of AO + Handle + Rule weights\n" +
                "Add this to the Computed Candidates values to make weights count in cases of equal values", GH_ParamAccess.list);
            pManager.AddGenericParameter("Candidates Rules", "R", "Rule associated to each candidate", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Assemblage AOa = null;
            if (!DA.GetData("Assemblage", ref AOa)) return;

            // Assign receiver values (if that's the case), otherwise set them to 0
            GH_Structure<GH_Number> AOReceiverData;
            if (DA.GetDataTree("AO Receiver Data", out AOReceiverData))
                foreach (GH_Path path in AOReceiverData.Paths)
                    AOa.AssemblyObjects[path, 0].ReceiverValue = AOReceiverData[path][0].Value;
            else
                foreach (GH_Path path in AOReceiverData.Paths)
                    AOa.AssemblyObjects[path, 0].ReceiverValue = 0;

            // TODO: this implementation is generalized for more than one AO in each path - this never happens so far, it was in case I implemented children AOs (not gonna happen anytime soon)
            //if (DA.GetDataTree("AO Receiver Data", out AOReceiverData))
            //    foreach (GH_Path path in AOReceiverData.Paths)
            //        for (int i = 0; i < AOReceiverData[path].Count; i++)
            //            AOa.AssemblyObjects[path, i].ReceiverValue = AOReceiverData[path][i].Value;

            int selection = 0;
            DA.GetData("Selection Criteria", ref selection);

            switch (selection)
            {
                case 0:
                    AOa.HeuristicsSettings.selectReceiver = ComputingRSMethods.SelectMinIndex;
                    break;
                case 1:
                    AOa.HeuristicsSettings.selectReceiver = ComputingRSMethods.SelectMaxIndex;
                    break;
                default:
                    goto case 0;
            }

            // reset iteration and try to get candidates
            AOa.ResetIterationVariables();

            List<AssemblyObjectGoo> candidates = null;

            if (AOa.TryGetReceiverAndCandidates())
                candidates = AOa.i_CandidateObjects.Select(ao => new AssemblyObjectGoo(ao)).ToList();

            DA.SetData("Assemblage", AOa);
            DA.SetData("Receiver Index", AOa.i_CurrentReceiver);
            DA.SetDataList("AO candidates", candidates);
            DA.SetDataList("Candidates factors", AOa.i_CandidateFactors);
            DA.SetDataList("Candidates Rules", AOa.ExtractCandidatesRules());
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
                return Resources.Select_Receiver;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("B1691A26-774E-49A7-8AC2-84C6633420A5"); }
        }
    }
}
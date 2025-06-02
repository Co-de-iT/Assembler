using Assembler.Properties;
using AssemblerLib;
using AssemblerLib.Utils;
using Grasshopper.Kernel;
using System;
using System.Collections.Generic;

namespace Assembler
{
    public class AssemblageSelectSender : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the AssemblageSelectSender class.
        /// </summary>
        public AssemblageSelectSender()
          : base("Assemblage Select Sender", "AOaSelSen",
              "Assembler Engine Select Sender\nFor use with iterative strategies",
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
            pManager.AddNumberParameter("AO candidates Sender Data", "SD", "AssemblyObjects candidates Sender data\n" +
                "One value for each candidate from Select Receiver", GH_ParamAccess.list);
            pManager.AddNumberParameter("AO candidates Receiver Data", "cRD", "AssemblyObjects candidates Receiver data\n" +
                "One value for each candidate from Select Receiver\n" +
                "Optional: if omitted, Sender data will be used", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Selection Criteria", "S", "0 - select min value\n1 - select MAX value", GH_ParamAccess.item, 0);
            pManager[2].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Assemblage", "AOa", "The updated Assemblage", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Assemblage AOa = null;
            if (!DA.GetData("Assemblage", ref AOa)) return;

            List<double> senderData = new List<double>();
            DA.GetDataList("AO candidates Sender Data", senderData);
            double[] senderDataArray = senderData.ToArray();

            List<double> receiverData = new List<double>();
            DA.GetDataList("AO candidates Receiver Data", receiverData);
            if (receiverData == null || receiverData.Count == 0) receiverData = senderData;
            double[] receiverDataArray = receiverData.ToArray();

            int selection = 0;
            DA.GetData("Selection Criteria", ref selection);

            switch (selection)
            {
                case 0:
                    AOa.HeuristicsSettings.selectSender = ComputingRSMethods.SelectMinIndex;
                    break;
                case 1:
                    AOa.HeuristicsSettings.selectSender = ComputingRSMethods.SelectMaxIndex;
                    break;
                default:
                    goto case 0;
            }

            // select the winner and update the Assemblage before output
            AssemblyObject newObject;
            Rule rule;
            int winnerIndex;
            (newObject, rule, winnerIndex) = AOa.SelectCandidate(AOa.i_CandidateObjects, senderDataArray);

            // assign Receiver value
            newObject.ReceiverValue = receiverDataArray[winnerIndex];
            // add new object (incrementally updates occupancy as well)
            AOa.AddValidObject(newObject, rule);

            DA.SetData("Assemblage", AOa);
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
                return Resources.Select_Sender;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("8DCC41BE-7D7E-41FC-AE95-632A09B29E0A"); }
        }
    }
}
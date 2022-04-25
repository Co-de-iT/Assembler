using System;
using System.Collections.Generic;
using System.Linq;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Assembler
{
    public class DeconstructRule : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the DeconstructRule class.
        /// </summary>
        public DeconstructRule()
          : base("Deconstruct Rule", "RDecon",
              "Deconstructs an Heuristics Rule string",
              "Assembler", "Heuristics")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Rule string", "R", "Rule string to deconstruct", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Receiver name", "rN", "Receiver Object name", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Receiver Handle", "rH", "Receiver Object Handle index", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Receiver Rotation index", "rRi", "Receiver Object Handle rotation index", GH_ParamAccess.item);
            pManager.AddTextParameter("Sender name", "sN", "Sender Object name", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Sender Handle", "sH", "Sender Object Handle Index", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Rule iWeight", "iW", "Rule integer Weight", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string rS = "";

            if (!DA.GetData(0, ref rS)) return;

            // rule format example
            // receiverType|rHindex=rRotIndex<senderType|sHindex%RiWeight
            // typeA|0=1<typeB|1%0

            string[] r = rS.Split(new[] { '|', '=', '<', '%' });

            DA.SetData(0, r[0]);
            DA.SetData(1, Convert.ToInt32(r[1]));
            DA.SetData(2, Convert.ToInt32(r[2]));
            DA.SetData(3, r[3]);
            DA.SetData(4, Convert.ToInt32(r[4]));
            DA.SetData(5, Convert.ToInt32(r[5]));
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
                return null;
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
            get { return new Guid("cc6fa023-e309-4d70-b8cd-319b8aa03d3f"); }
        }
    }
}
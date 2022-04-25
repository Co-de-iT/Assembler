using System;
using System.Linq;

using Grasshopper;
using Grasshopper.Kernel;

using AssemblerLib;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Assembler.Properties;

namespace Assembler
{
    [Obsolete]
    public class H_PopulateFieldiWeightsSc : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the PopulateFieldWeightsSc class.
        /// </summary>
        public H_PopulateFieldiWeightsSc()
          : base("Populate Field iWeights Scalar - HIDDEN", "AFPopWS",
              "Populate a Field's iWeights by its scalar values",
              "Assembler", "Exogenous")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Field", "F", "Field", GH_ParamAccess.item);
            pManager.AddIntegerParameter("iWeight Values", "iW", "Integer weight values - 2 lists of equal number of integer values", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Index", "i", "Index of scalar value to sample\n0 (default) for single scalar value per Field point", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Threshold", "T", "Threshold for Allocation\nif Blend option is true this value is ingored", GH_ParamAccess.item, 0.5);
            pManager.AddBooleanParameter("Blend", "B", "Blends weight values or assigns them according to the threshold", GH_ParamAccess.item, false);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Field", "F", "Populated Field", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Field f = null, fW;
            if (!DA.GetData(0, ref f)) return;

            fW = new Field(f);

            GH_Structure<GH_Integer> iW = null;

            if (!DA.GetDataTree(1, out iW)) return;

            if (iW.Branches.Count != 2)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "iWeight Branch Count must be 2");
                return;
            }

            int ind = 0;
            DA.GetData("Index", ref ind);

            if (fW.tensors[0].scalar == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Field does not have scalar values");
                return;
            }

            if (ind > fW.tensors[0].scalar.Length)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Field does not have scalar values at specified index");
                return;
            }

            double thres = 0.5;
            DA.GetData("Threshold", ref thres);

            bool blend = false;
            DA.GetData("Blend", ref blend);

            DataTree<int> iWeights = WeightsToRhType(iW);

            fW.DistributeiWeightsScalar(iWeights, thres, ind, blend);

            DA.SetData(0, fW);
        }

        DataTree<int> WeightsToRhType(GH_Structure<GH_Integer> iWeights)
        {
            DataTree<int> iWeightsRh = new DataTree<int>();

            if (iWeights != null)
                for (int i = 0; i < iWeights.Branches.Count; i++)
                    iWeightsRh.AddRange(iWeights.Branches[i].Select(n => n.Value).ToList(), iWeights.Paths[i]);

            return iWeightsRh;
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
                return Resources.Populate_Field_iW_scalars;
            }
        }

        /// <summary>
        /// Exposure override for position in the Subcategory (options primary to septenary)
        /// https://apidocs.co/apps/grasshopper/6.8.18210/T_Grasshopper_Kernel_GH_Exposure.htm
        /// </summary>
        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.hidden; }// secondary
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("7fdbad9b-6dc9-419f-8875-c392cff6493e"); }
        }
    }
}
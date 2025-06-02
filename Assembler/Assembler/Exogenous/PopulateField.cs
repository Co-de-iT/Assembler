using Assembler.Properties;
using AssemblerLib;
using AssemblerLib.Utils;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;

namespace Assembler
{
    public class PopulateField : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the PopulateField class.
        /// </summary>
        public PopulateField()
          : base("Populate Field", "AFPop",
              "Populates a Field with Scalar, Vector, and/or integer Weight values",
              "Assembler", "Exogenous")
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
            pManager.AddGenericParameter("Field", "F", "Empty Field", GH_ParamAccess.item);
            pManager.AddNumberParameter("Scalar Values", "S", "Scalar values for each point", GH_ParamAccess.tree);
            pManager.AddVectorParameter("Vector Values", "V", "Vector values for each point", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("iWeight Values", "iW", "Integer weight values for each point", GH_ParamAccess.tree);
            pManager[1].Optional = true; // scalars are optional
            pManager[2].Optional = true; // vectors are optional
            pManager[3].Optional = true; // iWeights are optional
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
            Field emptyField = null, populatedField;
            GH_Structure<GH_Number> scalars;
            GH_Structure<GH_Vector> vectors;
            GH_Structure<GH_Integer> iWeights;

            if (!DA.GetData(0, ref emptyField)) return;
            DA.GetDataTree(1, out scalars);
            DA.GetDataTree(2, out vectors);
            DA.GetDataTree(3, out iWeights);

            DataTree<double> scalarsTree = DataUtils.GHS2TreeDoubles(scalars);
            DataTree<Vector3d> vectorsTree = DataUtils.GHS2TreeVectors(vectors);
            DataTree<int> iWeightsTree = DataUtils.GHS2TreeIntegers(iWeights);

            populatedField = new Field(emptyField);

            if (scalarsTree != null && scalarsTree.BranchCount > 0)
                populatedField.PopulateScalars(scalarsTree);

            if (vectorsTree != null && vectorsTree.BranchCount > 0)
                populatedField.PopulateVectors(vectorsTree);

            if (iWeightsTree != null && iWeightsTree.BranchCount > 0)
                populatedField.PopulateiWeights(iWeightsTree);

            // output populated Field
            DA.SetData(0, populatedField);
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
                return Resources.Populate_Field;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("7ea58641-afb0-4d00-bed2-217aefac91ac"); }
        }
    }
}
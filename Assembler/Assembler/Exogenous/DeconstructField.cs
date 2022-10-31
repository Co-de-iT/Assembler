using Assembler.Properties;
using AssemblerLib;
using Grasshopper.Kernel;
using System;

namespace Assembler
{
    public class DeconstructField : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the DeconstructField class.
        /// </summary>
        public DeconstructField()
          : base("Deconstruct Field", "AFDecon",
              "Deconstructs a Field",
              "Assembler", "Exogenous")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Field", "F", "Empty Field", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Points", "P", "Field Points", GH_ParamAccess.list);
            pManager.AddNumberParameter("Scalar Values", "S", "Scalar Values", GH_ParamAccess.tree);
            pManager.AddVectorParameter("Vectors", "V", "Vectors", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("iWeights", "iW", "Integer Weights", GH_ParamAccess.tree);
            pManager.AddColourParameter("Colors", "C", "Field Colors", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Topology", "T", "Field topology", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Topology Weights", "tW", "Topology transmission weights", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Field f = null;

            if (!DA.GetData(0, ref f)) return;

            DA.SetDataList(0, f.GetGH_Points());
            if (f.Tensors != null)
            {
                DA.SetDataTree(1, f.GetGH_Scalars());
                DA.SetDataTree(2, f.GetGH_Vectors());
                DA.SetDataTree(3, f.GetGH_iWeights());
            }
            DA.SetDataList(4, f.Colors);
            DA.SetDataTree(5, Utilities.ToDataTree(f.Topology));
            DA.SetDataTree(6, Utilities.ToDataTree(f.TransCoeff));
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
                return Resources.Deconstruct_Field;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("908f3e47-41e6-4e3b-9235-ea1086220f47"); }
        }
    }
}
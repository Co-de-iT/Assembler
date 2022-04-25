using System;
using System.Collections.Generic;
using System.Linq;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using AssemblerLib;
using Assembler.Properties;

namespace Assembler
{
    [Obsolete]
    public class H_PopulateFieldiWeightsAtt : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the PopulateFieldWeightsAtt class.
        /// </summary>
        public H_PopulateFieldiWeightsAtt()
          : base("Populate Field iWeights Attractors - HIDDEN", "AFPopWA",
              "Populate a Field's iWeights by Attractor points",
              "Assembler", "Exogenous")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Field", "F", "Field", GH_ParamAccess.item);
            pManager.AddPointParameter("Attractor Points", "A", "Attractor points", GH_ParamAccess.list);
            pManager.AddIntegerParameter("iWeight Values", "iW", "Integer weight values\na DataTree with equal number of integer values per branch\none branch for each attractor point", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Blend", "B", "Blends weight values or assigns to the closest Attractor", GH_ParamAccess.item, false);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Populated Field", "F", "Field", GH_ParamAccess.item);
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

            List<Point3d> A = new List<Point3d>();
            if (!DA.GetDataList("Attractor Points", A)) return;

            GH_Structure<GH_Integer> iW = null;

            if (!DA.GetDataTree(2, out iW)) return;

            if(iW.Branches.Count != A.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "iWeight Branch Count does not match number of Attractors");
                return;
            }

            bool blend = false;
            DA.GetData("Blend", ref blend);

            DataTree<int> iWeights = WeightsToRhType(iW);

            fW.DistributeiWeights(iWeights, A, blend);

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
        /// Exposure override for position in the Subcategory (options primary to septenary)
        /// https://apidocs.co/apps/grasshopper/6.8.18210/T_Grasshopper_Kernel_GH_Exposure.htm
        /// </summary>
        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.hidden; }//secondary
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
                return Resources.Populate_Field_iW_attractors;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("22f76397-4eb4-485f-a22e-2c30471915a2"); }
        }
    }
}
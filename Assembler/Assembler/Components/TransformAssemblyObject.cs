using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using AssemblerLib;
using Assembler.Properties;
using Assembler.Utils;

namespace Assembler
{
    public class TransformAssemblyObject : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the TransformAssemblyObject class.
        /// </summary>
        public TransformAssemblyObject()
          : base("TransformAssemblyObject", "AOXform",
              "Apply a Transformation to an AssemblyObject",
              "Assembler", "Components")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Assembly Object", "AO", "The newly created Assembly Object", GH_ParamAccess.item);
            pManager.AddTransformParameter("Transformation", "X", "The Transformation to apply", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Assembly Object", "AO", "The newly created Assembly Object", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            AssemblyObjectGoo GH_AO = null;
            AssemblyObject AO;
            Transform X = new Transform();
            // sanity check on inputs
            if (!DA.GetData("Assembly Object", ref GH_AO)) return;
            if (!DA.GetData("Transformation", ref X)) return;

            AO = GH_AO.Value;

            // make a new AssemblyObject to avoid byRef retroactive transformations
            AssemblyObject AOt = Utilities.Clone(AO);//new AssemblyObject(AO);
            AOt.Transform(X);

            DA.SetData(0, new AssemblyObjectGoo(AOt));
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
                return Resources.Transform_AO;
            }
        }

        /// <summary>
        /// Exposure override for position in the SUbcategory (options primary to septenary)
        /// https://apidocs.co/apps/grasshopper/6.8.18210/T_Grasshopper_Kernel_GH_Exposure.htm
        /// </summary>
        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.primary; }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("9f0442fd-55be-4525-989b-330cf57630b3"); }
        }
    }
}
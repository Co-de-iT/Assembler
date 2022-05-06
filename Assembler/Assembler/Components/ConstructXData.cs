using System;
using System.Collections.Generic;
using System.Linq;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using AssemblerLib;
using Assembler.Properties;

namespace Assembler
{
    public class ConstructXData : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ConstructXData class.
        /// </summary>
        public ConstructXData()
          : base("Construct XData", "XDCon",
              "Construct an XData instance\nXData can be any kind of Xtended/Xtra data (Geometry, String, Numbers, ...) associated to an AssemblyObject Type",
              "Assembler", "Components")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Label", "L", "Label for the data", GH_ParamAccess.item);
            pManager.AddGenericParameter("Data", "D", "Data", GH_ParamAccess.list);
            pManager.AddPlaneParameter("Reference Plane", "P", "Reference plane for extended data", GH_ParamAccess.item);
            pManager.AddTextParameter("AssemblyObject Name reference", "N", "AssemblyObject name to which XData is associated", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("XData", "XD", "Extended Data associated to an AssemblyObject Type", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //List<object> data = new List<object>();
            List<IGH_Goo> data = new List<IGH_Goo>();
            string label = "";
            Plane p = new Plane();
            string AOname = "";

            if (!DA.GetData("Label", ref label)) return;
            if (!DA.GetDataList("Data", data)) return;
            if(!DA.GetData("Reference Plane", ref p)) return;
            if (!DA.GetData("AssemblyObject Name reference", ref AOname)) return;

            List<object> dataValue = ConvertGoo(data);
            XData xd = new XData(dataValue, label, p, AOname);

            DA.SetData(0, xd);
        }

        /// <summary>
        /// Converts IGH_Goo into generic object type - otherwise Transform won't work
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        List<object> ConvertGoo(List<IGH_Goo> data)
        {
            List<object> dataValue = new List<object>();

            object ob;

            foreach(IGH_Goo ig in data)
            {
                ig.CastTo<object>(out ob);

                dataValue.Add(ob);
            }

            return dataValue;
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
                return Resources.Construct_XData;
            }
        }

        /// <summary>
        /// Exposure override for position in the Subcategory (options primary to septenary)
        /// https://apidocs.co/apps/grasshopper/6.8.18210/T_Grasshopper_Kernel_GH_Exposure.htm
        /// </summary>
        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.tertiary; }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("638d1d76-10b0-442c-8e52-7e5098ca01ae"); }
        }
    }
}
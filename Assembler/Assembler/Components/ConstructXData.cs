using Assembler.Properties;
using Assembler.Utils;
using AssemblerLib;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using System;
using System.Collections.Generic;

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
            pManager.AddTextParameter("Label", "L", "Label for the data", GH_ParamAccess.item);
            pManager.AddGenericParameter("AssemblyObject", "AO", "The AssemblyObject to associate", GH_ParamAccess.item);
            pManager.AddGenericParameter("Data", "D", "Data", GH_ParamAccess.list);
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
            AssemblyObjectGoo GH_AO = null;
            AssemblyObject AO;
            // sanity check on inputs
            if (!DA.GetData("AssemblyObject", ref GH_AO)) return;
            AO = GH_AO.Value;

            string label = "";
            List<IGH_Goo> dataGoo = new List<IGH_Goo>();

            if (!DA.GetData("Label", ref label)) return;
            if (!DA.GetDataList("Data", dataGoo)) return;


            List<object> data = ConvertGoo(dataGoo);
            XData xd = new XData(data, label, AO.ReferencePlane, AO.Name);

            DA.SetData(0, xd);
        }

        /// <summary>
        /// Converts IGH_Goo into generic object type - otherwise Transform won't work
        /// </summary>
        /// <param name="dataGoo"></param>
        /// <returns></returns>
        private List<object> ConvertGoo(List<IGH_Goo> dataGoo)
        {
            List<object> data = new List<object>();

            object ob;

            foreach (IGH_Goo ig in dataGoo)
            {
                if (ig == null) ob = null; 
                else
                {
                    ig.CastTo<object>(out ob);
                }

                data.Add(ob);
            }

            return data;
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
            get { return new Guid("BC5D9C4C-A9C5-4076-901C-14EEE741DA12"); }
        }
    }
}
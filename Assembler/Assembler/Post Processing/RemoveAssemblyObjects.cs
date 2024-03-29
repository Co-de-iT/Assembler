﻿using Assembler.Properties;
using AssemblerLib;
using AssemblerLib.Utils;
using Grasshopper.Kernel;
using System;
using System.Collections.Generic;

namespace Assembler
{
    public class RemoveAssemblyObjects : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the RemoveAssemblyObjects class.
        /// </summary>
        public RemoveAssemblyObjects()
          : base("Remove AssemblyObjects", "AORem",
              "Removes AssemblyObjects from an Assemblage given their indexes - updating Topology",
              "Assembler", "Post Processing")
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
            pManager.AddIntegerParameter("Indexes", "i", "Indexes of AssemblyObjects to remove", GH_ParamAccess.list);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Assemblage", "AOa", "The modified Assemblage", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Results", "r", "Removal attempt results\nTrue - success, False - failure", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Assemblage AOa = null;
            if (!DA.GetData(0, ref AOa)) return;

            List<int> indexes = new List<int>();
            if (!DA.GetDataList(1, indexes)) return;

            Assemblage AOaCopy = AssemblageUtils.Clone(AOa);

            //for (int i = 0; i < indexes.Count; i++)
            //    AssemblageUtils.RemoveAssemblyObject(AOaCopy, indexes[i]);

            bool[] results = AssemblageUtils.RemoveAssemblyObjects(AOaCopy, indexes);

            DA.SetData(0, AOaCopy);
            DA.SetDataList(1, results);

        }


        /// <summary>
        /// Exposure override for position in the Subcategory (options primary to septenary)
        /// https://apidocs.co/apps/grasshopper/6.8.18210/T_Grasshopper_Kernel_GH_Exposure.htm
        /// </summary>
        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.quinary; }
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
                return Resources.Remove_AssemblyObject;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("DCC691EE-7B57-4556-BAE2-60C877004983"); }
        }
    }
}
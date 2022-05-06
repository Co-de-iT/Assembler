using Assembler.Properties;
using Assembler.Utils;
using AssemblerLib;
using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Assembler
{
    public class AssemblyObjectsFromJSON : GH_Component
    {
        public List<AssemblyObject> AOs = new List<AssemblyObject>();

        /// <summary>
        /// Initializes a new instance of the AssemblageFromJSON class.
        /// </summary>
        public AssemblyObjectsFromJSON()
          : base("AssemblyObjects From JSON", "JSON>AO",
              "Load a list of AssemblyObjects from a JSON file saved with brute force method",
              "Assembler", "I/O")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("File Path", "F", "File to read (full absolute path)", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("AssemblyObjects", "AO", "The list of AssemblyObjects in the JSON file", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string Path = "";

            // sanity check input data
            if (!DA.GetData("File Path", ref Path)) return;

            if (!System.IO.File.Exists(Path))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "File not found");
                return;
            }

            AOs = Utilities.AssemblageFromJSONdump(Path);

            List<AssemblyObjectGoo> GH_AOs = AOs.Select(ao => new AssemblyObjectGoo(ao)).ToList();

            DA.SetDataList("AssemblyObjects", GH_AOs);
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
                return Resources.Assemblage_From_JSON;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("4d776e49-3d32-44d1-aa2b-050ee6ae9947"); }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Properties;
using AssemblerLib;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Assembler
{
    public class DisplayCandidates : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the DisplayCandidates class.
        /// </summary>
        public DisplayCandidates()
          : base("Display Candidates", "AODispCand",
              "Displays Candidate Objects at last step in the Assemblage",
              "Assembler", "Post Processing")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Assemblage", "AOa", "The Assemblage", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Candidate Objects", "cO", "CAndidate Object Meshes for last iteration", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Assemblage AOa = null;
            if (!DA.GetData(0, ref AOa)) return;

            DA.SetDataList("Candidate Objects", AOa.candidateObjects.Select(a => a.collisionMesh));
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
                return Resources.Extract_Candidates;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("876f2a30-feb9-4bdb-8923-43f5840d9b30"); }
        }
    }
}
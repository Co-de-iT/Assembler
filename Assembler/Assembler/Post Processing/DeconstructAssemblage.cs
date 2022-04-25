using Assembler.Properties;
using Assembler.Utils;
using AssemblerLib;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Assembler
{
    public class DeconstructAssemblage : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the DeconstructAssemblage class.
        /// </summary>
        public DeconstructAssemblage()
          : base("Deconstruct Assemblage", "AOaDecon",
              "Deconstructs an Assemblage",
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
            pManager.AddGenericParameter("Assemblage Objects", "AO", "The tree of AssemblyObjects in the Assemblage", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Assemblage Rules", "AOr", "The sequential list of Rules in the Assemblage", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Receiver Objects Indexes", "rOi", "The sequential list of receiver Objects indexes in the Assemblage", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Available Object indexes", "avO", "List of indexes for AssemblyObjects with available Handles", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Unreachable Object indexes", "unO", "List of indexes for unreachable AssemblyObjects", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Assemblage AOa = null;
            if (!DA.GetData(0, ref AOa)) return;

            List<AssemblyObjectGoo> GH_AOs = AOa.assemblyObjects.AllData().Select(ao => new AssemblyObjectGoo(ao)).ToList();
            GH_Structure<AssemblyObjectGoo> AOGooTree = new GH_Structure<AssemblyObjectGoo>();
            for(int i = 0; i < AOa.assemblyObjects.BranchCount; i++)
            {
                AOGooTree.Append(new AssemblyObjectGoo(AOa.assemblyObjects.Branches[i][0]), AOa.assemblyObjects.Paths[i]);
            }

            DA.SetDataTree(0, AOGooTree);
            //DA.SetDataList(0, GH_AOs);
            DA.SetDataList("Assemblage Rules", AOa.assemblageRules);
            DA.SetDataList("Receiver Objects Indexes", AOa.receiverIndexes);
            DA.SetDataList("Available Object indexes", AOa.ExtractAvailableObjects());
            DA.SetDataList("Unreachable Object indexes", AOa.ExtractUnreachableObjects());

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
                return Resources.Deconstruct_Assemblage;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("7c119a86-7f9a-4b3b-a4d2-0f66d62ccdb2"); }
        }
    }
}
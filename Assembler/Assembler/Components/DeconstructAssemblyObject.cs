using Assembler.Properties;
using Assembler.Utils;
using AssemblerLib;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using System;
using System.Linq;

namespace Assembler
{
    public class DeconstructAssemblyObject : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the DeconstructAssemblyObject class.
        /// </summary>
        public DeconstructAssemblyObject()
          : base("Deconstruct AssemblyObject", "AODecon",
              "Deconstruct an AssemblyObject",
              "Assembler", "Components")
        {
        }

        static int nObj;

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("AssemblyObject", "AO", "An AssemblyObject", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "The object's unique name", GH_ParamAccess.item);
            pManager.AddMeshParameter("Collision Mesh", "M", "The mesh geometry used for collision checks", GH_ParamAccess.item);
            pManager.AddPlaneParameter("Reference Plane", "P", "The object's reference plane", GH_ParamAccess.item);
            pManager.AddVectorParameter("Direction", "D", "The object's direction vector", GH_ParamAccess.item);
            pManager.AddGenericParameter("Handles", "H", "The object's Handles", GH_ParamAccess.list);
            pManager.AddNumberParameter("Weight", "W", "The object's weight", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Occluded Neighbours", "On", "Occluded Neighbours in the assemblage (as Tree)\nEach Branch contains:\n. index of the occluded AssemblyObject\n. index of its occluded Handle", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Z Lock", "ZL", "Absolute Z-Lock status of the object", GH_ParamAccess.item);
            pManager.AddNumberParameter("Receiver value", "Rv", "Receiver value of the object in the Assemblage", GH_ParamAccess.item);
            //pManager.AddGenericParameter("Children", "C", "Children of a Composite AssemblyObject", GH_ParamAccess.list);
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
            if (!DA.GetData(0, ref GH_AO)) return;
            AO = GH_AO.Value;
            if (DA.Iteration == 0) nObj = 0;

            DataTree<GH_Integer> onTree = OccludedAOs(AO);

            // increase counter for occluded objects
            nObj++;

            // output data
            DA.SetData("Name", AO.name);
            DA.SetData("Collision Mesh", AO.collisionMesh);
            DA.SetData("Reference Plane", AO.referencePlane);
            DA.SetData("Direction", AO.direction);
            DA.SetDataList("Handles", AO.handles);
            DA.SetData("Weight", AO.weight);
            DA.SetDataTree(6, onTree);
            DA.SetData("Z Lock", AO.worldZLock);
            DA.SetData("Receiver value", AO.receiverValue);
            //if (AO.children != null)
            //    DA.SetDataList("Children", AO.children.Select(ao => new AssemblyObjectGoo(ao)).ToList());
        }

        DataTree<GH_Integer> OccludedAOs(AssemblyObject AO)
        {
            DataTree<GH_Integer> occludedTree = new DataTree<GH_Integer>();

            for (int i = 0; i < AO.occludedNeighbours.Count; i++)
                occludedTree.AddRange(AO.occludedNeighbours[i].Select(x => new GH_Integer(x)).ToList(), new GH_Path(nObj, i));

            return occludedTree;
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
                return Resources.Deconstruct_AO;
            }
        }

        /// <summary>
        /// Exposure override for position in the Subcategory (options primary to septenary)
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
            get { return new Guid("44C0B3D9-D7FC-4296-B1D5-C0B07D077142"); }
        }
    }
}
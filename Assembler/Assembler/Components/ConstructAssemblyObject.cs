using Assembler.Properties;
using Assembler.Utils;
using AssemblerLib;
using AssemblerLib.Utils;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace Assembler
{
    public class ConstructAssemblyObject : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ConstructAssemblyObject class.
        /// </summary>
        public ConstructAssemblyObject()
          : base("Construct AssemblyObject", "AOCon",
              "Construct an Assembly Object from relevant data",
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
            pManager.AddTextParameter("Name", "N", "The object's unique name", GH_ParamAccess.item);
            pManager.AddMeshParameter("Collision Mesh", "M", "The mesh geometry used for collision checks", GH_ParamAccess.item);
            pManager.AddPlaneParameter("Reference Plane", "P", "The object's reference plane\nif unspecified, it is set to an XY Plane in the mesh volume centroid", GH_ParamAccess.item);
            pManager.AddVectorParameter("Direction", "D", "The object's direction vector\ndefault: X direction vector", GH_ParamAccess.item, Vector3d.XAxis);
            pManager.AddGenericParameter("Handles", "H", "The object's Handles", GH_ParamAccess.list);
            pManager.AddNumberParameter("Weight", "W", "The object's weight\n(optional)", GH_ParamAccess.item, 1.0);
            pManager.AddBooleanParameter("World Z-Lock", "Z", "Preserve Z-up orientation during Assemblage\n" +
                "Requires checking the Z-Lock option in the Assembler Engine", GH_ParamAccess.item, false);
            pManager[2].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("AssemblyObject", "AO", "The newly created Assembly Object", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string name = "";
            int type = 0;
            Mesh collisionMesh = new Mesh();
            Plane referencePlane = new Plane();
            Vector3d directionVector = Vector3d.Zero;
            double weight = 1.0;
            List<Handle> handlesList = new List<Handle>();

            // input data sanity checks
            if (!DA.GetData("Name", ref name)) return;
            if (!DA.GetData("Collision Mesh", ref collisionMesh)) return;

            // if collision mesh is null return
            if (collisionMesh == null || !collisionMesh.IsValid || !collisionMesh.IsClosed)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Collision Mesh is null, open or invalid");
                return;
            }

            // if no reference plane is set or if null set it to XY plane in volume centroid
            if (!DA.GetData("Reference Plane", ref referencePlane) || referencePlane == null)
            {
                Point3d centroid = VolumeMassProperties.Compute(collisionMesh).Centroid;
                referencePlane = new Plane(centroid, Vector3d.ZAxis);
            }

            DA.GetData("Direction", ref directionVector);
            // if Direction is null or zero return
            if (directionVector == null || directionVector == Vector3d.Zero)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Direction vector is zero or invalid");
                return;
            }

            if (!DA.GetDataList(4, handlesList)) return;
            // if Handles are empty return
            if (handlesList.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No Handles supplied");
                return;
            }

            // purge nulls from Handles list
            handlesList = HandleUtils.PurgeNullHandlesFromList(handlesList);

            DA.GetData("Weight", ref weight);
            int iWeight = -1;

            bool worldZLock = false;
            DA.GetData("World Z-Lock", ref worldZLock);

            Message = worldZLock? "World Z Lock":"";

            // construct the AssemblyObject                                                                                                  Zlock
            AssemblyObject AO = new AssemblyObject(collisionMesh, handlesList, referencePlane, directionVector, name, type, weight, iWeight, worldZLock);

            DA.SetData(0, new AssemblyObjectGoo(AO));

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
                return Resources.Construct_AO;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("50CEA0F8-882B-4319-9FF2-DE63871D35D5"); }
        }
    }
}
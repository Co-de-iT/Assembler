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
    public class D_ConstructHandle_tree : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ConstructHandle class.
        /// </summary>
        public D_ConstructHandle_tree()
           : base("Construct Handles", "Handles",
              "Construct one or more Handles",
              "Assembler", "Components")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Polyline", "LP", "L-shaped Polylines", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Rotation angles", "R", "Rotation angles in degrees", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Type", "T", "Handle types", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Weight", "W", "Handle Weights", GH_ParamAccess.tree);
            pManager[3].Optional = true; // weights are optional
            pManager[0].DataMapping = GH_DataMapping.Graft;
            pManager[2].DataMapping = GH_DataMapping.Graft;
            pManager[3].DataMapping = GH_DataMapping.Graft;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Handles", "H", "Handles", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // get data
            Handle hand;
            List<Handle> handles = new List<Handle>();
            GH_Structure<GH_Curve> pCurves = new GH_Structure<GH_Curve>();
            //List<Curve> pCurves = new List<Curve>();
            List<Polyline> poly = new List<Polyline>();
            Polyline p;
            GH_Structure<GH_Number> rot = new GH_Structure<GH_Number>();
            GH_Structure<GH_Integer> type = new GH_Structure<GH_Integer>();
            GH_Structure<GH_Number> w = new GH_Structure<GH_Number>();

            // sanity checks
            if (!DA.GetDataTree(0, out pCurves)) return;
            if (!DA.GetDataTree(1, out rot)) return;
            if (!DA.GetDataTree(2, out type)) return;
            if (!DA.GetDataTree(3, out w) || w.IsEmpty)
            {
                // if weights are not given as input, set them to a defalult of 1.0 for each handle
                for(int i=0; i<pCurves.Branches.Count; i++)
                 w.Append(new GH_Number(1.0), new GH_Path(i));
            }


            if (pCurves == null || pCurves.Branches.Count == 0)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please provide one or more polylines");

            if (rot == null || rot.IsEmpty || pCurves.Branches.Count != rot.Branches.Count)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please specify a set of one or more rotations for each polyline");

            if (type == null || pCurves.Branches.Count != type.Branches.Count)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please specify a type for each polyline");

            if (w == null || pCurves.Branches.Count != w.Branches.Count)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please specify a weight for each polyline");

            foreach (GH_Curve po in pCurves)
            {
                if (!po.Value.TryGetPolyline(out p))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please feed a polyline");
                    return;
                }
                else if (p.Count != 3)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Polyline must have 3 points and be L-shaped");
                    return;
                }
                else poly.Add(p);
            }

            // create handles
            for (int i = 0; i < poly.Count; i++)
            {
                if (rot.Branches[i] == null || rot.Branches[i].Count == 0)
                {
                    List<double> rotats = new List<double> { 0.0 };
                    hand = new Handle(poly[i], type.Branches[i][0].Value, w.Branches[i][0].Value, rotats);
                }
                else
                {
                    List<double> rotD = rot.Branches[i].Select(r => r.Value).ToList();
                    hand = new Handle(poly[i], type.Branches[i][0].Value, w.Branches[i][0].Value, rotD);
                }
                handles.Add(hand);
            }

            // output handle
            DA.SetDataList(0, handles);
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
                return null;
            }
        }

        /// <summary>
        /// Exposure override for position in the SUbcategory (options primary to septenary)
        /// https://apidocs.co/apps/grasshopper/6.8.18210/T_Grasshopper_Kernel_GH_Exposure.htm
        /// </summary>
        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.hidden; }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("0f730d50-d73f-4c6b-90eb-44d5b61ccb37"); }
        }
    }
}
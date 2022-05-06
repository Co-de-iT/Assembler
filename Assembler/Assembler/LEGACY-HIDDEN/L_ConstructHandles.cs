using Assembler.Properties;
using AssemblerLib;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Assembler
{
    [Obsolete]
    public class L_ConstructHandles : GH_Component
    {

        /// <summary>
        /// Initializes a new instance of the ConstructHandle class.
        /// </summary>
        public L_ConstructHandles()
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
            pManager.AddCurveParameter("Polyline", "LP", "L-shaped Polylines", GH_ParamAccess.list);
            pManager.AddNumberParameter("Rotation angles", "R", "Rotation angles in degrees - as Tree\none branch for each polyline, containing the corresponding rotations", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Type", "T", "Handle types", GH_ParamAccess.list);
            pManager.AddNumberParameter("Weight", "W", "Handle Weights", GH_ParamAccess.list);
            pManager[3].Optional = true; // weights are optional
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
            List<Curve> pCurves = new List<Curve>();
            List<Polyline> poly = new List<Polyline>();
            Polyline p;
            GH_Structure<GH_Number> rot = new GH_Structure<GH_Number>();
            List<int> type = new List<int>();
            List<double> w = new List<double>();

            // sanity checks
            if (!DA.GetDataList(0, pCurves)) return;
            if (!DA.GetDataTree(1, out rot)) return;
            if (!DA.GetDataList(2, type)) return;
            if (!DA.GetDataList(3, w) || w.Count == 0)
                // if weights are not given as input, set them to a defalult of 1.0 for each handle
                w = pCurves.Select(c => 1.0).ToList();

            if (pCurves == null || pCurves.Count == 0)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please provide one or more L-shaped polylines");

            if (rot == null || rot.IsEmpty || pCurves.Count != rot.Branches.Count)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please specify a set of one or more rotations for each polyline");

            if (type == null || pCurves.Count != type.Count)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please specify a type for each polyline");

            if (w == null || pCurves.Count != w.Count)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please specify a weight for each polyline");

            foreach (Curve po in pCurves)
            {
                if (po == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Null polyline detected");
                    return;
                }
                if (!po.TryGetPolyline(out p))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please feed a L-shaped polyline");
                    return;
                }
                if (p.Count != 3)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Polyline must have 3 points and be L-shaped");
                    return;
                }
                poly.Add(p);
            }

            // create handles
            for (int i = 0; i < poly.Count; i++)
            {
                if (rot.Branches[i] == null || rot.Branches[i].Count == 0)
                {
                    List<double> rotats = new List<double> { 0.0 };
                    hand = new Handle(poly[i], type[i], w[i], rotats);
                }
                else
                {
                    List<double> rotD = rot.Branches[i].Select(r => r.Value).ToList();
                    hand = new Handle(poly[i], type[i], w[i], rotD);
                }
                handles.Add(hand);
            }

            // output handles
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
                return Resources.Construct_Handle;
            }
        }

        /// <summary>
        /// Exposure override for position in the Subcategory (options primary to septenary)
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
            get { return new Guid("531c4f0e-8a80-4244-a657-672f3ff790db"); }
        }
    }
}
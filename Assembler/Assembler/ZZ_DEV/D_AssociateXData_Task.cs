using Assembler.Utils;
using AssemblerLib;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Assembler
{
    // NOTE: this is slower than the traditional component / flagged as zz_dev
    public class D_AssociateXData_Task : GH_TaskCapableComponent<D_AssociateXData_Task.SolveResults>
    {
        /// <summary>
        /// Initializes a new instance of the AssociateXData_Task class.
        /// </summary>
        public D_AssociateXData_Task()
          : base("AssociateXData_Task", "AO<>XD_task",
              "Associates XData to a list of AssemblyObjects\nMake sure the XData matches the corresponding AssemblyObject kinds present in the list",
              "Assembler", "Post Processing")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("AssemblyObject", "AO", "The AssemblyObject for XData association", GH_ParamAccess.item);
            pManager.AddGenericParameter("XData", "XD", "The list of XData to associate", GH_ParamAccess.list);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("XData", "XD", "XData oriented in the AssemblyObject", GH_ParamAccess.list);
        }

        public class SolveResults
        {
            public List<XData> xData { get; set; }
        }

        public static SolveResults AssociateXDatatoAO(AssemblyObject AO, List<XData> xD)
        {
            SolveResults result = new SolveResults();
            result.xData = new List<XData>();

            for (int i = 0; i < xD.Count; i++)
            {
                // if the object matches XData's associated kind do the thing
                if (String.Equals(AO.Name, xD[i].AOName))
                {
                    XData xdC = new XData(xD[i]);
                    Transform orient = Transform.PlaneToPlane(xdC.ReferencePlane, AO.ReferencePlane);
                    xdC.Transform(orient);
                    result.xData.Add(xdC);
                }
            }

            return result;
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // input data (in parallel mode)
            if (InPreSolve)
            {
                AssemblyObjectGoo AOgoo = new AssemblyObjectGoo();
                List<XData> xD = new List<XData>();

                if (!DA.GetData(0, ref AOgoo)) return;
                if (!DA.GetDataList(1, xD)) return;

                // Queue up the task
                Task<SolveResults> task = Task.Run(() => AssociateXDatatoAO(AOgoo.Value, xD), CancelToken);
                TaskList.Add(task);
                return;
            }
            // usual process if component is not in parallel computing mode
            // Basically all the "old" SolveInstance goes here (except for output)
            if (!GetSolveResults(DA, out SolveResults result))
            {
                AssemblyObjectGoo AOgoo = new AssemblyObjectGoo();
                List<XData> xD = new List<XData>();

                if (!DA.GetData(0, ref AOgoo)) return;
                if (!DA.GetDataList(1, xD)) return;

                result = AssociateXDatatoAO(AOgoo.Value, xD);
            }

            // output data
            if (result != null)
            {
                DA.SetDataList(0, result.xData);
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
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("4EFE99DF-8D36-493B-9EFD-09F8A33E5661"); }
        }
    }
}
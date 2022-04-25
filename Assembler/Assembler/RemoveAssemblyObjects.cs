using System;
using System.Collections.Generic;

using Grasshopper.Kernel;

using AssemblerLib;

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

            //AOa = RemoveObjectAtIndexes(AOa, indexes);

            DA.SetData(0, AOa);

        }
        // PROBLEM: still buggy - after removal the connection indexes will not correspond anymore
        // with the actual indexes in the assemblage list
        // SOLUTION: AssemblyObject class must have a unique assemblage index or id assigned while assembling
        //Assemblage RemoveObjectAtIndexes(Assemblage AOa, List<int> seqIndexes)
        //{
        //    List<AssemblyObject> removed = new List<AssemblyObject>();
        //    // first: update all connection/occupancy statuses
        //    // and keep track of AssemblyObjects to remove
        //    for (int i = 0; i < seqIndexes.Count; i++)
        //    {
        //        // continue if an index is out of range
        //        if (seqIndexes[i] < 0 || seqIndexes[i] >= AOa.assemblyObjects.Count) continue;

        //        AssemblyObject rem = AOa.assemblyObjects[seqIndexes[i]];

        //        // parse occluded neighbour objects
        //        if (rem.occludedNeighbours.Count != 0)
        //            for (int k = 0; k < rem.occludedNeighbours.Count; k++)
        //            {
        //                int nO = AOa.AOIndexesMap.IndexOf(rem.occludedNeighbours[k][0]); // neighbour object squential Index
        //                int nH = rem.occludedNeighbours[k][1]; // neighbour handle index

        //                // reset connectivity data
        //                AOa.assemblyObjects[nO].handles[nH].occupancy = 0;
        //                AOa.assemblyObjects[nO].handles[nH].neighbourObject = -1;
        //            }

        //        // parse connected handles
        //        for (int j = 0; j < rem.handles.Length; j++)
        //            // case of connected handle - reset connectivity data on connected component's handle
        //            if (rem.handles[j].occupancy == 1)
        //            {
        //                int nO = AOa.AOIndexesMap.IndexOf(rem.handles[j].neighbourObject); // neighbour object squential Index
        //                int nH = rem.handles[j].neighbourHandle;
        //                AOa.assemblyObjects[nO].handles[nH].occupancy = 0;
        //                AOa.assemblyObjects[nO].handles[nH].neighbourObject = -1;
        //                AOa.assemblyObjects[nO].handles[nH].neighbourHandle = -1;
        //            }
        //            // case of occluded handle - remove object-handle index tuple from neighbour occluded list
        //            else if (rem.handles[j].occupancy == 1)
        //            {
        //                int[] occluded = new int[] { AOa.AOIndexesMap[i], j };
        //                int nO = AOa.AOIndexesMap.IndexOf(rem.handles[j].neighbourObject); // neighbour object squential Index
        //                AOa.assemblyObjects[nO].occludedNeighbours.Remove(occluded);
        //            }

        //        removed.Add(rem);
        //    }

        //    // then: perform the removal
        //    foreach (AssemblyObject rem in removed)
        //        AOa.assemblyObjects.Remove(rem);
        //    return AOa;
        //}

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
            get { return new Guid("3e77b75a-2368-4d56-a310-e3cb97451844"); }
        }
    }
}
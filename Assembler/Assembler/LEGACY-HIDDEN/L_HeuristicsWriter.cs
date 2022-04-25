using System;
using System.Collections.Generic;

using Grasshopper.Kernel;

using AssemblerLib;
using Assembler.Properties;

namespace Assembler
{
    [Obsolete]
    public class L_HeuristicsWriter : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the HeuristicWriter class.
        /// </summary>
        public L_HeuristicsWriter()
          : base("Heuristics Writer - LEGACY", "HeuW",
              "Generates heuristics for a list of AssemblyObjects\nLEGACY version",
              "Assembler", "Heuristics")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("AssemblyObjects Set", "AOs", "List of Assembly Objects in the set", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Allow Self-Object connections", "sO", "Allows an AssemblyObject to connect with itself\ndefault is true", GH_ParamAccess.item, true);
            pManager.AddBooleanParameter("Allow Self-Handle connections", "sH", "Allows an AssemblyObject Handle to connect with itself\ndefault is true", GH_ParamAccess.item, true);
            pManager.AddBooleanParameter("Allow Cross-Type connections", "cT", "Allows an AssemblyObject Handle connections with different Handle types\ndefault is false", GH_ParamAccess.item, false);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Heuristics String", "HeS", "Heuristics String", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "This is an obsolete component - replace it with a new version from the Ribbon");

            List<AssemblyObject> AOs = new List<AssemblyObject>();
            // sanity check on inputs
            if (!DA.GetDataList("AssemblyObjects Set", AOs)) return;
            bool selfObject = true;
            bool selfHandle = true;
            bool crossType = false;
            DA.GetData(1, ref selfObject);
            DA.GetData(2, ref selfHandle);
            DA.GetData(3, ref crossType);

            DA.SetDataList(0, H_Writer(AOs.ToArray(), selfObject, selfHandle, crossType));

        }

        List<string> H_Writer(AssemblyObject[] components, bool selfObject, bool selfHType, bool crossT)
        {
            //DataTree<Rule> heuT = new DataTree<Rule>();
            List<string> hsList = new List<string>();
            string hs;

            /*
             rOi - receiver object index
             rHi - receiver handle index
             sOi - sender object index
             sHi - sender handle index
             rRi - receiver rotation index
             */
            // scan all components as potential receivers
            for (int rOi = 0; rOi < components.Length; rOi++)
            {
                // scan all handles
                for (int rHi = 0; rHi < components[rOi].handles.Length; rHi++)
                {
                    // scan all components as potential senders
                    for (int sOi = 0; sOi < components.Length; sOi++)
                    {
                        // exclude self if selfObject is False
                        if (rOi == sOi && !selfObject) continue;
                        // scan potential sender handles
                        for (int sHi = 0; sHi < components[sOi].handles.Length; sHi++)
                        {
                            // exclude handles attaching to self if selfHType is False
                            if (rOi == sOi && rHi == sHi && !selfHType) continue;
                            // if handle types match or cross type is True generate heuristic
                            if (components[rOi].handles[rHi].type == components[sOi].handles[sHi].type || crossT)
                            {
                                // consider all receiver rotations
                                for (int rRi = 0; rRi < components[rOi].handles[rHi].receivers.Length; rRi++)
                                {
                                    hs = string.Format("{0}|{1}={2}<{3}|{4}%0", components[rOi].name, rHi, components[rOi].handles[rHi].rRotations[rRi], components[sOi].name, sHi);
                                    //hs = string.Format("{0}|{1}={2}<{3}|{4}%0", components[rOi].name, rHi, rRi, components[sOi].name, sHi);
                                    hsList.Add(hs);
                                }
                            }
                        }
                    }
                }
            }

            return hsList;
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
                return Resources.Heuristics_Writer_OLD;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("3d1c686c-5eb1-427f-8b32-dcb332f5c407"); }
        }
    }
}
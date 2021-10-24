﻿using System;
using System.Collections.Generic;
using System.Linq;

using Grasshopper.Kernel;
using AssemblerLib;
using Assembler.Properties;
using Assembler.Utils;

namespace Assembler
{
    public class AssemblyObjectsToJSON : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the AssemblageToJSON class.
        /// </summary>
        public AssemblyObjectsToJSON()
          : base("AssemblyObjects To JSON", "AO2JSON",
              "Save a list of AssemblyObjects to a JSON file - brute force method",
              "Assembler", "I/O")
        {
        }

        String info;

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("AssemblyObjects", "AO", "The list of AssemblyObjects", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Start index", "i", "When supplied, only AssemblyObjects from this index on will be saved", GH_ParamAccess.item, 0);
            pManager.AddTextParameter("Directory", "D", "Path to the save directory", GH_ParamAccess.item);
            pManager.AddTextParameter("File name", "F", "Filename (without extension)", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Save", "S", "Save file (trigger)\nAttach a button and press once to save file", GH_ParamAccess.item, false);

            pManager[1].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("info", "i", "status info", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<AssemblyObjectGoo> GH_AOs = new List<AssemblyObjectGoo>();
            List<AssemblyObject> AOs = new List<AssemblyObject>(), AOSelect = new List<AssemblyObject>();
            String path = "";
            String name = "";

            if (info == null) info = "Activate save Toggle to save JSON file";
            bool save = false;

            // input data sanity check
            if (!DA.GetDataList(0, GH_AOs)) return;
            if (!DA.GetData("Directory", ref path)) return;
            if (!DA.GetData("File name", ref name)) return;
            AOs = GH_AOs.Select(ao => ao.Value).ToList();
            DA.GetData("Save", ref save);
            int i = 0;
            DA.GetData("Start index", ref i);

            if (AOs == null) return;
            if (i == 0)
                AOSelect = AOs;
            else
                for (int j = i; j < AOs.Count; j++)
                    AOSelect.Add(AOs[j]);

            if (save)
                info = "Last assemblage saved as " + Utilities.AssemblageToJSONdump(AOSelect, path, name);


            DA.SetData("info", info);
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
                return Resources.Assemblage_To_JSON;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("844630c9-3683-43ee-94ce-3cf0d4acb2c0"); }
        }
    }
}
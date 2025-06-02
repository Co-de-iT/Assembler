using Assembler.Properties;
using AssemblerLib;
using AssemblerLib.Utils;
using Grasshopper.GUI.Gradient;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

namespace Assembler
{
    public class DisplayAssemblage : GH_Component
    {
        public enum DMType : int
        {
            Objects = 0, AOTypes = 1, Occupancy = 2, Sequence = 3, ZValue = 4, AOWeights = 5, Connectedness = 6, Orientation = 7, ZOrientation = 8, ReceiverValues = 9, LocalDensity = 10
        }
        internal struct DisplayMode
        {
            internal DMType type;
            internal string name;
            internal string description;
        }

        internal DisplayMode[] displayModes = new DisplayMode[]
        {
            new DisplayMode { type = DMType.Objects, name = "Objects", description =  "Just White. A timeless classic, approved by The Thin White Duke" },
            new DisplayMode {type = DMType.AOTypes, name = "AO Types", description = "Colors assigned by AssemblyObject type"},
            new DisplayMode {type = DMType.Occupancy, name = "Occupancy", description = "Colors assigned by occupancy status:\n  white (available), light green (unreachable), dark green (saturated)"},
            new DisplayMode {type = DMType.Sequence, name = "Sequence", description = "Colors assigned by assemblage sequence:\n  black (older) < blue shades > white (younger)"},
            new DisplayMode {type = DMType.ZValue, name = "Z Value", description = "Colors assigned by World Z value of Assemblyobject Reference Plane origin - pink gradient"},
            new DisplayMode {type = DMType.AOWeights, name = "AO Weights", description = "Colors assigned by AssemblyObjects weights\n  grayscale gradient - black (minimum) <--> white (maximum)"},
            new DisplayMode {type = DMType.Connectedness, name = "Connectedness", description = "Percentage of free handles\n  traffic gradient - green (free) <--> red (fully occupied)"},
            new DisplayMode {type = DMType.Orientation, name = "Orientation", description = "Faux-Normal-style color map for AssemblyObjects direction vector"},
            new DisplayMode {type = DMType.ZOrientation, name = "Z Orientation", description = "Faux-Normal-style color map for AssemblyObjects Reference Plane Z vector"},
            new DisplayMode {type = DMType.ReceiverValues, name = "Receiver Values", description = "Receiver value of each AssemblyObject\n  white (minimum) <--> dark red (maximum)"},
            new DisplayMode {type = DMType.LocalDensity, name = "Local Density", description = "(Sum of Bounding Box volumes of the AssemblyObject + its connected and occluded neighbours) / (2x the volume of the Bounding Box for the largest AO in the AOSet)"},
        };

        internal string displayModeString;

        private GH_Mesh[] meshes;
        private GH_Line[] edges;
        private Color[] colors;
        private GH_Number[] values;

        /// <summary>
        /// Initializes a new instance of the DisplayAssemblage class.
        /// </summary>
        public DisplayAssemblage()
          : base("Display Assemblage", "AOaDisp",
              "Display Assemblage with a set of modes",
              "Assembler", "Post Processing")
        {
            // this hides the component preview when placed onto the canvas
            // source: http://frasergreenroyd.com/how-to-stop-components-from-automatically-displaying-results-in-grasshopper/
            IGH_PreviewObject prevObj = (IGH_PreviewObject)this;
            prevObj.Hidden = true;

            Params.ParameterSourcesChanged += new GH_ComponentParamServer.ParameterSourcesChangedEventHandler(ParamSourceChanged);
        }

        // SOURCE: https://discourse.mcneel.com/t/automatic-update-of-valuelist-only-when-connected/152879/6?u=ale2x72
        // works much better as it does not clog the solver with exceptions if a list of numercal values is connected
        private void ParamSourceChanged(object sender, GH_ParamServerEventArgs e)
        {
            if ((e.ParameterSide == GH_ParameterSide.Input) && (e.ParameterIndex == 1))
                foreach (IGH_Param source in e.Parameter.Sources)
                    if (source is Grasshopper.Kernel.Special.GH_ValueList)
                    {
                        Grasshopper.Kernel.Special.GH_ValueList vListDispMode = source as Grasshopper.Kernel.Special.GH_ValueList;

                        if (!vListDispMode.NickName.Equals("Display Mode"))
                        {
                            vListDispMode.ClearData();
                            vListDispMode.ListItems.Clear();
                            vListDispMode.NickName = "Display Mode";

                            for (int i = 0; i < displayModes.Length; i++)
                                vListDispMode.ListItems.Add(new GH_ValueListItem(displayModes[i].name, i.ToString()));

                            vListDispMode.ListMode = Grasshopper.Kernel.Special.GH_ValueListMode.Cycle; // change this for a different mode
                            vListDispMode.ExpireSolution(true);
                        }
                    }
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Assemblage", "AOa", "The Assemblage", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Display Mode", "D", "Display Mode for the Assemblage. Available modes:\n\n" +
                "0. Objects - Just White. A timeless classic, approved by The Thin White Duke\n" +
                "1. AO Types - Colors assigned by AssemblyObject type\n" +
                "2. Occupancy - Colors assigned by occupancy status:\n" +
                "  white (available), light green (unreachable), dark green (saturated)\n" +
                "3. Sequence - Colors assigned by assemblage sequence:\n" +
                "  black (older) < blue shades > white (younger)\n" +
                "4. Z Value - Colors assigned by World Z value of Assemblyobject Reference Plane origin - pink gradient\n" +
                "5. AO Weights - Colors assigned by AssemblyObjects weights\n" +
                "  grayscale gradient - black (minimum) <--> white (maximum)\n" +
                "6. Connectedness - Percentage of free handles\n" +
                "  traffic gradient - green (free) <--> red (fully occupied)\n" +
                "7. Orientation - Faux-Normal-style color map for AssemblyObjects direction vector\n" +
                "8. Z Orientation - Faux-Normal-style color map for AssemblyObjects Reference Plane Z vector\n" +
                "9. Receiver Values - Receiver value of each AssemblyObject\n" +
                "  white (minimum) <--> dark red (maximum)\n" +
                "10. Local Density - (Sum of Bounding Box volumes of the AssemblyObject + its connected and occluded neighbours) / (2x the volume of the Bounding Box for the largest AO in the AOSet)\n\n" +
                "attach a Value List for automatic list generation", GH_ParamAccess.item, 0);
            pManager.AddBooleanParameter("Colors only", "C", "If true, just the Colors will output", GH_ParamAccess.item, false);
            pManager[1].Optional = true; // Display mode is optional (default 0)
            pManager[2].Optional = true; // Colors only is optional (default False)

        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Colored Collision Meshes", "CM", "The Collision Meshes, colored by selected mode", GH_ParamAccess.list);
            pManager.AddLineParameter("Edges", "E", "Collision Mesh Edges for display", GH_ParamAccess.list);
            pManager.AddColourParameter("Colors", "C", "The colors by selected mode", GH_ParamAccess.list);
            pManager.AddNumberParameter("Values", "V", "The values for the selected mode (if pertinent), normalized in a 0-1 range", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Assemblage AOa = null;
            if (!DA.GetData("Assemblage", ref AOa) || AOa == null) return;

            DMType displayMode;
            int displayModeIndex = 0;
            DA.GetData(1, ref displayModeIndex);

            displayMode = displayModes[displayModeIndex].type;
            displayModeString = displayModes[displayModeIndex].name;
            UpdateMessage();

            bool colOnly = false;
            DA.GetData(2, ref colOnly);

            if (AOa == null)
            {
                Message = "";
                ExpirePreview(true);
                return;
            }
            meshes = colOnly ? null : new GH_Mesh[AOa.AssemblyObjects.BranchCount];
            edges = null;
            colors = new Color[AOa.AssemblyObjects.BranchCount];
            values = new GH_Number[AOa.AssemblyObjects.BranchCount];

            SetPreviewData(AOa, displayMode, colOnly);

            DA.SetDataList(0, meshes);
            DA.SetDataList(1, edges);
            DA.SetDataList(2, colors);
            DA.SetDataList(3, values);
        }

        private void SetPreviewData(Assemblage AOa, DMType displayMode, bool colorOnly)
        {
            Mesh joined;

            switch (displayMode)
            {
                case DMType.Objects: // just objects
                    Parallel.For(0, colors.Length, i =>
                    {
                        colors[i] = Color.White;
                    });
                    break;
                case DMType.AOTypes: // by type
                    // use type palette if number of types is smaller than the number of Colors in the palette
                    if (AOa.AOSet.Length <= Constants.AOTypePalette.Length)
                        Parallel.For(0, colors.Length, i =>
                        {
                            colors[i] = Constants.AOTypePalette[AOa.AssemblyObjects.Branches[i][0].Type];
                        });
                    // otherwise use random Colors
                    else
                        Parallel.For(0, colors.Length, i =>
                        {
                            colors[i] = Color.FromKnownColor(Constants.KnownColorList[AOa.AssemblyObjects.Branches[i][0].Type % Constants.KnownColorList.Count]);
                        });
                    break;
                case DMType.Occupancy: // occupancy
                    // saturated: 112,117,57 (dark green) - available: white - unreachable: 194,199,137 (light green)
                    int[] availableObjects = AOa.ExtractAvailableObjectsIndexes().Select(i => i.Value).ToArray();
                    int[] unreachableObjects = AOa.ExtractUnreachableObjectsIndexes().Select(i => i.Value).ToArray();

                    Parallel.For(0, colors.Length, i =>
                    {
                        int AOInd = AOa.AssemblyObjects.Branches[i][0].AInd;
                        if (availableObjects.Contains(AOInd)) colors[i] = Color.White;
                        else if (unreachableObjects.Contains(AOInd)) colors[i] = Color.FromArgb(194, 199, 137);
                        else
                            colors[i] = Color.FromArgb(112, 117, 57);
                    });

                    break;
                case DMType.Sequence: // sequence
                    Parallel.For(0, colors.Length, i =>
                    {
                        colors[i] = Constants.HistoryGradient.ColourAt(i / ((double)colors.Length));
                    });
                    break;
                case DMType.ZValue: // zHeight
                    BoundingBox AOaBBox = BoundingBox.Empty;
                    for (int i = 0; i < AOa.AssemblyObjects.BranchCount; i++)
                        AOaBBox.Union(AOa.AssemblyObjects.Branches[i][0].CollisionMesh.GetBoundingBox(false));
                    double minZ = AOaBBox.Min.Z;
                    double maxZ = AOaBBox.Max.Z;
                    double invZSpan = 1 / (maxZ - minZ);
                    Parallel.For(0, colors.Length, i =>
                    {
                        double heightParam = (AOa.AssemblyObjects.Branches[i][0].ReferencePlane.Origin.Z - minZ) * invZSpan;
                        values[i] = new GH_Number(heightParam);
                        colors[i] = Constants.ZHeightGradient.ColourAt(heightParam);
                    });
                    break;
                case DMType.AOWeights: // AssemblyObject Weight
                    double[] weights = new double[colors.Length];
                    Parallel.For(0, colors.Length, i =>
                    {
                        weights[i] = AOa.AssemblyObjects.Branches[i][0].Weight;
                    });
                    double minWeight = weights.Min();
                    double maxWeight = weights.Max();
                    double denominator = minWeight == maxWeight ? 0 : 1.0 / (maxWeight - minWeight);
                    Parallel.For(0, colors.Length, i =>
                    {
                        colors[i] = GH_Gradient.GreyScale().ColourAt((weights[i] - minWeight) * denominator);
                    });
                    break;
                case DMType.Connectedness: // connectedness (n. of non-free handlesTree/total handlesTree)
                    // maybe change to n. of connected Handles over total (exclude occluded Handles)
                    Parallel.For(0, colors.Length, i =>
                    {
                        double connectedness = 1 - (AOa.AssemblyObjects.Branches[i][0].Handles.Where(h => h.Occupancy == 0).Sum(x => 1) / (double)(AOa.AssemblyObjects.Branches[i][0].Handles.Length));
                        colors[i] = GH_Gradient.Traffic().ColourAt(connectedness);
                        values[i] = new GH_Number(connectedness);
                    });
                    break;
                case DMType.Orientation: // Orientation
                    Parallel.For(0, colors.Length, i =>
                    {
                        Vector3d v = AOa.AssemblyObjects.Branches[i][0].Direction;
                        //normal-map like
                        //SOURCE: https://en.wikipedia.org/wiki/Normal_mapping
                        //Colors[i] = Color.FromArgb((int)((v.X * .5 + .5) * 255), (int)((v.Y * .5 + .5) * 255), (int)(v.Z <= 0 ? 128 : 128 + v.Z * 127));
                        // faux normal map
                        colors[i] = Color.FromArgb((int)((v.X * .5 + .5) * 255), (int)((v.Y * .5 + .5) * 255), (int)((v.Z * .5 + .5) * 255));
                    });
                    break;
                case DMType.ZOrientation: // Z Orientation
                    Parallel.For(0, colors.Length, i =>
                    {
                        Vector3d v = AOa.AssemblyObjects.Branches[i][0].ReferencePlane.ZAxis;
                        //normal-map like
                        //SOURCE: https://en.wikipedia.org/wiki/Normal_mapping
                        //Colors[i] = Color.FromArgb((int)((v.X * .5 + .5) * 255), (int)((v.Y * .5 + .5) * 255), (int)(v.Z <= 0 ? 128 : 128 + v.Z * 127));
                        // faux normal map
                        colors[i] = Color.FromArgb((int)((v.X * .5 + .5) * 255), (int)((v.Y * .5 + .5) * 255), (int)((v.Z * .5 + .5) * 255));
                    });
                    break;
                case DMType.ReceiverValues:
                    double[] rValues = AOa.AssemblyObjects.AllData().Select(ao => ao.ReceiverValue).ToArray();

                    if (rValues.Length == 0) break;

                    double min = rValues.Min();
                    double max = rValues.Max();

                    // avoid division by 0
                    double factor = min == max ? 0 : 1.0 / (max - min);
                    Parallel.For(0, colors.Length, i =>
                    {
                        double receiverParam = (AOa.AssemblyObjects.Branches[i][0].ReceiverValue - min) * factor;
                        colors[i] = Constants.ReceiverValuesGradient.ColourAt(receiverParam);
                        values[i] = new GH_Number(receiverParam);
                    });
                    break;
                case DMType.LocalDensity: // Local Density
                    // volume of AO+connected/occluding neighbours individual Collision meshes/volume of twice the largest object in the AOset
                    // compute density values over a fixed voulme reference, then remap results on 0-1 scale
                    // best thing would be user choice of the reference volume, but I'm not keen on putting another input on this

                    // Find volumes for AOset Collision Meshes and keep largest
                    double[] AOsetCollisionVolumes = new double[AOa.AOSet.Length];
                    double AOCollisionMaxVolume = 0;
                    for (int i = 0; i < AOa.AOSet.Length; i++)
                    {
                        AOsetCollisionVolumes[i] = AOa.AOSet[i].CollisionMesh.Volume();
                        if (AOsetCollisionVolumes[i] > AOCollisionMaxVolume)
                            AOCollisionMaxVolume = AOsetCollisionVolumes[i];
                    }

                    double referenceVolume = 1.0 / (AOCollisionMaxVolume * 8);// equals to a box twice the scale

                    Parallel.For(0, colors.Length, i =>
                    {
                        // get collision volume for the present object
                        double localVolumes = AOsetCollisionVolumes[AOa.AssemblyObjects.Branches[i][0].Type];

                        // get collision volume for connected or occluding neighbours
                        for (int j = 0; j < AOa.AssemblyObjects.Branches[i][0].Handles.Length; j++)
                        {
                            // if Handle is free (consider also occluding objects) go to next handle
                            if (AOa.AssemblyObjects.Branches[i][0].Handles[j].Occupancy == 0) continue;

                            // else (if connected or occluded) add other object collision volume
                            int connectedIndex = AOa.AssemblyObjects.Branches[i][0].Handles[j].NeighbourObject;
                            int connectedType = AOa.AssemblyObjects[new GH_Path(connectedIndex), 0].Type;

                            localVolumes += AOsetCollisionVolumes[connectedType];
                        }

                        double localDensity = localVolumes * referenceVolume;
                        colors[i] = Constants.DensityGradient.ColourAt(localDensity);
                        values[i] = new GH_Number(localDensity);
                    });
                    // write a "normalize with limits" function?
                    //double[] normalizedDensities = MathUtils.NormalizeRange(localDensities);
                    //Parallel.For(0, Colors.Length, i =>
                    //{
                    //    Colors[i] = Constants.densityGradient.ColourAt(normalizedDensities[i]);
                    //});
                    break;
                // possible other display modes:

                // Valence (n. of handlesTree)?
                default:
                    goto case DMType.Objects;
            }

            if (colorOnly) return;

            // assign Colors to meshes
            Parallel.For(0, AOa.AssemblyObjects.BranchCount, i =>
            {
                Mesh m = new Mesh();
                m.CopyFrom(AOa.AssemblyObjects.Branches[i][0].CollisionMesh);
                m.Unweld(0, true);
                m.VertexColors.Clear();
                for (int j = 0; j < m.Vertices.Count; j++)
                    m.VertexColors.Add(colors[i]);
                meshes[i] = new GH_Mesh(m);

            });

            joined = new Mesh();
            joined.Append(meshes.Select(m =>m.Value));
            edges = MeshUtils.GetSilhouette(joined);
        }

        private void UpdateMessage()
        {
            Message = displayModeString;
        }

        /// <summary>
        /// Exposure override for position in the Subcategory (options primary to septenary)
        /// https://apidocs.co/apps/grasshopper/6.8.18210/T_Grasshopper_Kernel_GH_Exposure.htm
        /// </summary>
        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.secondary; }
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
                return Resources.Display_Assemblage;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("DCC6220A-D864-4D29-AE55-08A669E235B7"); }
        }
    }
}
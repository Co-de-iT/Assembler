using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Components;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino.Render;

namespace Assembler
{
    // Hmm... not working
    // waiting for a reply here: https://discourse.mcneel.com/t/grasshopper-raytraced-display-pipeline/141408/7
    public class D_MyCustomPreviewComponent : GH_CustomPreviewComponent
    {
        private List<GH_CustomPreviewItem> _items;
        private BoundingBox _boundingBox;
        public override BoundingBox ClippingBox => _boundingBox;
        public override bool IsBakeCapable => _items.Count > 0;
        /// <summary>
        /// Initializes a new instance of the D_MyCustomPreviewComponent class.
        /// </summary>
        public D_MyCustomPreviewComponent()
          : base()
        {
            Name = "D_MyCustomPreviewComponent";
            NickName = "D_MyCPV";
            Category = "Assembler";
            SubCategory = "Z_experimental";
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGeometryParameter("Geometries", "G", "Geometries to display", GH_ParamAccess.item);
            pManager.HideParameter(0);
            Param_OGLShader param_OGLShader = new Param_OGLShader();
            param_OGLShader.SetPersistentData(new GH_Material(Color.Plum));
            pManager.AddParameter(param_OGLShader, "Material", "M", "The material override", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            IGH_GeometricGoo destination = null;
            GH_Material destination2 = null;
            if (DA.GetData(0, ref destination) && DA.GetData(1, ref destination2) && destination.IsValid)
            {
                if (!(destination is IGH_PreviewData))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, destination.TypeName + " does not support previews");
                }
                else if (destination2.Value != null)
                {
                    GH_CustomPreviewItem item = default(GH_CustomPreviewItem);
                    item.Geometry = (IGH_PreviewData)destination;
                    item.Shader = destination2.Value;
                    item.Colour = destination2.Value.Diffuse;
                    item.Material = destination2;
                    _items.Add(item);
                    _boundingBox.Union(destination.Boundingbox);
                }
            }
        }
        public override void AppendRenderGeometry(GH_RenderArgs args)
        {
            GH_Document gH_Document = OnPingDocument();
            if (gH_Document != null && (gH_Document.PreviewMode == GH_PreviewMode.Disabled || gH_Document.PreviewMode == GH_PreviewMode.Wireframe))
            {
                return;
            }
            List<GH_CustomPreviewItem> items = _items;
            if (items != null)
            {
                items = new List<GH_CustomPreviewItem>(items);
                if (items.Count != 0)
                {
                    foreach (GH_CustomPreviewItem item in items)
                    {
                        item.PushToRenderPipeline(args);
                    }
                }
            }
        }

        protected override void BeforeSolveInstance()
        {
            _items = new List<GH_CustomPreviewItem>();
            _boundingBox = BoundingBox.Empty;
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
            get { return new Guid("070EF045-895B-439D-AAE8-11EEE9CBA7B4"); }
        }
    }
}
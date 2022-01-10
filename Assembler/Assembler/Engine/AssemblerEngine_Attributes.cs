using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel.Attributes;

namespace Assembler.Engine
{
    class AssemblerEngine_Attributes : GH_ComponentAttributes
    {
        public AssemblerEngine_Attributes(AssemblerEngine owner) : base(owner)
        {
        }

        public override GH_ObjectResponse RespondToMouseDoubleClick(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            bool flag = this.ContentBox.Contains(e.CanvasLocation);
            GH_ObjectResponse result;
            if (flag)
            {
                AssemblerEngine fileToScript = (AssemblerEngine)this.Owner;
                fileToScript.ExpireSolution(true);
                result = GH_ObjectResponse.Handled;// 3;
            }
            else
            {
                result = 0;
            }
            return result;
        }
    }
}

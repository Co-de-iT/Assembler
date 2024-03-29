﻿using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel.Attributes;

namespace Assembler
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
                AssemblerEngine engineX = (AssemblerEngine)this.Owner;
                engineX.ExpireSolution(true);
                result = GH_ObjectResponse.Handled;
            }
            else
            {
                result = 0;
            }
            return result;
        }
    }
}

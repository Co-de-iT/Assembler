using AssemblerLib;
using Eto.Forms;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assembler.Utils
{
    class FieldGoo : GH_Goo<Field>
    {
        public FieldGoo()
        {
            m_value = new Field(Box.Empty, 1);
        }

        public FieldGoo(Field field)
        {
            if (field == null)
                field = new Field(Box.Empty, 1);
            m_value = field;
        }

        public override bool IsValid
        {
            get
            {
                if (Value == null || Value.GetGH_Points().Length == 0) return false;
                return true;
            }
        }
        public override string IsValidWhyNot
        {
            get
            {
                if (Value == null) { return "No internal Field instance"; }
                else { return string.Empty; }
            }
        }

        public override string TypeName
        {
            get { return "Assembler Field"; }
        }

        public override string TypeDescription
        {
            get { return Value.ToString(); }
        }

        public override IGH_Goo Duplicate()
        {
            return new FieldGoo(new Field(Value));
        }

        public override string ToString()
        {
            throw new NotImplementedException();
        }
    }
}

using AssemblerLib;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel;

namespace Assembler
{
    public class AssemblageGoo : GH_Goo<Assemblage>
    {
        public override bool IsValid => Value != null;

        public override string TypeName => "Assemblage";

        public override string TypeDescription => "Defines an Assemblage for the Assemblage class";

        public AssemblageGoo()
        {}

        public AssemblageGoo(Assemblage assemblage)
        {
            Value = assemblage;
        }

        public override IGH_Goo Duplicate()
        {
            return GH_Convert.ToGoo(Value);
        }

        public override string ToString()
        {
            return "Assemblage containing " + Value.assemblyObjects.Count + " AssemblyObjects of " + Value.AOset.Length + " different types";
        }
    }
}

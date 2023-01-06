using AssemblerLib;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace Assembler
{
    class RuleGoo : GH_Goo<Rule>
    {
        public override bool IsValid => true;

        public override string TypeName => "Rule";

        public override string TypeDescription => "A GH wrapper for the Rule struct";


        public RuleGoo()
        { }

        public RuleGoo(Rule rule)
        {
            Value = rule;
        }


        public override IGH_Goo Duplicate()
        {
            return GH_Convert.ToGoo(Value);
        }

        public override string ToString()
        {
            return $"{Value.rT}|{Value.rH}={Value.rRA}<{Value.sT}|{Value.sH}%{Value.iWeight}";
            //return string.Format("{0}|{1}={2}<{3}|{4}%{5}", Value.rT, Value.rH, Value.rRA, Value.sT, Value.sH, Value.iWeight);
        }
    }
}

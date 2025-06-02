using AssemblerLib;
using AssemblerLib.Utils;
using GH_IO.Serialization;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Assembler.Utils
{
    // TODO: complete this
    public class AssemblageGoo : GH_GeometricGoo<Assemblage>
    {
        #region properties
        public override bool IsValid
        {
            get
            {
                if (Value == null || Value.AssemblyObjects == null) return false;
                return true;
            }
        }

        public override string IsValidWhyNot
        {
            get
            {
                if (Value == null) { return "No internal Assemblage instance"; }
                if (Value.AssemblyObjects == null) { return "No AssemblyObjects in the Assemblage"; }
                else { return string.Empty; }
            }
        }

        public override string TypeName => "Assemblage";

        public override string TypeDescription => "Defines an Assemblage for the Assemblage class";

        public override BoundingBox Boundingbox => throw new System.NotImplementedException();

        #endregion

        #region constructors
        public AssemblageGoo()
        {
            m_value = new Assemblage();
        }

        public AssemblageGoo(Assemblage assemblage)
        {
            if (assemblage == null)
                assemblage = new Assemblage();

            m_value = assemblage;
        }

        #endregion

        public override string ToString()
        {
            return "Assemblage containing " + Value.AssemblyObjects.BranchCount + " AssemblyObjects of " + Value.AOSet.Length + " different types";
        }

        public override IGH_GeometricGoo DuplicateGeometry()
        {
            return DuplicateAssemblage();
        }

        public AssemblageGoo DuplicateAssemblage()
        {
            return new AssemblageGoo(Value == null? new Assemblage():AssemblageUtils.Clone(Value));
        }

        public override BoundingBox GetBoundingBox(Transform xform)
        {
            throw new System.NotImplementedException();
        }

        public override IGH_GeometricGoo Transform(Transform xform)
        {
            throw new System.NotImplementedException();
        }

        public override IGH_GeometricGoo Morph(SpaceMorph xmorph)
        {
            throw new System.NotImplementedException();
        }
        public override bool Write(GH_IWriter writer)
        {
            // EXAMPLE: write the AssemblyObjects in the Assemblage
            // better: see this:
            // SOURCE: https://discourse.mcneel.com/t/de-serialize-nested-classes-gh-geometricgoo/132086/2?u=ale2x72
            AssemblyObjectGoo aoGoo = new AssemblyObjectGoo(Value.AssemblyObjects.Branches[0][0]);
            aoGoo.Write(writer.CreateChunk("AO_"+0));
            return base.Write(writer);
        }
        public override bool Read(GH_IReader reader)
        {
            // EXAMPLE: read the AssemblyObject in the Assemblage (see link in Write)
            AssemblyObjectGoo aoGoo = new AssemblyObjectGoo();
            aoGoo.Read(reader.FindChunk("AO_" + 0));
            return base.Read(reader);
        }
    }
}

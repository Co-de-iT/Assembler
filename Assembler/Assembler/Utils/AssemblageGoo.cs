using AssemblerLib;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel;
using Rhino.Geometry;
using GH_IO.Serialization;
using System;
using Assembler.Utils;

namespace Assembler
{
    public class AssemblageGoo : GH_GeometricGoo<Assemblage>
    {
        #region properties
        public override bool IsValid
        {
            get
            {
                if (Value == null || Value.assemblyObjects == null) return false;
                return true;
            }
        }

        public override string IsValidWhyNot
        {
            get
            {
                if (Value == null) { return "No internal Assemblage instance"; }
                if (Value.assemblyObjects == null) { return "No AssemblyObjects in the Assemblage"; }
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
            return "Assemblage containing " + Value.assemblyObjects.Count + " AssemblyObjects of " + Value.AOset.Length + " different types";
        }

        public override IGH_GeometricGoo DuplicateGeometry()
        {
            return DuplicateAssemblage();
        }

        public AssemblageGoo DuplicateAssemblage()
        {
            return new AssemblageGoo(Value == null? new Assemblage():Utilities.Clone(Value));
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
            return base.Write(writer);
        }
        public override bool Read(GH_IReader reader)
        {
            return base.Read(reader);
        }
    }
}

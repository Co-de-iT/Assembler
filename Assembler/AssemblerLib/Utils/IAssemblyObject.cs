using Rhino.Geometry;

namespace AssemblerLib
{
    /// <summary>
    /// Interface for the AssemblyObject type
    /// For future extensibility of AssemblyObjects
    /// </summary>
    /// <exclude>Exclude from documentation</exclude>
    // BUG: this can't be used - structs cannot be used as properties (see opening comment on AssemblyObject class)
    public interface IAssemblyObject
    {

        /// <summary>
        /// A Mesh for collision detections
        /// </summary>
        Mesh CollisionMesh { get; set; }
        /// <summary>
        /// A Mesh for avoiding false positives in collision detection (overlaps)
        /// </summary>
        Mesh OffsetMesh { get; set; }
        /// <summary>
        /// Reference Plane for the AssemblyObject
        /// </summary>
        Plane ReferencePlane { get; set; }
        /// <summary>
        /// Handles for connectivity and assemblage operations
        /// </summary>
        Handle[] Handles { get; set; }
        /// <summary>
        /// Direction Vector - for vector <see cref="Field"/> generation/interaction
        /// </summary>
        Vector3d Direction { get; set; }
        /// <summary>
        /// AssemblyObject Type - identifies a unique object index in an <see cref="Assemblage.AOSet"/>
        /// </summary>
        int Type { get; set; }
        /// <summary>
        /// AssemblyObject Name
        /// </summary>
        string Name { get; set; }
        /// <summary>
        /// AssemblyObject unique index in an <see cref="Assemblage"/>
        /// </summary>
        int AInd { get; set; }

        //void MapHandles();

        void Transform(Transform xForm);
    }
}

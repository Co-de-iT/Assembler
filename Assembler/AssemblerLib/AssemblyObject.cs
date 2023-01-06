using AssemblerLib.Utils;
using Newtonsoft.Json;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AssemblerLib
{
    /// <summary>
    /// Assemblage Object with extended connectivity and collision-check properties
    /// </summary>

    // DO NOT USE Structs (such as Plane, Handles, Vectors and Points) as Properties, leave them as fields
    // Here's why: https://stackoverflow.com/questions/18292087/accessing-and-changing-structs-as-property-vs-as-field
    // "When you access a field, you are accessing the actual struct.
    // When you access it through property, you call a method that returns whatever is stored in the property.
    // In the case of a struct, which is a value type, you will get back a copy of the struct.
    // Apparently that copy is not a variable and cannot be changed."
    public class AssemblyObject
    {
        #region properties

        /// <summary>
        /// A Mesh for collision detections
        /// </summary>
        public Mesh CollisionMesh { get; set; }
        /// <summary>
        /// A Mesh for avoiding false positives in collision detection (overlaps)
        /// </summary>
        public Mesh OffsetMesh { get; set; }
        /// <summary>
        /// Reference Plane for the AssemblyObject
        /// </summary>
        public Plane ReferencePlane;
        /// <summary>
        /// Handles for connectivity and assemblage operations
        /// </summary>
        public Handle[] Handles;
        /// <summary>
        /// Direction Vector - for vector <see cref="Field"/> generation/interaction
        /// </summary>
        public Vector3d Direction;
        /// <summary>
        /// AssemblyObject Type - identifies a unique object index in an <see cref="Assemblage.AOSet"/>
        /// </summary>
        public int Type;
        /// <summary>
        /// AssemblyObject Name
        /// </summary>
        public string Name;
        /// <summary>
        /// AssemblyObject unique index in an <see cref="Assemblage"/>
        /// </summary>
        public int AInd
        { get => aInd; set => aInd = value; }
        private int aInd;
        /// <summary>
        /// List of <see cref="AssemblyObject"/>s occluded by this object and their respective <see cref="Handle"/>.
        /// Each List item is an int[] array with 2 items:
        /// <list type="bullet">
        /// <item>0 - <see cref="AInd"/></item>
        /// <item>1 - index of its occluded <see cref="Handle"/></item>
        /// </list>
        /// </summary>
        public List<int[]> OccludedNeighbours;
        /// <summary>
        /// Scalar value for density evaluation or scalar <see cref="Field"/> generation/interaction
        /// </summary>
        public double Weight;
        /// <summary>
        /// Initial value of <see cref="Weight"/> (for resetting purposes)
        /// </summary>
        public double IdleWeight;
        /// <summary>
        /// Integer Weight for heuristics assignment
        /// </summary>
        public int IWeight;
        /// <summary>
        /// stores a value during the Assemblage when acting as receiver;
        /// this value depends on the active <see cref="HeuristicsSettings.ReceiverSelectionMode"/>
        /// </summary>
        public double ReceiverValue;
        /// <summary>
        /// stores a value during the Assemblage when acting as a sender candidate;
        /// /// this value depends on the active <see cref="HeuristicsSettings.SenderSelectionMode"/>
        /// </summary>
        public double SenderValue;
        /// <summary>
        /// True to force the object's reference plane Z axys parallel to the World's Z axis
        /// </summary>
        public bool WorldZLock;

        #endregion properties

        #region fields

        // internal and experimental features
        /// <summary>
        /// List of optional children objects (for composite object) - NOT IMPLEMENTED YET
        /// </summary>
        internal List<AssemblyObject> children;
        /// <summary>
        /// internal map of Handles for composite object - NOT IMPLEMENTED YET
        /// </summary>
        internal List<int[]> handleMap;
        /// <summary>
        /// Supports - NOT IMPLEMENTED YET
        /// </summary>
        public List<Support> supports;
        /// <summary>
        /// n. of minimum connected Supports to consider the object supported - NOT IMPLEMENTED YET
        /// </summary>
        public int minSupports;
        /// <summary>
        /// True if minimum amount of required supports are connected - NOT IMPLEMENTED YET
        /// </summary>
        public bool supported;

        #endregion fields

        #region constructors
        /// <summary>
        /// empty constructor
        /// </summary>
        public AssemblyObject()
        {
            // no parameter is defined, this constructor builds an empty AssemblyObject
            CollisionMesh = new Mesh();
            OffsetMesh = new Mesh();
            ReferencePlane = Plane.Unset;
            Direction = Vector3d.Zero;
            supports = new List<Support>();
        }

        /// <summary>
        /// Constructor used for JSON deserialization
        /// This constructor DOES NOT execute a deep copy of data
        /// If you need to duplicate an <see cref="AssemblyObject"/> use the <see cref="Utils.AssemblyObjectUtils.Clone(AssemblyObject)"/> method
        /// or <see cref="Utils.AssemblyObjectUtils.CloneWithConnectivity(AssemblyObject)"/> method
        /// </summary>
        /// <param name="CollisionMesh"></param>
        /// <param name="OffsetMesh"></param>
        /// <param name="Handles"></param>
        /// <param name="ReferencePlane"></param>
        /// <param name="Direction"></param>
        /// <param name="aInd"></param>
        /// <param name="OccludedNeighbours"></param>
        /// <param name="Name"></param>
        /// <param name="Type"></param>
        /// <param name="Weight"></param>
        /// <param name="IdleWeight"></param>
        /// <param name="IWeight"></param>
        /// <param name="supports"></param>
        /// <param name="minSupports"></param>
        /// <param name="supported"></param>
        /// <param name="WorldZLock"></param>
        /// <param name="children"></param>
        /// <param name="handleMap"></param>
        /// <param name="ReceiverValue"></param>
        /// <param name="SenderValue"></param>
        [JsonConstructor]
        public AssemblyObject(Mesh CollisionMesh, Mesh OffsetMesh, Handle[] Handles, Plane ReferencePlane, Vector3d Direction, int aInd, List<int[]> OccludedNeighbours,
            string Name, int Type, double Weight, double IdleWeight, int IWeight, List<Support> supports,
            int minSupports, bool supported, bool WorldZLock, List<AssemblyObject> children, List<int[]> handleMap, double ReceiverValue, double SenderValue)
        {
            this.CollisionMesh = CollisionMesh;
            this.OffsetMesh = OffsetMesh;
            this.ReferencePlane = ReferencePlane;
            this.Direction = Direction;
            this.Handles = Handles;
            this.aInd = aInd;
            this.OccludedNeighbours = OccludedNeighbours;
            this.Name = Name;
            this.Type = Type;
            this.Weight = Weight;
            this.IdleWeight = IdleWeight;
            this.IWeight = IWeight;
            this.supports = supports;
            this.minSupports = minSupports;
            this.supported = supported;
            this.WorldZLock = WorldZLock;
            this.children = children;
            this.handleMap = handleMap;
            this.ReceiverValue = ReceiverValue;
            this.SenderValue = SenderValue;
        }

        /// <summary>
        /// Builds an <see cref="AssemblyObject"/> from all required parameters
        /// </summary>
        /// <param name="CollisionMesh">Mesh for collision calculation</param>
        /// <param name="Handles"><see cref="Handle"/>s for <see cref="Assemblage"/></param>
        /// <param name="ReferencePlane">Reference Plane</param>
        /// <param name="Direction">Direction for Vector <see cref="Field"/> interaction</param>
        /// <param name="Name">AssemblyObject kind Name</param>
        /// <param name="Type">AssemblyObject type id</param>
        /// <param name="Weight">Scalar for density operations or Field interactions</param>
        /// <param name="IWeight">Integer Weight</param>
        /// <param name="WorldZLock">Lock orientation of reference Plane Z-axis to World Z-axis</param>
        public AssemblyObject(Mesh CollisionMesh, IEnumerable<Handle> Handles, Plane ReferencePlane, Vector3d Direction, string Name, int Type, double Weight, int IWeight,
            bool WorldZLock)
        {
            // collision Mesh operations
            this.CollisionMesh = new Mesh();
            this.CollisionMesh.CopyFrom(CollisionMesh);
            this.CollisionMesh.Vertices.Align(Constants.RhinoAbsoluteTolerance);
            this.CollisionMesh.Weld(Math.PI);
            this.CollisionMesh.RebuildNormals();
            // generate offset Mesh
            double offsetTol = Constants.RhinoAbsoluteTolerance * 2.5;
            OffsetMesh = MeshUtils.MeshOffsetWeightedAngle(this.CollisionMesh, offsetTol); // do not use standard Mesh offset

            // orientation data
            this.ReferencePlane = ReferencePlane;
            this.Direction = Direction;

            // connectivity data
            this.Handles = Handles.Select(h => HandleUtils.Clone(ref h)).ToArray();
            //this.Handles = Handles.Select(h => new Handle(h)).ToArray(); // do NOT use this formula
            OccludedNeighbours = new List<int[]>();

            // ID and other non-geometry data
            this.Name = Name;
            this.Type = Type;
            this.Weight = Weight;
            IdleWeight = Weight;
            this.IWeight = IWeight;

            // Z Lock
            this.WorldZLock = WorldZLock;

            // "in Assemblage" values
            ReceiverValue = double.NaN;
            SenderValue = double.NaN;
            aInd = -1;

            // supports operations - NOT IMPLEMENTED YET
            supports = new List<Support>();
            minSupports = 0;
            supported = false;

            // children and handlemap initialization - NOT IMPLEMENTED YET
            children = new List<AssemblyObject>();
            handleMap = new List<int[]>();
        }

        #endregion

        #region composite constructors

        /// <summary>
        /// Builds a composite AssemblyObject from a list of children and a custom CollisionMesh - Children objects are NOT IMPLEMENTED YET
        /// </summary>
        /// <param name="collisionMesh"></param>
        /// <param name="referencePlane"></param>
        /// <param name="direction"></param>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="weight"></param>
        /// <param name="absoluteZLock"></param>
        /// <param name="children"></param>
        public AssemblyObject(Mesh collisionMesh, Plane referencePlane, Vector3d direction, string name, int type, double weight, bool absoluteZLock, List<AssemblyObject> children) :
            this(collisionMesh, new Handle[] { }, referencePlane, direction, name, type, weight, 1, absoluteZLock)
        {
            // copy children objects
            this.children = children.Select(ao => AssemblyObjectUtils.Clone(ao)).ToList();

            // generate internal obstruction and connectivity status
            AssemblageUtils.ObstructionCheckList(this.children);

            // set Handles and correspondance array
            handleMap = new List<int[]>();
            MapHandles();
        }

        /// <summary>
        /// Builds a composite AssemblyObject from a list of children, a custom CollisionMesh, and a custom set of Handles - Children objects are NOT IMPLEMENTED YET
        /// </summary>
        /// <param name="collisionMesh"></param>
        /// <param name="handles"></param>
        /// <param name="referencePlane"></param>
        /// <param name="direction"></param>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="weight"></param>
        /// <param name="absoluteZLock"></param>
        /// <param name="children"></param>
        public AssemblyObject(Mesh collisionMesh, IEnumerable<Handle> handles, Plane referencePlane, Vector3d direction, string name, int type, double weight, bool absoluteZLock, List<AssemblyObject> children) :
            this(collisionMesh, handles, referencePlane, direction, name, type, weight, 1, absoluteZLock)
        {
            // copy children objects
            this.children = children.Select(ao => AssemblyObjectUtils.Clone(ao)).ToList();

            handleMap = new List<int[]>();
        }

        #endregion

        #region methods

        private void MapHandles()
        {
            List<Handle> compositeHandles = new List<Handle>();

            // generate composite object Handles and handlemap
            for (int i = 0; i < children.Count; i++)
                for (int j = 0; j < children[i].Handles.Length; j++)
                    if (children[i].Handles[j].Occupancy == 0)
                    {
                        compositeHandles.Add(HandleUtils.Clone(ref children[i].Handles[j]));
                        //compositeHandles.Add(new Handle(children[i].Handles[j]));
                        handleMap.Add(new int[] { i, j });
                    }
            Handles = compositeHandles.ToArray();
        }

        /// <summary>
        /// Transform the <see cref="AssemblyObject"/> using a generic Transformation
        /// </summary>
        /// <param name="xForm">The Transformation to apply</param>
        public void Transform(Transform xForm)
        {

            // transform geometries
            CollisionMesh.Transform(xForm);
            OffsetMesh.Transform(xForm);
            ReferencePlane.Transform(xForm);
            Direction.Transform(xForm);

            // transform Handles (do not use a foreach loop)
            for (int i = 0; i < Handles.Length; i++) Handles[i].Transform(xForm);

            // transform children (if any)
            if (children != null)
                for (int i = 0; i < children.Count; i++) children[i].Transform(xForm);

            // transform Supports (if they exist)
            if (supports != null)
                for (int i = 0; i < supports.Count; i++) supports[i].Transform(xForm);
        }

        #endregion
    }
}

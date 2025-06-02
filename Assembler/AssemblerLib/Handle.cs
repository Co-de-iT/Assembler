using AssemblerLib.Utils;
using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace AssemblerLib
{
    /// <summary>
    /// Handle structure for connection Handles
    /// </summary>
    public struct Handle
    {
        /// <summary>
        /// Sender plane
        /// </summary>
        public Plane SenderPlane;
        /// <summary>
        /// Receiver planes
        /// </summary>
        public Plane[] ReceiverPlanes;
        /// <summary>
        /// Handle type identifier
        /// </summary>
        public int Type;
        /// <summary>
        /// Receiver rotations
        /// </summary>
        public double[] Rotations;
        /// <summary>
        /// Records used rotation (-1 at start)
        /// </summary>
        public int RotationIndex;
        /// <summary>
        /// Rotations dictionary (degrees, index)
        /// </summary>
        public Dictionary<double, int> RDictionary;
        /// <summary>
        /// Weight
        /// </summary>
        public double Weight;
        /// <summary>
        /// initial value of <see cref="Weight"/> (for resetting purposes)
        /// </summary>
        public double IdleWeight;
        /// <summary>
        /// Occupancy status
        /// <list type="bullet">
        /// <item><description>-1 occluded</description></item>
        /// <item><description>0 available</description></item>
        /// <item><description>1 connected</description></item>
        /// <item><description>2 contact - secondary connection</description></item>
        /// </list>
        /// </summary>
        public int Occupancy;
        //public enum OccupancyStatus : int { Occluded = -1, Available = 0, Connected = 1, Contact = 2 }
        /// <summary>
        /// Neighbour object index
        /// <list type="bullet">
        /// <item><description>-1 - when handle is available</description></item>
        /// <item><description>index of connected neighbour object - when handle is connected or occluded by another <see cref="AssemblyObject"/></description></item>
        /// </list>
        /// </summary>
        public int NeighbourObject;
        /// <summary>
        /// Neighbour handle index
        /// <list type="bullet">
        /// <item><description>-1 - when handle is either available or occluded</description></item>
        /// <item><description>neighbour object's handle index - when handle is connected</description></item>
        /// </list>
        /// </summary>
        public int NeighbourHandle;

        /// <summary>
        /// Builds a Handle from an L-shaped polyline, Handle type and List of rotations
        /// </summary>
        /// <param name="poly"></param>
        /// <param name="type"></param>
        /// <param name="weight"></param>
        /// <param name="rotations"></param>
        public Handle(Polyline poly, int type, double weight, List<double> rotations) : this(new Plane(poly[1], poly[0] - poly[1], poly[2] - poly[1]), type, weight, rotations)
        { }

        /// <summary>
        /// Builds a Handle from constituting parameters
        /// </summary>
        /// <param name="plane">The base (sender) <see cref="Plane"/> for the Handle</param>
        /// <param name="type">The Handle type</param>
        /// <param name="weight">The Handle weight</param>
        /// <param name="rotations">List of rotation angles in receiver mode</param>
        public Handle(Plane plane, int Type, double Weight, List<double> rotations)
        {
            this.Type = Type;
            this.Weight = Weight;
            IdleWeight = Weight;
            Occupancy = 0;
            NeighbourObject = -1;
            NeighbourHandle = -1;
            // sender plane
            SenderPlane = plane;
            Rotations = rotations.ToArray();
            RotationIndex = -1;
            RDictionary = new Dictionary<double, int>();
            // generate relative receiving Handles
            ReceiverPlanes = new Plane[rotations.Count];
            for (int i = 0; i < rotations.Count; i++)
            {
                ReceiverPlanes[i] = SenderPlane;
                // first rotate              (rotations arrive in degrees)
                ReceiverPlanes[i].Rotate(MathUtils.DegreesToRadians(rotations[i]), ReceiverPlanes[i].ZAxis);
                // then flip
                ReceiverPlanes[i].Rotate(Math.PI, ReceiverPlanes[i].YAxis);
                // add rotation to dictionary
                RDictionary.Add(rotations[i], i);
            }
        }

        /// <summary>
        /// Transform a <see cref="Handle"/> using a generic Transformation
        /// </summary>
        /// <param name="Xform">Transformation to apply</param>
        public void Transform(Transform Xform)
        {
            SenderPlane.Transform(Xform);
            // DO NOT use foreach - it does not work (you cannot change parts of a looping variable)
            for (int i = 0; i < ReceiverPlanes.Length; i++) ReceiverPlanes[i].Transform(Xform);
        }

        /// <summary>
        /// Returns a string with the Handle Occupancy status:
        /// <list type="bullet">
        /// <item><description>Occluded</description></item>
        /// <item><description>Available</description></item>
        /// <item><description>Connected</description></item>
        /// <item><description>Contact (secondary connection)</description></item>
        /// </list>
        /// </summary>
        /// <returns>The Occupancy status as string</returns>
        public string HandleStatus()
        {
            switch (Occupancy)
            {
                case -1: return "Occluded";
                case 0: return "Available";
                case 1: return "Connected";
                case 2: return "Contact";
                default: return "";
            }
        }

        public override string ToString()
        {
            return string.Format("Handle type {0} . {1} rotations . {2}", Type, Rotations.Length, HandleStatus());
        }

        ///// <summary>
        ///// Equals check if Handles have the same type, IdleWeight and Rotations
        ///// </summary>
        ///// <param name="A"></param>
        ///// <param name="B"></param>
        ///// <returns>true if Handles are considered equal</returns>
        //public static bool operator ==(Handle A, Handle B)
        //{
        //    bool equals = true;

        //    if(A.Type != B.Type) return false;
        //    if(A.Rotations.Length != B.Rotations.Length) return false;
        //    for (int i = 0; i < A.Rotations.Length; i++)
        //        if (A.Rotations[i] != B.Rotations[i]) return false;

        //    if(A.IdleWeight != B.IdleWeight) return false;

        //    return equals;
        //}

        //public static bool operator !=(Handle A, Handle B) => !(A == B);
    }
}

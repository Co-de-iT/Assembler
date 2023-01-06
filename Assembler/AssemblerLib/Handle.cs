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
        public Plane Sender;
        /// <summary>
        /// Receiver planes
        /// </summary>
        public Plane[] Receivers;
        /// <summary>
        /// Handle type identifier
        /// </summary>
        public int Type;
        /// <summary>
        /// Receiver rotations
        /// </summary>
        public double[] Rotations;
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
        /// </list>
        /// </summary>
        public int Occupancy;
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
            Sender = plane;
            Rotations = rotations.ToArray();
            RDictionary = new Dictionary<double, int>();
            // generate relative receiving Handles
            Receivers = new Plane[rotations.Count];
            for (int i = 0; i < rotations.Count; i++)
            {
                Receivers[i] = Sender;
                // first rotate
                Receivers[i].Rotate(MathUtils.DegreesToRadians(rotations[i]), Receivers[i].ZAxis); // rotations arrive in degrees
                // then flip
                Receivers[i].Rotate(Math.PI, Receivers[i].YAxis);
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
            Sender.Transform(Xform);
            // DO NOT use foreach - it does not work (you cannot change parts of a looping variable)
            for (int i = 0; i < Receivers.Length; i++) Receivers[i].Transform(Xform);
        }

        /// <summary>
        /// Returns a string with the Handle Occupancy status:
        /// <list type="bullet">
        /// <item><description>Occluded</description></item>
        /// <item><description>Available</description></item>
        /// <item><description>Connected</description></item>
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
                default: return "";
            }
        }

        public override string ToString()
        {
            return string.Format("Handle type {0} . {1} rotations . {2}", Type, Rotations.Length, HandleStatus());
        }

    }
}

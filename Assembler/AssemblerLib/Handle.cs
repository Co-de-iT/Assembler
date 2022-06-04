using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace AssemblerLib
{
    /// <summary>
    /// Handle structure for connection handles
    /// </summary>
    public struct Handle
    {
        /// <summary>
        /// Sender plane
        /// </summary>
        public Plane sender;
        /// <summary>
        /// Receiver planes
        /// </summary>
        public Plane[] receivers;
        /// <summary>
        /// Handle type identifier
        /// </summary>
        public int type;
        /// <summary>
        /// Receiver rotations
        /// </summary>
        public double[] rRotations;
        /// <summary>
        /// Rotations dictionary (degrees, index)
        /// </summary>
        public Dictionary<double, int> rDictionary;
        /// <summary>
        /// weight
        /// </summary>
        public double weight;
        /// <summary>
        /// initial value of <see cref="weight"/> (for resetting purposes)
        /// </summary>
        public double idleWeight;
        /// <summary>
        /// Occupancy status
        /// <list type="bullet">
        /// <item><description>-1 occluded</description></item>
        /// <item><description>0 available</description></item>
        /// <item><description>1 connected</description></item>
        /// </list>
        /// </summary>
        public int occupancy;
        /// <summary>
        /// Neighbour object index
        /// <list type="bullet">
        /// <item><description>-1 - when handle is available</description></item>
        /// <item><description>index of connected neighbour object - when handle is connected or occluded by another <see cref="AssemblyObject"/></description></item>
        /// </list>
        /// </summary>
        public int neighbourObject;
        /// <summary>
        /// Neighbour handle index
        /// <list type="bullet">
        /// <item><description>-1 - when handle is either available or occluded</description></item>
        /// <item><description>neighbour object's handle index - when handle is connected</description></item>
        /// </list>
        /// </summary>
        public int neighbourHandle;

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
        public Handle(Plane plane, int type, double weight, List<double> rotations)
        {
            this.type = type;
            this.weight = weight;
            idleWeight = weight;
            occupancy = 0;
            neighbourObject = -1;
            neighbourHandle = -1;
            // sender plane
            sender = plane;
            rRotations = rotations.ToArray();
            rDictionary = new Dictionary<double, int>();
            // generate relative receiving handles
            receivers = new Plane[rotations.Count];
            for (int i = 0; i < rotations.Count; i++)
            {
                receivers[i] = sender;
                // first rotate
                receivers[i].Rotate(Utilities.DegreesToRadians(rotations[i]), receivers[i].ZAxis); // rotations arrive in degrees
                // then flip
                receivers[i].Rotate(Math.PI, receivers[i].YAxis);
                // add rotation to dictionary
                rDictionary.Add(rotations[i], i);
            }
        }

        /// <summary>
        /// Transform a <see cref="Handle"/> using a generic Transformation
        /// </summary>
        /// <param name="Xform">Transformation to apply</param>
        public void Transform(Transform Xform)
        {
            sender.Transform(Xform);
            // DO NOT use foreach - it does not work (you cannot change parts of a looping variable)
            for (int i = 0; i < receivers.Length; i++) receivers[i].Transform(Xform);
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
            switch (occupancy)
            {
                case -1: return "Occluded";
                case 0: return "Available";
                case 1: return "Connected";
                default: return "";
            }
        }

        public override string ToString()
        {
            return string.Format("Handle type {0} . {1} rotations . {2}", type, rRotations.Length, HandleStatus());
        }

    }
}

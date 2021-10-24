using System;
using System.Collections.Generic;

namespace AssemblerLib
{
    /// <summary>
    /// Structure storing indexes to automate Assemblage operations
    /// </summary>
    public readonly struct Rule
    {
        /// <summary>
        /// Sender object name
        /// </summary>
        public readonly string senderName;
        /// <summary>
        /// Receiver object name
        /// </summary>
        public readonly string receiverName;
        /// <summary>
        /// sender object type
        /// </summary>
        public readonly int sT;
        /// <summary>
        /// sender handle index
        /// </summary>
        public readonly int sH;
        /// <summary>
        /// receiver object type
        /// </summary>
        public readonly int rT;
        /// <summary>
        /// receiver handle index
        /// </summary>
        public readonly int rH;
        /// <summary>
        /// receiver rotation index
        /// </summary>
        public readonly int rR;
        /// <summary>
        /// receiver rotation angle (in degrees)
        /// </summary>
        public readonly double rRA;
        /// <summary>
        /// integer weight
        /// </summary>
        public readonly int iWeight;

        /// <summary>
        /// Constructs a Rule from constituting parameters
        /// </summary>
        /// <param name="receiverName"></param>
        /// <param name="rT"></param>
        /// <param name="rH"></param>
        /// <param name="rR"></param>
        /// <param name="rRA"></param>
        /// <param name="senderName"></param>
        /// <param name="sT"></param>
        /// <param name="sH"></param>
        /// <param name="iWeight"></param>
        public Rule(string receiverName, int rT, int rH, int rR, double rRA, string senderName, int sT, int sH, int iWeight)
        {
            this.senderName = senderName;
            this.receiverName = receiverName;
            this.rT = rT;
            this.rH = rH;
            this.rR = rR;
            this.rRA = rRA;
            this.sT = sT;
            this.sH = sH;
            this.iWeight = iWeight;
        }

        /// <summary>
        /// Construct a Rule from basic parameters
        /// </summary>
        /// <param name="rT"></param>
        /// <param name="rH"></param>
        /// <param name="rR"></param>
        /// <param name="rRA"></param>
        /// <param name="sT"></param>
        /// <param name="sH"></param>
        public Rule(int rT, int rH, int rR, double rRA, int sT, int sH) : this("receiver", rT, rH, rR, rRA, "sender", sT, sH, 0)
        {

        }

        /// <summary>
        /// Construct a Rule from a string, the AssemblyObject set and the Component Dictionary
        /// </summary>
        /// <param name="rString"></param>
        /// <param name="AOset"></param>
        /// <param name="componentDictionary"></param>
        public Rule(string rString, List<AssemblyObject> AOset, Dictionary<string, int> componentDictionary)
        {

            string[] rule = rString.Split(new[] { '<', '%' });
            string[] rec = rule[0].Split(new[] { '|' });
            string[] sen = rule[1].Split(new[] { '|' });
            // sender and receiver names
            senderName = sen[0];
            receiverName = rec[0];
            // sender and receiver component types
            sT = componentDictionary[sen[0]];
            rT = componentDictionary[rec[0]];
            // sender handle index
            sH = Convert.ToInt32(sen[1]);
            // iWeight
            iWeight = Convert.ToInt32(rule[2]);
            string[] rRot = rec[1].Split(new[] { '=' });
            // receiver handle index and rotation
            rH = Convert.ToInt32(rRot[0]);
            rRA = Convert.ToDouble(rRot[1]);
            rR = AOset[rT].handles[rH].rDictionary[rRA]; // using rotations
        }

        /// <summary>
        /// Converts a Rule into a corresponding string
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Format("{0}|{1}={2}<{3}|{4}%{5}", receiverName, rH, rRA, senderName, sH, iWeight);
        }

    }
}

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
        /// Sender object type
        /// </summary>
        public readonly int sT;
        /// <summary>
        /// Sender handle index
        /// </summary>
        public readonly int sH;
        /// <summary>
        /// Receiver object type
        /// </summary>
        public readonly int rT;
        /// <summary>
        /// Receiver handle index
        /// </summary>
        public readonly int rH;
        /// <summary>
        /// Receiver rotation index
        /// </summary>
        public readonly int rR;
        /// <summary>
        /// Receiver rotation angle (in degrees)
        /// </summary>
        public readonly double rRA;
        /// <summary>
        /// Rule integer weight
        /// </summary>
        public readonly int iWeight;

        /// <summary>
        /// Constructs a Rule from constituting parameters
        /// </summary>
        /// <param name="receiverName">Receiver object name</param>
        /// <param name="rT">Receiver object type</param>
        /// <param name="rH">Receiver handle index</param>
        /// <param name="rR">Receiver rotation index</param>
        /// <param name="rRA">Receiver rotation angle (in degrees)</param>
        /// <param name="senderName">Sender object name</param>
        /// <param name="sT">Sender object type</param>
        /// <param name="sH">Sender handle index</param>
        /// <param name="iWeight">Rule integer weight</param>
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
        /// <param name="rT">Receiver object type</param>
        /// <param name="rH">Receiver handle index</param>
        /// <param name="rR">Receiver rotation index</param>
        /// <param name="rRA">Receiver rotation angle (in degrees)</param>
        /// <param name="sT">Sender object type</param>
        /// <param name="sH">Sender handle index</param>
        public Rule(int rT, int rH, int rR, double rRA, int sT, int sH) : this("receiver", rT, rH, rR, rRA, "sender", sT, sH, 0)
        { }

        /// <summary>
        /// Construct a Rule from a string, the AssemblyObject set and the Component Dictionary
        /// </summary>
        /// <param name="ruleString">Rule string</param>
        /// <param name="AOset">the <see cref="Assemblage.AOSet"/></param>
        /// <param name="AOSetDictionary">the <see cref="Assemblage.AOSetDictionary"/></param>
        public Rule(string ruleString, List<AssemblyObject> AOset, Dictionary<string, int> AOSetDictionary)
        {

            string[] rule = ruleString.Split(new[] { '<', '%' });
            string[] rec = rule[0].Split(new[] { '|' });
            string[] sen = rule[1].Split(new[] { '|' });
            // sender and receiver names
            senderName = sen[0];
            receiverName = rec[0];
            // sender and receiver component types
            sT = AOSetDictionary[sen[0]];
            rT = AOSetDictionary[rec[0]];
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
        /// <returns>A string representing the Rule</returns>
        public override string ToString()
        {
            return string.Format("{0}|{1}={2}<{3}|{4}%{5}", receiverName, rH, rRA, senderName, sH, iWeight);
        }

    }
}

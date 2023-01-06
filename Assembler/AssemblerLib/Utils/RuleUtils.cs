using System;
using System.Collections.Generic;

namespace AssemblerLib.Utils
{
    public static class RuleUtils
    {

        /// <summary>
        /// Returns a list of Rules from a heuristics string
        /// </summary>
        /// <param name="AOset"></param>
        /// <param name="AOCatalog"></param>
        /// <param name="heuristics"></param>
        /// <returns></returns>
        public static List<Rule> HeuristicsRulesFromString(List<AssemblyObject> AOset, Dictionary<string, int> AOCatalog, List<string> heuristics)
        {
            List<Rule> heuT = new List<Rule>();

            string[] ruleStrings = heuristics.ToArray();

            int rT, rH, rR, sT, sH;
            double rRA;
            int iWeight;
            for (int i = 0; i < ruleStrings.Length; i++)
            {
                string[] ruleString = ruleStrings[i].Split(new[] { '<', '%' });
                string[] rec = ruleString[0].Split(new[] { '|' });
                string[] sen = ruleString[1].Split(new[] { '|' });
                // sender and receiver component types
                sT = AOCatalog[sen[0]];
                rT = AOCatalog[rec[0]];
                // sender handle index
                sH = Convert.ToInt32(sen[1]);
                // iWeight
                iWeight = Convert.ToInt32(ruleString[2]);
                string[] rRot = rec[1].Split(new[] { '=' });
                // receiver handle index and rotation
                rH = Convert.ToInt32(rRot[0]);
                rRA = Convert.ToDouble(rRot[1]);
                rR = AOset[rT].Handles[rH].RDictionary[rRA]; // using rotations

                heuT.Add(new Rule(rec[0], rT, rH, rR, rRA, sen[0], sT, sH, iWeight));
            }
            return heuT;
        }
    }
}

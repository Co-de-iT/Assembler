using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AssemblerLib.Utils
{
    public static class FileUtils
    {

        /// <summary>
        /// Rebuilds and Assemblage from a JSON dump
        /// </summary>
        /// <param name="path"></param>
        /// <returns>An Assemblage as list of AssemblyObject</returns>
        public static List<AssemblyObject> AssemblageFromJSONdump(string path)
        {
            return DeserializeAssemblage(System.IO.File.ReadAllLines(path));
        }

        /// <summary>
        /// Saves an Assemblage as a JSON file dump - every object is serialized in its entirety
        /// </summary>
        /// <param name="assemblage"></param>
        /// <param name="path"></param>
        /// <param name="name"></param>
        /// <returns>File Name (with full path) of the saved assemblage</returns>
        public static string AssemblageToJSONdump(List<AssemblyObject> assemblage, string path, string name)
        {
            // converts assemblage to string array
            string[] AOjson = SerializeAssemblage(assemblage);

            // add sequential placeholder to filename - the suffix d indicates the dump mode
            name += "_{0}_d.JSON";

            // sanity checks
            // if there is no directory, create one
            if (!System.IO.Directory.Exists(path)) System.IO.Directory.CreateDirectory(path);

            string fileName = ProcessFileName(path, name);

            System.IO.File.WriteAllLines(fileName, AOjson);

            return fileName;
        }

        /// <summary>
        /// TO-DO - COMPLETE THIS METHOD
        /// </summary>
        /// <param name="assemblage"></param>
        /// <param name="path"></param>
        /// <param name="name"></param>
        public static void AssemblageToJSONSmart(Assemblage assemblage, string path, string name)
        {
            //string assemblageDir;
            string assetsDir = "assets";
            // checks on directory path and filenames

            // add sequential placeholder to filename
            name += "_{0}.JSON";

            // sanity checks
            // if there are no directories, create them
            if (!System.IO.Directory.Exists(path)) System.IO.Directory.CreateDirectory(path);
            else if (!System.IO.Directory.Exists($"{path}\\{assetsDir}")) System.IO.Directory.CreateDirectory($"{path}\\{assetsDir}");


            string fileName = ProcessFileName(path, name);

            int count = 0;
            // save geometries as assets from dictionary (collision and offsetmeshes)
            foreach (AssemblyObject AO in assemblage.AOSet)
            {
                string data = JsonConvert.SerializeObject(AO);
                string target = string.Format("AO{0}_{1}", count, fileName);
                System.IO.File.WriteAllText(target, data);
                count++;
            }

            // save other data from assemblage (objects, connectivity, other)

        }

        private static string ProcessFileName(string path, string name)
        {
            // Assume index=0 for the first filename.
            string fileName = path + string.Format(name, 0.ToString("D3"));

            // Try to increment the index until we find a Name which doesn't exist yet.
            if (System.IO.File.Exists(fileName))
                for (int i = 1; i < int.MaxValue; i++)
                {
                    string localName = path + string.Format(name, i.ToString("D3"));
                    if (localName == fileName)
                        continue;

                    if (!System.IO.File.Exists(localName))
                    {
                        fileName = localName;
                        break;
                    }
                }
            return fileName;
        }

        /// <summary>
        /// Saves an array of strings to a file in a given path
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="fileName"></param>
        /// <param name="data"></param>
        public static void SaveStringsToFile(string directory, string fileName, string[] data)
        {

            if (!System.IO.Directory.Exists(directory)) System.IO.Directory.CreateDirectory(directory);

            string target = directory + fileName;

            System.IO.File.WriteAllLines(target, data);
        }
        /// <summary>
        /// Appends string data to an existing file
        /// </summary>
        /// <param name="directory">The existing file directory</param>
        /// <param name="fileName"></param>
        /// <param name="data"></param>
        public static void AppendToFile(string directory, string fileName, string data)
        {
            string path = directory + fileName;
            var writer = System.IO.File.AppendText(path);
            writer.WriteLine(data);
            writer.Close();
        }

        /// <summary>
        /// Serializes an assemblage into a string array for subsequent file saving
        /// </summary>
        /// <param name="assemblage"></param>
        /// <returns></returns>
        internal static string[] SerializeAssemblage(List<AssemblyObject> assemblage)
        {
            string[] AOjson = new string[assemblage.Count];

            if (assemblage.Count < 1000)
                for (int i = 0; i < assemblage.Count; i++)

                    AOjson[i] = JsonConvert.SerializeObject(assemblage[i]);
            else
                Parallel.For(0, assemblage.Count, i =>
                {
                    AOjson[i] = JsonConvert.SerializeObject(assemblage[i]);
                });

            return AOjson;
        }

        /// <summary>
        /// Deserializes a string array into an AssemblyObject assemblage after file loading
        /// </summary>
        /// <param name="AOjson"></param>
        /// <returns></returns>
        internal static List<AssemblyObject> DeserializeAssemblage(string[] AOjson)
        {
            AssemblyObject[] assemblage = new AssemblyObject[AOjson.Length];
            if (AOjson.Length < 1000)
                for (int i = 0; i < AOjson.Length; i++)
                    assemblage[i] = JsonConvert.DeserializeObject<AssemblyObject>(AOjson[i]);
            else
                Parallel.For(0, AOjson.Length, i =>
                {
                    assemblage[i] = JsonConvert.DeserializeObject<AssemblyObject>(AOjson[i]);
                });

            return assemblage.ToList();
        }

    }
}

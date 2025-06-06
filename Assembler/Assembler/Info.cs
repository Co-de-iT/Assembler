﻿using System;
using System.Drawing;
using System.Reflection;
using Assembler.Properties;
using Grasshopper.Kernel;

namespace Assembler
{
    public class Info : GH_AssemblyInfo
    {
        public override string Name
        {
            get
            {
                return "Assembler";
            }
        }
        public override Bitmap Icon
        {
            get
            {
                //Return a 24x24 pixel bitmap to represent this GHA library.
                return Resources.Assembler_Icon;
            }
        }
        public override string Description
        {
            get
            {
                //Return a short string describing the purpose of this GHA library.
                return "Build and manage Assemblages";
            }
        }
        public override Guid Id
        {
            get
            {
                return new Guid("cb073f4a-9e77-4d58-8668-085fcb47dd30");
            }
        }

        public override string AuthorName
        {
            get
            {
                //Return a string identifying you or your company.
                return "Alessio Erioli - Co-de-iT";
            }
        }
        public override string AuthorContact
        {
            get
            {
                //Return a string representing your preferred contact details.
                return "info@co-de-it.com";
            }
        }
        public override string AssemblyVersion
        {
            // change it in Properties/AssemblyInfo.cs - this should sync it automatically
            get
            {
                var assembly = Assembly.GetExecutingAssembly();
                var assemblyName = new AssemblyName(assembly.FullName);
                return assemblyName.Version.ToString();
            }
            //get
            //{
            //    //Return a string representing the version.
            //    return "1.2.3";
            //}
        }

        public override string Version => AssemblyVersion;
    }

    /// <summary>
    /// Add Category icon
    /// </summary>
    public class AssemblerCategoryIcon : GH_AssemblyPriority
    {
        public override GH_LoadingInstruction PriorityLoad()
        {
            Grasshopper.Instances.ComponentServer.AddCategoryIcon("Assembler", Resources.Assembler_Icon);
            Grasshopper.Instances.ComponentServer.AddCategorySymbolName("Assembler", 'a');
            return GH_LoadingInstruction.Proceed;
        }
    }
}

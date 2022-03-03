using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.IO;

namespace DependencyIdentifier
{
    public class ProjectAndSolutionParser
    {
        //private string FILE_PATH = @"";  // = @"C:\svn_src\Homeworks\trunk\src\";
        Program p = new Program();
        private ReferenceMaps referenceMap = new ReferenceMaps();
        private List<string> addtionalList = new List<string>();
        List<string> homecareFinancial = new List<string>() { "PWF", "TestBox", "BillingNET", "BillingUI", "AR", "HOMECARE.FINANCIAL" };


        /// <summary>
        /// This method parses the .vbp project files and identifies what other project are referenced.
        /// Changes to 'COMMON-VB' folder is handled as a special case, as any changes to it will impact several VB projects and all those
        /// projects are built in that scenario.
        /// </summary>
        private void processVbp(string vbpFile, List<string> projects)
        {
            bool hasRef = false;
            bool hasCommonVBRef = false;
            using (StreamReader sr = new StreamReader(Path.Combine(p.InputPath, vbpFile)))
            {
                while (sr.EndOfStream == false)
                {
                    string line = sr.ReadLine();
                    if (line.StartsWith("Reference"))
                    {
                        string[] stringParts = line.Split("#".ToCharArray());
                        string component = stringParts[stringParts.Length - 2];
                        component = component.Substring(component.LastIndexOf(@"\") + 1).Replace(".tlb", "");

                        if (projects.FindIndex(s => s.Trim().ToUpper() == component.Trim().ToUpper()) > -1)
                        {
                            AddReference(FormatProjectFile(vbpFile), component.Trim().ToUpper());
                            hasRef = true;
                        }

                    }
                    else if (line.StartsWith("Object"))
                    {
                        string[] stringParts = line.Split(";".ToCharArray());
                        string component = stringParts[stringParts.Length - 1].ToUpper().Replace(".OCX", "");

                        if (projects.FindIndex(s => s.Trim().ToUpper() == component.Trim().ToUpper()) > 1)
                        {
                            AddReference(FormatProjectFile(vbpFile), component.Trim().ToUpper());
                            hasRef = true;
                        }
                    }
                    else if (!hasCommonVBRef && (line.StartsWith("Class", StringComparison.CurrentCultureIgnoreCase) ||
                                            line.StartsWith("Module", StringComparison.CurrentCultureIgnoreCase) ||
                                            line.StartsWith("Form", StringComparison.CurrentCultureIgnoreCase)))
                    {
                        string[] stringParts = line.Split("\\".ToCharArray());
                        foreach (string component in stringParts)
                        {
                            if (component.Trim().ToUpper().Equals("COMMON-VB"))
                            {
                                AddReference(component.Trim().ToUpper(), FormatProjectFile(vbpFile));
                                hasRef = true;
                                hasCommonVBRef = true;
                            }
                        }
                    }
                }

                if (hasRef == false)
                {
                    AddReference(FormatProjectFile(vbpFile), "");
                }
            }
        }


        /// <summary>
        /// This method parses the StdAfx.h file to parse and identify any C++ project dependencies.
        /// </summary>
        private void processVcpp(string dspFile, List<string> projects)
        {
            string searchFile = Path.Combine(p.InputPath, dspFile.Substring(0, dspFile.LastIndexOf(@"\") + 1), "StdAfx.h");
            bool hasRef = false;

            using (StreamReader sr = new StreamReader(searchFile))
            {
                while (sr.EndOfStream == false)
                {
                    string line = sr.ReadLine();
                    line = line.Replace("\t", " ").Replace("\"", "");
                    string component = null;
                    if (line.StartsWith("#include"))
                    {
                        string[] stringParts = line.Split(" ".ToCharArray());
                        component = stringParts[1].Trim().ToUpper().Replace("<", "").Replace(">", "").Replace(".H", "");

                    }
                    else if (line.StartsWith("#import"))
                    {
                        string[] stringParts = line.Split(" ".ToCharArray());
                        component = stringParts[1].Trim().ToUpper().Replace("<", "").Replace(">", "").Replace(".DLL", "").Replace(".TLB", "");
                    }

                    if (component != null)
                    {
                        if (projects.FindIndex(s => s.Trim().ToUpper() == component.Trim().ToUpper()) > 1)
                        {
                            if (FormatProjectFile(dspFile) != component.Trim().ToUpper())
                            {

                                string[] stringArray = dspFile.Split("\\".ToCharArray());
                                if (stringArray[0].ToUpper().Equals("BTISYNC_OLD"))
                                {
                                    //special case to handle BTISync_old, so we can differenciate from the BTISync
                                    AddReference("BTISync_old", component.Trim().ToUpper());
                                }
                                else
                                {
                                    //General case
                                    AddReference(FormatProjectFile(dspFile), component.Trim().ToUpper());
                                }
                                hasRef = true;
                            }
                        }
                    }
                }

                if (hasRef == false)
                {
                    AddReference(FormatProjectFile(dspFile), "");
                }
            }
        }


        /// <summary>
        /// check if a given project is in the list of full_build projects list.
        /// </summary>
        private bool IdentifyProject(string proj, List<string> projects)
        {
            foreach (string pro in projects)
            {
                if (pro.Trim().ToUpper().Equals(proj.ToUpper()))
                    return true;
            }
            return false;
        }


        /// <summary>
        /// Parses the .Net solution file and inturn retrieves the individual project files (.csproj) and parses each project file.
        /// </summary>
        private void processSolution(string slnFile, List<string> projects)
        {
            using (StreamReader sr = new StreamReader(Path.Combine(p.InputPath, slnFile)))
            {
                bool hasRef = false;
                string projFile = "";

                while (sr.EndOfStream == false)
                {
                    string line = sr.ReadLine();
                    if (line.StartsWith("Project"))
                    {
                        string[] stringParts = line.Split(",".ToCharArray());
                        FileInfo fi = new FileInfo(Path.Combine(p.InputPath, slnFile));
                        string projectFile = string.Format("{0}{1}", p.InputPath, slnFile).Replace(fi.Name, stringParts[1].Replace("\"", "").Trim());
                        projFile = projectFile;

                        if (projFile.Trim().EndsWith(".csproj") == true)
                        {
                            string[] projFileParts = projFile.Split("\\".ToCharArray());
                            string projName = projFileParts[projFileParts.Length - 1].Replace(".csproj", "");

                            if (!IdentifyProject(projName, projects))
                            {
                                addtionalList.Add(projName.ToString());
                            }
                        }

                        if (projectFile.Trim().EndsWith(".vdproj"))
                        {
                            //setup project
                            //Not coded: because of the effort involved in parsing a .vdproj file
                            //occurs in only one scenario: below special case covers it.

                            //Special cases
                            string[] solutionName = projectFile.Split("\\".ToCharArray());
                            if (solutionName[solutionName.Length - 1].ToUpper().Equals("PATLOADSETUP.VDPROJ"))
                            {
                                //<ReferenceMap><Component>PatLoadsetup</Component><Reference>PatLoad</Reference></ReferenceMap>
                                AddReference("PATLOADSERVICE", "PATLOAD");
                            }

                            continue;
                        }
                        if (projectFile.Trim().EndsWith(".csproj") == false)
                        {
                            //look for ProjectReferences
                            string line2 = sr.ReadLine();
                            if (line2.Trim().StartsWith("ProjectSection"))
                            {
                                while (line2.Trim().StartsWith("ProjectReferences") == false)
                                {
                                    line2 = sr.ReadLine();
                                    if (line2.Trim().StartsWith("EndProjectSection"))
                                    {
                                        break;
                                    }
                                }

                                if (line2.Trim().StartsWith("ProjectReferences"))
                                {
                                    string[] programs = line2.Split(";".ToCharArray());
                                    foreach (string program in programs)
                                    {
                                        string[] programParts = program.Split("|".ToCharArray());
                                        if (programParts.Length == 2)
                                        {
                                            AddReference(FormatProjectFile(projectFile), programParts[1].Replace(".dll", "").Trim().ToUpper());
                                            hasRef = true;
                                        }
                                    }
                                }
                            }
                            continue;
                        }
                        if (File.Exists(projectFile) == false)
                        {
                            //can't find the project, so can't get the references
                            continue;
                        }
                        IEnumerable<string> refs = GetDotNETReferences(projectFile).Intersect<string>(projects.Select(s => s.Trim().ToUpper()));
                        foreach (string reference in refs)
                        {
                            AddReference(FormatProjectFile(projectFile), reference);
                            hasRef = true;
                        }
                    }
                }

                if (hasRef == false)
                {
                    AddReference(FormatProjectFile(projFile), "");
                }

            }
        }


        /// <summary>
        /// Starting method, starts looking for dependencies of all the projects in full_build.proj file.
        /// Handles projects of type: VSDevEnv, MSBuild, ASP .Net, VB6, VC6
        /// </summary>
        public void StartParsing(Program prog)
        {
            if (prog.InputPath != null && prog.InputPath != "")
            {
                p.InputPath = prog.InputPath;
            }

            XDocument doc = XDocument.Load(string.Format("{0}full_build.proj", p.InputPath));
            XElement targets = doc.Descendants().OfType<XElement>().Where(x => x.Name.LocalName == "FullBuildTargets").First();
            XElement itemGroup = doc.Descendants().OfType<XElement>().Where(x => x.Name.LocalName == "ItemGroup").First();

            List<string> projects = targets.Value.Replace("\n", "").Split(";".ToCharArray()).ToList<string>();

            foreach (string project in projects)
            {
                if (project.Trim().Equals(String.Empty))
                    continue;

                bool hasRef = false;
                XElement target = doc.Descendants().OfType<XElement>().Where(x => x.Name.LocalName == "Target" && x.Attribute("Name").Value.ToUpper() == project.ToUpper().Trim()).First();
                if (target.Descendants().OfType<XElement>().Where(x => x.Name.LocalName == "MSBuild.ExtensionPack.VisualStudio.VSDevEnv").FirstOrDefault() != null)
                {
                    //get solution
                    XElement sln = target.Descendants().OfType<XElement>().Where(x => x.Name.LocalName == "MSBuild.ExtensionPack.VisualStudio.VSDevEnv").FirstOrDefault();
                    string slnFile = sln.Attribute("FilePath").Value;
                    //get projects

                    processSolution(slnFile, projects);


                    string searchFile = Path.Combine(p.InputPath, slnFile.Substring(0, slnFile.LastIndexOf(@"\") + 1));
                    var files = from file in Directory.EnumerateFiles(searchFile, "*.*").Where(s => s.EndsWith(".cs"))
                                from line in File.ReadLines(file)
                                where line.StartsWith("using")
                                select new
                                {
                                    File = file,
                                    Line = line
                                };

                    string component = null;
                    foreach (var f in files)
                    {
                        string[] stringParts = f.Line.Split(" ".ToCharArray());
                        component = stringParts[1].Trim().ToUpper().Replace(";", "");

                        if (component != null)
                        {
                            if (projects.FindIndex(s => s.Trim().ToUpper() == component.Trim().ToUpper()) > 1)
                            {
                                if (FormatProjectFile(slnFile) != component.Trim().ToUpper())
                                {
                                    AddReference(FormatProjectFile(slnFile), component.Trim().ToUpper());
                                    hasRef = true;
                                }
                            }
                            else
                            {
                                //for handling solutions in the full_build.proj file
                                string result = CheckIfComponentInSolution(component);
                                if (!String.IsNullOrEmpty(result))
                                {
                                    if (FormatProjectFile(slnFile) != component.Trim().ToUpper())
                                    {
                                        AddReference(FormatProjectFile(slnFile), component.Trim().ToUpper());
                                        hasRef = true;
                                    }
                                }
                            }
                        }
                    }

                    //Special case
                    string[] solutionName = slnFile.Split("\\".ToCharArray());
                    if (solutionName[solutionName.Length - 1].ToUpper().Equals("BTICAREWATCHSVCCONFIG.SLN"))
                    {
                        //<ReferenceMap><Component>BTICAREWATCHSVCCONFIG</Component><Reference>BtiCareWatchSvcUI</Reference></ReferenceMap>
                        AddReference(FormatProjectFile(slnFile), "BTICAREWATCHSVCUI");
                    }

                }
                else if (target.Descendants().OfType<XElement>().Where(x => x.Name.LocalName == "MSBuild").FirstOrDefault() != null)
                {
                    //get project
                    XElement proj = target.Descendants().OfType<XElement>().Where(x => x.Name.LocalName == "MSBuild").FirstOrDefault();
                    if (proj.Attribute("Projects").Value.EndsWith(".sln"))
                    {
                        processSolution(proj.Attribute("Projects").Value, projects);
                    }
                    else
                    {
                        string projectFile = Path.Combine(p.InputPath, proj.Attribute("Projects").Value);

                        IEnumerable<string> refs = GetDotNETReferences(projectFile).Intersect<string>(projects.Select(s => s.Trim().ToUpper()));
                        foreach (string reference in refs)
                        {
                            AddReference(FormatProjectFile(projectFile), reference);
                            hasRef = true;
                        }

                        if (hasRef == false)
                        {
                            AddReference(FormatProjectFile(project), "");
                        }
                    }


                    string searchFile = string.Format("{0}{1}", p.InputPath, proj.Attribute("Projects").Value.Substring(0, proj.Attribute("Projects").Value.LastIndexOf(@"\") + 1));
                    var files = from file in Directory.EnumerateFiles(searchFile, "*.*", SearchOption.AllDirectories).Where(s => s.EndsWith(".cs"))
                                from line in File.ReadLines(file)
                                where line.StartsWith("using")
                                select new
                                {
                                    File = file,
                                    Line = line
                                };

                    string component = null;
                    foreach (var f in files)
                    {
                        string[] stringParts = f.Line.Split(" ".ToCharArray());
                        component = stringParts[1].Trim().ToUpper().Replace(";", "");

                        if (component != null)
                        {
                            if (projects.FindIndex(s => s.Trim().ToUpper() == component.Trim().ToUpper()) > 1)
                            {
                                if (FormatProjectFile(proj.Attribute("Projects").Value) != component.Trim().ToUpper())
                                {
                                    AddReference(FormatProjectFile(proj.Attribute("Projects").Value), component.Trim().ToUpper());
                                    hasRef = true;
                                }
                            }
                            else
                            {
                                //for handling solutions in the full_build.proj file
                                string result = CheckIfComponentInSolution(component);
                                if (!String.IsNullOrEmpty(result))
                                {
                                    if (FormatProjectFile(proj.Attribute("Projects").Value) != component.Trim().ToUpper())
                                    {
                                        AddReference(FormatProjectFile(proj.Attribute("Projects").Value), component.Trim().ToUpper());
                                        hasRef = true;
                                    }
                                }
                            }
                        }
                    }

                }
                else if (target.Descendants().OfType<XElement>().Where(x => x.Name.LocalName == "Exec" && x.Attribute("Command").Value.StartsWith("$(AspnetTool")).FirstOrDefault() != null)
                {
                    //asp.net
                    List<string> includedRefs = new List<string>();
                    IEnumerable<XElement> elems = target.Descendants().OfType<XElement>().Where(x => x.Name.LocalName == "MSBuild.ExtensionPack.FileSystem.RoboCopy");
                    foreach (XElement elem in elems)
                    {
                        includedRefs.Add(getReference(elem, "Files").Trim().ToUpper());
                    }

                    IEnumerable<string> refs = includedRefs.Intersect<string>(projects.Select(s => s.Trim().ToUpper()));
                    foreach (string reference in refs)
                    {
                        AddReference(FormatProjectFile(target.Attribute("Name").Value), reference);
                        hasRef = true;
                    }
                    if (hasRef == false)
                    {
                        AddReference(FormatProjectFile(project), "");
                    }
                }
                else if (target.Descendants().OfType<XElement>().Where(x => x.Name.LocalName == "MSBuild.ExtensionPack.VisualStudio.VB6").FirstOrDefault() != null)
                {
                    //vb6
                    string vbpVariable = target.Descendants().OfType<XElement>().Where(x => x.Name.LocalName == "MSBuild.ExtensionPack.VisualStudio.VB6").First<XElement>().Attribute("Projects").Value.Replace("@", "").Replace("(", "").Replace(")", "");

                    string vbp = itemGroup.Descendants().OfType<XElement>().Where(x => x.Name.LocalName == vbpVariable).First().Attribute("Include").Value;

                    processVbp(vbp, projects);
                }
                else if (target.Descendants().OfType<XElement>().Where(x => x.Name.LocalName == "MSBuild.ExtensionPack.VisualStudio.VC6").FirstOrDefault() != null)
                {
                    //vc++
                    string dspVariable = target.Descendants().OfType<XElement>().Where(x => x.Name.LocalName == "MSBuild.ExtensionPack.VisualStudio.VC6").First<XElement>().Attribute("Projects").Value.Replace("@", "").Replace("(", "").Replace(")", "");

                    string dsp = itemGroup.Descendants().OfType<XElement>().Where(x => x.Name.LocalName == dspVariable).First().Attribute("Include").Value;

                    processVcpp(dsp, projects);

                    processDsp(dsp);

                    //Start: process .h and .cpp files
                    string searchFile = string.Format("{0}{1}", p.InputPath, dsp.Substring(0, dsp.LastIndexOf(@"\") + 1));
                    var files = from file in Directory.EnumerateFiles(searchFile, "*.*").Where(s => s.EndsWith(".h") || s.EndsWith(".cpp"))
                                from line in File.ReadLines(file)
                                where line.Contains("#import")
                                select new
                                {
                                    File = file,
                                    Line = line
                                };

                    string component = null;
                    foreach (var f in files)
                    {
                        string[] filesPath = f.File.Split("\\".ToCharArray());
                        if (filesPath[filesPath.Length - 1].ToUpper().Equals("STDAFX.H"))
                            continue;

                        string[] stringParts = f.Line.Split(" ".ToCharArray());
                        component = stringParts[1].Trim().ToUpper().Replace("<", "").Replace(">", "").Replace(".DLL", "").Replace(".TLB", "").Replace("\"", "");

                        if (component != null)
                        {
                            if (component.ToUpper().StartsWith("FINANCIAL"))
                                component = component.Replace('.', '_');

                            if (projects.FindIndex(s => s.Trim().ToUpper() == component.Trim().ToUpper()) > 1)
                            {
                                if (FormatProjectFile(dsp) != component.Trim().ToUpper())
                                {
                                    AddReference(FormatProjectFile(dsp), component.Trim().ToUpper());
                                    hasRef = true;
                                }
                            }
                            else
                            {
                                //for handling solutions in the full_build.proj file
                                string result = CheckIfComponentInSolution(component);
                                if (!String.IsNullOrEmpty(result))
                                {
                                    if (FormatProjectFile(dsp) != component.Trim().ToUpper())
                                    {
                                        AddReference(FormatProjectFile(dsp), component.Trim().ToUpper());
                                        hasRef = true;
                                    }
                                }
                            }
                        }
                    }
                    //End
                }

                else
                {
                    //no build
                    AddReference(FormatProjectFile(project), "");
                }
            }

            //Validate if each project in full_build.proj has a corresponding <component> tag. If not, add a component tag for the project with an empty reference.
            foreach (string proj in projects)
            {
                bool componentFound = false;
                foreach (ReferenceMap rm in referenceMap.ReferenceMap)
                {
                    if(proj.Trim().ToUpper().Equals(rm.Component.ToUpper()))
                    {
                        componentFound = true;
                        break;
                    }
                }

                if (!componentFound && (String.IsNullOrEmpty(proj) == false))
                {
                    AddReference(proj.Trim().ToString(), "");
                }
            }          


            foreach (ReferenceMap rm in referenceMap.ReferenceMap)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("{0}, {1}", rm.Component, rm.Reference));
            }

            XmlTextWriter xmlText = new XmlTextWriter("projectReferences.txt", Encoding.ASCII);

            System.Xml.Serialization.XmlSerializer xmlSer = new System.Xml.Serialization.XmlSerializer(referenceMap.GetType());
            xmlSer.Serialize(xmlText, referenceMap);
        }


        /// <summary>
        /// Changes to 'COMMON-VC' folder is handled as a special case, as any changes to it will impact several C++ projects and all those
        /// projects are built in that scenario.
        /// </summary>
        private void processDsp(string dspFile)
        {
            bool hasCommonVBRef = false;
            string searchFile = string.Format("{0}{1}", p.InputPath, dspFile);

            using (StreamReader sr = new StreamReader(searchFile))
            {
                while (sr.EndOfStream == false)
                {
                    string line = sr.ReadLine();
                    if (!hasCommonVBRef && line.StartsWith("SOURCE"))
                    {
                        string[] stringParts = line.Split("\\".ToCharArray());
                        foreach (string str in stringParts)
                        {
                            if (str.Equals("Common-VC", StringComparison.OrdinalIgnoreCase))
                            {
                                AddReference("COMMON-VC", FormatProjectFile(dspFile));
                                hasCommonVBRef = true;
                            }
                        }
                    }
                }
            }
        }


        /// <summary>
        /// For handling solutions in the full_build.proj file
        /// </summary>
        private string CheckIfComponentInSolution(string component)
        {
            for (int var = 0; var < homecareFinancial.Count - 2; var++)
            {
                if (component.Trim().ToUpper().Equals(homecareFinancial[var].ToUpper()))
                    return homecareFinancial[homecareFinancial.Count - 1];
            }
            return String.Empty;
        }


        /// <summary>
        /// Parses .Net project files looking for any references and project references and add them as referenced project list. 
        /// </summary>
        private List<string> GetDotNETReferences(string project)
        {
            List<string> includedReferences = new List<string>();
            XDocument doc = XDocument.Load(project);
            IEnumerable<XElement> references = doc.Descendants().OfType<XElement>().Where(x => x.Name.LocalName == "Reference");

            foreach (XElement elem in references)
            {
                includedReferences.Add(getReference(elem, "Include").Trim().ToUpper());
            }

            IEnumerable<XElement> projReferences = doc.Descendants().OfType<XElement>().Where(x => x.Name.LocalName == "ProjectReference");
            foreach (XElement elem in projReferences)
            {
                includedReferences.Add(getReference(elem, "Include").Trim().ToUpper());
            }
            return includedReferences;
        }


        /// <summary>
        /// Retrive the included project from each project reference.
        /// </summary>
        private string getReference(XElement elem, string attribute)
        {
            string tmpRef = elem.Attribute(attribute).Value.Split(",".ToCharArray())[0];
            if (tmpRef.Contains(@"\"))
            {
                string[] stringParts = tmpRef.Split(@"\".ToCharArray());
                tmpRef = stringParts[stringParts.Length - 1];
            }
            if (tmpRef.StartsWith("Interop."))
            {
                tmpRef = tmpRef.Replace("Interop.", "");
                tmpRef = tmpRef.Replace("Lib", "");
            }
            tmpRef = tmpRef.ToUpper().Replace(".DLL", "").Replace(".CSPROJ", "");

            if (tmpRef.ToUpper().StartsWith("FINANCIAL"))
            {
                tmpRef = tmpRef.Replace('.', '_');
            }

            return tmpRef;
        }


        /// <summary>
        /// Retrive the included project file name.
        /// </summary>
        private string FormatProjectFile(string project)
        {
            if (project.Equals(""))
                return project;
            return project.Substring(project.LastIndexOf(@"\", project.Length - 2) + 1).Replace(".csproj", "").Replace(".vbp", "")
                .Replace(".dsp", "").Replace(".sln", "").Replace(".vdproj", "").Trim().ToUpper();
        }

        private string[] exceptionProjects = { "E-REFERRALSDBTOOLS", "E-REFERRALSUTIL", "E-REFERRALSDBOBJECTS", "E-REFERRALSBUSINESSRULES",
                                          "E-REFERRALS", "COMMON-VB", "COMMON-VC" };


        /// <summary>
        /// Retrive the included project file name.
        /// Before adding a [component - project refrence] entry there are several special cases that i had to handle to accomodate for the non-standard 
        /// project naming convention that we have with naming our projects.
        /// Some examples are:
        /// 1. In Clinical project file, we see the reference to Clinical.WinForms, but in full_build.proj file it is named as Clinical_WinForms
        /// 2. similarly Financial.core will be renamed as Financial_core and we have few more that needs renamed
        /// 3. Some projects has a '-' like APM-Launcher which is renamed as APM_Launcher
        /// 4. OOPFACTORY.X12.HIPAA is the reference found in project file, but in full_build.proj, it is just referenced as 'OOPFACTORY'
        /// 5. Roalodex binary is actually named as RolodexCards.dll, but it is named as 'Rolodex' in full_build.proj.
        /// </summary>
        private void AddReference(string component, string reference)
        {
            component = component.Replace('.', '_');
            if (!exceptionProjects.Contains(component) && component.Contains('-'))
                component = component.Replace('-', '_');

            reference = reference.Replace('.', '_');
            if (!exceptionProjects.Contains(reference) && reference.Contains('-'))
                reference = reference.Replace('-', '_');

            if (component.ToUpper().StartsWith("ROLODEXCARDS"))
            {
                component = "ROLODEX";
            }

            if (component.ToUpper().StartsWith("OOPFACTORY"))
            {
                component = "OOPFACTORY";
            }

            if (component.ToUpper().StartsWith("LTCMOBILE"))
            {
                char[] trimEnd = { '\\' };
                component = component.TrimEnd(trimEnd);
            }

            if (referenceMap.ReferenceMap.Exists(o => o.Component == component && o.Reference == reference) == false)
            {
                referenceMap.ReferenceMap.Add(new ReferenceMap { Component = component, Reference = reference });
            }
        }

    }
}

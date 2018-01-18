using System;
using System.Configuration;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LumenWorks.Framework.IO.Csv;

namespace ConfigUsageReport
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                string configSchemaFilePath = ConfigurationManager.AppSettings["ConfigSchemaFilePath"];
                string dfxSourceCodeRootDirectory = ConfigurationManager.AppSettings["DfxSourceCodeRootDirectory"];
                Console.WriteLine(string.Format("Config Schema File: {0}\nDFX Source Code Base Path: {1}\n\n", configSchemaFilePath, dfxSourceCodeRootDirectory));
                Console.WriteLine("Processing...");

                //Get List of Config Codes from spreadsheet
                List<string> configCodes = GetConfigCodes(configSchemaFilePath);
                //Construct Regex
                Regex regex = BuildRegex(configCodes);
                //Get List of files to search
                List<string> searchFiles = GetSearchFilePaths(dfxSourceCodeRootDirectory, new List<string>() { "*.aspx", "*.cshtml", "*.cs" });
                //Search each file
                HashSet<ConfigUsageInstance> usages = GetUsages(searchFiles, regex);
                //Process results into collections based on ConfigCode
                List<ConfigUsageReport> usageReportData = ProcessUsages(usages);
                WriteCSV(usageReportData, "out.csv");
                
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.Write(ex);
            }
        }

        private static List<string> GetConfigCodes(string filePath)
        {   
            using (var csv = new CachedCsvReader(new StreamReader(filePath), true))
            {
                //column 15 is the config code
                return csv.Select(x => x[15])
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();
            }
        }

        private static Regex BuildRegex(List<string> codes)
        {
            return new Regex(string.Join("|", codes.Select(x => string.Format("(?<{0}>{1}\\.Name)", x, x))));
        }

        private static List<string> GetSearchFilePaths(string rootDirectory, List<string> filters)
        {
            //build up lists of all the files
            List<string> results = new List<string>();
            foreach(string filter in filters) 
            {
                results.AddRange(Directory.GetFiles(rootDirectory, filter, SearchOption.AllDirectories));
            }
            return results;
        }

        private static HashSet<ConfigUsageInstance> GetUsages(List<string> files, Regex regex)
        {
            ConcurrentBag<ConfigUsageInstance> usages = new ConcurrentBag<ConfigUsageInstance>();
            Parallel.ForEach(files, (file) =>
            {
                string contents = File.ReadAllText(file);
                foreach(Match match in regex.Matches(contents))
                {
                    GroupCollection groups = match.Groups;
                    foreach (string groupName in regex.GetGroupNames().Where(x => x != "0"))
                    {
                        if (groups[groupName].Captures.Count > 0)
                        {
                            usages.Add(new ConfigUsageInstance()
                            {
                                ConfigCode = groupName,
                                FilePath = file
                            });
                        }
                    }
                }
            });
            return new HashSet<ConfigUsageInstance>(usages); 
        }

        private static List<ConfigUsageReport> ProcessUsages(HashSet<ConfigUsageInstance> usages)
        {
            List<ConfigUsageReport> results = new List<ConfigUsageReport>();
            string configCode = "";
            while (usages.Count > 0)
            {
                configCode = usages.First().ConfigCode;
                results.Add(new ConfigUsageReport()
                {
                    ConfigCode = configCode,
                    Files = usages.Where(x => x.ConfigCode == configCode).Select(x => x.FilePath).ToList()
                });
                usages.RemoveWhere(x => x.ConfigCode == configCode);
            }
            return results;
        }

        private static void WriteCSV(List<ConfigUsageReport> items, string path)
        {
            using (var writer = new StreamWriter(
                new FileStream(path, FileMode.Create, FileAccess.ReadWrite),
                Encoding.ASCII)
                )
            {
                writer.WriteLine("ConfigCode, ASPX, CSHTML, CS");
                writer.WriteLine("");

                foreach (var item in items)
                {
                    writer.WriteLine(string.Join(",", new List<string>()
                    {
                        item.ConfigCode,
                        item.AspxUsages,
                        item.CsHtmlUsages,
                        item.CsUsages
                    }));
                }
            }
        }
    }

    class ConfigSchemaRecord
    {
        public string ConfigCode { get; set; }
        public string LongDesc { get; set; }
    }

    class ConfigUsageInstance
    {
        public string ConfigCode { get; set; }
        public string FilePath { get; set; }
    }

    class ConfigUsageReport
    {
        public string ConfigCode { get; set; }

        private List<string> _files = new List<string>();

        public List<string> Files
        {
            get { return _files; }
            set { _files = value; }
        }

        public string AspxUsages
        {
            get
            {
                return "\"" + string.Join("\n", this.Files.Where(x => Path.GetExtension(x) == ".aspx").Distinct().ToList()) + "\"";
            }
        }

        public string CsHtmlUsages
        {
            get 
            {
                return "\"" + string.Join("\n", this.Files.Where(x => Path.GetExtension(x) == ".cshtml").Distinct().ToList()) + "\"";
            }
        }

        public string CsUsages
        {
            get
            {
                return "\"" + string.Join("\n", this.Files.Where(x => Path.GetExtension(x) == ".cs").Distinct().ToList()) + "\"";
            }
        }
    }
}

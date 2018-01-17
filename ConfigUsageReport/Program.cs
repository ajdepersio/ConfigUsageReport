using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ConfigUsageReport
{
    class Program
    {
        static void Main(string[] args)
        {
            //Get List of Config Codes from spreadsheet
            List<string> configCodes = GetConfigCodes("path/to/file");
            //Construct Regex
            Regex regex = BuildRegex(configCodes);
            //Get List of files to search
            List<string> searchFiles = GetSearchFilePaths("path/to/file", new List<string>() { "*.txt" });
            //Search each file
            List<ConfigUsage> usages = GetUsages(searchFiles, regex);
        }

        private static List<string> GetConfigCodes(string filePath)
        {
            return new List<string>()
            {
                "foo",
                "bar",
                "hello",
                "world",
                "cat",
                "dog"
            };
        }

        private static Regex BuildRegex(List<string> codes)
        {
            return new Regex(string.Join("|", codes.Select(x => $"(?<{x}>{x}\\.Name)")));
        }

        private static List<string> GetSearchFilePaths(string rootDirectory, List<string> filter)
        {
            return new List<string>()
            {
                "C:/basket/test1.txt",
                "C:/basket/test2.txt",
                "C:/basket/sub/test3.txt"
            };
        }

        private static List<ConfigUsage> GetUsages(List<string> files, Regex regex)
        {
            ConcurrentBag<ConfigUsage> usages = new ConcurrentBag<ConfigUsage>();
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
                            usages.Add(new ConfigUsage()
                            {
                                ConfigCode = groupName,
                                FilePath = file
                            });
                        }
                    }
                }
            });
            return usages.ToList();
        }
    }

    class ConfigUsage
    {
        public string ConfigCode { get; set; }
        public string FilePath { get; set; }
    }
}

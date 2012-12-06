using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.IO;

namespace HistoricalVaR
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            DateTime start_date = new DateTime(2012, 1, 2);
            DateTime end_date = new DateTime(2012, 3, 29);
            //DateTime start_date = new DateTime(2012, 4, 18);
            //DateTime end_date = new DateTime(2012, 4, 18);

            string root_dir = @"R:\";
            string current_time_str = @"0600";
            string market_str = @"T-2";

            DateTime current_date = new DateTime();
            current_date = start_date;
            while (current_date <= end_date)
            {
                string current_date_str = current_date.Year.ToString();
                if (current_date.Month < 10)
                    current_date_str += "0" + current_date.Month;
                else
                    current_date_str += current_date.Month;
                if (current_date.Day < 10)
                    current_date_str += "0" + current_date.Day;
                else
                    current_date_str += current_date.Day;
                Console.WriteLine(current_date_str);

                // result dirs
                string macroTargetDir = root_dir + @"RM Reports\" + "DAMF " + current_date_str;
                string ccyTargetDir = root_dir + @"RM Reports\" + "DACVF " + current_date_str;
                string subDir = macroTargetDir + @"\" + current_time_str + @"_" + market_str;
                bool results_found = false;
                if (Directory.Exists(macroTargetDir))
                {
                    // check if sub-direcorty exists
                    if (Directory.Exists(subDir))
                    {
                        results_found = true;
                    }
                }
                else
                {
                    // create the main dirs for current date
                    Directory.CreateDirectory(macroTargetDir);
                    Directory.CreateDirectory(ccyTargetDir);
                }

                if (!results_found)
                {
                    // start RiskMetrics batch job
                    RiskMetricsReport(root_dir, current_date_str, current_time_str, market_str);
                }

                // update date
                current_date = next_business_day(current_date);
            }

            return;
        }

        static private DateTime next_business_day(DateTime current)
        {
            DateTime next = current;
            next = next.AddDays(1);
            while (next.DayOfWeek == DayOfWeek.Saturday || next.DayOfWeek == DayOfWeek.Sunday)
                next = next.AddDays(1);
            return next;
        }

        static private void RiskMetricsReport(string rootDir, string current_date_str, string currentTimeStr, string marketStr)
        {
            string posFile = "";
            string mainPath = rootDir + @"Citco Portfolio Files\";

            string latestFileName = @"DYAS_RiskMetrics_" + current_date_str + currentTimeStr + @".csv";
            string srcFile = mainPath + latestFileName;
            if (!File.Exists(srcFile))
            {
                Console.WriteLine("Position file does not exist");
                return;
            }

            Console.WriteLine(srcFile);

            string exePath = @"c:\Users\mark.devries\My Documents\Visual Studio 2010\Projects\AexeoToRM\";
            string dstFile = exePath + @"AexeoToRM\" + latestFileName;
            int idx = latestFileName.IndexOf(".csv");
            string reducedFileName = latestFileName.Substring(0, idx);
            posFile = mainPath + reducedFileName + "a.csv";

            // transform the position file to correct for Aexeo issues
            if (File.Exists(srcFile))
            {
                File.Copy(srcFile, dstFile, true);

                System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();

                startInfo.FileName = exePath + @"Release\AexeoToRM.exe";
                startInfo.Arguments = "-i " + latestFileName;
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardOutput = true;
                using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(startInfo))
                {
                    using (StreamReader reader = process.StandardOutput)
                    {
                        string result = reader.ReadToEnd();
                        Console.Write(result);
                    }
                }

                // copy the file
                string resFile = exePath + @"AexeoToRM\" + reducedFileName + "a.csv";
                File.Copy(resFile, posFile, true);

                File.Delete(dstFile);
                File.Delete(resFile);

                Console.WriteLine("RiskMetrics input file has been generated");
            }

            // run the RiskMetrics batch file
            if (File.Exists(posFile))
            {
                Console.WriteLine("Run the batch job on " + current_date_str + " for " + posFile);

                System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
                startInfo.FileName = @"c:\Users\mark.devries\Desktop\cib_3.7.2\cibRunBatch.exe";
                startInfo.Arguments = @"prod DymonAsiaBatchJob " + current_date_str + @" abax.risk01@abaxglobalcapital.com dymon123 c:\test " + '"' + posFile + '"';
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardOutput = true;
                using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(startInfo))
                {
                    using (StreamReader reader = process.StandardOutput)
                    {
                        string result = reader.ReadToEnd();
                        Console.Write(result);
                    }
                }

                Console.WriteLine("RiskMetrics batch job has been completed");
            }
            else
            {
                Console.WriteLine("No RM input file found");
            }

            // if result files exist move to repository
            string sourceDir = @"c:\test\downloads\";
            string rmFile = sourceDir + @"DymonAsiaBatchJob.VaR Attribution Matrix.Dymon Asia Macro." + current_date_str + ".pdf";
            string macroTargetDir = rootDir + @"RM Reports\" + "DAMF " + current_date_str + @"\" + currentTimeStr + @"_" + marketStr;
            string ccyTargetDir = rootDir + @"RM Reports\" + "DACVF " + current_date_str + @"\" + currentTimeStr + @"_" + marketStr;
            if (File.Exists(rmFile))
            {
                Directory.CreateDirectory(macroTargetDir);
                Directory.CreateDirectory(ccyTargetDir);

                // move files and simplify the names
                DirectoryInfo source = new DirectoryInfo(sourceDir);
                FileInfo[] files = source.GetFiles();
                foreach (FileInfo file in files)
                {
                    // Macro Fund
                    if (file.Name.Contains("Dymon Asia Macro") && file.Name.Contains(current_date_str) && !file.Name.Contains("DetailLog"))
                    {
                        int idx3 = file.Name.IndexOf(".");
                        int idx4 = file.Name.IndexOf(".", idx3 + 1);
                        int idx5 = file.Name.LastIndexOf(".");  // for file extension
                        string destinationFileName = macroTargetDir + @"\" + file.Name.Substring(idx3 + 1, idx4 - idx3 - 1) + @" - " + current_date_str + file.Name.Substring(idx5);
                        file.CopyTo(destinationFileName, true);
                    }

                    // Ccy Value Fund
                    if (file.Name.Contains("Dymon Asia Ccy Value") && file.Name.Contains(current_date_str) && !file.Name.Contains("DetailLog"))
                    {
                        int idx3 = file.Name.IndexOf(".");
                        int idx4 = file.Name.IndexOf(".", idx3 + 1);
                        int idx5 = file.Name.LastIndexOf(".");  // for file extension
                        string destinationFileName = ccyTargetDir + @"\" + file.Name.Substring(idx3 + 1, idx4 - idx3 - 1) + @" - " + current_date_str + file.Name.Substring(idx5);
                        file.CopyTo(destinationFileName, true);
                    }
                }

                Console.WriteLine("Copied the results to repository");
            }
            else
            {
                Console.WriteLine("Batch did not generate results");
            }
        }
    }
}

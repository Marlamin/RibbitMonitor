using Ribbit.Constants;
using Ribbit.Protocol;
using System;
using System.Collections.Generic;

namespace RibbitMonitor
{
    class Program
    {
        private static bool isMonitoring = true;
        static void Main(string[] args)
        {
            Console.CancelKeyPress += delegate
            {
                // TODO: Clean up stuff
                isMonitoring = false;
                Console.WriteLine("Goodbye!");
                Environment.Exit(0);
            };

            var client = new Client(Region.US);

            var currentSummary = ParseSummary(client.Request("v1/summary").ToString());

            Console.Write("Monitoring..");
            while (isMonitoring)
            {
                var newSummary = ParseSummary(client.Request("v1/summary").ToString());
                
                foreach(var newEntry in newSummary)
                {
                    if (currentSummary.ContainsKey(newEntry.Key))
                    {
                        if(currentSummary[newEntry.Key] != newEntry.Value)
                        {
                            // Sequence number changed!
                            Console.WriteLine("[" + DateTime.Now + "] Sequence number for " + newEntry.Key + " changed!");

                            // TODO: Retrieve new thing
                            Console.WriteLine(client.Request("v1/products/" + newEntry.Key.Item1 + "/" + newEntry.Key.Item2).ToString());

                            // Work around sometimes getting incomplete results, just reuse existing lib and update
                            currentSummary[newEntry.Key] = newEntry.Value;
                        }
                    }
                    else
                    {
                        // End point is new!
                        Console.WriteLine("[" + DateTime.Now + "] New endpoint found: " + newEntry.Key);
                        currentSummary[newEntry.Key] = newEntry.Value;
                    }
                }

                // Sleep 5 seconds 
                System.Threading.Thread.Sleep(5000);
            }
        }

        private static Dictionary<(string, string), int> ParseSummary(string summary)
        {
            var summaryDictionary = new Dictionary<(string, string), int>();
            foreach(var line in summary.Split("\n"))
            {
                if (string.IsNullOrEmpty(line) || line.StartsWith("Product") || line.StartsWith("#")) continue;
                
                var splitLine = line.Split("|");
                if (string.IsNullOrEmpty(splitLine[2]))
                {
                    summaryDictionary.Add((splitLine[0], "version"), int.Parse(splitLine[1]));
                }
                else
                {
                    summaryDictionary.Add((splitLine[0], splitLine[2]), int.Parse(splitLine[1]));
                }
            }
            return summaryDictionary;
        }
    }
}

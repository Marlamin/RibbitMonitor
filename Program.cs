using Ribbit.Constants;
using Ribbit.Protocol;
using Ribbit.Parsing;
using System;
using System.Collections.Generic;
using System.IO;

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

            if (!Directory.Exists("cache"))
            {
                Directory.CreateDirectory("cache");
            }

            var client = new Client(Region.US);
            var req = client.Request("v1/summary");
            var currentSummary = ParseSummary(req.ToString());
            foreach (var entry in currentSummary)
            {
                if(entry.Value == 0)
                {
                    Console.WriteLine("Sequence number for " + entry.Key + " is 0, skipping..");
                    continue;
                }

                Console.WriteLine(entry.Key.Item1);

                var endpoint = "";

                if (entry.Key.Item2 == "version" || entry.Key.Item2 == "cdn")
                {
                    endpoint = entry.Key.Item2 + "s";
                }
                else if(entry.Key.Item2 == "bgdl")
                {
                    endpoint = entry.Key.Item2;
                }

                try
                {
                    var subRequest = client.Request("v1/products/" + entry.Key.Item1 + "/" + endpoint);
                    var filename = entry.Key.Item2 + "-" + entry.Key.Item1 + "-" + entry.Value + ".bmime";
                    File.WriteAllText(Path.Combine("cache", filename), subRequest.message.ToString());
                }
                catch(FormatException e)
                {
                    Console.WriteLine(entry.Key + " is forked");
                }
                
                // Play nice, wait 100ms
                System.Threading.Thread.Sleep(100);
            }

            Console.WriteLine("Monitoring..");
            while (isMonitoring)
            {
                var newSummaryString = client.Request("v1/summary").ToString();
                var newSummary = new Dictionary<(string, string), int>();

                try
                {
                    newSummary = ParseSummary(newSummaryString);
                }
                catch(Exception e)
                {
                    TelegramClient.SendMessage("Parsing summary failed, send help: " + e.Message);
                    continue;
                }

                foreach(var newEntry in newSummary)
                {
                    if (currentSummary.ContainsKey(newEntry.Key))
                    {
                        if(currentSummary[newEntry.Key] != newEntry.Value)
                        {
                            // Sequence number changed!
                            TelegramClient.SendMessage("Sequence number for " + newEntry.Key + " changed from " + currentSummary[newEntry.Key] + " to " + newEntry.Value);
                            Console.WriteLine("[" + DateTime.Now + "] Sequence number for " + newEntry.Key + " changed from " + currentSummary[newEntry.Key] + " to " + newEntry.Value);

                            var endpoint = "";

                            if (newEntry.Key.Item2 == "version" || newEntry.Key.Item2 == "cdn")
                            {
                                endpoint = newEntry.Key.Item2 + "s";
                            }
                            else if (newEntry.Key.Item2 == "bgdl")
                            {
                                endpoint = newEntry.Key.Item2;
                            }

                            try
                            {
                                var subRequest = client.Request("v1/products/" + newEntry.Key.Item1 + "/" + endpoint);
                                var filename = newEntry.Key.Item2 + "-" + newEntry.Key.Item1 + "-" + newEntry.Value + ".bmime";
                                File.WriteAllText(Path.Combine("cache", filename), subRequest.message.ToString());
                            }
                            catch (FormatException e)
                            {
                                Console.WriteLine(newEntry.Key + " is forked");
                            }

                            // TODO: Diff new thing

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
            var parsedFile = new BPSV(summary);

            foreach(var entry in parsedFile.data)
            {
                if (string.IsNullOrEmpty(entry[2]))
                {
                    summaryDictionary.Add((entry[0], "version"), int.Parse(entry[1]));
                }
                else
                {
                    summaryDictionary.Add((entry[0], entry[2].Trim()), int.Parse(entry[1]));
                }
            }
            return summaryDictionary;
        }
    }
}

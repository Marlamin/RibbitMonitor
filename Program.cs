using Ribbit.Constants;
using Ribbit.Parsing;
using Ribbit.Protocol;
using System;
using System.Collections.Generic;
using System.IO;

namespace RibbitMonitor
{
    class Program
    {
        private static bool isMonitoring = false;
        private static Dictionary<(string, string), string> CachedFiles = new Dictionary<(string, string), string>();

        static void Main(string[] args)
        {
            if (!Directory.Exists("cache"))
            {
                Directory.CreateDirectory("cache");
            }

            Console.WriteLine("Grabbing updates since last run..");

            var client = new Client(Region.US);
            var req = client.Request("v1/summary");

            var summaryString = req.ToString();
            var currentSummary = ParseSummary(summaryString);
            File.WriteAllText(Path.Combine("cache", "latestsummary.bmime"), summaryString);

            foreach (var entry in currentSummary)
            {
                if (entry.Value == 0)
                {
                    Console.WriteLine("Sequence number for " + entry.Key + " is 0, skipping..");
                    continue;
                }

                var endpoint = "";

                if (entry.Key.Item2 == "version" || entry.Key.Item2 == "cdn")
                {
                    endpoint = entry.Key.Item2 + "s";
                }
                else if (entry.Key.Item2 == "bgdl")
                {
                    endpoint = entry.Key.Item2;
                }

                try
                {
                    var filename = entry.Key.Item2 + "-" + entry.Key.Item1 + "-" + entry.Value + ".bmime";

                    Response subRequest;

                    if (File.Exists(Path.Combine("cache", entry.Key.Item1, filename)))
                    {
                        subRequest = new Response(new MemoryStream(File.ReadAllBytes(Path.Combine("cache", entry.Key.Item1, filename))));
                    }
                    else
                    {
                        System.Threading.Thread.Sleep(100);

                        Console.WriteLine(entry.Key.Item1);

                        subRequest = client.Request("v1/products/" + entry.Key.Item1 + "/" + endpoint);

                        if (!Directory.Exists(Path.Combine("cache", entry.Key.Item1)))
                        {
                            Directory.CreateDirectory(Path.Combine("cache", entry.Key.Item1));
                        }

                        if(subRequest.message.ToString().Contains("## seqn = " + entry.Value))
                        {
                            File.WriteAllText(Path.Combine("cache", entry.Key.Item1, filename), subRequest.message.ToString());
                        }
                        else
                        {
                            Console.WriteLine("Retrieved Ribbit message for " + entry.Key.Item1 + " is old, not saving to disk..");
                        }
                    }

                    CachedFiles[entry.Key] = subRequest.ToString();
                }
                catch (FormatException e)
                {
                    Console.WriteLine(entry.Key + " is forked");
                }
            }

            if (args.Length > 0 && args[0] == "monitor")
            {
                isMonitoring = true;
                Console.WriteLine("Monitoring..");
            }

            while (isMonitoring)
            {
                var newSummaryString = client.Request("v1/summary").ToString();
                var newSummary = new Dictionary<(string, string), int>();

                try
                {
                    newSummary = ParseSummary(newSummaryString);
                }
                catch (Exception e)
                {
                    TelegramClient.SendMessage("Parsing summary failed, send help: " + e.Message);
                    continue;
                }

                foreach (var newEntry in newSummary)
                {
                    if (currentSummary.ContainsKey(newEntry.Key))
                    {
                        if (newEntry.Value > currentSummary[newEntry.Key])
                        {
                            TelegramClient.SendMessage("Sequence number for " + newEntry.Key + " increased from " + currentSummary[newEntry.Key] + " to " + newEntry.Value);
                            Console.WriteLine("[" + DateTime.Now + "] Sequence number for " + newEntry.Key + " increased from " + currentSummary[newEntry.Key] + " to " + newEntry.Value);

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

                                var cacheDir = newEntry.Key.Item1;
                                if (!Directory.Exists(Path.Combine("cache", cacheDir)))
                                {
                                    Directory.CreateDirectory(Path.Combine("cache", cacheDir));
                                }

                                File.WriteAllText(Path.Combine("cache", cacheDir, filename), subRequest.message.ToString());

                                //DiffFile(CachedFiles[newEntry.Key], subRequest.ToString());
                            }
                            catch (Exception e)
                            {
                                TelegramClient.SendMessage("Error during diff: " + e.Message);
                                Console.WriteLine("Error during diff: " + e.Message);
                            }

                            currentSummary[newEntry.Key] = newEntry.Value;
                        }
                    }
                    else
                    {
                        Console.WriteLine("[" + DateTime.Now + "] New endpoint found: " + newEntry.Key);
                        TelegramClient.SendMessage("New endpoint found: " + newEntry.Key);
                        currentSummary[newEntry.Key] = newEntry.Value;
                    }
                }

                System.Threading.Thread.Sleep(5000);
            }
        }

        private static Dictionary<(string, string), int> ParseSummary(string summary)
        {
            var summaryDictionary = new Dictionary<(string, string), int>();
            var parsedFile = new BPSV(summary);

            foreach (var entry in parsedFile.data)
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

        private static string DiffFile(string oldContent, string newContent)
        {
            var oldFile = new BPSV(oldContent);
            var newFile = new BPSV(newContent);

            foreach (var oldEntry in oldFile.data)
            {
                var regionMatch = false;

                foreach (var newEntry in newFile.data)
                {
                    // Region matches
                    if (oldEntry[0] == newEntry[0])
                    {
                        regionMatch = true;
                        // diff each field
                    }
                }

                if (regionMatch == false)
                {
                    // new region
                }
            }

            return "";
        }
    }
}

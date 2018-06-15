using CsvHelper;
using NCrontab;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace TimerTriggerProcessor
{
    class Program
    {
        static bool _debug = true;

        static void Main(string[] args)
        {
            var outputFileWriter = new StreamWriter(args[1]);
            var csv = new CsvWriter(outputFileWriter);

            var _triggersCounter = new StringCounter();
            var _funcNameCounter = new StringCounter();
            int appsWithFrequentTimerTriggers = 0;

            // Go through each function app
            foreach (var line in File.ReadAllLines(args[0]).Skip(1))
            {
                var parts = line.Split(',');
                string stamp = parts[0];
                string site = parts[1];
                var lastModifiedTime = DateTimeOffset.Parse(parts[parts.Length-1]);

                if (lastModifiedTime > DateTimeOffset.UtcNow.AddMonths(-6)) continue;

                int jsonStartIndex = line.IndexOf('[');
                int jsonEndsIndex = line.LastIndexOf(']');
                string jsonString = line.Substring(jsonStartIndex, jsonEndsIndex - jsonStartIndex + 1);

                List<dynamic> triggers;
                try
                {
                    triggers = JsonConvert.DeserializeObject<List<dynamic>>(jsonString);
                }
                catch (Exception e)
                {
                    //Console.WriteLine($"{lastModifiedTime}: ERROR: can't parse json '{jsonString}'. {e.Message}");
                    continue;
                }

                var modifiedTriggers = new List<dynamic>();

                bool hasFrequentCronExpression = false;

                // Go through each trigger
                foreach (var trigger in triggers)
                {
                    string triggerType = trigger.type;
                    _triggersCounter.AddString(triggerType);

                    if (triggerType == "timerTrigger")
                    {
                        string cronExpression = trigger.schedule;
                        string functionName = trigger.functionName;
                        if (IsFrequentCronExpression(cronExpression) && IsTemplateFunctionName(functionName))
                        {
                            hasFrequentCronExpression = true;
                            if (functionName != null)
                            {
                                //Console.WriteLine($"{trigger.functionName}: {trigger.schedule}");
                                _funcNameCounter.AddString(functionName);
                            }
                            continue;
                        }
                        else
                        {
                            //Console.WriteLine($"Allow infrequent timer {cronExpression}");
                        }
                    }

                    modifiedTriggers.Add(trigger);
                }

                if (hasFrequentCronExpression)
                {
                    appsWithFrequentTimerTriggers++;

                    csv.WriteRecord(new
                    {
                        SourceStamp = stamp,
                        SiteName = site,
                        NewTriggers = JsonConvert.SerializeObject(modifiedTriggers)
                    });
                    outputFileWriter.WriteLine();
                    //Console.WriteLine(JsonConvert.SerializeObject(triggers));
                    //Console.WriteLine(JsonConvert.SerializeObject(modifiedTriggers));
                    //Console.WriteLine();
                    csv.Flush();
                }
            }

            if (_debug)
            {
                foreach (var pair in _triggersCounter.OrderByDescending(p => p.Value))
                {
                    Console.WriteLine($"{pair.Key}: {pair.Value}");
                }

                Console.WriteLine();
                Console.WriteLine("Trigger function names:");

                foreach (var pair in _funcNameCounter.OrderByDescending(p => p.Value).Take(200))
                {
                    Console.WriteLine($"{pair.Key}: {pair.Value}");
                }

                Console.WriteLine();

                Console.WriteLine($"Apps with frequent timer triggers: {appsWithFrequentTimerTriggers}");
            }
        }

        static bool IsFrequentCronExpression(string cronExpression)
        {
            try
            {
                var schedule = CrontabSchedule.Parse(cronExpression, new CrontabSchedule.ParseOptions { IncludingSeconds = true });

                // View it as frequent if it fires more than once a day in average over the next 30 days
                //return schedule.GetNextOccurrences(DateTime.Now, DateTime.Now.AddDays(30)).Take(31).Count() == 31;

                return schedule.GetNextOccurrences(DateTime.Now, DateTime.Now.AddHours(24)).Take(24).Count() == 24;
            }
            catch (Exception e)
            {
                //Console.WriteLine($"ERROR: Can't parse CRON expression '{cronExpression}'. {e.Message}");
                return false;
            }
        }

        static string[] _templatePrefixes = new[] {
            "TimerTriggerCSharp",
            "TimerTriggerJS",
            "TimerTriggerNodeJS",
            "TimerTriggerPowerShell",
            "TimerTriggerFSharp",
            "Function",
            "MyTimerTrigger"
        };

        static bool IsTemplateFunctionName(string functionName)
        {
            if (functionName == null) return false;

            if (functionName.IndexOf("python", StringComparison.OrdinalIgnoreCase) >= 0) return true;

            if (functionName.StartsWith("test", StringComparison.OrdinalIgnoreCase) ||
                functionName.EndsWith("test", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            foreach (var prefix in _templatePrefixes)
            {
                if (MatchesPrefixWithNumberSuffix(functionName, prefix)) return true;
            }

            return false;
        }

        static bool MatchesPrefixWithNumberSuffix(string s, string prefix)
        {
            if (!s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;

            string suffix = s.Substring(prefix.Length);
            return suffix.Length == 0 || Int32.TryParse(suffix, out int dummy);
        }
    }
}

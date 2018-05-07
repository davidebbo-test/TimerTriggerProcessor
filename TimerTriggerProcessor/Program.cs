using NCrontab;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TimerTriggerProcessor
{
    class Program
    {
        static void Main(string[] args)
        {
            Dictionary<string, int> _triggersByType = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int appsWithFrequentTimerTriggers = 0;

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
                    Console.WriteLine($"{lastModifiedTime}: ERROR: can't parse json '{jsonString}'. {e.Message}");
                    continue;
                }

                var modifiedTriggers = new List<dynamic>();

                bool hasFrequentCronExpression = false;

                foreach (var trigger in triggers)
                {
                    string triggerType = trigger.type;
                    if (_triggersByType.TryGetValue(triggerType, out int count))
                    {
                        _triggersByType[triggerType] = count + 1;
                    }
                    else
                    {
                        _triggersByType[triggerType] = 1;
                    }

                    if (triggerType == "timerTrigger")
                    {
                        string cronExpression = trigger.schedule;
                        if (IsFrequentCronExpression(cronExpression))
                        {
                            hasFrequentCronExpression = true;
                            //Console.WriteLine(trigger.schedule);
                            continue;
                        }
                        else
                        {
                            //Console.WriteLine($"Allow infrequent timer {cronExpression}");
                        }
                    }

                    modifiedTriggers.Add(trigger);
                }

                if (hasFrequentCronExpression) appsWithFrequentTimerTriggers++;

                //Console.WriteLine(JsonConvert.SerializeObject(triggers));
                //Console.WriteLine(JsonConvert.SerializeObject(modifiedTriggers));
            }

            foreach (var pair in _triggersByType.OrderByDescending(p => p.Value))
            {
                Console.WriteLine($"{pair.Key}: {pair.Value}");
            }

            Console.WriteLine();

            Console.WriteLine($"Apps with frequent timer triggers: {appsWithFrequentTimerTriggers}");
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
    }
}

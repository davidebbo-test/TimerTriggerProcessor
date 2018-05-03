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
            foreach (var line in File.ReadAllLines(args[0]).Skip(1))
            {
                int jsonStartIndex = line.IndexOf('[');
                int jsonEndsIndex = line.LastIndexOf(']');
                string jsonString = line.Substring(jsonStartIndex, jsonEndsIndex - jsonStartIndex + 1);
                var triggers = JsonConvert.DeserializeObject<List<dynamic>>(jsonString);

                var modifiedTriggers = new List<dynamic>();

                foreach (var trigger in triggers)
                {
                    if (trigger.type == "timerTrigger")
                    {
                        string cronExpression = trigger.schedule;
                        if (IsFrequentCronExpression(cronExpression))
                        {
                            Console.WriteLine(trigger.schedule);
                            continue;
                        }
                        else
                        {
                            Console.WriteLine($"Allow infrequent timer {cronExpression}");
                        }
                    }

                    modifiedTriggers.Add(trigger);
                }

                Console.WriteLine(JsonConvert.SerializeObject(triggers));
                Console.WriteLine(JsonConvert.SerializeObject(modifiedTriggers));
            }
        }

        static bool IsFrequentCronExpression(string cronExpression)
        {
            var schedule = CrontabSchedule.Parse(cronExpression, new CrontabSchedule.ParseOptions { IncludingSeconds = true });

            // View it as frequent if it fires more than once a day in average over the next 30 days
            return schedule.GetNextOccurrences(DateTime.Now, DateTime.Now.AddDays(30)).Take(31).Count() == 31;
        }
    }
}

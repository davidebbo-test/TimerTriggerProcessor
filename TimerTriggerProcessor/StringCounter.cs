using System;
using System.Collections.Generic;
using System.Text;

namespace TimerTriggerProcessor
{
    class StringCounter: Dictionary<string, int>
    {
        public StringCounter(): base(StringComparer.OrdinalIgnoreCase)
        {
        }

        public void AddString(string s)
        {
            if (TryGetValue(s, out int count))
            {
                this[s] = count + 1;
            }
            else
            {
                this[s] = 1;
            }
        }
    }
}

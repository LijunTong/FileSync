using System;

namespace FileSync.Utils
{
    public static class CronEvaluator
    {
        public static bool IsMatch(string? cronExpression, DateTime time)
        {
            if (string.IsNullOrWhiteSpace(cronExpression))
                return false;

            var fields = cronExpression!.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 5 || fields.Length > 6)
                return false;

            int offset = fields.Length == 6 ? 0 : -1;
            long second = offset == 0 ? ParseField(fields[0], 0, 59) : 0;
            long minute = ParseField(fields[offset + 1], 0, 59);
            long hour = ParseField(fields[offset + 2], 0, 23);
            long day = ParseField(fields[offset + 3], 1, 31);
            long month = ParseField(fields[offset + 4], 1, 12);
            long dow = ParseField(fields[offset + 5], 0, 6);

            return (second == -1 || MatchValue(second, time.Second))
                && (minute == -1 || MatchValue(minute, time.Minute))
                && (hour == -1 || MatchValue(hour, time.Hour))
                && (day == -1 || MatchValue(day, time.Day))
                && (month == -1 || MatchValue(month, time.Month))
                && (dow == -1 || MatchValue(dow, (int)time.DayOfWeek));
        }

        public static bool IsValid(string? cronExpression)
        {
            if (string.IsNullOrWhiteSpace(cronExpression))
                return false;
            var fields = cronExpression!.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 5 || fields.Length > 6)
                return false;

            int offset = fields.Length == 6 ? 0 : -1;
            int[][] ranges = new[] {
                new[] { 0, 59 }, new[] { 0, 59 }, new[] { 0, 23 },
                new[] { 1, 31 }, new[] { 1, 12 }, new[] { 0, 6 }
            };

            for (int i = 0; i < 6; i++)
            {
                int fieldIdx = offset + i;
                if (fieldIdx < 0) continue;
                if (ParseField(fields[fieldIdx], ranges[i][0], ranges[i][1]) == 0
                    && fields[fieldIdx] != "0"
                    && !fields[fieldIdx].StartsWith("0,")
                    && fields[fieldIdx] != "0/1")
                    return false;
            }
            return true;
        }

        public static DateTime? GetNextOccurrence(string? cronExpression, DateTime from)
        {
            if (string.IsNullOrWhiteSpace(cronExpression) || !IsValid(cronExpression))
                return null;

            var fields = cronExpression!.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            bool hasSeconds = fields.Length == 6;
            var test = hasSeconds
                ? new DateTime(from.Year, from.Month, from.Day, from.Hour, from.Minute, from.Second)
                : new DateTime(from.Year, from.Month, from.Day, from.Hour, from.Minute, 0);

            int maxIterations = hasSeconds ? 86400 : 525600;
            for (int i = 0; i < maxIterations; i++)
            {
                test = hasSeconds ? test.AddSeconds(1) : test.AddMinutes(1);
                if (IsMatch(cronExpression, test))
                    return test;
            }
            return null;
        }

        private static long ParseField(string field, int min, int max)
        {
            if (field == "*" || field == "?")
                return -1L;

            long result = 0;
            foreach (var part in field.Split(','))
            {
                int step = 1;
                var range = part;

                int slashIdx = part.IndexOf('/');
                if (slashIdx >= 0)
                {
                    range = part.Substring(0, slashIdx);
                    if (!int.TryParse(part.Substring(slashIdx + 1), out step))
                        continue;
                }

                if (range == "*" || range == "?")
                {
                    for (int v = min; v <= max; v += step)
                        result |= 1L << v;
                    continue;
                }

                int dashIdx = range.IndexOf('-');
                if (dashIdx >= 0)
                {
                    if (!int.TryParse(range.Substring(0, dashIdx), out int start))
                        continue;
                    if (!int.TryParse(range.Substring(dashIdx + 1), out int end))
                        continue;
                    for (int v = start; v <= end; v += step)
                        result |= 1L << v;
                }
                else if (slashIdx >= 0)
                {
                    // N/step: start at N and step through to max
                    if (int.TryParse(range, out int start))
                        for (int v = start; v <= max; v += step)
                            result |= 1L << v;
                }
                else
                {
                    if (int.TryParse(range, out int val))
                        result |= 1L << val;
                }
            }
            return result;
        }

        private static bool MatchValue(long bitmask, int value)
        {
            return (bitmask & (1L << value)) != 0;
        }
    }
}

namespace ESSLeaveSystem.Services
{
    public interface IPublicHolidayService
    {
        /// <summary>
        /// Check if a date is a South African public holiday
        /// </summary>
        bool IsPublicHoliday(DateTime date);
        
        /// <summary>
        /// Get all South African public holidays for a year
        /// </summary>
        List<PublicHoliday> GetPublicHolidays(int year);
        
        /// <summary>
        /// Count working days between two dates (excluding weekends and public holidays)
        /// </summary>
        int CountWorkingDays(DateTime startDate, DateTime endDate);
        
        /// <summary>
        /// Get the name of the public holiday
        /// </summary>
        string? GetHolidayName(DateTime date);
    }

    public class PublicHolidayService : IPublicHolidayService
    {
        public bool IsPublicHoliday(DateTime date)
        {
            var holidays = GetPublicHolidays(date.Year);
            return holidays.Any(h => h.Date.Date == date.Date);
        }

        public List<PublicHoliday> GetPublicHolidays(int year)
        {
            var holidays = new List<PublicHoliday>
            {
                // Fixed public holidays
                new PublicHoliday { Date = new DateTime(year, 1, 1), Name = "New Year's Day", IsFixed = true },
                new PublicHoliday { Date = new DateTime(year, 3, 21), Name = "Human Rights Day", IsFixed = true },
                new PublicHoliday { Date = new DateTime(year, 4, 27), Name = "Freedom Day", IsFixed = true },
                new PublicHoliday { Date = new DateTime(year, 5, 1), Name = "Workers' Day", IsFixed = true },
                new PublicHoliday { Date = new DateTime(year, 6, 16), Name = "Youth Day", IsFixed = true },
                new PublicHoliday { Date = new DateTime(year, 8, 9), Name = "National Women's Day", IsFixed = true },
                new PublicHoliday { Date = new DateTime(year, 9, 24), Name = "Heritage Day", IsFixed = true },
                new PublicHoliday { Date = new DateTime(year, 12, 16), Name = "Day of Reconciliation", IsFixed = true },
                new PublicHoliday { Date = new DateTime(year, 12, 25), Name = "Christmas Day", IsFixed = true },
                new PublicHoliday { Date = new DateTime(year, 12, 26), Name = "Day of Goodwill", IsFixed = true }
            };

            // Add moveable holidays (Easter-based)
            var easterSunday = CalculateEasterSunday(year);
            holidays.Add(new PublicHoliday 
            { 
                Date = easterSunday.AddDays(-2), // Good Friday
                Name = "Good Friday", 
                IsFixed = false 
            });
            holidays.Add(new PublicHoliday 
            { 
                Date = easterSunday.AddDays(1), // Easter Monday (Family Day)
                Name = "Family Day", 
                IsFixed = false 
            });

            // Handle holidays falling on Sunday (observed on Monday)
            var adjustedHolidays = new List<PublicHoliday>();
            foreach (var holiday in holidays)
            {
                adjustedHolidays.Add(holiday);
                
                // If holiday falls on Sunday, next Monday is also a holiday
                if (holiday.Date.DayOfWeek == DayOfWeek.Sunday && holiday.IsFixed)
                {
                    adjustedHolidays.Add(new PublicHoliday
                    {
                        Date = holiday.Date.AddDays(1),
                        Name = $"{holiday.Name} (Observed)",
                        IsFixed = false
                    });
                }
            }

            return adjustedHolidays.OrderBy(h => h.Date).ToList();
        }

        public int CountWorkingDays(DateTime startDate, DateTime endDate)
        {
            if (endDate < startDate)
            {
                throw new ArgumentException("End date must be after start date");
            }

            int workingDays = 0;
            var currentDate = startDate.Date;

            while (currentDate <= endDate.Date)
            {
                // Check if it's a weekend
                if (currentDate.DayOfWeek != DayOfWeek.Saturday && 
                    currentDate.DayOfWeek != DayOfWeek.Sunday)
                {
                    // Check if it's a public holiday
                    if (!IsPublicHoliday(currentDate))
                    {
                        workingDays++;
                    }
                }
                
                currentDate = currentDate.AddDays(1);
            }

            return workingDays;
        }

        public string? GetHolidayName(DateTime date)
        {
            var holidays = GetPublicHolidays(date.Year);
            var holiday = holidays.FirstOrDefault(h => h.Date.Date == date.Date);
            return holiday?.Name;
        }

        /// <summary>
        /// Calculate Easter Sunday using Computus algorithm (Anonymous Gregorian algorithm)
        /// </summary>
        private DateTime CalculateEasterSunday(int year)
        {
            int a = year % 19;
            int b = year / 100;
            int c = year % 100;
            int d = b / 4;
            int e = b % 4;
            int f = (b + 8) / 25;
            int g = (b - f + 1) / 3;
            int h = (19 * a + b - d - g + 15) % 30;
            int i = c / 4;
            int k = c % 4;
            int l = (32 + 2 * e + 2 * i - h - k) % 7;
            int m = (a + 11 * h + 22 * l) / 451;
            int month = (h + l - 7 * m + 114) / 31;
            int day = ((h + l - 7 * m + 114) % 31) + 1;

            return new DateTime(year, month, day);
        }
    }

    public class PublicHoliday
    {
        public DateTime Date { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsFixed { get; set; } // False for moveable holidays like Easter
    }
}
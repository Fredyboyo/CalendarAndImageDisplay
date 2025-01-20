using Google.Apis.Calendar.v3.Data;

namespace CalendarAndImageDisplay.Model
{
    public class Calendar
    {
        public string? Name { get; set; }
        public ColorDefinition? Color { get; set; }
        public string? GoogleId { get; set; }
    }
    public class Day
    {
        public List<DayEvent> Events { get; set; } = [];
        public DateTime Date { get; set; }
        public Day(DateTime date)
        {
            Date = date;
        }
        public Day(DateTime date, List<DayEvent> events)
        {
            Events = events;
            Date = date;
        }
    }
    public class DayEvent
    {
        public string? Title { get; set; }
        public string? CalendarName { get; set; }
        public ColorDefinition? Color { get; set; }
        public string? Location { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime StartTime { get; set; }
        public bool IsFullDayEvent { get; set; }

        public DayEvent(Calendar calendar, Event googleEvent)
        {
            Title = googleEvent.Summary;
            CalendarName = calendar.Name;
            Color = calendar.Color;
            Location = googleEvent.Location;
            if (googleEvent.Start.DateTimeRaw == null) // Is full day event
            {
                StartTime = DateTime.Parse(googleEvent.Start.Date);
            }
            else
            {
                StartTime = DateTime.Parse(googleEvent.Start.DateTimeRaw);
                EndTime = DateTime.Parse(googleEvent.End.DateTimeRaw);
            }
        }
    }
}

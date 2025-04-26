using Google.Apis.Calendar.v3.Data;

namespace CalendarAndImageDisplay.Model;

public class Calendar
{
    public string? Name { get; set; }
    public ColorDefinition? Color { get; set; }
    public string? Id { get; set; }

    public Calendar(string id, string name, ColorDefinition color)
    {
        Id = id;
        Name = name;
        Color = color;
    }
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
        TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time") ?? throw new Exception("Wrong time zone");
        if (googleEvent.Start.DateTimeRaw == null) // Is full day event
        {
            StartTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.Parse(googleEvent.Start.Date).ToUniversalTime(), timeZone);
            IsFullDayEvent = true;
        }
        else
        {
            StartTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.Parse(googleEvent.Start.DateTimeRaw).ToUniversalTime(), timeZone);
            EndTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.Parse(googleEvent.End.DateTimeRaw).ToUniversalTime(), timeZone);
            IsFullDayEvent = false;
        }
    }
}

public class EventGroup
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public List<DayEvent> Events { get; set; } = [];

    public EventGroup(DayEvent dayEvent)
    {
        StartTime = dayEvent.StartTime;
        EndTime = dayEvent.EndTime;
        Add(dayEvent);
    }
    public void Add(DayEvent dayEvent)
    {
        Events.Add(dayEvent);
        if (EndTime < dayEvent.EndTime) EndTime = dayEvent.EndTime;
    }
}

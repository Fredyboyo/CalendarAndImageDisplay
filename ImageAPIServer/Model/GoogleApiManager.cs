using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using System.Text.Json.Serialization;
using System.Text.Json;
using static Google.Apis.Calendar.v3.EventsResource;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Util.Store;
using Google.Apis.Auth.OAuth2.Responses;
using static CalendarAndImageDisplay.Model.GoogleApiManager;

namespace CalendarAndImageDisplay.Model;

public class TokenError(string a) : Exception(a) { }

public static class GoogleApiManager
{
    private const string _authDirectory = "./Auth";

    public static MyCalendarService? Service { get; set; }

    public interface IGetCalendarServiceResponse;
    public static async Task<IGetCalendarServiceResponse> GetCalendarService()
    {
        return Service ?? await CreateCalendarService();
    }
    /// <summary>
    /// Creates a new CalendarService, either from loaded file, from the google servers using the ClientSecret
    /// </summary>
    /// <returns>A MyCalendarService object, except if the token is missing or stale. Then it will return a DeviceAuthorization object, to send to client.</returns>
    private static async Task<IGetCalendarServiceResponse> CreateCalendarService()
    {
        ClientSecrets secrets = GetFirstClientSecrets();

        GoogleAuthorizationCodeFlow flow = GetGoogleAuthFlow(secrets);

        TokenResponse? token = await flow.LoadTokenAsync(Environment.UserName, CancellationToken.None);

        if (token == null)
        {
            HttpClient httpClient = new();
            // Step 1: Request device UserCode from Google's device UserCode endpoint
            DeviceAuthorization deviceCodeData = await RequestDeviceCode(httpClient, secrets.ClientId);
            // Step 2: Poll for the authorization response, this will wait for step 3 to be complete.
            _ = PollAuthentication(httpClient, deviceCodeData, secrets.ClientId, secrets.ClientSecret);
            // Step 3: Display the verification URL and user UserCode to the user

            return deviceCodeData;
        }

        UserCredential credential = new(flow, Environment.UserName, token);

        if (credential.Token.IsStale)
        {
            await credential.RefreshTokenAsync(CancellationToken.None);
            Console.WriteLine("Token refreshed successfully.");
        }
        else
        {
            Console.WriteLine("Token is still valid.");
        }

        BaseClientService.Initializer Init = new()
        {
            HttpClientInitializer = credential,
            ApplicationName = "Desktop client 1",
        };
        MyCalendarService calendarService = new MyCalendarService(Init);
        await calendarService.PopulateCalendars();
        return calendarService;
    }

    private static async Task<IGetCalendarServiceResponse> CreateCalendarServiceWithToken(TokenResponse token)
    {
        ClientSecrets secrets = GetFirstClientSecrets();

        GoogleAuthorizationCodeFlow flow = GetGoogleAuthFlow(secrets);

        UserCredential credential = new(flow, Environment.UserName, token);

        if (credential.Token.IsStale)
        {
            await credential.RefreshTokenAsync(CancellationToken.None);
            Console.WriteLine("Token refreshed successfully.");
        }
        else
        {
            Console.WriteLine("Token is still valid.");
        }

        BaseClientService.Initializer Init = new()
        {
            HttpClientInitializer = credential,
            ApplicationName = "Desktop client 1",
        };

        MyCalendarService calendarService = new MyCalendarService(Init);
        await calendarService.PopulateCalendars();
        return calendarService;
    }

    /// <summary>
    /// Gets the first clientSecrets file in the '/Auth' dictionary.
    /// </summary>
    /// <returns>clientSecret object</returns>
    /// <exception cref="FileNotFoundException"></exception>
    private static ClientSecrets GetFirstClientSecrets()
    {
        Directory.CreateDirectory(_authDirectory);
        string[] files = Directory.GetFiles(_authDirectory, "*.json");
        if (files.Length > 0)
        {
            return GoogleClientSecrets.FromFile(files[0]).Secrets;
        }
        else
        {
            string errorMessage = $"No ClientSecrets were included in the application! Please add the Google ClientSecrets to the '{_authDirectory}' directory";
            Console.WriteLine(errorMessage);
            throw new FileNotFoundException(errorMessage);
        }
    }

    /// <summary>
    /// Simplified way to get the AuthFlow for the google server, with ClientSecrets.
    /// Contains hardcoded info for Scopes and DataStore.
    /// </summary>
    /// <param name="secrets"></param>
    /// <returns>A new GoogleAuthorizationCodeFlow</returns>
    private static GoogleAuthorizationCodeFlow GetGoogleAuthFlow(ClientSecrets secrets)
    {
        return new(new GoogleAuthorizationCodeFlow.Initializer()
        {
            ClientSecrets = secrets,
            Scopes = ["https://www.googleapis.com/auth/calendar"],
            DataStore = new FileDataStore("Store", true)
        });
    }
    public static async Task<DeviceAuthorization> RequestDeviceCode(HttpClient httpClient, string clientId)
    {
        FormUrlEncodedContent deviceCodeRequest = new (
        [
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("scope", "https://www.googleapis.com/auth/calendar")
        ]);

        HttpResponseMessage deviceCodeResponse = await httpClient.PostAsync("https://oauth2.googleapis.com/device/code", deviceCodeRequest);
        deviceCodeResponse.EnsureSuccessStatusCode();

        string deviceCodeResponseBody = await deviceCodeResponse.Content.ReadAsStringAsync();

        DeviceAuthorization? response = JsonSerializer.Deserialize<DeviceAuthorization>(deviceCodeResponseBody);

        if (response == null)
        {
            Console.WriteLine("Error:/GoogleAPIManager/Auth - deviceCodeData is null");
            throw new Exception("Error:/GoogleAPIManager/Auth - deviceCodeData is null");
        }
        return response;
    }

    public static async Task PollAuthentication(HttpClient httpClient, DeviceAuthorization deviceCodeData, string clientId, string clientSecret)
    {
        TokenResponse? tokenResponse = null;
        DateTime startTime = DateTime.UtcNow;
        while (tokenResponse == null)
        {
            if ((DateTime.UtcNow - startTime).TotalSeconds > 60)
            {
                Console.WriteLine("Polling timeout exceeded. Aborting authentication.");
                return;
            }
            FormUrlEncodedContent tokenRequest = new(
            [
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string?>("device_code", deviceCodeData.DeviceCode),
                new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:device_code")
            ]);
            HttpResponseMessage tokenResponseHttp = await httpClient.PostAsync("https://oauth2.googleapis.com/token", tokenRequest);
            if (tokenResponseHttp.IsSuccessStatusCode)
            {
                string tokenResponseBody = await tokenResponseHttp.Content.ReadAsStringAsync();
                //Console.WriteLine($"tokenResponseBody: {tokenResponseBody}");

                TokenTuple? tokenTuple = JsonSerializer.Deserialize<TokenTuple>(tokenResponseBody);
                if (tokenTuple == null) return;
                tokenResponse = new TokenResponse
                {
                    AccessToken = tokenTuple.AccessToken,
                    TokenType = tokenTuple.TokenType,
                    ExpiresInSeconds = tokenTuple.ExpiresInSeconds,
                    RefreshToken = tokenTuple.RefreshToken,
                    Scope = tokenTuple.Scope
                };
            }
            else
            {
                // Handle "authorization_pending" response and retry after the interval
                var errorResponseBody = await tokenResponseHttp.Content.ReadAsStringAsync();
                if (errorResponseBody.Contains("authorization_pending"))
                {
                    await Task.Delay(deviceCodeData.Interval * 1000);
                }
                else
                {
                    throw new Exception("Error during token retrieval: " + errorResponseBody);
                }
            }
        }
        await CreateCalendarServiceWithToken(tokenResponse);
    }

    public class TokenTuple
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }

        [JsonPropertyName("expires_in")]
        public long? ExpiresInSeconds { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }
    }

    // ===========================[ Methods ]==============================


    public class DeviceAuthorization : IGetCalendarServiceResponse
    {
        [JsonPropertyName("device_code")]
        public string? DeviceCode { get; set; }

        [JsonPropertyName("user_code")]
        public string? UserCode { get; set; }

        [JsonPropertyName("verification_url")]
        public string? VerificationUrl { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("interval")]
        public int Interval { get; set; }
    }
}

public class MyCalendarService(BaseClientService.Initializer initializer) : CalendarService(initializer), IGetCalendarServiceResponse
{
    private static List<Calendar> MyCalendarList { get; set; } = [];

    public async Task PopulateCalendars()
    {
        Colors colors = Colors.Get().Execute();
        IList<CalendarListEntry>? googleCalendars = await GetAllCalendar();
        if (googleCalendars == null) return;
        MyCalendarList = [.. googleCalendars.Select(cal => ConvertCalendarEntryToOwnCalendar(colors, cal))];
    }

    private static Calendar ConvertCalendarEntryToOwnCalendar(Colors colors, CalendarListEntry cal)
    {
        return new(cal.Id, cal.Summary, colors.Calendar[cal.ColorId]);
    }

    private async Task<List<DayEvent>> GetEventsBetween(DateTime start, DateTime end, int eventLimitCount = 20)
    {
        List<DayEvent> allEvents = [];
        foreach (Calendar calendar in MyCalendarList)
        {
            // Create a list request to fetch events from Google Calendar
            ListRequest request = Events.List(calendar.Id);
            // Set the TimeMin and TimeMax to eventLimitCount the events to today
            request.TimeMinDateTimeOffset = start;
            request.TimeMaxDateTimeOffset = end;
            // Additional request parameters
            request.SingleEvents = true;
            request.MaxResults = eventLimitCount;
            request.OrderBy = ListRequest.OrderByEnum.StartTime;

            // Execute the request
            Events events = await request.ExecuteAsync();
            if (events.Items == null) continue;
            foreach (Event calendarEvent in events.Items)
            {
                allEvents.Add(new DayEvent(calendar, calendarEvent));
            }
        }
        return allEvents;
    }

    public async Task<Day[]> GetDays(int daysLookAhead)
    {
        DateTime today = DateTime.Today;
        DateTime lookAhead = today.AddDays(daysLookAhead);

        List<DayEvent> ThisWeeksEvents = await GetEventsBetween(today, lookAhead);

        Day[] days = new Day[daysLookAhead];

        for (int i = 0; i < daysLookAhead; i++)
        {
            DateTime date = today.Date.AddDays(i);
            List<DayEvent> dayEvents = ThisWeeksEvents.Where(e => e.StartTime.Day.Equals(date.Day)).ToList();

            days[i] = new Day(date)
            {
                Events = dayEvents,
            };
        }
        return days;
    }
    public async Task<Day[]> GetWeek()
    {

        DateTime today = DateTime.Today;

        DateTime startOfWeek = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
        DateTime endOfWeek = startOfWeek.AddDays(6);

        List<DayEvent> ThisWeeksEvents = await GetEventsBetween(startOfWeek, endOfWeek);

        Day[] days = new Day[7];

        for (int i = 0; i < 7; i++)
        {
            DateTime date = startOfWeek.Date.AddDays(i);
            List<DayEvent> dayEvents = ThisWeeksEvents.Where(e => e.StartTime.Day.Equals(date.Day)).ToList();

            days[i] = new Day(date, dayEvents);
        }
        return days;
    }


    public async Task<CalendarListEntry?> GetCalendar(string calendarId)
    {
        CalendarList calendarList = await base.CalendarList.List().ExecuteAsync();
        return calendarList.Items.FirstOrDefault(c => c.Id == calendarId);
    }

    public async Task<IList<CalendarListEntry>?> GetAllCalendar()
    {
        CalendarList calendarList = await base.CalendarList.List().ExecuteAsync();
        return calendarList.Items;
    }

    public async Task PrintAllCalendars()
    {
        try
        {
            // List the calendars from the API
            CalendarList calendarList = await base.CalendarList.List().ExecuteAsync();
            Console.WriteLine($"Total Calendars: {calendarList.Items.Count}");
            foreach (CalendarListEntry calendar in calendarList.Items)
            {
                Console.WriteLine($"- {calendar.Summary} (ID: {calendar.Id})");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while retrieving the calendars: {ex.Message}");
        }
    }

}
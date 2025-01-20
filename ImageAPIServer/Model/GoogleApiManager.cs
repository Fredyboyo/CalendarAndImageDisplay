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

namespace CalendarAndImageDisplay.Model
{
    public class TokenError(string a) : Exception(a) { }

    static public class GoogleApiManager
    {
        private static readonly string _authDirectory = "./Auth";
        private static string _clientSecretPath = string.Empty;
        private static List<Calendar> calendars = [];
        private static CalendarService? service;

        // ===========================[ Auth ]==============================
        public static async Task UpdateCredentials(TokenResponse? token = null)
        {
            string[] files = Directory.GetFiles(_authDirectory, "*.json");
            if (files.Length > 0)
            {
                _clientSecretPath = files[0]; // Use the first file found
            }
            else
            {
                throw new FileNotFoundException("No valid client secret files found in the directory.");
            }

            ClientSecrets secrets = GoogleClientSecrets.FromFile(_clientSecretPath).Secrets;

            GoogleAuthorizationCodeFlow flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = secrets,
                Scopes = ["https://www.googleapis.com/auth/calendar"],
                DataStore = new FileDataStore("Store", true)
            });
            token ??= await flow.LoadTokenAsync(Environment.UserName, CancellationToken.None) ?? throw new TokenError("Token is null");
            UserCredential credential = new UserCredential(flow, Environment.UserName, token);

            if (credential.Token.IsStale)
            {
                await credential.RefreshTokenAsync(CancellationToken.None);
                //Console.WriteLine("Token refreshed successfully.");
            }
            else
            {
                //Console.WriteLine("Token is still valid.");
            }

            service = new CalendarService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Desktop client 1",
            });
            PopulateCalendars();
        }

        private static void PopulateCalendars()
        {
            if (service == null) { return; }

            Colors colors = service.Colors.Get().Execute();
            IList<CalendarListEntry>? googleCalendars = GetAllCalendar();
            if (googleCalendars == null) return;
            calendars = googleCalendars.Select(cal =>
            {
                return new Calendar
                {
                    Name = cal.Summary,
                    Color = colors.Calendar[cal.ColorId],
                    GoogleId = cal.Id,
                };
            }).ToList();
        }

        public static async Task<(string verificationUrl, string userCode)> StartAuthenticationProcess()
        {
            HttpClient httpClient = new HttpClient();
            ClientSecrets secrets = GoogleClientSecrets.FromFile(_clientSecretPath).Secrets;

            // Step 1: Request device code from Google's device code endpoint
            DeviceAuthorizationResponse deviceCodeData = await RequestDeviceCode(httpClient, secrets.ClientId);
            // Step 2: Poll for the authorization response, this will wait for step 3 to be complete.
            _ = PollAuthentication(httpClient, deviceCodeData, secrets.ClientId, secrets.ClientSecret);
            // Step 3: Display the verification URL and user code to the user
            return (deviceCodeData.VerificationUrl, deviceCodeData.UserCode);
        }

        public static async Task<DeviceAuthorizationResponse> RequestDeviceCode(HttpClient httpClient, string clientId)
        {

            FormUrlEncodedContent deviceCodeRequest = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("scope", "https://www.googleapis.com/auth/calendar")
            ]);

            HttpResponseMessage deviceCodeResponse = await httpClient.PostAsync("https://oauth2.googleapis.com/device/code", deviceCodeRequest);
            deviceCodeResponse.EnsureSuccessStatusCode();

            string deviceCodeResponseBody = await deviceCodeResponse.Content.ReadAsStringAsync();
            DeviceAuthorizationResponse? deviceCodeData = JsonSerializer.Deserialize<DeviceAuthorizationResponse>(deviceCodeResponseBody)
                ?? throw new Exception("Error:/GoogleAPIManager/Auth - deviceCodeData is null");
            return deviceCodeData;
        }

        public static async Task PollAuthentication(HttpClient httpClient, DeviceAuthorizationResponse deviceCodeData, string clientId, string clientSecret)
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
                FormUrlEncodedContent tokenRequest = new FormUrlEncodedContent(
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
                    Console.WriteLine($"tokenResponseBody: {tokenResponseBody}");
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
            await UpdateCredentials(tokenResponse);
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

        private static List<DayEvent> GetEventsBetween(DateTime start, DateTime end, int limit = 20)
        {
            if (service == null) return [];
            List<DayEvent> allEvents = [];
            foreach (Calendar calendar in calendars)
            {
                // Create a list request to fetch events from Google Calendar
                ListRequest request = service.Events.List(calendar.GoogleId);
                // Set the TimeMin and TimeMax to limit the events to today
                request.TimeMinDateTimeOffset = start;
                request.TimeMaxDateTimeOffset = end;
                // Additional request parameters
                request.SingleEvents = true;
                request.MaxResults = limit;
                request.OrderBy = ListRequest.OrderByEnum.StartTime;

                // Execute the request
                Events events = request.Execute();
                if (events.Items == null) continue;
                foreach (Event @event in events.Items)
                {
                    allEvents.Add(new DayEvent(calendar, @event));
                }
            }
            return allEvents;
        }

        public static CalendarListEntry? GetCalendar(string calendarId)
        {
            if (service == null) return null;
            CalendarList calendarList = service.CalendarList.List().Execute();
            return calendarList.Items.FirstOrDefault(c => c.Id == calendarId);
        }

        public static IList<CalendarListEntry>? GetAllCalendar()
        {
            if (service == null) return null;
            CalendarList calendarList = service.CalendarList.List().Execute();
            return calendarList.Items;
        }

        public static void PrintAllCalendars()
        {
            if (service == null) return;
            try
            {
                // List the calendars from the API
                var calendarList = service.CalendarList.List().Execute();
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

        public static Day[] GetDays(int daysLookAhead)
        {
            DateTime today = DateTime.Today;
            DateTime lookAhead = today.AddDays(daysLookAhead);

            List<DayEvent> ThisWeeksEvents = GetEventsBetween(today, lookAhead);

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
        public static Day[] GetWeek()
        {

            DateTime today = DateTime.Today;

            DateTime startOfWeek = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
            DateTime endOfWeek = startOfWeek.AddDays(6);

            List<DayEvent> ThisWeeksEvents = GetEventsBetween(startOfWeek, endOfWeek);

            Day[] days = new Day[7];

            for (int i = 0; i < 7; i++)
            {
                DateTime date = startOfWeek.Date.AddDays(i);
                List<DayEvent> dayEvents = ThisWeeksEvents.Where(e => e.StartTime.Day.Equals(date.Day)).ToList();

                days[i] = new Day(date, dayEvents);
            }
            return days;
        }
        public class DeviceAuthorizationResponse
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
        public class TokenFile
        {
            public string? Type { get; set; }
            public string? ClientId { get; set; }
            public string? ClientSecret { get; set; }
            public string? AccessToken { get; set; }
            public string? RefreshToken { get; set; }
            public DateTime ExpirationDate { get; set; }
        }
    }
}

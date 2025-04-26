using CalendarAndImageDisplay.Model;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static CalendarAndImageDisplay.Model.GoogleApiManager;

namespace CalendarAndImageDisplay.Pages
{
    public class IndexModel : PageModel
    {
        public bool AuthenticationNeeded { get; set; } = false;
        public Day[] Days { get; set; } = [];
        public string? VerificationUrl { get; set; }
        public string? UserCode { get; set; }

        public async Task OnGet()
        {
            IGetCalendarServiceResponse response = await GetCalendarService();

            if (response is MyCalendarService service)
            {
                Days = await service.GetDays(5);
            } else if (response is DeviceAuthorization device)
            {
                AuthenticationNeeded = true;
                VerificationUrl = device.VerificationUrl;
                UserCode = device.UserCode;
            }
        }
    }
}

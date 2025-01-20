using CalendarAndImageDisplay.Model;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CalendarAndImageDisplay.Pages
{
    public class IndexModel : PageModel
    {
        public Day[] Days = [];

        public void OnGet()
        {
            Days = GoogleApiManager.GetDays(2);
        }
    }
}

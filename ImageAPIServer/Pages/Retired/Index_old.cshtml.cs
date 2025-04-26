using CalendarAndImageDisplay.Model;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Threading.Tasks;

namespace CalendarAndImageDisplay.Pages
{
    public class IndexModelOld : PageModel
    {
        public Day[] Days = [];

        public async Task OnGet()
        {
            //Days = await GoogleApiManager.GetWeek();
        }
    }
}

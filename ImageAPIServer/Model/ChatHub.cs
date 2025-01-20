using Microsoft.AspNetCore.SignalR;

namespace CalendarAndImageDisplay.Model
{
    public class ChatHub : Hub
    {
        public async Task NotifyClientsToUpdate()
        {
            await Clients.All.SendAsync("RefreshPage");
        }
    }
}

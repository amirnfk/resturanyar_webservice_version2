using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace Resturanyar.Hubs
{
    public class OrderHub : Hub
    {
        public async Task SendOrderUpdate(string restaurantId, string message)
        {
            await Clients.Group(restaurantId).SendAsync("ReceiveOrderUpdate", message);
        }

        // 🔥 اضافه کردن این متد برای تست اتصال
        public async Task JoinRestaurantGroup(string restaurantId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, restaurantId);
            await Clients.Caller.SendAsync("JoinedGroup", $"Connected to restaurant {restaurantId}");
        }

        public override async Task OnConnectedAsync()
        {
            var httpContext = Context.GetHttpContext();
            var restaurantId = httpContext.Request.Query["restaurantId"];
            if (!string.IsNullOrEmpty(restaurantId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, restaurantId);
                await Clients.Caller.SendAsync("Connected", $"Connected to restaurant {restaurantId}");
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            await base.OnDisconnectedAsync(exception);
        }
    }
}
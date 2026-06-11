using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace Web_Quản_Lí_Nhà_Thuốc.Hubs
{
    public class InventoryHub : Hub
    {
        public async Task UpdateStock(int drugId, int newQuantity)
        {
            await Clients.All.SendAsync("ReceiveStockUpdate", drugId, newQuantity);
        }
    }
}

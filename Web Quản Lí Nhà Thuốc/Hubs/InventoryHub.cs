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

        // Nhận tọa độ từ Shipper và phát sóng tới Khách hàng đang xem đơn hàng đó
        public async Task UpdateLocation(int orderId, double lat, double lng)
        {
            await Clients.All.SendAsync("ReceiveLocationUpdate", orderId, lat, lng);
        }
    }
}

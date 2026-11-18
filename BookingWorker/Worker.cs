using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BookingWorker;

public class Worker(IServiceScopeFactory scopeFactory, ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory { HostName = "localhost" };
        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(
            queue: "booking_queue",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);

        // Xử lý tuần tự từng tin nhắn để tránh xung đột trừ vé đồng thời
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);
            var payload = JsonSerializer.Deserialize<BookingMessage>(json);

            if (payload is not null)
            {
                await ProcessBookingAsync(payload.BookingId, stoppingToken);
            }

            await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
        };

        await channel.BasicConsumeAsync(
            queue: "booking_queue",
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        logger.LogInformation("BookingWorker đã sẵn sàng lắng nghe hàng đợi RabbitMQ...");
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task ProcessBookingAsync(int bookingId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var booking = await db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId, ct);
        if (booking is null || booking.Status != "Pending")
            return;

        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == booking.EventId, ct);
        if (ev is not null && ev.AvailableTickets >= booking.Quantity)
        {
            ev.AvailableTickets -= booking.Quantity;
            booking.Status = "Confirmed";
            logger.LogInformation("Đơn #{BookingId}: XÁC NHẬN thành công ({Quantity} vé). Vé còn lại: {Remaining}",
                booking.Id, booking.Quantity, ev.AvailableTickets);
        }
        else
        {
            booking.Status = "Rejected";
            logger.LogWarning("Đơn #{BookingId}: TỪ CHỐI do không đủ vé.", booking.Id);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private record BookingMessage(int BookingId);
}
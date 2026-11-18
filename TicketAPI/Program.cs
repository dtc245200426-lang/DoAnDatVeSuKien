
var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

// =====================================
// 1. DANH SACH SU KIEN
// =====================================

var events = new List<EventDto>
{
    new EventDto(1, "Dem nhac mua he",
        "Su kien am nhac mua he", 500, 350),

    new EventDto(2, "Hoi thao cong nghe",
        "Hoi thao chia se cong nghe", 200, 120),

    new EventDto(3, "Le hoi am nhac",
        "Le hoi am nhac ngoai troi", 1000, 800)
};

// =====================================
// 2. DANH SACH DON DAT VE
// =====================================

var bookings = new List<BookingDto>();

// =====================================
// 3. API LAY DANH SACH SU KIEN
// GET /events
// =====================================

app.MapGet("/events", () =>
{
    return Results.Ok(events);
});

// =====================================
// 4. API XEM CHI TIET SU KIEN
// GET /events/{id}
// =====================================

app.MapGet("/events/{id:int}", (int id) =>
{
    var selectedEvent = events.FirstOrDefault(e => e.Id == id);

    if (selectedEvent == null)
    {
        return Results.NotFound(new
        {
            message = "Khong tim thay su kien"
        });
    }

    return Results.Ok(selectedEvent);
});

// =====================================
// 5. API TAO DON DAT VE
// POST /bookings
// =====================================

app.MapPost("/bookings", (CreateBookingRequest request) =>
{
    // Kiem tra UserId
    if (string.IsNullOrWhiteSpace(request.UserId))
    {
        return Results.BadRequest(new
        {
            message = "UserId khong duoc de trong"
        });
    }

    // Kiem tra so luong ve
    if (request.Quantity <= 0)
    {
        return Results.BadRequest(new
        {
            message = "So luong ve phai lon hon 0"
        });
    }

    // Tim su kien
    var eventIndex = events.FindIndex(e => e.Id == request.EventId);

    if (eventIndex == -1)
    {
        return Results.NotFound(new
        {
            message = "Khong tim thay su kien"
        });
    }

    var selectedEvent = events[eventIndex];

    // Kiem tra so ve con lai
    if (selectedEvent.AvailableTickets < request.Quantity)
    {
        return Results.BadRequest(new
        {
            message = "Khong du so ve con lai"
        });
    }

    // Tru so ve con lai
    events[eventIndex] = selectedEvent with
    {
        AvailableTickets = selectedEvent.AvailableTickets - request.Quantity
    };

    // Tao ma don
    var nextId = bookings.Count == 0
        ? 1
        : bookings.Max(b => b.Id) + 1;

    // Tao don dat ve
    var booking = new BookingDto(
        nextId,
        request.UserId,
        request.EventId,
        request.Quantity,
        "Pending"
    );

    bookings.Add(booking);

    return Results.Created(
        $"/bookings/{booking.Id}",
        booking
    );
});

// =====================================
// 6. API XEM DON DAT VE
// GET /bookings/{id}
// =====================================

app.MapGet("/bookings/{id:int}", (int id) =>
{
    var booking = bookings.FirstOrDefault(b => b.Id == id);

    if (booking == null)
    {
        return Results.NotFound(new
        {
            message = "Khong tim thay don dat ve"
        });
    }

    return Results.Ok(booking);
});

// =====================================
// 7. KHOI CHAY UNG DUNG
// =====================================

app.Run();

// =====================================
// 8. CAC CAU TRUC DU LIEU
// =====================================

public record EventDto(
    int Id,
    string Name,
    string Description,
    int TotalTickets,
    int AvailableTickets
);

public record CreateBookingRequest(
    string UserId,
    int EventId,
    int Quantity
);

public record BookingDto(
    int Id,
    string UserId,
    int EventId,
    int Quantity,
    string Status
);

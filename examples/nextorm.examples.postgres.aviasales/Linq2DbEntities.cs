using LinqToDB.Mapping;

namespace NextORM.Examples.Postgres.Aviasales;

// linq2db mappings for the same `bookings` schema modelled by `Entities.cs`. The nextorm entities are
// interfaces (a nextorm requirement); linq2db maps concrete classes, so the comparison arm keeps a
// parallel set of POCOs with identical table/column names and CLR types.
//
// The `model`/`city`/`country` columns are `jsonb`: they are never materialised, only dereferenced
// with `->>` in SQL, so they are mapped as `string?`.

[Table("airplanes_data", Schema = "bookings")]
public sealed class AviasalesAirplaneData
{
    [PrimaryKey, Column("airplane_code")] public string AirplaneCode { get; set; } = "";
    [Column("model")] public string? Model { get; set; }
}

[Table("airports_data", Schema = "bookings")]
public sealed class AviasalesAirportData
{
    [PrimaryKey, Column("airport_code")] public string AirportCode { get; set; } = "";
    [Column("city")] public string? City { get; set; }
    [Column("country")] public string? Country { get; set; }
}

[Table("timetable", Schema = "bookings")]
public sealed class AviasalesTimetable
{
    [PrimaryKey, Column("flight_id")] public int FlightId { get; set; }
    [Column("route_no")] public string RouteNo { get; set; } = "";
    [Column("departure_airport")] public string DepartureAirport { get; set; } = "";
    [Column("arrival_airport")] public string ArrivalAirport { get; set; } = "";
    [Column("status")] public string Status { get; set; } = "";
    [Column("airplane_code")] public string AirplaneCode { get; set; } = "";
    [Column("scheduled_departure")] public DateTime ScheduledDeparture { get; set; }
    [Column("actual_departure")] public DateTime? ActualDeparture { get; set; }
}

[Table("flights", Schema = "bookings")]
public sealed class AviasalesFlight
{
    [PrimaryKey, Column("flight_id")] public int FlightId { get; set; }
    [Column("route_no")] public string RouteNo { get; set; } = "";
    [Column("status")] public string Status { get; set; } = "";
    [Column("scheduled_departure")] public DateTime ScheduledDeparture { get; set; }
    [Column("actual_departure")] public DateTime? ActualDeparture { get; set; }
}

[Table("tickets", Schema = "bookings")]
public sealed class AviasalesTicket
{
    [PrimaryKey, Column("ticket_no")] public string TicketNo { get; set; } = "";
    [Column("passenger_id")] public string PassengerId { get; set; } = "";
    [Column("passenger_name")] public string PassengerName { get; set; } = "";
    [Column("outbound")] public bool Outbound { get; set; }
}

[Table("bookings", Schema = "bookings")]
public sealed class AviasalesBooking
{
    [PrimaryKey, Column("book_ref")] public string BookRef { get; set; } = "";
    [Column("book_date")] public DateTime BookDate { get; set; }
    [Column("total_amount")] public decimal TotalAmount { get; set; }
}

[Table("segments", Schema = "bookings")]
public sealed class AviasalesSegment
{
    [PrimaryKey(1), Column("ticket_no")] public string TicketNo { get; set; } = "";
    [PrimaryKey(2), Column("flight_id")] public int FlightId { get; set; }
    [Column("fare_conditions")] public string FareConditions { get; set; } = "";
    [Column("price")] public decimal Price { get; set; }
}

[Table("seats", Schema = "bookings")]
public sealed class AviasalesSeat
{
    [PrimaryKey(1), Column("airplane_code")] public string AirplaneCode { get; set; } = "";
    [PrimaryKey(2), Column("seat_no")] public string SeatNo { get; set; } = "";
    [Column("fare_conditions")] public string FareConditions { get; set; } = "";
}

[Table("boarding_passes", Schema = "bookings")]
public sealed class AviasalesBoardingPass
{
    [PrimaryKey(1), Column("ticket_no")] public string TicketNo { get; set; } = "";
    [PrimaryKey(2), Column("flight_id")] public int FlightId { get; set; }
    [Column("seat_no")] public string? SeatNo { get; set; }
}

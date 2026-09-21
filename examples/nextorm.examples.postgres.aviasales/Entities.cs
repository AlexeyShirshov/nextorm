using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;

namespace NextORM.Examples.Postgres.Aviasales;

// Schema: PostgreSQL Pro demo database "Авиаперевозки" (bookings), version 2025-09-01.

[SqlTable("bookings.airplanes_data")]
public interface IAirplaneData
{
    [Key, Column("airplane_code")] string AirplaneCode { get; set; }
    [Column("model")] string? Model { get; set; }
}

[SqlTable("bookings.airports_data")]
public interface IAirportData
{
    [Key, Column("airport_code")] string AirportCode { get; set; }
    [Column("city")] string? City { get; set; }
    [Column("country")] string? Country { get; set; }
}

[SqlTable("bookings.timetable")]
public interface ITimetable
{
    [Key, Column("flight_id")] int FlightId { get; set; }
    [Column("route_no")] string RouteNo { get; set; }
    [Column("departure_airport")] string DepartureAirport { get; set; }
    [Column("arrival_airport")] string ArrivalAirport { get; set; }
    [Column("status")] string Status { get; set; }
    [Column("airplane_code")] string AirplaneCode { get; set; }
    [Column("scheduled_departure")] DateTime ScheduledDeparture { get; set; }
    [Column("actual_departure")] DateTime? ActualDeparture { get; set; }
}

[SqlTable("bookings.flights")]
public interface IFlight
{
    [Key, Column("flight_id")] int FlightId { get; set; }
    [Column("route_no")] string RouteNo { get; set; }
    [Column("status")] string Status { get; set; }
    [Column("scheduled_departure")] DateTime ScheduledDeparture { get; set; }
    [Column("actual_departure")] DateTime? ActualDeparture { get; set; }
}

[SqlTable("bookings.tickets")]
public interface ITicket
{
    [Key, Column("ticket_no")] string TicketNo { get; set; }
    [Column("passenger_id")] string PassengerId { get; set; }
    [Column("passenger_name")] string PassengerName { get; set; }
    [Column("outbound")] bool Outbound { get; set; }
}

[SqlTable("bookings.bookings")]
public interface IBooking
{
    [Key, Column("book_ref")] string BookRef { get; set; }
    [Column("book_date")] DateTime BookDate { get; set; }
    [Column("total_amount")] decimal TotalAmount { get; set; }
}

[SqlTable("bookings.segments")]
public interface ISegment
{
    [Key, Column("ticket_no")] string TicketNo { get; set; }
    [Key, Column("flight_id")] int FlightId { get; set; }
    [Column("fare_conditions")] string FareConditions { get; set; }
    [Column("price")] decimal Price { get; set; }
}

[SqlTable("bookings.seats")]
public interface ISeat
{
    [Key, Column("airplane_code")] string AirplaneCode { get; set; }
    [Key, Column("seat_no")] string SeatNo { get; set; }
    [Column("fare_conditions")] string FareConditions { get; set; }
}

[SqlTable("bookings.boarding_passes")]
public interface IBoardingPass
{
    [Key, Column("ticket_no")] string TicketNo { get; set; }
    [Key, Column("flight_id")] int FlightId { get; set; }
    [Column("seat_no")] string? SeatNo { get; set; }
}

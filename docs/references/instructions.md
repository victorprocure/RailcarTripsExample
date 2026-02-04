# Test instructions: #
 
Write a Blazor Web Assembly application with the following requirements:
  - Create a 'Railcar Trips' page.
  - The page has a way to process railcar/equipment events into trips by uploading a file such as the attached: equipment_events.csv.
    - *Note:* event times are local to the time zone of the corresponding city and are not ordered in the attached file.
  - Events should be processed into trips per railcar/equipment and stored in a database where Trip is a parent.
  - The logic for processing trips is as follows:
    - Event code W (Released) starts a new trip.
    - Event code Z (Placed) ends a trip.
  - Trips should contain fields:  equipment id, origin city id, destination city id, start utc, end utc, and total trip hours (all stored in the database).
  - The railcar trips page should show the processed trips in a grid (equipment id, origin, destination, start date/time, end date/time, total trip hours).
  - There should be a way to select a particular trip and see its events in order (bonus / nice to have).
  - Entity Framework should be used for database access.

 *Note:* the other 2 CSV files can be scripted into the database (don't need a UI to upload or configure).
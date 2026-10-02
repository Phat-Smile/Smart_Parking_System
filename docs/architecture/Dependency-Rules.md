# Dependency rules

The required dependency direction for every business service is:

- Domain has no service-layer project references.
- Application may reference its own Domain and generic BuildingBlocks.
- Persistence may reference its own Domain and Application.
- Infrastructure may reference its own Application and generic BuildingBlocks.
- WebAPI may reference its own Application, Persistence, Infrastructure, and generic BuildingBlocks.
- Persistence and Infrastructure do not reference each other.
- Projects in one business service do not reference projects belonging to another service.
- MassTransit and the RabbitMQ transport are referenced only by the five service Infrastructure projects. WebAPI invokes each service's Infrastructure registration; Domain, Application, Persistence, and BuildingBlocks remain transport-free.

BuildingBlocks contains only dependency-light technical primitives. It must not reference ASP.NET Core, EF Core, Npgsql, YARP, MassTransit, RabbitMQ, or any business service.

The ArchitectureTests suite reads project-reference edges from the service csproj files and enforces these rules. Run it with:

    dotnet test ParkingSystem.slnx

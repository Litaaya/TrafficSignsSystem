# Traffic Signs System

> Status: In Development

Traffic Signs System is a full-stack web application for managing traffic sign data with geospatial support, user/account management, authentication, authorization, and API-based system architecture.

## Project Overview

The system aims to provide a centralized platform for managing traffic signs and their related metadata, including:

- Traffic sign code, name, location, and custom metadata
- Geospatial traffic sign storage using coordinate-based location data
- Account and user management
- API documentation for testing and integration
- Frontend interface for interacting with the system

## Tech Stack

### Orchestration

- .NET Aspire

### Backend

- .NET 10
- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL / Npgsql
- NetTopologySuite for geospatial data
- Marten for event/document persistence
- MediatR for CQRS-style request handling
- FluentValidation
- Keycloak authentication
- OpenAPI / Scalar API documentation
- Serilog
- Elasticsearch logging sink

### Frontend

- Angular
- TypeScript
- Angular Material
- Tailwind CSS
- Leaflet
- Keycloak Angular

### Testing

- xUnit / unit test project scaffold

## Prerequisites

- Git
- Docker Desktop
- .NET SDK 10
- Node.js / npm
- Visual Studio 2026 is recommended for .NET 10 and .NET Aspire development

> Docker is required for local infrastructure services.  
> The .NET SDK is required to run the Aspire AppHost from source.

## Clone and Run Locally

Make sure Docker Desktop is running, then run:

```bash
git clone https://github.com/Litaaya/TrafficSignsSystem.git
cd TrafficSignsSystem
dotnet run --project TrafficSigns.AppHost
```

After the AppHost starts, open the Aspire Dashboard URL shown in the terminal.

## Frontend

If the Angular frontend is not started automatically by Aspire, run it manually:

```bash
cd TrafficSigns.WebUI
npm install
npm start
```

The frontend runs at:

```text
http://localhost:4200
```

## Notes

This project is currently under development. Some features, setup steps, and configuration details may change as the project evolves.

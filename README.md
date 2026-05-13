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

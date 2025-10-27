# BIR Calendar Feed

This project translates the [BIR](https://bir.no/) (Bergen waste management) website into a calendar feed format, making it easier to track waste collection schedules and environmental service dates.

## Features

- Converts BIR waste collection schedules into standardized calendar events
- Provides calendar feed endpoints for easy integration
- Caching service for improved performance
- ICS format support for calendar applications

## Quick Start

Build and run the application using Docker Compose:

```bash
docker compose build
docker compose up
```

The API will be available at `http://localhost:1999` (or as configured in your docker-compose.yml).

## Usage

Once running, you can access the calendar feed endpoints to integrate BIR waste collection schedules into your calendar application of choice.
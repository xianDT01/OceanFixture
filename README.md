# OceanFixture

OceanFixture es una recreacion independiente de la plataforma estudiada, orientada a la gestion del proceso de fletamento maritimo: alta de cargas, seleccion de armadores, intercambio de ofertas, cierre de condiciones y recapitulacion.

## Arquitectura

- `OceanFixture.Web`: SPA Angular 17.
- `OceanFixture.Api`: API principal ASP.NET Core 8 para ofertas, empresas y dashboard.
- `OceanFixture.Identity.Api`: sesion y permisos.
- `OceanFixture.Notification.Api`: plantillas de mensajes y notificaciones.
- `OceanFixture.Storage.Api`: metadatos de documentos adjuntos.
- `OceanFixture.Domain`: contratos y modelos de dominio compartidos.

El estado actual incluye datos de demostracion en memoria. El frontend intenta usar la API principal y, si esta no esta levantada, muestra automaticamente un conjunto de datos local para poder revisar la interfaz.

## Puesta en marcha

Requisitos: .NET SDK 8, Node.js 18 o posterior y npm.

En una terminal:

```powershell
dotnet run --project src/OceanFixture.Api
```

En otra terminal:

```powershell
cd src/OceanFixture.Web
npm install
npm start
```

Abrir `http://localhost:4200`. La API principal escucha en `http://localhost:5100`.

Servicios auxiliares opcionales:

```powershell
dotnet run --project src/OceanFixture.Identity.Api
dotnet run --project src/OceanFixture.Notification.Api
dotnet run --project src/OceanFixture.Storage.Api
```

Sus puertos son `5101`, `5102` y `5103`, respectivamente. Cada servicio expone `/health`.

## Funcionalidad iniciada

- Dashboard por etapas: cargo order, firm offer, counter offer y fixture recap.
- Vista de mapa y tarjetas de operaciones.
- Formulario de nueva orden de carga.
- Espacio de trabajo y detalle de una oferta.
- Endpoints para seleccionar firm offer, contraofertar y confirmar recap.
- Catalogo de empresas, sesion/permisos, plantillas de mensaje y documentos.

## Endpoints principales

- `GET /v1/api/offers/dashboard`
- `GET /v1/api/offers`
- `GET /v1/api/offers/{id}`
- `POST /v1/api/offers`
- `POST /v1/api/offers/{id}/firm-offer`
- `POST /v1/api/offers/{id}/counter`
- `POST /v1/api/offers/{id}/recap/confirm`
- `GET /v1/api/companies`

## Pruebas

Backend:

```powershell
dotnet test OceanFixture.sln --configuration Release
```

Frontend:

```powershell
cd src/OceanFixture.Web
npm run test:ci
```

La integración continua ejecuta las pruebas y compilaciones de Angular y .NET en cada push y pull request.

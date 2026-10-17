# OceanFixture

OceanFixture es una recreacion independiente de la plataforma estudiada, orientada a la gestion del proceso de fletamento maritimo: alta de cargas, seleccion de armadores, intercambio de ofertas, cierre de condiciones y recapitulacion.

## Documentacion

- [Estado funcional y pendientes](docs/ESTADO_DEL_PROYECTO.md).
- [Registro de desarrollo y decisiones](docs/REGISTRO_DE_DESARROLLO.md).
- [Recorridos de prueba por rol](docs/PRUEBAS_MANUALES.md).

Este README cubre arranque, arquitectura y API. Los documentos anteriores separan lo implementado de la demo y de lo pendiente; las pruebas y Git conservan las comprobaciones y cambios de codigo. Las capturas originales, credenciales y bases locales no se suben al repositorio.

## Arquitectura

- `OceanFixture.Web`: SPA Angular 17.
- `OceanFixture.Api`: API principal ASP.NET Core 8 para ofertas, empresas y dashboard.
- `OceanFixture.Identity.Api`: reserva para identidad de produccion; el antiguo login simulado esta deshabilitado.
- `OceanFixture.Notification.Api`: plantillas de mensajes y notificaciones.
- `OceanFixture.Storage.Api`: metadatos de documentos adjuntos.
- `OceanFixture.Domain`: contratos y modelos de dominio compartidos.

La API principal conserva empresas, cargas, ofertas, condiciones, clausulas y confirmaciones en SQLite local. Los datos de demostracion solo se crean en el primer arranque de una base vacia. Las pantallas requieren una sesion y la API; ya no se muestran datos locales de sustitucion que ignoren el rol.

El Recap carga las condiciones de cada oferta y registra las confirmaciones de ambas partes para la misma version. Una contraoferta invalida las confirmaciones anteriores; una confirmacion de una version desactualizada devuelve HTTP 409. Las confirmaciones sobreviven tanto a recargar la pagina como a reiniciar la API. El firmante se toma de la sesion del servidor, nunca del texto enviado por el navegador. La identidad sigue siendo de demostracion: este flujo no constituye autenticacion ni firma legal de produccion.

## Roles y sesiones demo locales

Abrir `/login` para elegir un perfil demo. La API debe ejecutarse en `Development` (el perfil normal de `dotnet run --project src/OceanFixture.Api` ya lo configura). Si se usa `--no-launch-profile`, establecer `ASPNETCORE_ENVIRONMENT=Development`.

Esta primera separacion funcional usa tokens opacos aleatorios emitidos y validados por el servidor, de 8 horas de duracion. El navegador conserva solo el token en `sessionStorage`; el perfil se consulta al servidor al restaurar la sesion. Cerrar sesion revoca el token. Reiniciar la API invalida las sesiones, pero conserva los datos de negocio en SQLite. Los tokens no se aceptan como roles ni empresas enviados libremente en cabeceras.

El selector solo esta disponible con `Authentication:EnableLocalDemoSessions=true`, entorno Development y conexion de loopback. Solo se acepta el origen web `http://localhost:4200` o clientes locales sin Origin. En Production no se emiten ni aceptan estas sesiones y los endpoints de negocio quedan protegidos. No publicar esta demo como un sistema de autenticacion real.

| Perfil | Acceso y acciones implementadas |
| --- | --- |
| Cargador | Solo sus cargas; crea ordenes, selecciona armador, negocia, modifica sus clausulas y confirma su parte del Recap. |
| Armador | Ordenes a las que esta invitado; envia o actualiza su propuesta y solo ve sus propias ofertas. Tras la seleccion, solo participa en las operaciones donde es el armador elegido. Negocia, modifica sus clausulas y confirma su parte. No crea cargas ni selecciona competidores. |
| Administrador | Consulta todas las operaciones y empresas. Puede crear cargas y seleccionar ofertas en gestion. No negocia ni confirma el Recap como si fuera una de las partes. El panel completo de administracion sigue pendiente. |

La API devuelve permisos por operacion para los controles de la interfaz, pero tambien los comprueba en el servidor. Los accesos a operaciones ajenas devuelven 404 y las acciones de rol no permitidas, 403. El armador no recibe cotizaciones ni el listado de invitaciones de competidores. La version enviada a todos los participantes sigue siendo la del estado completo, incluso en respuestas con datos ocultos.

Las instantaneas locales anteriores a esta separacion pertenecen al cargador demo 39 por compatibilidad; no se reescriben sus condiciones ni sus hashes de confirmacion. No se inventa un armador seleccionado en operaciones antiguas que no lo tengan. Los perfiles ofrecidos por el selector se limitan a empresas aprobadas existentes en la base; el cargador demo 40 solo aparece si existe. Quedan pendientes usuarios y contrasenas reales, verificacion de identidad, OIDC/Identity, administracion de membresias, auditoria, turnos de negociacion y la gestion avanzada de respuestas del armador.

## Persistencia local

- Por defecto, la base esta en `src/OceanFixture.Api/App_Data/oceanfixture.db`. La ruta se resuelve respecto al directorio de contenido de la API, no al directorio desde el que se ejecuta el comando.
- Puede cambiarse con `Storage:DatabasePath` en la configuracion o la variable de entorno `Storage__DatabasePath`.
- Cada cambio se guarda en una transaccion antes de devolver exito. Si falla la escritura, se restaura el estado anterior en memoria y la API devuelve HTTP 503.
- La base, archivos auxiliares y su directorio estan excluidos de Git. Las capturas de referencia del original siguen fuera del repositorio.
- Para una copia de seguridad local, apagar la API y copiar el archivo `.db` a una ubicacion segura. No editar ni eliminar la base en uso. No se incluye un boton de reinicio ni se sobreescriben datos existentes con datos demo.
- Una base corrupta, inaccesible o con una version de esquema desconocida impide arrancar; no se borra ni reinicia automaticamente.
- Esta primera implementacion guarda una instantanea JSON del estado en una tabla SQLite, con version de esquema. Esta orientada a una sola instancia local de la API; un bloqueo exclusivo impide abrir una segunda instancia sobre la misma base. Quedan pendientes el modelo relacional normalizado, migraciones, despliegue multi-instancia, identidad de produccion, permisos administrativos detallados y copias de seguridad automatizadas.

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

El dashboard incorpora un mapa geografico Leaflet con cartografia OpenStreetMap: permite arrastrar, hacer zoom con botones, teclado o gestos tactiles y plegar/desplegar el panel. No contiene posiciones ni seguimiento de barcos. Las teselas requieren internet y muestran atribucion visible; si fallan, hay un aviso con reintento. Se usan las teselas estandar solo bajo demanda, sin descarga masiva ni precarga offline. Antes de un despliegue con trafico elevado se debe configurar un proveedor apropiado y revisar la [politica de teselas de OpenStreetMap](https://operations.osmfoundation.org/policies/tiles/).

- Dashboard por etapas: cargo order, firm offer, counter offer y fixture recap.
- Vista de mapa y tarjetas de operaciones.
- Formulario de nueva orden de carga.
- Espacio de trabajo y detalle de una oferta.
- Endpoints para seleccionar firm offer, contraofertar y confirmar recap.
- Catalogo de empresas, sesion/permisos, plantillas de mensaje y documentos.

## Endpoints principales

El alta de carga usa el catalogo de armadores aprobados, conserva los datos entre los tres pasos y guarda la orden mediante la API. La API valida referencia, fletador, descripcion, cantidad positiva, puertos, laycan y destinatarios; asigna la referencia y vincula la empresa del cargador desde la sesion. Los destinatarios quedan registrados, pero no se envian correos. El detalle permite continuar a la comparativa, negociacion o Recap segun su etapa. Los datos se conservan en SQLite local; la identidad de produccion sigue pendiente.

La comparativa carga las ofertas de armadores desde la API, resalta diferencias y selecciona una oferta firm con control de version. Solo permite seleccionar mientras la operacion esta en CargoOrder; al guardar abre la negociacion con las condiciones del armador elegido. No reabre operaciones negociadas, operativas o archivadas. La referencia demo 2941 comienza en CargoOrder y contiene tres ofertas para probar este recorrido. Chat, notas, recordatorios y archivo de la comparativa siguen pendientes.

La negociacion carga los datos de cada oferta desde la API y envia condiciones y clausulas con control de version. Si otra peticion cambia la oferta, el servidor rechaza el envio con HTTP 409; los cambios escritos se mantienen en pantalla. Preparar el Recap requiere enviar primero los cambios. Borradores, chat y documentos de esta pantalla siguen pendientes y sus botones estan deshabilitados. El almacenamiento es local y la identidad sigue siendo de demostracion.

- `GET /v1/api/offers/dashboard`
- `GET /v1/api/offers`
- `GET /v1/api/offers/{id}`
- `GET /v1/api/offers/{reference}` (referencia numerica)
- `POST /v1/api/offers` (`customerReference`, `terms`, `invitedShipownerIds`)
- `POST /v1/api/offers/{id}/bids` (`terms`, `versionHash`): solo armador invitado mientras la carga sigue abierta; crea o actualiza su propuesta vigente. Empresa y nombre se toman del servidor. No envia correos.
- `POST /v1/api/offers/{id}/firm-offer/{shipownerId}` (requiere `{ "versionHash": "version recibida en recapVersion" }`)
- `POST /v1/api/offers/{id}/counter` (endpoint antiguo: HTTP 410; usar negociacion con version)
- `POST /v1/api/offers/{id}/negotiation/counter` (condiciones, clausulas y version)
- `POST /v1/api/offers/{id}/recap/confirm`
- `GET /v1/api/companies`
- `GET /v1/api/demo-session/accounts`, `POST /v1/api/demo-session/login`, `GET /v1/api/demo-session/me`, `POST /v1/api/demo-session/logout`

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
